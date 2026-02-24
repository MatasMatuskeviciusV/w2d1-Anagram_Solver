using AnagramSolver.EF.CodeFirst.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AnagramSolver.EF.CodeFirst.Data.Configurations;

public class SearchLogConfiguration : IEntityTypeConfiguration<SearchLog>
{
    public void Configure(EntityTypeBuilder<SearchLog> b)
    {
        b.ToTable("SearchLogs");

        b.HasKey(x => x.Id);

        b.Property(x => x.Input).IsRequired();

        b.Property(x => x.ResultCount).IsRequired();

        b.Property(x => x.ResultsJson);

        b.Property(x => x.CreatedAt).IsRequired();
    }
}
