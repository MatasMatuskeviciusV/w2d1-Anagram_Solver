using Microsoft.EntityFrameworkCore;
using AnagramSolver.EF.CodeFirst.Data;
using AnagramSolver.EF.CodeFirst.Models;
using System.Text;

var options = new DbContextOptionsBuilder<AnagramDbContext>().UseSqlServer("Server=localhost;Database=AnagramSolver_CF;Trusted_Connection=True;TrustServerCertificate=True").Options;

using var db = new AnagramDbContext(options);

Console.OutputEncoding = UTF8Encoding.UTF8;
Console.InputEncoding = UTF8Encoding.UTF8;

if (!await db.Categories.AnyAsync())
{
    db.Categories.AddRange(
        new Category { Name = "daiktavardis" },
        new Category { Name = "veiksmažodis" },
        new Category { Name = "būdvardis" },
        new Category { Name = "prieveiksmis" },
        new Category { Name = "skaitvardis" }
        );
    await db.SaveChangesAsync();
}

var dktId = await db.Categories.Where(c => c.Name == "daiktavardis").Select(c => c.Id).FirstAsync();
var vksmId = await db.Categories.Where(c => c.Name == "veiksmažodis").Select(c => c.Id).FirstAsync();
var bdvId = await db.Categories.Where(c => c.Name == "būdvardis").Select(c => c.Id).FirstAsync();
var prvkId = await db.Categories.Where(c => c.Name == "prieveiksmis").Select(c => c.Id).FirstAsync();
var sktvId = await db.Categories.Where(c => c.Name == "skaitvardis").Select(c => c.Id).FirstAsync();

static async Task InsertWordAsync(AnagramDbContext db, string value, int? categoryId)
{
    if(!await db.Words.AnyAsync(w => w.Value == value))
    {
        db.Words.Add(new Word { Value = value, CategoryId = categoryId });
    }
}

await InsertWordAsync(db, "alus", dktId);
await InsertWordAsync(db, "bėgti", vksmId);
await InsertWordAsync(db, "puikus", bdvId);
await InsertWordAsync(db, "smagiai", prvkId);
await InsertWordAsync(db, "ketvirtas", sktvId);
await InsertWordAsync(db, "jis", null);

await db.SaveChangesAsync();

var allWords = await db.Words.Include(w => w.Category).Take(10).ToListAsync();

Console.WriteLine("Visi zodziai ir ju kategorijos: ");
foreach(var word in allWords)
{
    Console.WriteLine($"Zodis: {word.Value}, kategorija: {word.Category?.Name ?? "[No category]"}");
}


var dktv = await db.Words.Include(w => w.Category).Where(w => w.Category != null && w.Category.Name == "daiktavardis").Take(10).ToListAsync();

Console.WriteLine("Only daiktavardziai: ");
foreach(var word in dktv)
{
    Console.WriteLine(word.Value);
}

var bdv = await db.Words.Include(w => w.Category).Where(w => w.Category != null && w.Category.Name == "būdvardis").Take(10).ToListAsync();

Console.WriteLine("Only budvardziai: ");
foreach (var word in bdv)
{
    Console.WriteLine(word.Value);
}

var vksm = await db.Words.Include(w => w.Category).Where(w => w.Category != null && w.Category.Name == "veiksmažodis").Take(10).ToListAsync();

Console.WriteLine("Only veiksmazodziai: ");
foreach (var word in vksm)
{
    Console.WriteLine(word.Value);
}


var counts = await db.Words.Include(w => w.Category).GroupBy(w => w.Category == null ? "[No category]" : w.Category.Name)
    .Select(g => new { Category = g.Key, Count = g.Count() }).OrderBy(x => x.Category).Take(10).ToListAsync();

Console.WriteLine("Words in each category: ");
foreach(var line in counts)
{
    Console.WriteLine($"{line.Category}: {line.Count}"); 
}



var noCategoryId = await db.Words.Where(w => w.CategoryId == null).Take(10).ToListAsync();

Console.WriteLine("Words with no category: ");

foreach (var word in noCategoryId)
{
    Console.WriteLine(word.Value);
}


/*using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

var filePath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "zodynas.txt");

var batchSize = (args.Length > 1 && int.TryParse(args[1], out var bs)) ? bs : 5000;

await ImportDictionary.RunAsync(filePath, batchSize);*/