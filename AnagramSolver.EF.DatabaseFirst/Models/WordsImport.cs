using System;
using System.Collections.Generic;

namespace AnagramSolver.EF.DatabaseFirst.Models;

public partial class WordsImport
{
    public string? Value { get; set; }

    public int? CategoryId { get; set; }
}
