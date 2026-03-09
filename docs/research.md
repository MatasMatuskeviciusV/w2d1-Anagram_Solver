# Research: Word Frequency Analysis Endpoint

## Request Summary
Add a new Web API endpoint:
- `POST /api/analysis/frequency`

Expected behavior:
- Accept raw text input.
- Return:
	- top 10 most frequent words
	- total word count
	- unique word count
	- longest word
- Ignore stop words.
- Be case-insensitive.
- Handle empty input and special characters.

This document analyzes how the current codebase works and how to align the new feature with existing patterns.

## Current Architecture and Technology

### Platform and Framework
- ASP.NET Core Web App on `.NET 8` (`AnagramSolver.WebApp/AnagramSolver.WebApp.csproj`).
- API controllers use attribute routing and `[ApiController]`.
- Dependency Injection is configured in `AnagramSolver.WebApp/Program.cs`.
- Swagger is already enabled (`AddSwaggerGen`, `UseSwagger`, `UseSwaggerUI`).

### Existing API Style
Relevant controllers:
- `AnagramSolver.WebApp/Controllers/AnagramsController.cs`
- `AnagramSolver.WebApp/Controllers/WordsApiController.cs`
- `AnagramSolver.WebApp/Controllers/AiChatController.cs`

Observed conventions:
- API routes use either explicit route strings (`api/words`) or tokenized (`api/[controller]`).
- Input validation returns `BadRequest(...)` on invalid input.
- Simple DTO approach is used for request/response in AI chat (`ChatRequest`, `ChatResponseDto`).
- CancellationToken is passed through endpoint methods in multiple places.

### Text Processing in Business Logic
Relevant files:
- `AnagramSolver.BusinessLogic/WordNormalizer.cs`
- `AnagramSolver.BusinessLogic/UserProcessor.cs`
- `AnagramSolver.BusinessLogic/DefaultAnagramSolver.cs`
- `AnagramSolver.BusinessLogic/FileWordRepository.cs`
- `AnagramSolver.EF.CodeFirst/Repositories/EfWordRepository.cs`
- `AnagramSolver.EF.CodeFirst/Repositories/DapperWordRepository.cs`

Current behavior:
- Normalization generally means trimming + lowercasing.
- `WordNormalizer.NormalizeUserWords` splits only by spaces/tabs, lowercases, then concatenates tokens (for anagram key generation).
- `WordNormalizer.NormalizeFileWords` lowercases and deduplicates via `HashSet<string>`.
- `UserProcessor.IsValid` checks null/whitespace and minimum per-token length (split by spaces/tabs).

Important implication:
- Existing normalization is optimized for anagram solving, not general-purpose tokenization or linguistic analysis.
- There is currently no stop-word filtering utility and no punctuation/special-character tokenization pipeline dedicated to analysis.

## Test and Quality Baseline

Relevant tests:
- `AnagramSolver.Tests/WordNormalizerTests.cs`
- `AnagramSolver.Tests/UserInputProcessorTests.cs`
- `AnagramSolver.WebApp.Tests/HomeControllerMoqTests.cs`

Coverage observations:
- Word normalization tests verify lowercase conversion, whitespace trimming, and empty/null handling.
- No tests currently target API analysis endpoints, stop-word behavior, punctuation handling, or frequency ranking.

## Gap Analysis for the New Feature

### What Exists Already
- A stable API controller pattern that can host `POST /api/analysis/frequency`.
- DI and service registration points in `Program.cs`.
- Basic normalization logic and test style conventions.

### What Is Missing
- Analysis-specific request/response DTOs.
- A dedicated text-analysis service (frequency counting, tokenization, stop-word filtering).
- A stop-word source and policy (hardcoded, configuration-based, or file-based).
- Deterministic tie-breaking rules for top 10 output.

## Risks and Edge Cases

### 1) Tokenization Ambiguity
Risk:
- If splitting only on space/tab (current pattern), punctuation-heavy text (`hello, world!`, `it's`, `C#`, `e-mail`) is miscounted.

