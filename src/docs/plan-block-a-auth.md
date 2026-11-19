# Блок A. Аутентификация и авторизация

План действий для блока A из `plan-level-2.4.md`. План описывает, что делаем и как это работает; статус ниже отражает текущий код.

## Цель блока

Сервис проверяет JWT-токен и работает только с заказами текущего пользователя. Эндпоинты без токена возвращают `401`, Swagger умеет передавать токен. Локально достаточно заглушки-издателя токенов.

## Текущее состояние (проверено в репозитории)

1. JWT Bearer подключён: в `Program.cs` есть `AddAuthentication`/`AddJwtBearer` с параметрами валидации, `OnTokenValidated` отклоняет отсутствующий, невалидный и пустой `sub`, `AddAuthorization` с политикой `orders.write`, `UseAuthentication`, `UseAuthorization`; `OrdersController` помечен `[Authorize(Policy = "orders.write")]`; `/health` анонимный.
2. `appsettings.json` содержит вложенный контракт `Auth:Scheme` + `Auth:Jwt`; `Auth:Jwt:SigningKey` в репозитории `null`, локально приходит из user-secrets, в compose — из `Auth__Jwt__SigningKey`; тестовая фабрика задаёт отдельный тестовый ключ.
3. В `src/Orders.csproj` добавлен пакет `Microsoft.AspNetCore.Authentication.JwtBearer`.
4. `Order` содержит `Id`, `Name`, `Status`, `Created` и `OwnerId` (`Guid`) с `[Index(nameof(OwnerId))]`; созданы и применены миграции `AddOrderOwnerId`, `AddOrderOwnerIdIndex` и `DropOwnerIdDefaultAndAddCheck`.
5. `IVault` принимает владельца во всех операциях: `GetAllAsync`, `GetByIdAsync`, `UpdateBatchAsync`, `DeleteAsync`, `InsertAsync`. `PostgresVault` фильтрует по `OwnerId` при чтении, удалении и batch-обновлении и проставляет владельца при вставке.
6. `OrdersController` во всех действиях получает `sub` через `User.GetUserId()` в локальную переменную `currentUserId` и передаёт её в хранилище.
7. Пакетное обновление остаётся «всё или ничего»: если хотя бы один id отсутствует в наборе текущего пользователя, возвращается общий `404` без перечисления id.
8. Swagger подключён в `Development`: добавлены `AddEndpointsApiExplorer()`, Bearer-схема и глобальное требование этой схемы. Ручная проверка кнопки Authorize в UI не выполнялась.
9. Тесты: `OrdersApiFactory` поднимает приложение в памяти и отдельный контейнер `postgres:16-alpine` через Testcontainers, задаёт тестовый JWT-ключ. `TestJwt.CreateToken` параметризует `userId`, ключ, `iss`, `aud`, `scope` и интервалы жизни. Покрыты `401` без токена, просроченный токен, токен с чужим ключом, изоляция пользователей, чужой id на `GetById`/`Delete`, batch с чужим id и `403` без `scope=orders.write`. Последний прогон: `27/27`.
10. Тип `OwnerId` — `Guid`; он совпадает с `ClaimsPrincipalExtensions.GetUserId` и всеми параметрами владельца в `IVault`.
11. В `Development` доступен `POST /dev/token` — минимальный эндпоинт, который выпускает JWT с `sub`, `name`, `scope=orders.write` и `jti`, подписанный теми же `iss`/`aud`/ключом, что использует валидация. В `Production` маршрут не регистрируется. Доменные операции остаются в контроллерах; минимальный стиль используется для инфраструктурных/dev-эндпоинтов, как и `/health`.
12. Локальная БД очищена: 3 старых заказа получили dev-id `11111111-1111-1111-1111-111111111111`; миграция `DropOwnerIdDefaultAndAddCheck` применена, `DEFAULT` снят, создан `CK_Orders_OwnerId_NotEmpty`. Проверено в psql.

## Как это будет работать

1. Клиент получает JWT у локальной заглушки-издателя в `Development`.
2. Токен подписан симметричным ключом `HS256`; payload содержит `iss`, `aud`, `sub`, `name`, `scope`, `iat`, `nbf`, `exp`, `jti`.
3. На каждом запросе middleware `JwtBearer` проверяет подпись, издателя, аудиторию и срок жизни токена.
4. `[Authorize]` на контроллере требует аутентифицированного пользователя; `/health` помечается `[AllowAnonymous]`.
5. Контроллер берёт `sub` из `HttpContext.User` и передаёт его в слой данных как идентификатор владельца.
6. При создании заказа `OwnerId` проставляется из токена, а не из тела запроса.
7. Чтение, обновление и удаление фильтруются по `OwnerId` текущего пользователя.
8. Заказ ищется только в наборе текущего пользователя. Если в этом наборе заказа с таким id нет, ответ — `404` с текстом `"Order not found."`. `403` применяется только к нехватке прав/роли.
9. Пакетное обновление — «всё или ничего»: если хотя бы одного id из списка нет в наборе текущего пользователя, весь запрос отклоняется, изменения откатываются, ответ — общий `404` без перечисления id.

