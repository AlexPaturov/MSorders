using Microsoft.EntityFrameworkCore;

namespace Orders.Infrastructure;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options):base(options)
    {
        
    }
    
    public DbSet<Order> Orders => Set<Order>();
 
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .Property(o => o.Created)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        modelBuilder.Entity<Order>()
            .ToTable("Orders", t =>
                t.HasCheckConstraint(
                    "CK_Orders_OwnerId_NotEmpty",
                    "\"OwnerId\" <> '00000000-0000-0000-0000-000000000000'"));
    }
    
}