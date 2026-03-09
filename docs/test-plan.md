# Test Plan: Word Frequency Analysis Endpoint

## 1. Purpose
Validate `POST /api/analysis/frequency` end-to-end for correctness, deterministic behavior, and robust error handling, based on `docs/plan.md`.

## 2. Scope
In scope:
- Business logic in `AnagramSolver.BusinessLogic/WordFrequencyAnalyzer.cs`
- API behavior in `AnagramSolver.WebApp/Controllers/AnalysisController.cs`
- DTO contract and HTTP status behavior for `/api/analysis/frequency`
- Stop-word filtering, case-insensitivity, tokenization, top-10 ordering, longest-word behavior

Out of scope:
- Existing anagram endpoints and solver behavior
- Database persistence or analytics storage

## 3. Test Levels
- Unit tests: analyzer and controller unit behavior with mocks
- Integration/API tests: real ASP.NET pipeline via `WebApplicationFactory<Program>`

## 4. Current Coverage Snapshot
Already present:
- `AnagramSolver.Tests/WordFrequencyAnalyzerTests.cs`
- `AnagramSolver.WebApp.Tests/AnalysisControllerTests.cs`
- `AnagramSolver.WebApp.Tests/AnalysisControllerApiTests.cs`

Coverage is good for baseline scenarios, but important risk-based gaps remain (listed below).

## 5. Unit Tests To Add

### 5.1 Analyzer tests to add (`AnagramSolver.Tests/WordFrequencyAnalyzerTests.cs`)
1. `Analyze_WhenTopNIsZero_ShouldReturnNoTopWordsButKeepMetrics`
- Arrange: `"apple apple banana"`, `topN = 0`
- Assert: `TopWords` empty, `TotalWordCount = 3`, `UniqueWordCount = 2`, `LongestWord = "banana"`

2. `Analyze_WhenTopNIsNegative_ShouldThrowArgumentOutOfRangeException`
- Arrange: any valid text, `topN = -1`
- Assert: throws `ArgumentOutOfRangeException` with parameter name `topN`

3. `Analyze_WhenStopWordsContainMixedCaseAndWhitespace_ShouldNormalizeAndExclude`
- Arrange stop words: `" The "`, `"AND"`; text: `"the apple and banana THE"`
- Assert: only `apple`, `banana` remain; correct counts and metrics

4. `Analyze_WhenInputContainsUnicodeLetters_ShouldTokenizeAndCountCorrectly`
- Arrange: `"Žodis žodis Ąžuolas"`
- Assert: case-insensitive counting works with Unicode letters

5. `Analyze_WhenInputContainsDigitsAndWords_ShouldTreatBothAsTokens`
- Arrange: `"r2d2 R2D2 model3"`
- Assert: `r2d2` count is 2; `model3` count is 1

6. `Analyze_WhenInputContainsApostrophesAndHyphens_ShouldSplitPerCurrentRegexRules`
- Arrange: `"it's e-mail re-enter"`
- Assert: tokens are split according to `@[\p{L}\p{Nd}]+` behavior (e.g., `it`, `s`, `e`, `mail`, `re`, `enter`)
- Note: this locks current behavior and prevents accidental tokenization drift

7. `Analyze_WhenCountsAndLengthsTie_ShouldApplyBothDeterministicRules`
- Arrange: text with equal frequencies and equal max lengths, e.g. `"zeta beta"`
- Assert: top words alphabetical; longest word alphabetical among longest

8. `Analyze_WhenTextIsNullAtRuntime_ShouldReturnZeroedMetrics`
- Arrange: call analyzer with `null!` from test code
- Assert: no exception; zeroed result
- Rationale: defensive behavior is currently implemented via `string.IsNullOrWhiteSpace`

### 5.2 Controller unit tests to add (`AnagramSolver.WebApp.Tests/AnalysisControllerTests.cs`)
1. `AnalyzeFrequency_WhenCancellationIsRequested_ShouldThrowOperationCanceledException`
- Arrange: canceled `CancellationToken`
- Assert: `OperationCanceledException` is thrown before analyzer invocation

2. `AnalyzeFrequency_WhenModelStateInvalid_ShouldNotCallAnalyzer`
- Arrange: add model error; mock analyzer
- Assert: returns `BadRequest`; verify analyzer `Analyze` not called

3. `AnalyzeFrequency_WhenRequestTextIsNull_ShouldNotCallAnalyzer`
- Arrange: `Text = null`
- Assert: returns `BadRequest`; analyzer not called

4. `AnalyzeFrequency_WhenValidRequest_ShouldCallAnalyzerWithDefaultTopN10`
- Arrange: valid request and mock
- Assert: analyzer invoked exactly once with request text and `topN = 10`

