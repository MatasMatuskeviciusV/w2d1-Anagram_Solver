using AnagramSolver.EF.CodeFirst.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AnagramSolver.EF.CodeFirst.Data.Configurations;

public class WordConfiguration : IEntityTypeConfiguration<Word>
{
    public void Configure(EntityTypeBuilder<Word> b)
    {
        b.ToTable("Words");

        b.HasKey(x => x.Id);

        b.Property(x => x.Value).IsRequired();

        b.HasIndex(x => x.Value).IsUnique();

        b.Property(x => x.IsApproved).IsRequired();

        b.Property(x => x.CreatedAt).IsRequired();

        b.HasOne(x => x.Category).WithMany(c => c.Words).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.SetNull);
    }
}