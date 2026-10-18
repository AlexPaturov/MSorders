using Microsoft.EntityFrameworkCore;

namespace Orders.Infrastructure;

public class SqlDbContext : DbContext
{
    public SqlDbContext(DbContextOptions<SqlDbContext> options) : base(options){ }
    
    public DbSet<Order> Orders => Set<Order>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .Property(o => o.Created)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}