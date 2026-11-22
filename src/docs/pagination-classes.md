# Размещение классов пагинации

Документ описывает целевое размещение и контракт классов для пагинации блока B. Код в этом шаге не меняется.

## 1. Проверенное текущее состояние

1. `src/Controllers/OrdersController.cs:24-35`: `GET /orders/all` вызывает `IVault.GetAllAsync` и возвращает все заказы владельца.
2. `src/Repositories/Interfaces/IVault.cs:5`: `GetAllAsync` возвращает `ReadOnlyDictionary<Guid, Order>`.
3. `src/Repositories/Implementations/OrdersVault.cs:16-22`: запрос содержит фильтр `OwnerId` и `ToListAsync()`, сортировки и `Skip/Take` нет.
4. `src/DTOs/Mappers/OrderMappings.cs:10-16`: маппинг `Order -> OrderDto` задан одним `ProjectionExpr`; `OwnerId` в DTO не входит.
5. `src/Models/Order.cs:5`: на модели есть `[Index(nameof(OwnerId))]`; составного индекса нет.
6. `src/Infrastructure/OrdersDbContext.cs:17-18`: `Created` имеет default `CURRENT_TIMESTAMP`.
7. В рабочем дереве есть незакоммиченные stub-файлы:
   1. `src/Application/Common/PaginationParams.cs`: `namespace Orders.Application.Common`, `public class PaginationParams`, комментарий «Входные параметры (Page, Size, Sort)».
   2. `src/Models/PagedResult.cs`: `namespace Orders.Application.Common`, `public class PagedResult`, комментарий «Обертка (Items, TotalCount)».
8. Файл `src/Models/PagedResult.cs` объявлен в namespace `Orders.Application.Common`, то есть его текущий путь не совпадает с namespace. В целевой схеме он размещается в `src/Application/Common/PagedResult.cs`.
9. Комментарий в stub `PaginationParams` упоминает `Sort`, но контракт в `src/docs/plan-block-b-data.md:51` фиксирует порядок `Created DESC, Id DESC` и исключает `sort` из блока. В схеме ниже `Sort` отсутствует.
10. Контракт ответа в `src/docs/plan-block-b-data.md:55-64` содержит `items`, `page`, `pageSize`, `totalCount`. Демо-тест не использует точное значение `totalCount` как критерий корректности.

## 2. Целевое размещение

1. `src/Application/Common/PaginationParams.cs` — базовые входные параметры страницы: `Page`, `PageSize` и вычисляемый `Skip`.
2. `src/Application/Common/PagedResult.cs` — универсальная обёртка результата: `Items`, `Page`, `PageSize`, `TotalCount`.
3. `src/DTOs/Orders/GetOrdersQuery.cs` — параметры запроса `GET /orders`: базовые поля страницы плюс `Status`.
4. `src/Repositories/Interfaces/IVault.cs` — добавляется метод `GetPageAsync`.
5. `src/Repositories/Implementations/OrdersVault.cs` — реализуется SQL-уровневая пагинация.
6. `src/DTOs/Mappers/OrderMappings.cs` — добавляется маппинг страницы доменных заказов в страницу DTO.
7. `src/Controllers/OrdersController.cs` — добавляется действие `GET /orders`.
8. `src/Models/Order.cs` — добавляется составной индекс `(OwnerId, Created, Id)`.
9. `src/Migrations/` — EF генерирует миграцию на составной индекс.
10. `tests/Orders.Tests/Api/PaginationDemoTests.cs` — демонстратор проблемы; `tests/Orders.Tests/Api/OrdersPaginationTests.cs` — контрактные тесты нового эндпоинта.

## 3. `PaginationParams`

Путь: `src/Application/Common/PaginationParams.cs`  
Namespace: `Orders.Application.Common`  
Тип: новый/изменённый stub, уже существует в рабочем дереве.

