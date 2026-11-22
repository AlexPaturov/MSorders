# Блок C. Второй сервис и синхронное общение

План действий для раздела «Основное» блока C из `plan-level-2.4.md`. Документ фиксирует текущее состояние, границу между сервисами, контракт, шаги реализации и проверку критерия.

## Цель блока

Появляется второй самостоятельный сервис `Customers`. `Orders` обращается к нему по HTTP внутри сети Docker, находимой по имени сервиса, и корректно отвечает при его недоступности. Оба сервиса поднимаются одним `docker compose`.

## Текущее состояние (проверено в репозитории)

1. Репозиторий один; `Orders.sln` — solution сервиса `Orders`, в нём два проекта: `Orders` (`src/Orders.csproj`) и `Orders.Tests` (`tests/Orders.Tests/Orders.Tests.csproj`).
2. `src/compose.yaml` поднимает `orders-container` и `postgres`; сервиса `Customers` нет.
3. `Orders` — ASP.NET Core 8 Web API: JWT-политика `orders.write`, `OrdersController`, `IVault`/`PostgresVault`, собственный `PostgresDbContext`, миграции, `/health`, Swagger и Serilog.
4. В `src/Program.cs` нет регистрации `IHttpClientFactory`; в коде сервиса нет typed client для вызовов других сервисов.
5. `src/appsettings.json` содержит секции `Databases` и `Auth`; секции настроек `Customers` нет.
6. `GlobalExceptionHandler` сейчас возвращает `500` для любого необработанного исключения; маппингов на ошибки downstream-сервиса нет.
7. Тесты `Orders.Tests` поднимают приложение через `WebApplicationFactory` и отдельный Postgres через Testcontainers; внешние HTTP-зависимости пока не подменяются.
8. `POST /dev/token` доступен только в `Development` и выпускает токен с `sub`, равным переданному `userId` (`Program.cs:186`); пустой `userId` отклоняется (`Program.cs:171-172`).
9. Отдельный NuGet-пакет для `IHttpClientFactory` не нужен: `AddHttpClient` входит в ASP.NET Core. Провайдер PostgreSQL для `Customers` берётся тот же, что уже используется в `Orders`.

## Что мы должны понять

1. Микросервис начинается не с второго контейнера, а с границы ответственности: данные клиентов принадлежат `Customers`, `Orders` не читает их напрямую из чужой базы.
2. Взаимодействие идёт через версионированный HTTP-контракт, а не через общий тип из общей сборки.
3. `IHttpClientFactory` управляет временем жизни `HttpMessageHandler`; typed client не создаёт новый handler на каждый запрос.
4. Сетевой вызов может не вернуться вовремя, поэтому таймаут и понятная ошибка обязательны.
5. Отказ `Customers` не должен ронять `Orders`: сервис отвечает ошибкой только там, где реально нужны данные клиента.
6. Имя сервиса в compose — это DNS-имя внутри сети Docker; `localhost` внутри контейнера означает сам контейнер.
7. Solution — по одному на сервис: проекты `Orders` не добавляются в solution `Customers`, а проекты `Customers` не добавляются в `Orders.sln`. Это усложняет создание недопустимой зависимости между сервисами.

## Как это будет работать

1. В репозитории остаётся один solution на сервис: `Orders.sln` остаётся за `Orders`, для `Customers` создаётся отдельный solution.
2. `Customers` хранит клиентов и отдаёт их по `GET /api/v1/customers/{id}`.
3. `Orders` регистрирует typed client `ICustomersClient` через `IHttpClientFactory` с `BaseAddress` из конфигурации.
4. В `Orders` добавляется эндпоинт `GET /orders/by-id/{id}/customer`: сначала находит заказ текущего пользователя, затем запрашивает клиента в `Customers`.
5. Успешный ответ `Customers` возвращается как есть в контракте `Orders`.
6. Ответ `404` от `Customers` превращается в `404` с текстом `Customer not found.`
7. Таймаут или сетевой сбой превращается в `503` с текстом `Customer service is unavailable.`
8. Неожиданный статус или неразбираемый JSON превращается в `502` с текстом `Customer service returned an invalid response.`
9. Остальные эндпоинты `Orders` не вызывают `Customers` и продолжают работать при его остановке.
10. `/health` в `Orders` остаётся проверкой только самого `Orders` и его базы; `Customers` в health check не добавляется.

## Граница ответственности

| Сервис | Данные и логика | Что отдаёт наружу |
| --- | --- | --- |
| `Orders` | Заказы, владелец заказа, связь заказ → `OwnerId` | Заказы и новый эндпоинт клиента заказа |
| `Customers` | Клиенты: `id`, `name`, `email`, `created` | `GET /api/v1/customers/{id}`, `/health`, dev-seed |

