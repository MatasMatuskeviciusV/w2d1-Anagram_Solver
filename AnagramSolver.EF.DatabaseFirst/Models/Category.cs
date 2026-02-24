using System;
using System.Collections.Generic;

namespace AnagramSolver.EF.DatabaseFirst.Models;

public partial class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Word> Words { get; set; } = new List<Word>();
}
