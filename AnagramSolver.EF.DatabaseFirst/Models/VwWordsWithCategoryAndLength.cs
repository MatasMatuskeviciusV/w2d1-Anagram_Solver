using System;
using System.Collections.Generic;

namespace AnagramSolver.EF.DatabaseFirst.Models;

public partial class VwWordsWithCategoryAndLength
{
    public string Word { get; set; } = null!;

    public string? Category { get; set; }

    public int? WordLength { get; set; }
}
