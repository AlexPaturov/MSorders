using System.Collections.ObjectModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Orders.Controllers;
using Orders.DTOs.Orders;
using Orders.Models;
using Orders.Repositories.Interfaces;

namespace Orders.Tests.Controllers;

public class OrdersControllerTests
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetAll_ReturnsOk_WithMappedOrders()
    {
        var first = NewOrder("First", "new");
        var second = NewOrder("Second", "paid");
        var vault = new FakeVault
        {
            AllOrders = new ReadOnlyDictionary<Guid, Order>(new Dictionary<Guid, Order>
            {
                [first.Id] = first,
                [second.Id] = second
            })
        };
        var controller = CreateController(vault);

        var result = await controller.GetAll(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var orders = Assert.IsAssignableFrom<IEnumerable<OrderDto>>(ok.Value).ToList();
        Assert.Equal(TestUserId, vault.LastGetAllOwnerId);
        Assert.Equal(2, orders.Count);
        Assert.Contains(orders, order => order.Id == first.Id && order.Name == "First" && order.Status == "new");
        Assert.Contains(orders, order => order.Id == second.Id && order.Name == "Second" && order.Status == "paid");
    }

    [Fact]
    public async Task GetById_WhenOrderExists_ReturnsOk_WithMappedOrder()
    {
        var order = NewOrder("Existing", "new");
        var vault = new FakeVault { OrderById = order };
        var controller = CreateController(vault);

        var result = await controller.GetById(order.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<OrderDto>(ok.Value);
        Assert.Equal(order.Id, dto.Id);
        Assert.Equal(order.Name, dto.Name);
        Assert.Equal(order.Status, dto.Status);
        Assert.Equal(order.Created, dto.Created);
    }

    [Fact]
    public async Task GetById_WhenOrderMissing_Returns404_WithProblemDetails()
    {
        var vault = new FakeVault { OrderById = null };
        var controller = CreateController(vault);

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("Order not found.", problem.Detail);
    }

    [Fact]
    public async Task UpdateBatch_WhenListIsEmpty_Returns400_WithProblemDetails()
    {
        var controller = CreateController(new FakeVault());

        var result = await controller.UpdateBatch(new List<BatchUpdateOrderDto>(), CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("At least one order is required.", problem.Detail);
    }

    [Fact]
    public async Task UpdateBatch_WhenListIsNull_Returns400_WithProblemDetails()
    {
        var controller = CreateController(new FakeVault());

        var result = await controller.UpdateBatch(null!, CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("At least one order is required.", problem.Detail);
    }

    [Fact]
    public async Task UpdateBatch_WhenOrderMissing_Returns404_WithProblemDetails()
    {
        var vault = new FakeVault { UpdatedOrders = null };
        var controller = CreateController(vault);

        var result = await controller.UpdateBatch(
            new List<BatchUpdateOrderDto>
            {
                new() { Id = Guid.NewGuid(), Name = "Missing", Status = "paid" }
            },
            CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("One or more orders not found.", problem.Detail);
    }

    [Fact]
    public async Task UpdateBatch_WhenOrdersExist_ReturnsOk_WithMappedOrders()
    {
        var id = Guid.NewGuid();
        var vault = new FakeVault
        {
            UpdatedOrders = new[]
            {
                new Order { Id = id, Name = "Changed", Status = "paid", Created = DateTime.UtcNow }
            }
        };
        var controller = CreateController(vault);

        var result = await controller.UpdateBatch(
            new List<BatchUpdateOrderDto>
            {
                new() { Id = id, Name = "Changed", Status = "paid" }
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.Single(Assert.IsAssignableFrom<IEnumerable<OrderDto>>(ok.Value));
        Assert.Equal(id, dto.Id);
        Assert.Equal("Changed", dto.Name);
        Assert.Equal("paid", dto.Status);

        var input = Assert.Single(vault.LastUpdateInput!);
        Assert.Equal(id, input.Id);
        Assert.Equal("Changed", input.Name);
        Assert.Equal("paid", input.Status);
    }

    [Fact]
    public async Task Delete_WhenOrderExists_ReturnsNoContent()
    {
        var vault = new FakeVault { DeleteResult = true };
        var controller = CreateController(vault);

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Delete_WhenOrderMissing_Returns404_WithProblemDetails()
    {
        var vault = new FakeVault { DeleteResult = false };
        var controller = CreateController(vault);

        var result = await controller.Delete(Guid.NewGuid(), CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("Order not found.", problem.Detail);
    }

    [Fact]
    public async Task Create_ReturnsCreated_WithLocationAndServerFields()
    {
        var vault = new FakeVault();
        var controller = CreateController(vault);

        var result = await controller.Create(
            new CreateOrderDto { Name = "Created", Status = "new" },
            CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(nameof(OrdersController.GetById), created.ActionName);
        Assert.NotNull(created.RouteValues);

        var dto = Assert.IsType<OrderDto>(created.Value);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("Created", dto.Name);
        Assert.Equal("new", dto.Status);
        Assert.True(dto.Created > DateTime.MinValue);
        Assert.Equal(dto.Id, Assert.IsType<Guid>(created.RouteValues["id"]));
        Assert.NotNull(vault.LastInserted);
        Assert.Equal(TestUserId, vault.LastInserted.OwnerId);
    }

    private static OrdersController CreateController(FakeVault vault)
    {
        return new OrdersController(vault)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim("sub", TestUserId.ToString()) },
                        "Test"))
                },
                ActionDescriptor = new ControllerActionDescriptor
                {
                    ControllerName = "Orders",
                    ActionName = nameof(OrdersController.Create)
                }
            }
        };
    }

    private static Order NewOrder(string name, string status)
    {
        return new Order
        {
            Id = Guid.NewGuid(),
            Name = name,
            Status = status,
            Created = DateTime.UtcNow
        };
    }

    private sealed class FakeVault : IVault
    {
        public ReadOnlyDictionary<Guid, Order> AllOrders { get; init; } =
            new(new Dictionary<Guid, Order>());

        public Order? OrderById { get; init; }

        public IReadOnlyCollection<Order>? UpdatedOrders { get; init; }

        public bool DeleteResult { get; init; }

        public IReadOnlyCollection<Order>? LastUpdateInput { get; private set; }

        public Order? LastInserted { get; private set; }

        public Guid? LastGetAllOwnerId { get; private set; }

        public Task<ReadOnlyDictionary<Guid, Order>> GetAllAsync(Guid ownerId, CancellationToken cancellationToken = default)
        {
            LastGetAllOwnerId = ownerId;
            return Task.FromResult(AllOrders);
        }

        public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(OrderById);
        }

        public Task<IReadOnlyCollection<Order>?> UpdateBatchAsync(
            IReadOnlyCollection<Order> orders,
            CancellationToken cancellationToken = default)
        {
            LastUpdateInput = orders;
            return Task.FromResult(UpdatedOrders);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DeleteResult);
        }

        public Task<bool> InsertAsync(Guid ownerId, Order order, CancellationToken cancellationToken = default)
        {
            if (order.Id == Guid.Empty)
                order.Id = Guid.NewGuid();

            if (order.Created == default)
                order.Created = DateTime.UtcNow;

            order.OwnerId = ownerId;
            LastInserted = order;
            return Task.FromResult(true);
        }
    }
}
