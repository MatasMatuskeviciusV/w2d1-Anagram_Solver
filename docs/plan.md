# Technical Plan: Word Frequency Analysis for AnagramSolver

## 1. Goal and Scope
Implement a new endpoint `POST /api/analysis/frequency` that accepts raw text and returns:
- Top 10 most frequent words
- Total word count
- Unique word count
- Longest word

Constraints from research:
- Ignore stop words
- Case-insensitive analysis
- Robust handling of punctuation/special characters
- Deterministic ordering for ties
- `400 Bad Request` for structurally invalid payloads
- `200 OK` with zeroed metrics for valid but analyzable-empty text

Out of scope:
- Any changes to existing anagram-solving behavior
- Database persistence for analysis results

## 2. Architecture Decisions
- Keep controller thin in `AnagramSolver.WebApp/Controllers`.
- Place analysis computation in `AnagramSolver.BusinessLogic`.
- Place service contract in `AnagramSolver.Contracts` to match existing cross-layer pattern (`IAnagramSolver`, repositories).
- Use configuration-based stop words in `appsettings.json` for easy environment override.
- Keep tokenization logic analysis-specific (do not reuse `WordNormalizer` directly).

## 3. File Structure (Create/Modify)
### 3.1 Create
1. `AnagramSolver.Contracts/IWordFrequencyAnalyzer.cs`
2. `AnagramSolver.BusinessLogic/WordFrequencyAnalyzer.cs`
3. `AnagramSolver.WebApp/Controllers/AnalysisController.cs`
4. `AnagramSolver.WebApp/Models/FrequencyAnalysisRequest.cs`
5. `AnagramSolver.WebApp/Models/FrequencyAnalysisResponse.cs`
6. `AnagramSolver.WebApp/Models/FrequentWordDto.cs`
7. `AnagramSolver.Tests/WordFrequencyAnalyzerTests.cs`
8. `AnagramSolver.WebApp.Tests/AnalysisControllerTests.cs`

### 3.2 Modify
1. `AnagramSolver.WebApp/Program.cs`
2. `AnagramSolver.WebApp/appsettings.json`
3. `AnagramSolver.WebApp/appsettings.Development.json` (optional if different stop words are needed in dev)

## 4. Contracts and Class Design

### 4.1 Contracts Layer
File: `AnagramSolver.Contracts/IWordFrequencyAnalyzer.cs`

```csharp
namespace AnagramSolver.Contracts;

public interface IWordFrequencyAnalyzer
{
    WordFrequencyAnalysisResult Analyze(string text, int topN = 10);
}
```

Also add a shared result model in the same file or a dedicated file in `Contracts`:

```csharp
namespace AnagramSolver.Contracts;

public sealed class WordFrequencyAnalysisResult
{
    public required IReadOnlyList<FrequentWordResult> TopWords { get; init; }
    public int TotalWordCount { get; init; }
    public int UniqueWordCount { get; init; }
    public string LongestWord { get; init; } = string.Empty;
}

public sealed class FrequentWordResult
{
    public required string Word { get; init; }
    public int Count { get; init; }
}
```

Notes:
- Keep this result model transport-agnostic (no MVC dependencies).
- `topN` default is 10 to satisfy endpoint requirement while keeping contract reusable.

### 4.2 Business Logic Layer
File: `AnagramSolver.BusinessLogic/WordFrequencyAnalyzer.cs`

```csharp
namespace AnagramSolver.BusinessLogic;

public sealed class WordFrequencyAnalyzer : IWordFrequencyAnalyzer
{
    public WordFrequencyAnalyzer(IEnumerable<string> stopWords);

    public WordFrequencyAnalysisResult Analyze(string text, int topN = 10);

    private static IEnumerable<string> Tokenize(string text);
    private static string NormalizeToken(string token);
}
```

Implementation requirements to follow during development:
- Use invariant case normalization (`ToLowerInvariant`).
- Normalize stop words with the same normalization strategy as input tokens.
- Ignore empty tokens and stop words.
- Frequency map key comparer must be case-insensitive or use pre-normalized tokens.
- Longest word computed from filtered tokens only.
- If multiple words have same max length, deterministic choice: alphabetical ascending.
- Top words ordering: count descending, then word ascending.
- Complexity target: O(n) counting + O(k log k) sort on unique tokens.

