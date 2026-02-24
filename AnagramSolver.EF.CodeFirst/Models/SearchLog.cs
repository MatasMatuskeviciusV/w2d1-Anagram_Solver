using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnagramSolver.EF.CodeFirst.Models
{
    public class SearchLog
    {
        public int Id { get; set; }
        public string Input { get; set; } = null!;
        public int ResultCount { get; set; }
        public string? ResultsJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
