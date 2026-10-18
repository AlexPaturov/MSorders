using System.Linq.Expressions;
using Orders.DTOs.Orders;
using Orders.DTOs.Mappers;
using Orders.Models;

namespace Orders.Tests;

public class OrderMappingsTests
{
    // Цель класса: держать маппер Order -> OrderDto в синхроне с самими моделями.
    // Идея — брать списки полей из кода во время выполнения (через рефлексию), а не
    // хардкодить их в тесте. Тогда появление нового поля ломает тест автоматически,
    // и списки в тесте переписывать не нужно.

    // Проверяем сторону ИСТОЧНИКА: маппер читает все поля модели Order.
    // Сценарий, который ловим: в Order добавили поле, а в ProjectionExpr — нет.
    // Тогда множество прочитанных полей разойдётся с полным списком свойств Order.
    [Fact]
    public void Projection_reads_every_property_of_Order()
    {
        // Поля, которые реально читает expression-дерево маппера.
        var mappedFromModel = ProjectionBindings()
            .Select(binding => ((MemberExpression)binding.Expression).Member.Name)
            .OrderBy(name => name)
            .ToArray();

        // Полный список публичных свойств модели Order (источник правды — сам тип).
        var modelProperties = typeof(Order)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        // Сравниваем множества: каждое поле модели должно быть прочитано маппером.
        Assert.Equal(modelProperties, mappedFromModel);
    }

    // Проверяем сторону ПРИЁМНИКА: маппер заполняет все поля OrderDto.
    // Сценарий, который ловит: поле добавили в OrderDto, а присвоение в ProjectionExpr забыли —
    // тогда у DTO это поле молча остаётся значением по умолчанию.
    [Fact]
    public void Projection_assigns_every_property_of_OrderDto()
    {
        // Поля OrderDto, которым маппер присваивает значение (левая часть присвоений).
        var assignedTargets = ProjectionBindings()
            .Select(binding => binding.Member.Name)
            .OrderBy(name => name)
            .ToArray();

        // Полный список публичных свойств OrderDto.
        var dtoProperties = typeof(OrderDto)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        // Сравниваем множества: каждое поле DTO должно получать значение из маппера.
        Assert.Equal(dtoProperties, assignedTargets);
    }

    // Достаёт из маппера реальные присвоения вида `new OrderDto { Id = o.Id, ... }`
    // через expression-дерево, построенное публичным методом ProjectToDto.
    // Проверяем именно тот expression, который уходит в EF Core (SELECT нужных колонок),
    // поэтому одна проверка покрывает и ToDto/ToDtos (они компилируют тот же ProjectionExpr).
    private static IEnumerable<MemberAssignment> ProjectionBindings()
    {
        // Пустой запрос достаточен: нам нужно дерево выражения, а не данные,
        // поэтому запрос к БД здесь не выполняется.
        var query = Array.Empty<Order>().AsQueryable();
        var projected = query.ProjectToDto();

        // Разбираем дерево: Queryable.Select -> lambda -> new OrderDto { ... }.
        var selectCall = Assert.IsAssignableFrom<MethodCallExpression>(projected.Expression);
        var quotedLambda = Assert.IsAssignableFrom<UnaryExpression>(selectCall.Arguments[1]);
        var lambda = Assert.IsAssignableFrom<LambdaExpression>(quotedLambda.Operand);
        var init = Assert.IsAssignableFrom<MemberInitExpression>(lambda.Body);

        // Каждый MemberAssignment — одно присвоение поля внутри `new OrderDto { ... }`.
        return init.Bindings.OfType<MemberAssignment>();
    }
}