## Контракт конфигурации

```json
"Auth": {
  "Scheme": "Jwt",
  "Jwt": {
    "Issuer": "orders-local",
    "Audience": "orders-api",
    "SigningKey": null
  }
}
```

1. `Auth` — корень контракта аутентификации; он не привязан к конкретному механизму.
2. `Auth:Scheme` — выбранный механизм; сейчас `Jwt`.
3. `Auth:Jwt` — настройки JWT: `Issuer`, `Audience`, `SigningKey`.
4. `Auth:Jwt:SigningKey` в репозитории всегда `null`; локально значение приходит из user-secrets, в CI — из `Auth__Jwt__SigningKey`.
5. Смена механизма добавляет новую подсекцию (`Auth:ApiKey` и т.п.), корень `Auth` остаётся.

## Контракт токена (референс)

1. Claims в payload:

```json
{
  "iss": "orders-local",
  "aud": "orders-api",
  "sub": "<id текущего пользователя>",
  "name": "<имя пользователя>",
  "scope": "orders.write",
  "iat": 1762586400,
  "nbf": 1762586400,
  "exp": 1762590000,
  "jti": "<уникальный id токена>"
}
```

2. Параметры валидации:

```json
{
  "ValidateIssuer": true,
  "ValidIssuer": "orders-local",
  "ValidateAudience": true,
  "ValidAudience": "orders-api",
  "ValidateLifetime": true,
  "RequireExpirationTime": true,
  "ValidateIssuerSigningKey": true,
  "IssuerSigningKey": "<секретный ключ, в токене отсутствует>",
  "ValidAlgorithms": ["HS256"],
  "ClockSkew": "00:00:30",
  "NameClaimType": "name",
  "RoleClaimType": "role"
}
```

3. Заголовок токена:

```json
{
  "alg": "HS256",
  "typ": "JWT"
}
```

4. Токен целиком: `base64url(header).base64url(payload).signature`. Подпись считается ключом `IssuerSigningKey`; в токене ключ отсутствует.

## Последовательность действий

1. Зафиксировать контракт аутентификации.
   1.1. Формат: JWT Bearer в заголовке `Authorization`.
   1.2. Claims: `iss`, `aud`, `sub`, `name`, `scope`, `iat`, `nbf`, `exp`, `jti`.
   1.3. Значения: `Auth:Scheme = Jwt`, `Auth:Jwt:Issuer = orders-local`, `Auth:Jwt:Audience = orders-api`, срок жизни 60 минут, `scope = orders.write`, алгоритм `HS256`.
   1.4. Параметры валидации: `ValidateIssuer`, `ValidIssuer`, `ValidateAudience`, `ValidAudience`, `ValidateLifetime`, `RequireExpirationTime`, `ValidateIssuerSigningKey`, `IssuerSigningKey`, `ValidAlgorithms = [HS256]`, `ClockSkew = 00:00:30`, `NameClaimType = name`, `RoleClaimType = role`.
   1.5. `Auth:Jwt:SigningKey` хранить локально в переменных окружения или user-secrets, в CI — в секрете репозитория и передавать как `Auth__Jwt__SigningKey`; в репозиторий не коммитить.
   1.6. Зафиксировать решения: заказа с таким id у текущего пользователя нет (`404`, текст `"Order not found."`); batch-обновление — «всё или ничего» с общим `404`.

2. Добавить JWT Bearer в сервис.
   - [x] 2.1. Добавить пакет `Microsoft.AspNetCore.Authentication.JwtBearer` (изменение зависимости согласовано).
   - [x] 2.2. Добавить в `appsettings.json` секцию `Auth` с `Scheme = Jwt` и подсекцией `Jwt`: `Issuer = orders-local`, `Audience = orders-api`, `SigningKey = null`.
   - [ ] 2.3. Положить `Auth:Jwt:SigningKey` в user-secrets локально и в `Auth__Jwt__SigningKey` в CI; в репозиторий не коммитить. Локально и в compose сделано; CI — в блоке F.
   - [ ] 2.4. Настроить `AddAuthentication` с `JwtBearerDefaults.AuthenticationScheme`. Сейчас используется `Auth:Scheme` как имя схемы; отклонение сохраняется.
   - [x] 2.5. Настроить `TokenValidationParameters` и `MapInboundClaims = false`.
   - [x] 2.6. Добавить `AddAuthorization` с политикой по умолчанию и именованной `orders.write`. `DefaultPolicy` явно не переопределён, используется встроенная.
   - [x] 2.7. Вставить `app.UseAuthentication()` и `app.UseAuthorization()` перед `MapControllers`.
   - [x] 2.8. Пометить контроллер `[Authorize]`; `/health` оставить анонимным.
   - [x] 2.9. Читать пользователя в контроллере только из `HttpContext.User`, не из тела запроса. Все действия вызывают `User.GetUserId()` и передают `currentUserId` в хранилище.
   - [x] 2.10. Проверить: сборка проходит, запрос без токена даёт `401`. Тест `Get_All_WithoutToken_Returns401` добавлен; итоговый прогон `27/27`.

