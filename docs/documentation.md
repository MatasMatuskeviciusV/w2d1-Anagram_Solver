# Word Frequency Analysis Endpoint Documentation

## Purpose
This change adds a text-analysis API endpoint, `POST /api/analysis/frequency`, to compute frequency-based statistics from raw text.

The feature was introduced to provide:
- Top words by frequency (default top 10)
- Total analyzed word count
- Unique analyzed word count
- Longest analyzed word

The implementation is intentionally isolated from anagram-solving logic so existing anagram behavior is unchanged.

## Implementation Overview
The solution follows the existing layered architecture:
- `AnagramSolver.Contracts` defines the analyzer interface and result contracts.
- `AnagramSolver.BusinessLogic` implements tokenization, normalization, stop-word filtering, and ranking.
- `AnagramSolver.WebApp` exposes the HTTP endpoint, validates input, and maps contract models to API DTOs.
- `AnagramSolver.WebApp/Program.cs` wires the analyzer through DI and reads stop words from configuration.

Core flow:
1. Client sends JSON payload with `text`.
2. `AnalysisController` validates request and cancellation.
3. Controller calls `IWordFrequencyAnalyzer.Analyze(text)`.
4. `WordFrequencyAnalyzer` tokenizes text with regex, normalizes tokens, filters stop words, computes metrics.
5. Controller maps result to response DTO and returns `200 OK`.

## File and Class Responsibilities
- `AnagramSolver.Contracts/IWordFrequencyAnalyzer.cs`: Defines `IWordFrequencyAnalyzer`, `WordFrequencyAnalysisResult`, and `FrequentWordResult`.
- `AnagramSolver.BusinessLogic/WordFrequencyAnalyzer.cs`: Implements token extraction with regex `"[\p{L}\p{Nd}]+"`, normalization (`Trim()` + `ToLowerInvariant()`), stop-word filtering, counting, deterministic sorting, longest-word selection, and `topN` validation.
- `AnagramSolver.WebApp/Controllers/AnalysisController.cs`: Hosts `POST /api/analysis/frequency`, validates request/model/cancellation, invokes analyzer, and maps domain result to response DTO.
- `AnagramSolver.WebApp/Models/FrequencyAnalysisRequest.cs`: Request DTO with required `Text` property (`[Required(AllowEmptyStrings = true)]`).
- `AnagramSolver.WebApp/Models/FrequencyAnalysisResponse.cs`: API response DTO for aggregate metrics and top words.
- `AnagramSolver.WebApp/Models/FrequentWordDto.cs`: Per-word output item (`word`, `count`).
- `AnagramSolver.WebApp/Program.cs`: Registers `IWordFrequencyAnalyzer` with stop words resolved from configuration.
- `AnagramSolver.WebApp/appsettings.json`: Provides default `Analysis:StopWords` values.

## Important Design Decisions
- **Analysis-specific tokenization**: Frequency analysis uses a dedicated tokenization strategy instead of reusing anagram normalization.
- **Culture-safe normalization**: `ToLowerInvariant()` avoids locale-specific surprises.
- **Deterministic outputs**:
- Top words sorted by count descending, then word ascending.
- Longest word sorted by length descending, then word ascending.
- **Config-driven stop words**: Stop-word policy is externalized to app settings for environment-specific overrides.
- **Valid-but-empty is not an error**: Structurally valid requests with no analyzable tokens return `200 OK` with zeroed metrics.

## Public API
### Endpoint
- Method: `POST`
- Route: `/api/analysis/frequency`
- Content-Type: `application/json`

### Request Body
```json
{
  "text": "Apple apple banana"
}
```

### Success Response (`200 OK`)
```json
{
  "topWords": [
    { "word": "apple", "count": 2 },
    { "word": "banana", "count": 1 }
  ],
  "totalWordCount": 3,
  "uniqueWordCount": 2,
  "longestWord": "banana"
}
```

