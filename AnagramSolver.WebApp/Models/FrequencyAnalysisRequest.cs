using System.ComponentModel.DataAnnotations;

namespace AnagramSolver.WebApp.Models;

public sealed class FrequencyAnalysisRequest
{
    [Required(AllowEmptyStrings = true)]
    public string? Text { get; set; }
}
