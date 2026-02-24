using System;
using System.Collections.Generic;

namespace AnagramSolver.EF.DatabaseFirst.Models;

public partial class SearchLog
{
    public int Id { get; set; }

    public string SearchText { get; set; } = null!;

    public int ResultCount { get; set; }

    public DateTime? SearchedAt { get; set; }
}