### 4.3 Web API Models
Files:
- `AnagramSolver.WebApp/Models/FrequencyAnalysisRequest.cs`
- `AnagramSolver.WebApp/Models/FrequencyAnalysisResponse.cs`
- `AnagramSolver.WebApp/Models/FrequentWordDto.cs`

```csharp
namespace AnagramSolver.WebApp.Models;

public sealed class FrequencyAnalysisRequest
{
    public required string Text { get; set; }
}

public sealed class FrequencyAnalysisResponse
{
    public required IReadOnlyList<FrequentWordDto> TopWords { get; init; }
    public int TotalWordCount { get; init; }
    public int UniqueWordCount { get; init; }
    public string LongestWord { get; init; } = string.Empty;
}

public sealed class FrequentWordDto
{
    public required string Word { get; init; }
    public int Count { get; init; }
}
```

Optional validation enhancement:
- Add `[Required]` to `Text` and keep explicit guard in controller for null body cases.

### 4.4 Web API Controller
File: `AnagramSolver.WebApp/Controllers/AnalysisController.cs`

```csharp
namespace AnagramSolver.WebApp.Controllers;

[ApiController]
[Route("api/analysis")]
public sealed class AnalysisController : ControllerBase
{
    public AnalysisController(IWordFrequencyAnalyzer analyzer);

    [HttpPost("frequency")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<FrequencyAnalysisResponse> AnalyzeFrequency(
        [FromBody] FrequencyAnalysisRequest request,
        CancellationToken ct);
}
```

Controller behavior:
- `400` when request body is missing or model is invalid.
- `200` when request is structurally valid even if no analyzable tokens remain.
- Map contract result (`WordFrequencyAnalysisResult`) to API response DTO.
- `CancellationToken` accepted for consistency with existing controllers (even if current analyzer method is synchronous).

## 5. Endpoint Specification

### 5.1 Endpoint
- Method: `POST`
- Route: `/api/analysis/frequency`
- Content-Type: `application/json`

### 5.2 Request Model
```json
{
  "text": "Some raw text to analyze"
}
```

### 5.3 Success Response (200)
```json
{
  "topWords": [
    { "word": "example", "count": 4 },
    { "word": "text", "count": 2 }
  ],
  "totalWordCount": 9,
  "uniqueWordCount": 5,
  "longestWord": "example"
}
```

### 5.4 Invalid Request Response (400)
Examples:
- Missing request body
- Missing required `text` field (if data annotations are applied)

## 6. Configuration Plan

### 6.1 App Settings Shape
Add to `AnagramSolver.WebApp/appsettings.json`:

```json
{
  "Analysis": {
    "StopWords": ["the", "a", "an", "and", "or", "is", "are"]
  }
}
```

### 6.2 DI Registration
In `AnagramSolver.WebApp/Program.cs`:
- Read `Analysis:StopWords` from configuration.
- Register analyzer as scoped service:

```csharp
builder.Services.AddScoped<IWordFrequencyAnalyzer>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var stopWords = cfg.GetSection("Analysis:StopWords").Get<string[]>() ?? Array.Empty<string>();
    return new WordFrequencyAnalyzer(stopWords);
});
```

Rationale:
- Mirrors existing factory-style DI registrations in the solution.
- Keeps stop-word policy centralized and environment-configurable.

## 7. Task Breakdown (Incremental, Numbered)

### 1. API and Domain Contracts
1.1 Create `IWordFrequencyAnalyzer` in `AnagramSolver.Contracts` with analysis method signature.
1.2 Add contract result models (`WordFrequencyAnalysisResult`, `FrequentWordResult`) in `Contracts`.
1.3 Verify project references do not need changes (BusinessLogic and WebApp already reference Contracts).

### 2. Business Logic Implementation Skeleton
2.1 Create `WordFrequencyAnalyzer` class implementing `IWordFrequencyAnalyzer`.
2.2 Add constructor dependency for stop words (`IEnumerable<string>`).
2.3 Define deterministic tokenization/normalization rules and helper methods.
2.4 Implement frequency counting and longest-word selection rules.
2.5 Implement top-N selection with deterministic tie-break.

