# Code Review: Word Frequency Analysis Tests

Reviewed against: `docs/test-plan.md`

## Findings (ordered by severity)

### 1. Medium: Wrong-content-type API test is still intentionally ambiguous instead of locking observed behavior
- Evidence: `AnagramSolver.WebApp.Tests/AnalysisControllerApiTests.cs:167`
- Plan expectation: `docs/test-plan.md` section 6.5 allows `400` or `415` initially, but explicitly says to record actual expected status once confirmed by execution.
- Current issue: the assertion remains `BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnsupportedMediaType)`, which does not lock current runtime behavior.
- Risk: regressions between `400` and `415` will not be detected, even after behavior has already been observed in CI/local execution.
- Suggested fix:
  - Capture the actual status code currently returned by the app.
  - Replace `BeOneOf(...)` with a single expected status assertion.
  - If behavior legitimately differs by hosting profile, split into explicit environment-specific tests with clear naming.

### 2. Low: Overridden-stopwords integration test leaks disposable test infrastructure
- Evidence: `AnagramSolver.WebApp.Tests/AnalysisControllerApiTests.cs:174`
- Current issue: `new WebApplicationFactory<Program>()...CreateClient()` is created without disposing the factory/client.
- Risk: resource leakage and intermittent instability in larger test runs.
- Suggested fix:
  - Wrap both factory and client in `using` statements.
  - Example pattern:
    - `using var factory = new WebApplicationFactory<Program>()...;`
    - `using var client = factory.CreateClient();`

### 3. Low: Large-body perf-smoke test name implies timing guarantee but does not measure or bound runtime
- Evidence: `AnagramSolver.WebApp.Tests/AnalysisControllerApiTests.cs:206`
- Plan expectation: section 6.7 calls for a bounded integration/perf-smoke check and "within reasonable time".
- Current issue: test asserts correctness and successful completion but has no explicit time bound.
- Risk: significant performance regressions can slip through while the test still passes.
- Suggested fix:
  - Add a lightweight upper bound using `Stopwatch` and an intentionally relaxed threshold suitable for CI.
  - Keep the test non-benchmark (single coarse threshold only).

## Security Review
- No security concerns identified in reviewed scope.
- Input handling, cancellation behavior, and null/invalid payload handling remain aligned with expected API behavior.

## Coverage and Plan Alignment
- Analyzer unit tests required by `docs/test-plan.md` section 5.1 are present.
- Controller unit tests required by `docs/test-plan.md` section 5.2 are present.
- Integration scenarios in section 6 are implemented, with the quality gaps above.

## Overall Assessment
- No critical bugs found.
- Main remaining work is tightening test precision and reliability so behavior regressions are caught deterministically.
