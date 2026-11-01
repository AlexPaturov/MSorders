using Microsoft.EntityFrameworkCore;

namespace Orders.Infrastructure;

public class PostgresDbContext : DbContext
{
    public PostgresDbContext(DbContextOptions<PostgresDbContext> options):base(options)
    {
        
    }
    
    public DbSet<Order> Orders => Set<Order>();
 
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>()
            .Property(o => o.Created)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
    
}