### 3. Web API Surface
3.1 Create request/response DTOs in `AnagramSolver.WebApp/Models`.
3.2 Create `AnalysisController` with route `api/analysis` and action `POST frequency`.
3.3 Add model validation and proper `400` handling.
3.4 Map business result model to response DTO.

### 4. Dependency Injection and Configuration
4.1 Add `Analysis:StopWords` section to `appsettings.json`.
4.2 Register `IWordFrequencyAnalyzer` in `Program.cs`.
4.3 (Optional) Override stop words in `appsettings.Development.json` for local experimentation.

### 5. Unit Tests (Business Logic)
5.1 Create `WordFrequencyAnalyzerTests` in `AnagramSolver.Tests`.
5.2 Add deterministic tests for case-insensitive counting.
5.3 Add tests for stop-word exclusion.
5.4 Add tests for punctuation/special-character handling.
5.5 Add tests for empty/whitespace/noise-only input.
5.6 Add tests for input containing only stop words.
5.7 Add tests for tie-breaking order in top words.
5.8 Add tests for longest-word selection and tie-break behavior.

### 6. API Tests (Controller)
6.1 Create `AnalysisControllerTests` in `AnagramSolver.WebApp.Tests`.
6.2 Test `400` on missing/invalid payload.
6.3 Test `200` and zeroed response for valid but analyzable-empty input.
6.4 Test `200` response mapping for normal input and contract shape.
6.5 Test top words are returned in deterministic order.

### 7. Verification and Documentation
7.1 Run `AnagramSolver.Tests` and `AnagramSolver.WebApp.Tests` test suites.
7.2 Verify endpoint appears in Swagger UI.
7.3 Add short endpoint usage note in docs if required by team workflow.

## 8. Test Plan Details

### 8.1 Business Logic Test Scenarios (`AnagramSolver.Tests`)
- `Analyze_WhenTextContainsMixedCase_ShouldCountCaseInsensitively`
- `Analyze_WhenTextContainsStopWords_ShouldExcludeStopWords`
- `Analyze_WhenTextContainsPunctuation_ShouldTokenizeCorrectly`
- `Analyze_WhenTextIsWhitespaceOrSymbolsOnly_ShouldReturnZeroedMetrics`
- `Analyze_WhenAllTokensAreStopWords_ShouldReturnZeroedMetrics`
- `Analyze_WhenFrequenciesTie_ShouldSortAlphabetically`
- `Analyze_WhenLongestWordTies_ShouldPickAlphabeticallyFirst`
- `Analyze_WhenTopNExceeded_ShouldReturnOnlyTop10`

Assertions per test:
- Exact `TopWords` order and counts
- `TotalWordCount` and `UniqueWordCount`
- `LongestWord`

### 8.2 Web API Test Scenarios (`AnagramSolver.WebApp.Tests`)
- `AnalyzeFrequency_WhenRequestBodyMissing_ShouldReturnBadRequest`
- `AnalyzeFrequency_WhenRequestInvalid_ShouldReturnBadRequest`
- `AnalyzeFrequency_WhenNoAnalyzableTokens_ShouldReturnOkWithZeroMetrics`
- `AnalyzeFrequency_WhenValidInput_ShouldReturnExpectedResponseModel`
- `AnalyzeFrequency_WhenTieOccurs_ShouldReturnDeterministicTopWords`

Assertions per test:
- HTTP status code
- Action result type (`BadRequestObjectResult`, `OkObjectResult`)
- Response DTO content and ordering

## 9. Risks and Mitigations in Execution
- Risk: tokenization drift between stop-word list and input processing.
  - Mitigation: normalize stop words using same `NormalizeToken` path as runtime tokens.
- Risk: unstable order for equal counts.
  - Mitigation: enforce explicit secondary sort by `Word` ascending in all results.
- Risk: null handling inconsistencies between model binding and analyzer.
  - Mitigation: validate request in controller before analyzer call and keep analyzer null-safe.

## 10. Acceptance Criteria
- `POST /api/analysis/frequency` is available and documented by Swagger.
- Endpoint returns top 10 words, totals, unique count, and longest word.
- Stop words are ignored via configuration.
- Case-insensitive behavior is verified by tests.
- Empty/special-character-only input returns `200` with zeroed metrics.
- Deterministic tie-breaking is verified in both unit and API tests.
