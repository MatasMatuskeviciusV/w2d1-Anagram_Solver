using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.Text.Json;
using AnagramSolver.Contracts;
using Microsoft.Data.SqlClient;

namespace AnagramSolver.EF.CodeFirst.Repositories
{
    public class DapperSearchLogRepository : ISearchLogRepository
    {
        private readonly string _connectionString;

        public DapperSearchLogRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task AddAsync(string input, IList<string> results, CancellationToken ct = default)
        {
            using var connection = new SqlConnection(_connectionString);

            var sql = @"INSERT INTO SearchLogs (Input, ResultCount, ResultsJson, CreatedAt)
                    VALUES (@Input, @Count, @Json, GETUTCDATE())";

            await connection.ExecuteAsync(sql, new
            {
                Input = input,
                Count = results.Count,
                Json = JsonSerializer.Serialize(results)
            });
        }
    }
}
