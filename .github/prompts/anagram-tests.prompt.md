---
name: Generate business logic unit tests (AnagramSolver with mocks)
description: Creates focused, deterministic unit tests for AnagramSolver business logic using mocks/fakes where dependencies exist.
---

You are working in the AnagramSolver repository. Generate unit tests for the business logic layer only.

Scope:
- Tests MUST target pure business logic types/services.
- Do NOT reference HTTP, controllers, webapp layer, real DB connections or real repositories.
- If business logic depends on abstractions (e.g., IWordRepository, IDictionaryProvider, INormalizer, ILogger), use mocks or fakes.
- NEVER use real infrastructure implementations.
- Do not modify production code unless tests are impossible without it.

Mocking and Test Data Rules:
- Use mock frameworks available in the project (e.g., Moq for C#).
- If no mocking framework is detected, create lightweight in-memory fake implementations.
- Use deterministic mock data.
- Do not use random data.
- Avoid time-dependent logic unless explicitly required.

Testing Structure:
- Use AAA (Arrange-Act-Assert).
- Name tests descriptively (e.g., `FindAnagrams_IgnoresCases_ReturnsMatches`).
- Keep each test focused on one behavior.
- One assertion focus per test; prefer multiple tests over one big test.

Minimum required coverage:

1) Case-insensitive matching
    Example: "Listen" vs "Silent"
2) Whitespace and punctuation normalization
    Example: "New York Times" vs "Monkeys write"
3) Diacritics / locale behavior
    - Explicitly define rule:
        - Diacritics are treated as distinct characters. If current implementation differs, propose minimal refactor to align behavior.
    - Tests must reflect the current implementation rule.
4) Duplicate handling
    - Detect current behavior for duplicates and lock it with tests (include a comment explaining it).
5) Edge cases
    - Empty input
    - Null input
    - Very long input string: sanity check for correctness and non-pathological behavior (no exceptions, no extreme runtime). Avoid strict time limits unless the repo already uses them.

Dependency Rules:
- If normalization or dictionary access is internal/private and blocks testing:
    - Suggest the smallest possible refactor (e.g., extract INormalizer or inject IWordSource).
    - Provide minimal diff patch.
    - Do NOT redesign architecture.

Framework Selection:
- If a test framework exists in the repo, use it.
- Otherwise auto-select based on language:
    - C# -> xUnit + Moq
    - Python -> pytest + unittest.mock
    - etc.

Output format:
1) Full test file(s) with complete code.
2) Mock/fake implementations (if needed).
3) Minimal refactor patch (if required).
4) One command to run the tests.