3. Ввести владельца заказа.
   - [x] 3.1. Добавить в `Order` поле `OwnerId` (`Guid`). Тип выбран через `ClaimsPrincipalExtensions.GetUserId`.
   - [x] 3.2. Описать поле в модели: `[Index(nameof(OwnerId))]`; в `PostgresDbContext` добавлен check constraint `CK_Orders_OwnerId_NotEmpty` через `ToTable(t => t.HasCheckConstraint(...))`.
   - [x] 3.3. Создать и применить миграции `AddOrderOwnerId`, `AddOrderOwnerIdIndex` и `DropOwnerIdDefaultAndAddCheck`.
   - [x] 3.4. При создании заказа проставлять `OwnerId` из токена в `InsertAsync`.
   - [x] 3.5. В `GetAll` возвращать только заказы текущего пользователя.
   - [x] 3.6. В `GetById`, `Delete` и `UpdateBatch` фильтровать по `OwnerId`.
   - [x] 3.7. Для batch: если хотя бы один заказ из списка не найден в наборе текущего пользователя, вернуть общий `404` без перечисления id и не менять данные.

4. Сделать локальную заглушку-издателя токенов.
   - [x] 4.1. Добавить dev-only способ выпускать токен для указанного пользователя: `POST /dev/token` внутри `if (app.Environment.IsDevelopment())`.
   - [x] 4.2. Использовать те же `Auth:Jwt:Issuer`, `Auth:Jwt:Audience` и `Auth:Jwt:SigningKey`, что и сервис проверки; токен подписывается `HS256`.
   - [x] 4.3. Отключить заглушку в `Production`: маршрут регистрируется только в `Development`.
   - [x] 4.4. Документировать получение токена руками:

```bash
dotnet user-secrets set "Auth:Jwt:SigningKey" "<PLACEHOLDER_32_PLUS_CHARS>"

curl -s -X POST http://localhost:<PORT>/dev/token \
  -H "Content-Type: application/json" \
  -d '{"userId":"11111111-1111-1111-1111-111111111111","name":"Dev User"}'

curl -H "Authorization: Bearer <TOKEN>" http://localhost:<PORT>/orders/all
```

Ответ `/dev/token`: `access_token`, `token_type = Bearer`, `expires_in = 3600`. `AddEndpointsApiExplorer()` подключён, поэтому минимальный эндпоинт отображается в Swagger; для ручной проверки можно使用该 токен через кнопку Authorize.

5. Настроить Swagger.
   - [x] 5.1. Добавлена схема безопасности Bearer в `Program.cs`: HTTP-схема `bearer`, формат JWT, заголовок `Authorization`.
   - [x] 5.2. Добавлено глобальное требование схемы `Bearer` через `AddSecurityRequirement`.
   - [ ] 5.3. Ручная проверка кнопки Authorize и передачи токена в Swagger UI не выполнялась; код схемы и требования уже добавлен.

6. Добавить тесты.
   - [x] 6.1. Тест `401`: `OrdersEndpointsTests.Get_All_WithoutToken_Returns401` использует `CreateClient()` без заголовка `Authorization` и ожидает `401`.
   - [x] 6.2. Тесты `401`: `GetAll_WithExpiredToken_Returns401` и `GetAll_WithTokenSignedByOtherKey_Returns401`.
   - [x] 6.3. Тест изоляции: `GetAll_UserSeesOnlyOwnOrders` проверяет, что второй пользователь не видит заказ первого.
   - [x] 6.4. Тест `404`: `ForeignOrderId_Returns404_ForGetByIdAndDelete` проверяет чтение и удаление чужого id с текстом `"Order not found."`.
   - [x] 6.5. Тест batch: `UpdateBatch_WithForeignId_Returns404_WithoutPartialChanges` проверяет общий `404`, отсутствие id в ответе и отсутствие частичных изменений.
   - [x] 6.6. Тест создания: unit-тест проверяет, что `OwnerId` берётся из токена, а не из тела.
   - [x] 6.7. Тест политики: `GetAll_WithoutRequiredScope_Returns403` проверяет отказ при `scope=orders.read`.

