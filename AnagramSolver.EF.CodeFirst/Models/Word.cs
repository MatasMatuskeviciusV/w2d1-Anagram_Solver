namespace AnagramSolver.EF.CodeFirst.Models;

public class Word
{
    public int Id { get; set; }
    public string Value { get; set; } = string.Empty;

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public int? WordLengthColumn { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsApproved { get; set;  }
}