```csharp
/// <summary>
/// Базовые параметры страничного запроса: номер страницы и размер страницы.
/// </summary>
/// <remarks>
/// Класс находится в Application/Common, потому что параметры страницы не зависят от Order,
/// EF Core или PostgreSQL. Он используется как входная модель для GET /orders.
/// Значения по умолчанию совпадают с контрактом: page = 1, pageSize = 20.
/// Sort не включается: порядок зафиксирован планом как Created DESC, Id DESC.
/// Класс не sealed, чтобы order-специфичный GetOrdersQuery мог его расширить.
/// </remarks>
public class PaginationParams
{
    /// <summary>
    /// Номер запрашиваемой страницы.
    /// </summary>
    /// <remarks>
    /// Нумерация начинается с 1. Значение меньше 1 отклоняется как 400
    /// через ModelState в контроллере с [ApiController].
    /// </remarks>
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    /// <summary>
    /// Количество записей на одной странице.
    /// </summary>
    /// <remarks>
    /// Допустимый диапазон 1..100 включительно. Значение 0, 101 и нечисловое значение
    /// отклоняются как 400. По умолчанию 20.
    /// </remarks>
    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Смещение в строках для SQL-запроса.
    /// </summary>
    /// <remarks>
    /// Вычисляется как (Page - 1) * PageSize. Тип long исключает переполнение
    /// при очень большом Page; OrdersVault проверяет результат перед передачей в EF Skip,
    /// который принимает int.
    /// </remarks>
    public long Skip => (long)(Page - 1) * PageSize;
}
```

### Что делает класс

1. Даёт единую модель для номеров страниц и размера страницы.
2. Держит валидацию на границе запроса, а не в БД-репозитории.
3.的计算ывает `Skip` так, чтобы репозиторий не дублировал формулу `(Page - 1) * PageSize`.

## 4. `PagedResult<T>`

Путь: `src/Application/Common/PagedResult.cs`  
Namespace: `Orders.Application.Common`  
Тип: новый/изменённый stub; текущий файл `src/Models/PagedResult.cs` не перезаписывался.

```csharp
/// <summary>
/// Страничный результат с элементами и метаданными запрошенной страницы.
/// </summary>
/// <remarks>
/// Универсальный контейнер: Items типизирован T, поэтому один класс работает
/// и для доменных Order, и для OrderDto. Используется как результат IVault.GetPageAsync
/// и как тип ответа GET /orders. Текущий stub был не-generic; generic-вариант сохраняет
/// тип Items и не требует object или отдельного PagedOrderDto.
/// </remarks>
public sealed class PagedResult<T>
{
    /// <summary>
    /// Элементы текущей страницы.
    /// </summary>
    /// <remarks>
    /// IReadOnlyList<T> не даёт внешнему коду изменить результат после создания
    /// и корректно сериализуется в JSON-массив.
    /// </remarks>
    public IReadOnlyList<T> Items { get; }

    /// <summary>
    /// Номер страницы, которая была запрошена.
    /// </summary>
    /// <remarks>Значение возвращается в ответе как page.</remarks>
    public int Page { get; }

    /// <summary>
    /// Размер страницы, который был применён к запросу.
    /// </summary>
    /// <remarks>Значение возвращается в ответе как pageSize.</remarks>
    public int PageSize { get; }

    /// <summary>
    /// Количество элементов после фильтрации и до Skip/Take.
    /// </summary>
    /// <remarks>
    /// Считается в OrdersVault до применения страницы. В ответе используется
    /// для навигации клиента; демо-тест не проверяет его как критерий корректности.
    /// Тип long покрывает большие объёмы, например 100 000 заказов.
    /// </remarks>
    public long TotalCount { get; }

    /// <summary>
    /// Создаёт страничный результат.
    /// </summary>
    /// <remarks>
    /// ПроверяетItems на null, Page >= 1, PageSize в диапазоне 1..100
    /// и TotalCount >= 0. Это исключает некорректные метаданные в API-ответе.
    /// </remarks>
    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, long totalCount)
}
```

### Что делает класс

1. Переносит из репозитория в ответ единый набор полей: `items`, `page`, `pageSize`, `totalCount`.
2. Остаётся типобезопасным за счёт `T`.
3. Защищает ответ от внешней мутации через `IReadOnlyList<T>`.