7. Встроить тесты в будущий CI/CD (блок F), не меняя подход.
   - [x] 7.1. Тесты не ходят к внешнему identity provider: тестовая фабрика задаёт `Auth:Jwt:SigningKey`, `Auth:Jwt:Issuer` и `Auth:Jwt:Audience`.
   - [x] 7.2. Каждый тестовый класс получает свою фабрику и свой Postgres-контейнер; это сохраняет изоляцию при параллельном запуске.
   - [x] 7.3. Новые тесты используют `Guid.NewGuid()` для второго пользователя и уникальные заказы; базовый пользователь в `TestJwt` остаётся фиксированным.
   - [ ] 7.4. Подписывающий ключ для CI передаётся секретом репозитория, не коммитится; это относится к блоку F.
   - [ ] 7.5. Тесты запускаются до публикации образов; красный тест останавливает конвейер; это относится к блоку F.

8. Проверить критерий блока руками.
   - [x] 8.1. Без токена защищённые эндпоинты возвращают `401` — покрыто тестом; `/health` остаётся доступным — покрыто `HealthCheckTests`.
   - [x] 8.2. С валидным токеном возвращаются только свои заказы — покрыто `GetAll_UserSeesOnlyOwnOrders`.
   - [x] 8.3. По id, которого нет у текущего пользователя, возвращается `404` с текстом `"Order not found."` — покрыто `ForeignOrderId_Returns404_ForGetByIdAndDelete`.
   - [ ] 8.4. Swagger передаёт токен: схема и требование добавлены, ручная проверка в UI не выполнялась.
   - [x] 8.5. Все тесты зелёные локально: последний прогон дал `27/27`.

## Открытые вопросы

1. `GetUserId()` всё ещё бросает `InvalidOperationException` при отсутствии или невалидном `sub`, но в HTTP-пайплайне такие токены отклоняются `OnTokenValidated` с `401`; прямое использование хелпера вне пайплайна остаётся опасным.
2. `Auth:Scheme` используется как имя схемы аутентификации; при значении, отличном от JWT, код всё равно регистрирует JWT-обработчик под этим именем.
3. `DefaultPolicy` явно не переопределён; используется встроенная политика, требующая аутентификации.
4. Ручная проверка кнопки Authorize в Swagger UI не выполнялась; Bearer-схема и глобальное требование уже добавлены в `Program.cs`.

## Тесты и параллельный запуск: уточнение текущего поведения

1. xUnit по умолчанию выполняет тестовые коллекции параллельно, а каждый тестовый класс без `[Collection]` образует отдельную коллекцию.
2. `OrdersEndpointsTests`, `HealthCheckTests` и `ErrorHandlingTests` используют `IClassFixture`, поэтому каждый класс получает свой экземпляр фабрики и свой контейнер Postgres на случайном порту.
3. Общей базы между классами нет, поэтому параллельный запуск уже изолирован по данным. Утверждение «параллельный запуск с базой не учитывается» не подтверждается кодом.
4. Реальная цена такой изоляции — несколько контейнеров Postgres одновременно; это ресурсы и время CI, а не ошибка корректности.
5. Приложение как Docker-образ тестам не нужно: `OrdersApiFactory` поднимает его в памяти, а Testcontainers сам скачивает `postgres:16-alpine`, если образа нет.
6. Утверждение «тесты упадут без заранее созданного образа» не подтверждается кодом: тесты не ссылаются на `orders-api` и не используют compose.

## Техдолг

1. Контейнер Postgres на каждый тестовый класс увеличивает время и нагрузку CI. Пересмотреть, если прогон станет долгим: общий collection fixture с изоляцией данных по тестам или ограничение `maxParallelThreads`.
2. Нет `xunit.runner.json`: политика параллелизма неявная. Зафиксировать её явно, когда появятся тесты блока A.
3. Testcontainers скачивает образ при первом запуске на раннере; при необходимости предзагружать образ или кэшировать слой Docker в CI.
4. Ручная проверка Swagger UI ещё не выполнялась; код Bearer-схемы, требования и `AddEndpointsApiExplorer()` уже добавлен.

## Критерий готовности

1. Защищённые эндпоинты без токена возвращают `401`.
2. По id, которого нет у текущего пользователя, возвращается `404` с текстом `"Order not found."`; `403` используется только при нехватке роли/политики.
3. Swagger умеет передавать токен: Bearer-схема и глобальное требование добавлены; осталась ручная проверка кнопки Authorize в UI.
4. Тесты покрывают `401`, доступ к своим заказам, отсутствие заказа с заданным id у текущего пользователя и семантику batch.
5. Тесты зелёные локально и не зависят от внешнего identity provider или заранее собранного образа приложения.