Impact:
- Wrong frequencies and longest-word results.

Mitigation:
- Define explicit tokenization rules for letters/digits/apostrophes/hyphens.
- Use one consistent approach across counting and longest-word selection.

### 2) Case and Culture Pitfalls
Risk:
- `ToLower()` without explicit culture can lead to locale-specific differences.

Mitigation:
- Use invariant case normalization for analysis (`ToLowerInvariant` or equivalent comparer policy).

### 3) Stop-Word Matching Mismatch
Risk:
- Stop words may not match tokens if normalization differs (case, punctuation).

Mitigation:
- Normalize stop-word list with the same pipeline as input tokens.
- Store stop words in a case-insensitive set.

### 4) Empty/Noise Input
Risk:
- Input with only punctuation/whitespace/stop words may produce null or confusing output.

Mitigation:
- Define clear response contract for "no valid tokens".
- Ensure totals are explicit and non-error (unless request body itself is invalid).

### 5) Deterministic Top 10 Ordering
Risk:
- Equal-frequency words can appear in unstable order.

Mitigation:
- Apply deterministic secondary sort (for example alphabetical ascending after frequency descending).

### 6) Performance for Large Payloads
Risk:
- Large text bodies can allocate many transient strings.

Mitigation:
- Prefer streaming/token iteration where practical.
- Keep complexity linear in token count.

## Best Practices for This Codebase

### Placement and Separation of Concerns
- Keep controller thin (`AnagramSolver.WebApp/Controllers`).
- Place frequency-analysis logic in BusinessLogic as a dedicated service/class.
- Register service in DI via `Program.cs`.

This matches current layering where controllers orchestrate and business logic performs computation.

### API Contract Recommendation
Use explicit DTOs (similar to chat models):
- Request DTO with text payload (and optional future options like `topN`).
- Response DTO containing:
	- `topWords` (word + count)
	- `totalWordCount`
	- `uniqueWordCount`
	- `longestWord`

### Validation Behavior
- Return `400 Bad Request` for structurally invalid payloads (missing body, required field absent).
- Return `200 OK` with zeroed metrics for valid but analyzable-empty text (only symbols/stop words).

### Configuration Strategy for Stop Words
Preferred options (in order):
1. `appsettings.json` section (easy deployment and environment overrides).
2. Dedicated text/json file loaded at startup (better for larger lists).

For current project size, configuration-based start is simplest.

### Testing Strategy
Add deterministic unit tests for the analysis service covering:
- Case-insensitive counting.
- Stop-word exclusion.
- Special-character tokenization.
- Empty/null/whitespace input.
- Input with only stop words.
- Tie-breaking order for top 10.
- Longest-word extraction when punctuation and casing vary.

Add Web API tests for:
- Route and status codes.
- DTO validation failures.
- Contract shape and key fields.

## Integration Notes with Existing Patterns
- Existing `WordNormalizer` is anagram-focused and should not be repurposed directly for frequency analysis; create analysis-specific normalization/tokenization.
- Current repositories are not needed for this feature if analysis is request-text only.
- Swagger will automatically expose the new endpoint once controller and DTOs are added.

## Suggested Implementation Direction (Research Outcome)
1. Add `AnalysisController` under `AnagramSolver.WebApp/Controllers` with route `api/analysis` and action `POST frequency`.
2. Add request/response DTOs under `AnagramSolver.WebApp/Models` (or a dedicated `Dtos` folder if preferred by team).
3. Add a dedicated business service (for example, `WordFrequencyAnalyzer`) in `AnagramSolver.BusinessLogic`.
4. Register the analyzer service in DI in `AnagramSolver.WebApp/Program.cs`.
5. Add unit tests in `AnagramSolver.Tests` and endpoint tests in `AnagramSolver.WebApp.Tests`.

This approach is low-risk, consistent with current architecture, and leaves anagram behavior untouched.