## 5. `GetOrdersQuery`

Путь: `src/DTOs/Orders/GetOrdersQuery.cs`  
Namespace: `Orders.DTOs.Orders`  
Тип: новый класс.

```csharp
/// <summary>
/// Входные параметры запроса GET /orders.
/// </summary>
/// <remarks>
/// Класс лежит рядом с другими DTO заказов, потому что поле Status относится именно
/// к заказам. Общие поля Page и PageSize наследуются от PaginationParams.
/// Контроллер связывает модель через [FromQuery] GetOrdersQuery query.
/// </remarks>
public sealed class GetOrdersQuery : PaginationParams
{
    /// <summary>
    /// Необязательный фильтр по статусу заказа.
    /// </summary>
    /// <remarks>
    /// Передаётся в query string. Пустая строка и пробелы означают отсутствие фильтра.
    /// Значение проверяется только как наличие/отсутствие; формат статуса планом не ограничен.
    /// </remarks>
    public string? Status { get; init; }

    /// <summary>
    /// Нормализованное значение фильтра статуса.
    /// </summary>
    /// <remarks>
    /// Возвращает null для null, пустой строки и строки из пробелов; иначе возвращает
    /// Status без ведущих и завершающих пробелов. Репозиторий получает уже нормализованное
    /// значение и не знает о форматах query string.
    /// </remarks>
    public string? NormalizedStatus => string.IsNullOrWhiteSpace(Status) ? null : Status.Trim();
}
```

### Что делает класс

1. Добавляет к общим параметрам страницы фильтр `Status`, предусмотренный планом.
2. Нормализует пустой фильтр до `null`, чтобы `OrdersVault` не добавлял лишний `WHERE`.
3. Остаётся DTO-моделью запроса: не знает о EF Core и не обращается к БД.

## 6. `IVault`

Путь: `src/Repositories/Interfaces/IVault.cs`  
Namespace: `Orders.Repositories.Interfaces`  
Тип: существующий интерфейс; ниже показан только новый член.

```csharp
/// <summary>
/// Репозиторий заказов с изоляцией по владельцу.
/// </summary>
public interface IVault
{
    /// <summary>
    /// Возвращает страницу заказов текущего владельца.
    /// </summary>
    /// <remarks>
    /// ownerId всегда ограничивает выборку. status является необязательным фильтром.
    /// Сортировка, подсчёт TotalCount, Skip и Take выполняются в реализации репозитория,
    /// чтобы в память не попадали все заказы владельца.
    /// </remarks>
    Task<PagedResult<Order>> GetPageAsync(
        Guid ownerId,
        int page,
        int pageSize,
        string? status,
        CancellationToken cancellationToken = default);
}
```

### Что делает класс

1. Фиксирует контракт страничной выборки на уровне абстракции репозитория.
2. Оставляет `GetAllAsync` для совместимого `GET /orders/all`, пока тот не удалён отдельным шагом.
3. Передаёт `CancellationToken` для отмены запроса к БД.

## 7. `OrdersVault`

Путь: `src/Repositories/Implementations/OrdersVault.cs`
Namespace: `Orders.Repositories.Implementations`  
Тип: существующий класс; ниже показана только новая реализация.

```csharp
/// <summary>
/// PostgreSQL-реализация репозитория заказов.
/// </summary>
public class OrdersVault : IVault
{
    /// <summary>
    /// Возвращает страницу заказов владельца одним SQL-запросом с фильтрацией и сортировкой.
    /// </summary>
    /// <remarks>
    /// Порядок операций: AsNoTracking, WHERE OwnerId, optional WHERE Status,
    /// CountAsync до Skip/Take, ORDER BY Created DESC, Id DESC, Skip, Take, ToListAsync.
    /// AsNoTracking уместен, потому что результат только читается и мапится в DTO.
    /// Если Skip больше int.MaxValue, возвращается пустая страница с уже посчитанным
    /// TotalCount без вызова EF Skip, который принимает int.
    /// </remarks>
    public Task<PagedResult<Order>> GetPageAsync(
        Guid ownerId,
        int page,
        int pageSize,
        string? status,
        CancellationToken cancellationToken = default)
}
```

