using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace AnagramSolver.EF.DatabaseFirst.Models;

public partial class AnagramDbContext : DbContext
{
    public AnagramDbContext()
    {
    }

    public AnagramDbContext(DbContextOptions<AnagramDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<SearchLog> SearchLogs { get; set; }

    public virtual DbSet<VwWordsWithCategoryAndLength> VwWordsWithCategoryAndLengths { get; set; }

    public virtual DbSet<Word> Words { get; set; }

    public virtual DbSet<WordsImport> WordsImports { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=localhost;Database=AnagramSolver;Trusted_Connection=True;TrustServerCertificate=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Categori__3214EC07AB0BA409");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(50);
        });

        modelBuilder.Entity<SearchLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__SearchLo__3214EC0728C405AB");

            entity.ToTable("SearchLog");

            entity.Property(e => e.SearchText).HasMaxLength(100);
            entity.Property(e => e.SearchedAt).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<VwWordsWithCategoryAndLength>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_WordsWithCategoryAndLength");

            entity.Property(e => e.Category).HasMaxLength(50);
            entity.Property(e => e.Word).HasMaxLength(100);
        });

        modelBuilder.Entity<Word>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Words__3214EC074D5AF41A");

            entity.HasIndex(e => e.WordLengthColumn, "IDX_Words_Length");

            entity.HasIndex(e => e.Value, "IDX_Words_Value");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Value).HasMaxLength(100);
            entity.Property(e => e.WordLengthColumn).HasComputedColumnSql("(len([Value]))", true);

            entity.HasOne(d => d.Category).WithMany(p => p.Words).HasForeignKey(d => d.CategoryId);
        });

        modelBuilder.Entity<WordsImport>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("Words_Import");

            entity.Property(e => e.Value).HasMaxLength(255);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
