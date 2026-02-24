using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using AnagramSolver.Contracts;
using Microsoft.Data.SqlClient;

namespace AnagramSolver.EF.CodeFirst.Repositories
{
    public class DapperWordRepository : IWordRepository
    {
        private readonly string _connectionString;

        public DapperWordRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<IEnumerable<string>> GetAllWordsAsync(CancellationToken ct = default)
        {
            using var connection = new SqlConnection(_connectionString);

            var sql = "SELECT Value FROM Words";
            return await connection.QueryAsync<string>(sql);
        }

        public async Task<AddWordResult> AddWordAsync(string word, CancellationToken ct = default)
        {
            using var connection = new SqlConnection(_connectionString);

            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Words WHERE Value = @Value",
                new { Value = word });

            if (exists > 0)
            {
                return AddWordResult.AlreadyExists;
            }

            await connection.ExecuteAsync(
                "INSERT INTO Words (Value, IsApproved, CreatedAt) VALUES (@Value, 1, GETUTCDATE())",
                new { Value = word });

            return AddWordResult.Added;
        }
    }
}