### Contract Interface
`IWordFrequencyAnalyzer`:
- `WordFrequencyAnalysisResult Analyze(string text, int topN = 10)`

## Validation Rules and Error Handling
### Controller-level validation
In `AnalysisController.AnalyzeFrequency(...)`:
- If request body is missing (`request is null`) -> `400 Bad Request` with message `"Request body is required."`
- If model state is invalid -> `400 Bad Request` with model state details
- If `request.Text is null` -> `400 Bad Request` with message `"The Text field is required."`
- If cancellation token is already canceled -> throws `OperationCanceledException` before analyzer call

### Analyzer-level validation and behavior
In `WordFrequencyAnalyzer.Analyze(...)`:
- If `topN < 0` -> throws `ArgumentOutOfRangeException` (`ParamName = "topN"`)
- If input text is null/empty/whitespace -> zeroed result, not exception
- If no tokens remain after tokenization/stop-word filtering -> zeroed result

Zeroed result shape:
- `topWords = []`
- `totalWordCount = 0`
- `uniqueWordCount = 0`
- `longestWord = ""`

## Tokenization and Counting Rules
Implemented in `WordFrequencyAnalyzer`:
- Token regex: `"[\p{L}\p{Nd}]+"`
- Includes Unicode letters and digits.
- Punctuation, apostrophes, and hyphens split tokens (for example, `it's` -> `it`, `s`; `re-enter` -> `re`, `enter`).
- Stop words are normalized using the same token normalization path before entering the internal hash set.

## Configuration
Default stop words are configured in `AnagramSolver.WebApp/appsettings.json`:
```json
"Analysis": {
  "StopWords": [ "the", "a", "an", "and", "or", "is", "are" ]
}
```

DI registration (`AnagramSolver.WebApp/Program.cs`) resolves this list and constructs `WordFrequencyAnalyzer`.

## Testing Summary
Executed during documentation work:
- `AnagramSolver.Tests/WordFrequencyAnalyzerTests.cs`: **16 passed, 0 failed**
- `AnagramSolver.WebApp.Tests/AnalysisControllerTests.cs` and `AnagramSolver.WebApp.Tests/AnalysisControllerApiTests.cs`: **21 passed, 0 failed**

Current automated coverage includes:
- Analyzer unit tests for case-insensitivity, stop-word behavior, punctuation handling, Unicode and alphanumeric tokenization, deterministic tie-breaking, topN edge cases, and null/empty handling.
- Controller unit tests for missing body, invalid model state, cancellation, null text, response mapping, and analyzer invocation contract.
- API integration tests for endpoint shape, status codes, deterministic ordering, stop-word filtering, punctuation-heavy input, large request payload handling, and config override behavior.

## Usage Examples
### cURL
```bash
curl -X POST "https://localhost:5001/api/analysis/frequency" \
  -H "Content-Type: application/json" \
  -d '{"text":"Hello, hello... world-world!!!"}'
```

Expected behavior:
- `hello` and `world` each counted as `2`
- total count `4`, unique count `2`

### Stop-word override example
If `Analysis:StopWords` is overridden to include `apple` and `banana`, then input:
```json
{ "text": "apple banana kiwi" }
```
returns only `kiwi` in `topWords` with total/unique counts of `1`.

## Known Limitations and Future Improvements
- Wrong content-type test currently accepts either `400` or `415` in integration tests; behavior is not yet locked to a single expected status.
- The stop-word override integration test creates a custom `WebApplicationFactory` client without explicit disposal in the test body.
- Large-payload integration test validates correctness but does not enforce an explicit time bound.

Potential future improvements:
- Lock content-type behavior to one status code in tests after confirming expected runtime behavior.
- Add explicit disposal pattern for factory/client in stop-word override test.
- Add a relaxed runtime guard (coarse threshold) for large payload test to catch major regressions.
- Consider optional request parameter for client-controlled `topN` if API requirements expand.
