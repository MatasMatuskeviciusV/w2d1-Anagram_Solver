namespace AnagramSolver.EF.CodeFirst.Models;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Word> Words { get; set; } = new();
}