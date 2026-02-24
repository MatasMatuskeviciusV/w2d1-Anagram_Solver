using System;
using System.Collections.Generic;

namespace AnagramSolver.EF.DatabaseFirst.Models;

public partial class Word
{
    public int Id { get; set; }

    public string Value { get; set; } = null!;

    public int? CategoryId { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int? WordLengthColumn { get; set; }

    public virtual Category? Category { get; set; }
}
