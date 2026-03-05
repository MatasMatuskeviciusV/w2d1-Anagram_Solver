# Copilot workspace instructions

## General Guidelines
- Use clear, intention-revealing names (e.g., `NormalizeInput`, `ComputeSignature`, etc.).
- Prefer small, single-purpose functions; avoid long methods unless justified.
- No magic strings/numbers. Extract constants and configuration.
- Follow SOLID principles.
- Do not introduce new architectural patterns without explicit instruction/confirmation.
- Do not refactor unrelated code.

## Testing Rules
- Use AAA (Arrange-Act-Assert) and deterministic tests.
- Tests must be deterministic.
- Avoid real network, filesystem, or database calls in unit tests.
- Every new public method must have at least one test.

## Data and Security
- Use parameterized queries / ORM safe patterns only.
- Never build SQL strings from user input.
- Validate and normalize user input consistently before anagram processing.
- Fail fast on invalid input.

## Project-Specific Rules
- Implement auto function calling for AI plugins using Semantic Kernel.
- Register plugins (e.g., AnagramPlugin for finding anagrams, TimePlugin for current time) in Dependency Injection (DI).
- Enable auto function calling via `OpenAIPromptExecutionSettings` with `FunctionChoiceBehavior.Auto()`.
- Inject both plugins into `AiChatService` via constructor and import them into the kernel.
- Register `IChatHistoryRepository` and `InMemoryChatHistoryRepository` in DI after the TimePlugin registration in `Program.cs`: `builder.Services.AddSingleton<IChatHistoryRepository, InMemoryChatHistoryRepository>();` This creates a singleton instance that persists across all requests with thread-safe `ConcurrentDictionary` storage.