1. `Orders` не получает доступ к базе `Customers`.
2. `Customers` не знает о таблице заказов.
3. `OwnerId` заказа используется как `id` клиента: `sub` JWT совпадает с ключом клиента.
4. Общей сборки с DTO нет: контракт дублируется в `Orders` и проверяется тестами клиента.

## Контракт Customers

### Успешный ответ

```http
GET /api/v1/customers/11111111-1111-1111-1111-111111111111
```

```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "name": "Dev User",
  "email": "dev.user@example.test",
  "created": "2026-01-01T00:00:00Z"
}
```

1. `Content-Type: application/json`.
2. Клиент с заданным `id` не найден — `404` с `ProblemDetails`.
3. Некорректный GUID в маршруте — `400`.
4. `/health` доступен анонимно.
5. Префикс `/api/v1` фиксирует версию контракта; ломающее изменение требует нового префикса.

### Ответы Orders

```json
{
  "status": 503,
  "title": "Customer service unavailable.",
  "detail": "Customer service is unavailable."
}
```

1. `404` — заказа у текущего пользователя нет либо клиента нет в `Customers`; тексты различаются: `Order not found.` и `Customer not found.`
2. `503` — сеть, таймаут или отказ контейнера `Customers`.
3. `502` — `Customers` ответил неожиданным статусом или некорректным телом.
4. `401`/`403` сохраняют смысл из блока A и проверяются до вызова `Customers`.

## Конфигурация

### Orders

```json
"Services": {
  "Customers": {
    "BaseUrl": "http://localhost:8082",
    "TimeoutSeconds": 2
  }
}
```

1. `Services:Customers:BaseUrl` в compose переопределяется как `http://customers:8080`.
2. `Services:Customers:TimeoutSeconds` задаёт `HttpClient.Timeout`; значение `2` секунды достаточно для локального синхронного вызова.
3. Вне Docker для ручного запуска используется `http://localhost:8082`.
4. Секрет в конфигурацию не добавляется.

### Customers

```json
"Databases": {
  "ActiveDatabase": "Postgres",
  "Postgres": {
    "ConnectionString": "",
    "CommandTimeoutSeconds": 30
  }
}
```

1. Строка подключения приходит из переменных окружения compose, в репозитории хранится пустая строка.
2. Имя базы и учётные данные `Customers` отдельны от `Orders`.
3. Том базы `Customers` отделён от `orders-postgres-data`.

## Последовательность действий

1. Зафиксировать границу и контракт.

   - ☐ 1.1. Выбрать сценарий зависимости: `GET /orders/by-id/{id}/customer`.
   - ☐ 1.2. Зафиксировать, что создание, обновление, удаление и список заказов `Customers` не вызывают.
   - ☐ 1.3. Зафиксировать формат `CustomerDto`: `id`, `name`, `email`, `created`.
   - ☐ 1.4. Зафиксировать коды: `200`, `404`, `502`, `503`.
   - ☐ 1.5. Зафиксировать, что ретраи и circuit breaker из дополнительного улучшения блока C в этот план не входят.

2. Создать проект `Customers`.
   - ☐ 2.1. Добавить `src/Customers/Customers.csproj` на `net8.0` с ASP.NET Core Web SDK.
   - ☐ 2.2. Создать `Customers.sln` в корне репозитория для сервиса `Customers`.
   - ☐ 2.3. Не добавлять проекты `Orders` в `Customers.sln` и проекты `Customers` в `Orders.sln`.
   - ☐ 2.4. Создать `Models/Customer.cs`: `Guid Id`, `string Name`, `string Email`, `DateTime Created`.
   - ☐ 2.5. Создать `DTOs/CustomerDto.cs` с полями, описанными в контракте.
   - ☐ 2.6. Создать `Infrastructure/CustomersDbContext.cs` с `DbSet<Customer>`, первичным ключом `Id`, обязательными `Name` и `Email`, ограничениями длины.
   - ☐ 2.7. Добавить миграцию `InitialCreate`; таблица создаётся пустой.
   - ☐ 2.8. Создать `ICustomersStore` и `PostgresCustomersStore` с методом `GetByIdAsync(Guid id, CancellationToken ct)`.
   - ☐ 2.9. Добавить `CustomersController` с `GET /api/v1/customers/{id}` и `ProducesResponseType` для `200` и `404`.
   - ☐ 2.10. Добавить `/health` с проверкой подключения к базе `Customers`.
   - ☐ 2.11. Добавить dev-only `POST /dev/customers` внутри `if (app.Environment.IsDevelopment())` для сида клиента с заданным `id`.
   - ☐ 2.12. Подключить Serilog и Swagger по образцу `Orders`.
   - ☐ 2.13. В `Program.cs` при старте применять миграции, как это уже делает `Orders`.