## 6. Integration Tests To Add

Add to `AnagramSolver.WebApp.Tests/AnalysisControllerApiTests.cs`.

1. `AnalyzeFrequency_WhenValidInput_ShouldReturnExpectedContractShapeAndValues`
- Payload: `{"text":"Apple apple banana"}`
- Assert: `200`, JSON fields present, normalized output values correct

2. `AnalyzeFrequency_WhenTieInFrequency_ShouldReturnDeterministicOrdering`
- Payload: `{"text":"beta alpha"}`
- Assert: `topWords[0].word == "alpha"`, `topWords[1].word == "beta"`

3. `AnalyzeFrequency_WhenOnlyStopWords_ShouldReturnOkWithZeroMetrics`
- Payload based on configured stop words, e.g. `{"text":"the and a"}`
- Assert: `200` with zero metrics and empty `topWords`

4. `AnalyzeFrequency_WhenPunctuationHeavyInput_ShouldReturnOkAndCorrectTokenCounts`
- Payload: `{"text":"Hello, hello... world-world!!!"}`
- Assert: `hello=2`, `world=2`, totals and unique count correct

5. `AnalyzeFrequency_WhenWrongContentType_ShouldReturnClientError`
- Request: `text/plain` or malformed body
- Assert: `400` or `415` depending on ASP.NET model binding behavior in current setup
- Note: record actual expected status once confirmed in test execution

6. `AnalyzeFrequency_WhenAnalysisConfigStopWordsOverridden_ShouldUseOverriddenValues`
- Setup: custom `WebApplicationFactory` override for `Analysis:StopWords`
- Payload: include override stop words
- Assert: words are excluded according to override

7. `AnalyzeFrequency_WhenRequestBodyIsLarge_ShouldReturnWithinReasonableTime`
- Payload: repeated text (for example 50k-200k tokens)
- Assert: `200`, no timeout/exceptions, deterministic result shape
- Note: keep this as a bounded integration/perf-smoke test, not a benchmark

## 7. Edge Cases and Failure Scenarios
- Missing body: `POST` with no JSON body -> `400`
- Missing `text` field: `{}` -> `400`
- `text = null`: `{"text":null}` -> `400`
- Empty/whitespace text: `""`, `"   "` -> `200` with zero metrics
- Noise-only text: `"!!! ###"` -> `200` with zero metrics
- Only stop words -> `200` with zero metrics
- `topN` negative (unit-level analyzer API) -> `ArgumentOutOfRangeException`
- Canceled request token -> operation canceled before processing

## 8. Expected Behavior Matrix

Normal valid input:
- Returns `200 OK`
- `topWords` sorted by count descending then word ascending
- `totalWordCount` is count after stop-word filtering
- `uniqueWordCount` equals number of filtered unique tokens
- `longestWord` chosen from filtered tokens; tie resolved alphabetically

Structurally invalid input:
- Returns `400 Bad Request`
- Analyzer is not invoked

Valid but analyzable-empty input:
- Returns `200 OK`
- `topWords = []`, `totalWordCount = 0`, `uniqueWordCount = 0`, `longestWord = ""`

## 9. Test Data Set (Reusable)
1. Basic frequency: `"apple apple banana"`
2. Case insensitivity: `"Apple apple APPLE banana"`
3. Stop words: `"The apple and banana the"`
4. Punctuation: `"Hello, hello... world-world!!!"`
5. Empty/noise: `""`, `"   "`, `"!!! ###"`
6. Tie ordering: `"beta alpha gamma"`
7. Longest tie: `"zebra apple"`
8. Unicode: `"Žodis žodis Ąžuolas"`
9. Alphanumeric: `"r2d2 R2D2 model3"`
10. Apostrophe/hyphen behavior lock: `"it's e-mail re-enter"`

## 10. Execution Plan
1. Extend analyzer unit tests first (fast feedback on parsing/counting rules).
2. Extend controller unit tests second (validation and call contract).
3. Extend API tests third (HTTP pipeline + configuration behavior).
4. Run `AnagramSolver.Tests`.
5. Run `AnagramSolver.WebApp.Tests`.
6. If failures occur, classify into behavior mismatch vs test expectation mismatch and update tests/spec accordingly.

## 11. Exit Criteria
- All new tests pass deterministically on repeated runs.
- Existing test suite remains green.
- Endpoint behavior for valid, invalid, and analyzable-empty input is fully asserted.
- Deterministic tie-breaking and stop-word configuration behavior are explicitly verified at unit and API levels.