### Что делает класс

1. Формирует `IQueryable<Order>` с обязательным фильтром `OwnerId`.
2. Добавляет `Where(o => o.Status == status)` только когда `status` не `null`.
3. Считает `TotalCount` после фильтров и до `Skip/Take`.
4. Применяет стабильную сортировку `Created DESC, Id DESC`.
5. Применяет `Skip((int)skip).Take(pageSize)` и получает только запрошенную страницу.
6. Возвращает `PagedResult<Order>`; маппинг в `OrderDto` остаётся вне репозитория.

## 8. `OrderMappings`

Путь: `src/DTOs/Mappers/OrderMappings.cs`  
Namespace: `Orders.DTOs.Mappers`  
Тип: существующий static-класс; ниже показан только новый член.

```csharp
/// <summary>
/// Единый источник маппинга Order в OrderDto.
/// </summary>
public static class OrderMappings
{
    /// <summary>
    /// Преобразует страницу доменных заказов в страницу DTO.
    /// </summary>
    /// <remarks>
    /// Использует существующий ProjectionExpr через ToDtos, поэтому поля OrderDto
    /// не дублируются. OwnerId по-прежнему не попадает в ответ.
    /// </remarks>
    public static PagedResult<OrderDto> ToPagedDto(this PagedResult<Order> source)
}
```

### Что делает класс

1. Сохраняет один источник правды для полей `OrderDto`.
2. Переносит метаданные `Page`, `PageSize`, `TotalCount` в страницу DTO без пересчёта.
3. Не добавляет новый маппер с отдельным списком полей.

## 9. `OrdersController`

Путь: `src/Controllers/OrdersController.cs`  
Namespace: `Orders.Controllers`  
Тип: существующий класс; ниже показано только новое действие.

```csharp
/// <summary>
/// Операции над заказами текущего пользователя.
/// </summary>
[ApiController]
[Route("[controller]")]
[Authorize(Policy = "orders.write")]
public class OrdersController : ControllerBase
{
    /// <summary>
    /// Возвращает страницу заказов текущего пользователя.
    /// </summary>
    /// <remarks>
    /// Маршрут: GET /orders?page=1&pageSize=20&status=Processing.
    /// query связывается через [FromQuery]; невалидные Page/PageSize дают 400
    /// автоматически благодаря [ApiController] и DataAnnotations в PaginationParams.
    /// Владелец берётся из User.GetUserId(), а не из query.
    /// GET /orders/all остаётся для совместимости.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetOrders([FromQuery] GetOrdersQuery query, CancellationToken cancellationToken)
}
```

### Что делает класс

1. Принимает и валидирует HTTP-параметры на границе API.
2. Вызывает `_orders.GetPageAsync(currentUserId, query.Page, query.PageSize, query.NormalizedStatus, cancellationToken)`.
3. Мапит результат через `source.ToPagedDto()` и возвращает `Ok(...)`.

## 10. `Order`

Путь: `src/Models/Order.cs`  
Namespace: `Orders.Models`  
Тип: существующий класс; новые члены не добавляются, изменяется атрибут класса.

```csharp
/// <summary>
/// Заказ; OwnerId задаёт границу видимости для текущего пользователя.
/// </summary>
/// <remarks>
/// Индекс (OwnerId, Created, Id) соответствует запросу страницы:
/// фильтр по владельцу и стабильная сортировка Created DESC, Id DESC.
/// Существующий индекс OwnerId сохраняется до отдельного решения о его удалении.
/// </remarks>
[Index(nameof(OwnerId))]
[Index(nameof(OwnerId), nameof(Created), nameof(Id))]
public class Order
```

### Что делает класс

1. Описывает доменный заказ.
2. Добавляет составной индекс под страничную выборку владельца.
3. Не меняет состав полей `Order` на этом шаге.

## 11. Миграция составного индекса

Путь: `src/Migrations/<timestamp>_AddOrdersOwnerIdCreatedIdIndex.cs`  
Namespace: совпадает с остальными миграциями проекта  
Тип: генерируемый класс; вручную не пишется.