3. Собрать контейнер `Customers`.
   - ☐ 3.1. Создать `src/Customers/Dockerfile` на той же базе SDK/ASP.NET образов, что и `Dockerfile` сервиса `Orders`.
   - ☐ 3.2. Скопировать структуру `Dockerfile` `Orders`, заменить имена проекта и точки входа на `Customers`.
   - ☐ 3.3. Проверить, что сборка образа `Customers` проходит отдельно от `Orders`.

4. Добавить typed client в `Orders`.
   - ☐ 4.1. Добавить секцию `Services:Customers` в `appsettings.json`.
   - ☐ 4.2. Создать `DTOs/Customers/CustomerDto.cs` с контрактом из раздела выше.
   - ☐ 4.3. Создать `Infrastructure/Customers/ICustomersClient.cs` с методом `Task<CustomerDto> GetAsync(Guid id, CancellationToken ct)`.
   - ☐ 4.4. Создать `Infrastructure/Customers/CustomersClient.cs` на базе `HttpClient`.
   - ☐ 4.5. Зарегистрировать клиент через `builder.Services.AddHttpClient<ICustomersClient, CustomersClient>(...)`, задав `BaseAddress`, таймаут и `Accept: application/json`.
   - ☐ 4.6. Формировать запрос `GET api/v1/customers/{id}` через `System.Net.Http.Json`.
   - ☐ 4.7. Проверять `id` в ответе; несовпадение считать некорректным контрактом.
   - ☐ 4.8. Преобразовать `404` в `CustomerNotFoundException`.
   - ☐ 4.9. Преобразовать `HttpRequestException` и таймаут в `CustomersUnavailableException`.
   - ☐ 4.10. При `OperationCanceledException` проверять `ct.IsCancellationRequested`: отмену вызывающего запроса пробрасывать дальше, таймаут клиента превращать в `CustomersUnavailableException`.
   - ☐ 4.11. Неожиданный статус и ошибки десериализации превращать в `CustomersResponseException`.
   - ☐ 4.12. Логировать `customerId`, статус ответа и длительность; токены и строки подключения в логи не попадают.

5. Добавить эндпоинт в `Orders`.
   - ☐ 5.1. Добавить `GET by-id/{id}/customer` в `OrdersController`; контроллер уже требует `orders.write`.
   - ☐ 5.2. Получить `currentUserId` через `User.GetUserId()`.
   - ☐ 5.3. Найти заказ через `_orders.GetByIdAsync(currentUserId, id, ct)`; отсутствие вернуть как уже существующий `404 Order not found.`
   - ☐ 5.4. Вызвать `_customersClient.GetAsync(order.OwnerId, ct)`.
   - ☐ 5.5. Вернуть `Ok(customer)` без изменения доменных DTO заказа.
   - ☐ 5.6. Добавить `ProducesResponseType` для `200`, `404`, `502`, `503`.
   - ☐ 5.7. Обновить XML-комментарии и описание в Swagger.

6. Добавить маппинг ошибок.
   - ☐ 6.1. Добавить три типа исключений клиента в `Infrastructure/Customers`.
   - ☐ 6.2. В `GlobalExceptionHandler` добавить ветки до обработчика по умолчанию.
   - ☐ 6.3. `CustomerNotFoundException` → `404` с `ProblemDetails` и текстом `Customer not found.`
   - ☐ 6.4. `CustomersUnavailableException` → `503` с текстом `Customer service is unavailable.`
   - ☐ 6.5. `CustomersResponseException` → `502` с текстом `Customer service returned an invalid response.`
   - ☐ 6.6. Ожидаемые ошибки downstream логировать на уровне `Warning`, прочие — на уровне `Error`.
   - ☐ 6.7. Сохранить текущее поведение `500` для всех остальных необработанных исключений.

7. Объединить сервисы в `compose.yaml`.
   - ☐ 7.1. Добавить сервис `customers` с `image: customers-api` и `container_name: customers-container`.
   - ☐ 7.2. Указать `build.context: ./Customers` и `dockerfile: Dockerfile`.
   - ☐ 7.3. Пробросить порт хоста `8082:8080` для локальной проверки.
   - ☐ 7.4. Добавить отдельный `customers-postgres` с отдельным томом `customers-postgres-data`.
   - ☐ 7.5. Передать в `customers` строки подключения и имя базы через переменные окружения из `.env`.
   - ☐ 7.6. Добавить healthcheck `customers` через `curl http://localhost:8080/health`.
   - ☐ 7.7. Добавить у `orders-container` зависимость от `customers` с `condition: service_healthy`.
   - ☐ 7.8. Передать `Services__Customers__BaseUrl=http://customers:8080` в окружение `orders-container`.
   - ☐ 7.9. Передать `Services__Customers__TimeoutSeconds` через окружение.
   - ☐ 7.10. Обновить `docs/Docker_comands.md`: добавить новые сервисы и команды запуска/остановки.

