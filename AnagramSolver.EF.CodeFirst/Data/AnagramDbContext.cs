using Microsoft.EntityFrameworkCore;
using AnagramSolver.EF.CodeFirst.Models;

namespace AnagramSolver.EF.CodeFirst.Data;

public class AnagramDbContext : DbContext
{
    public DbSet<Word> Words { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<SearchLog> SearchLogs { get; set; }

    public AnagramDbContext(DbContextOptions<AnagramDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
        {
            options.UseSqlServer("Server=localhost;Database=AnagramSolver_CF;Trusted_Connection=True;TrustServerCertificate=True");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AnagramDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
