using AnagramSolver.EF.CodeFirst.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AnagramSolver.EF.CodeFirst.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.ToTable("Categories");

        b.HasKey(c => c.Id);

        b.Property(x => x.Name).IsRequired();

        b.HasIndex(x => x.Name).IsUnique();
    }
}