8. Добавить тесты.
   - ☐ 8.1. Создать `tests/Customers.Tests` по образцу `Orders.Tests` и добавить в `Customers.sln`.
   - ☐ 8.2. Тест `Customers`: `/health` возвращает `200`.
   - ☐ 8.3. Тест `Customers`: существующий клиент возвращается с `200`, отсутствующий — с `404`.
   - ☐ 8.4. В `Orders.Tests` добавить подмену `ICustomersClient` через `ConfigureTestServices`.
   - ☐ 8.5. Тест успеха: `GET /orders/by-id/{id}/customer` возвращает данные клиента и запрашивает клиента с `OwnerId` заказа.
   - ☐ 8.6. Тест отсутствия заказа: `404 Order not found.`, клиент не вызывается.
   - ☐ 8.7. Тест отсутствия клиента: `404 Customer not found.`
   - ☐ 8.8. Тест недоступности: клиент бросает `CustomersUnavailableException`, эндпоинт возвращает `503`.
   - ☐ 8.9. Тест таймаута: в отдельном тесте подменить `HttpMessageHandler`, а не `ICustomersClient`, задержать ответ сверх `TimeoutSeconds` и получить `503`.
   - ☐ 8.10. Тест некорректного контракта: неожиданный JSON или статус дают `502`.
   - ☐ 8.11. Тест изоляции: при падающем `ICustomersClient` `GET /orders/all` возвращает `200`.
   - ☐ 8.12. Тест `Orders`: запрос без токена на новый эндпоинт возвращает `401`.
   - ☐ 8.13. Тест typed client проверяет путь `api/v1/customers/{id}` и передачу `CancellationToken`.

9. Проверить критерий вручную.
   - ☐ 9.1. `docker compose -f src/compose.yaml up -d --build` поднимает четыре контейнера: `orders-container`, `customers-container`, базы `Orders` и `Customers`.
   - ☐ 9.2. `curl http://localhost:8081/health` и `curl http://localhost:8082/health` возвращают `200`.
   - ☐ 9.3. Получить dev-токен через `POST http://localhost:8081/dev/token`.
   - ☐ 9.4. Создать клиента `POST http://localhost:8082/dev/customers` с id `11111111-1111-1111-1111-111111111111`.
   - ☐ 9.5. Создать заказ через `POST http://localhost:8081/orders` и взять его `id` из `GET /orders/all`.
   - ☐ 9.6. `GET http://localhost:8081/orders/by-id/{id}/customer` с токеном возвращает данные клиента.
   - ☐ 9.7. `docker compose -f src/compose.yaml stop customers` останавливает только `Customers`.
   - ☐ 9.8. Повторный вызов того же эндпоинта возвращает `503` с текстом `Customer service is unavailable.` не дольше таймаута.
   - ☐ 9.9. `GET http://localhost:8081/orders/all` продолжает возвращать `200`.
   - ☐ 9.10. `docker compose -f src/compose.yaml start customers` восстанавливает успешный ответ.
   - ☐ 9.11. `dotnet test Orders.sln` и `dotnet test Customers.sln` зелёные.

## Открытые вопросы

1. Межсервисная авторизация не добавляется: `Customers` доступен внутри сети Docker и дополнительно на `localhost:8082` только для локальной отладки. Передача JWT и проверка прав в `Customers` — отдельное решение.
2. Проверка существует ли клиент при создании заказа не добавляется: в блоке C зависимость используется только в новом read-эндпоинте.
3. Сквозной идентификатор запроса между сервисами относится к блоку E и в этом плане не реализуется.
4. Ретраи и circuit breaker относятся к дополнительному улучшению блока C и не смешиваются с базовым сценарием.
5. Общая сборка контракта не создаётся: независимый выпуск важнее отсутствия дублирования DTO.

## Критерий готовности

1. В одном репозитории два solution, в compose два образа и два контейнера сервисов с собственными базами.
2. `Orders` находит `Customers` по имени сервиса внутри Docker и получает данные через HTTP по версионированному контракту.
3. При доступном `Customers` новый эндпоинт возвращает клиента заказа.
4. При остановленном `Customers` новый эндпоинт возвращает `503` с понятным текстом, а остальные эндпоинты `Orders` продолжают работать.
5. Таймаут, `404` клиента и некорректный ответ покрыты тестами; локальные `dotnet test Orders.sln` и `dotnet test Customers.sln` зелёные.
