using System;
using System.Collections.Generic;
using System.Text;

namespace AnagramSolver.Dapper.Models
{
    public class Word
    {
        public int Id { get; set; }
        public string Value { get; set; }
        public int? CategoryId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int? WordLengthColumn { get; set; }
        public bool? IsApproved { get; set; }
    }
}