```csharp
/// <summary>
/// Добавляет составной индекс Orders(OwnerId, Created, Id).
/// </summary>
/// <remarks>
/// Индекс создаётся миграцией после изменения атрибутов Order.
/// Имя файла и timestamp определяет EF Core.
/// </remarks>
public partial class AddOrdersOwnerIdCreatedIdIndex : Migration
{
    /// <summary>
    /// Создаёт индекс в базе данных.
    /// </summary>
    protected override void Up(MigrationBuilder migrationBuilder);

    /// <summary>
    /// Удаляет индекс при откате миграции.
    /// </summary>
    protected override void Down(MigrationBuilder migrationBuilder);
}
```

### Что делает класс

1. Переносит изменение индекса в историю схемы БД.
2. Позволяет применить и откатить изменение через стандартные миграции EF Core.

## 12. Тесты

### 12.1. `PaginationDemoTests`

Путь: `tests/Orders.Tests/Api/PaginationDemoTests.cs`  
Namespace: `Orders.Tests.Api`  
Тип: уже создан в рабочем дереве.

```csharp
/// <summary>
/// Демонстратор проблемы unbounded-списка заказов.
/// </summary>
public class PaginationDemoTests : IClassFixture<OrdersApiFactory>
{
    /// <summary>
    /// Создаёт тест с фабрикой API и выводом xUnit.
    /// </summary>
    public PaginationDemoTests(OrdersApiFactory factory, ITestOutputHelper output);

    /// <summary>
    /// Проверяет, что GET /orders возвращает не больше pageSize записей.
    /// </summary>
    /// <remarks>
    /// Theory выполняется для count 0 и 100. Тест проверяет page, pageSize
    /// и количество элементов Math.Min(count, pageSize). Точное totalCount
    /// не используется как критерий корректности.
    /// </remarks>
    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [Trait("Category", "ProblemDemo")]
    public Task GetAll_WithPageSize_ReturnsPage(int count);
}
```

### 12.2. `OrdersPaginationTests`

Путь: `tests/Orders.Tests/Api/OrdersPaginationTests.cs`  
Namespace: `Orders.Tests.Api`  
Тип: новый класс.

```csharp
/// <summary>
/// Контрактные тесты страничного эндпоинта GET /orders.
/// </summary>
public class OrdersPaginationTests : IClassFixture<OrdersApiFactory>
{
    /// <summary>
    /// Создаёт тест с фабрикой API.
    /// </summary>
    public OrdersPaginationTests(OrdersApiFactory factory);

    /// <summary>
    /// Проверяет, что первая и вторая страницы не пересекаются и порядок стабилен.
    /// </summary>
    public Task GetOrders_FirstAndSecondPages_DoNotOverlap();

    /// <summary>
    /// Проверяет, что невалидный размер страницы даёт 400.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public Task GetOrders_InvalidPageSize_Returns400(int pageSize);

    /// <summary>
    /// Проверяет, что фильтр по статусу работает только внутри текущего владельца.
    /// </summary>
    public Task GetOrders_StatusFilter_UsesCurrentOwnerOnly();
}
```

## 13. Best practice, зафиксированные в схеме

1. Валидация `page` и `pageSize` живёт во входной модели и на границе API; репозиторий получает уже корректные значения.
2. Репозиторий возвращает доменные `Order`, а не `OrderDto`; маппинг остаётся в `OrderMappings`.
3. `AsNoTracking()` используется для страничного чтения, потому что сущности не меняются.
4. `TotalCount` считается до `Skip/Take`, иначе он был бы равен размеру страницы.
5. Стабильная сортировка включает `Id`, потому что `Created` может совпадать у нескольких заказов.
6. Составной индекс повторяет порядок фильтра и сортировки запроса.
7. `Skip` вычисляется как `long`, чтобы крайние значения `Page` не вызывали переполнение.
8. Новые зависимости не добавляются: достаточно возможностей ASP.NET Core, EF Core и xUnit, уже используемых проектом.
