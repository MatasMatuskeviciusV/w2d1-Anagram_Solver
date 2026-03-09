using AnagramSolver.Contracts;
using AnagramSolver.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace AnagramSolver.WebApp.Controllers;

[ApiController]
[Route("api/analysis")]
public sealed class AnalysisController : ControllerBase
{
    private readonly IWordFrequencyAnalyzer _analyzer;

    public AnalysisController(IWordFrequencyAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    [HttpPost("frequency")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<FrequencyAnalysisResponse> AnalyzeFrequency(
        [FromBody] FrequencyAnalysisRequest? request,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.Text is null)
        {
            return BadRequest("The Text field is required.");
        }

        var result = _analyzer.Analyze(request.Text);
        var response = new FrequencyAnalysisResponse
        {
            TopWords = result.TopWords
                .Select(item => new FrequentWordDto
                {
                    Word = item.Word,
                    Count = item.Count
                })
                .ToArray(),
            TotalWordCount = result.TotalWordCount,
            UniqueWordCount = result.UniqueWordCount,
            LongestWord = result.LongestWord
        };

        return Ok(response);
    }
}
