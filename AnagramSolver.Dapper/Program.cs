using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using AnagramSolver.Dapper.Models;

var connectionString = "Server=localhost;Database=AnagramSolver_CF;Trusted_Connection=True;TrustServerCertificate=True";

await using var connection  = new SqlConnection(connectionString);
await connection.OpenAsync();

Console.WriteLine("All words:");
var words = (connection.Query<Word>("SELECT Value FROM dbo.Words")).ToList();

foreach (var word in words)
{
    Console.WriteLine(word.Value);
}


connection.Execute(
    @"IF NOT EXISTS (SELECT 1 FROM dbo.Words WHERE Value = @Value)
    BEGIN
        INSERT INTO dbo.Words (Value, CategoryId, CreatedAt, WordLengthColumn)
        VALUES (@Value, @CategoryId, @CreatedAt, @WordLengthColumn)
    END",

    new
    {
        Value = "Pompa",
        CategoryId = 1,
        CreatedAt = DateTime.UtcNow,
        WordLengthColumn = 5
    });

Console.WriteLine("All words after insert: ");
var wordsAfterInsert = (connection.Query<Word>("SELECT Value FROM dbo.Words")).ToList();
foreach (var word in wordsAfterInsert)
{
    Console.WriteLine(word.Value);
}

connection.Execute(
    @"UPDATE dbo.Words
    SET Value = @Value, WordLengthColumn = @WordLengthColumn 
    WHERE Id = @Id",

    new
    {
        Id = 7,
        Value = "pompos",
        WordLengthColumn = 6
    });

Console.WriteLine("All words after update: ");
var wordsAfterUpdate = (connection.Query<Word>("SELECT * FROM dbo.Words")).ToList();
foreach (var word in wordsAfterUpdate)
{
    Console.WriteLine(word.Value);
}


connection.Execute(
    @"IF EXISTS (SELECT 1 FROM dbo.Words WHERE Id = @Id)
    BEGIN
        DELETE FROM dbo.Words WHERE Id = @Id
    END",
    new
    {
        Id = 8
    });

Console.WriteLine("All words after deletion: ");
var wordsAfterDelete = (connection.Query<Word>("SELECT * FROM dbo.Words")).ToList();
foreach (var word in wordsAfterDelete)
{
    Console.WriteLine(word.Value);
}