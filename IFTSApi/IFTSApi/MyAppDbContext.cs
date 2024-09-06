using Microsoft.EntityFrameworkCore;

public class IFTSDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public IFTSDbContext(DbContextOptions<IFTSDbContext> options): base(options){}
    public Microsoft.EntityFrameworkCore.DbSet<Catalog> Catalog { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Catalog>().HasNoKey(); // Configura l'entità come senza chiave
        base.OnModelCreating(modelBuilder);
    }
}