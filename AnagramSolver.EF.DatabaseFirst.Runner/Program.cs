using Microsoft.EntityFrameworkCore;
using AnagramSolver.EF.DatabaseFirst.Models;

var connectionString = "Server=localhost;Database=AnagramSolver;Trusted_Connection=True;TrustServerCertificate=True";

var options = new DbContextOptionsBuilder<AnagramDbContext>().UseSqlServer(connectionString).Options;

using var db = new AnagramDbContext(options);

var longWords = await db.Words.Where(w => w.Value.Length > 25).ToListAsync();

Console.WriteLine("Words longer than 25: ");
foreach (var word in longWords)
{
    Console.WriteLine(word.Value);
}

var fiveShortWords = await db.Words.Where(w => w.Value.Length < 8).Take(5).ToListAsync();

Console.WriteLine("Five short words: ");
foreach(var word in fiveShortWords)
{
    Console.WriteLine(word.Value); 
}

var categoryList = await db.Categories.ToListAsync();

Console.WriteLine("Categories: ");
foreach (var category in categoryList)
{
    Console.Write($"{category.Name} ");
}