# Technical Plan: Group Chat Workflow for AnagramMsAgentFramework.Console

## 1. Objective
Implement a new switchable Group Chat workflow in `AnagramMsAgentFramework.Console` with 4 agents:
1. Orchestrator: chooses which agent speaks next.
2. First player: selects a single dictionary word and sends a one-word anagram.
3. Second player: guesses the original word.
4. Reviewer: checks whether the guess is correct.

The workflow must live under `AnagramMsAgentFramework.Console/Workflows` and be selectable via the same `Workflows:ActiveWorkflow` mechanism used by existing workflows.

## 2. Scope and Non-Scope
In scope:
1. New `Workflows/GroupChat` workflow, models, options, and streaming writer.
2. DI and host routing updates in `Program.cs` and `appsettings.json`.
3. Deterministic host-side validation for anagram correctness and reviewer verdict safety.
4. Tests for routing, state progression, safety guards, and timeout/cancellation behavior.

Out of scope:
1. Refactoring existing Sequential or Handoff workflows beyond shared host wiring points.
2. New external APIs/endpoints.
3. Persistent storage of chat/game state outside process memory.

## 3. File Structure Plan

### 3.1 Files to Create
1. `AnagramMsAgentFramework.Console/Workflows/GroupChat/IGroupChatWorkflow.cs`
2. `AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatWorkflow.cs`
3. `AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatWorkflowOptions.cs`
4. `AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatWorkflowAgentFactory.cs`
5. `AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatAgentRole.cs`
6. `AnagramMsAgentFramework.Console/Workflows/GroupChat/Models/GroupChatConversationState.cs`
7. `AnagramMsAgentFramework.Console/Workflows/GroupChat/Models/OrchestratorDecision.cs`
8. `AnagramMsAgentFramework.Console/Workflows/GroupChat/Models/FirstPlayerTurnResult.cs`
9. `AnagramMsAgentFramework.Console/Workflows/GroupChat/Models/SecondPlayerTurnResult.cs`
10. `AnagramMsAgentFramework.Console/Workflows/GroupChat/Models/ReviewerTurnResult.cs`
11. `AnagramMsAgentFramework.Console/Workflows/GroupChat/Models/GroupChatTurnResult.cs`
12. `AnagramMsAgentFramework.Console/Workflows/GroupChat/Streaming/IGroupChatStreamWriter.cs`
13. `AnagramMsAgentFramework.Console/Workflows/GroupChat/Streaming/ConsoleGroupChatStreamWriter.cs`
14. `AnagramMsAgentFramework.Console/Workflows/GroupChat/Streaming/GroupChatStreamEvent.cs`
15. `AnagramSolver.Tests/GroupChatWorkflowTests.cs`
16. `AnagramSolver.Tests/GroupChatWorkflowRoutingTests.cs`
17. `AnagramSolver.Tests/GroupChatWorkflowStreamingTests.cs`
18. `AnagramSolver.Tests/GroupChatWorkflowContractIntegrationTests.cs`

### 3.2 Files to Modify
1. `AnagramMsAgentFramework.Console/Program.cs`
2. `AnagramMsAgentFramework.Console/appsettings.json`
3. `AnagramMsAgentFramework.Console/AnagramTools.cs` (only if helper method is needed for deterministic random/selectable dictionary candidate acquisition)

## 4. Planned Contracts and Signatures

### 4.1 Workflow Entry Interface
File: `AnagramMsAgentFramework.Console/Workflows/GroupChat/IGroupChatWorkflow.cs`

```csharp
public interface IGroupChatWorkflow
{
    Task<GroupChatTurnResult> ExecuteAsync(string userInput, CancellationToken ct = default);
    Task ResetAsync(CancellationToken ct = default);
}
```

### 4.2 Workflow Options
File: `AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatWorkflowOptions.cs`

```csharp
public sealed class GroupChatWorkflowOptions
{
    public const string SectionName = "Workflows:GroupChat";

    public bool StreamingEnabled { get; set; } = true;
    public int StreamingStageTimeoutSeconds { get; set; } = 30;
    public int MaxRoleHopsPerTurn { get; set; } = 4;
    public int MaxRoundsPerGame { get; set; } = 1;
    public int MinWordLength { get; set; } = 4;
    public string? OrchestratorModel { get; set; }
    public string? FirstPlayerModel { get; set; }
    public string? SecondPlayerModel { get; set; }
    public string? ReviewerModel { get; set; }
}
```

### 4.3 Agent Factory
File: `AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatWorkflowAgentFactory.cs`

```csharp
public sealed class GroupChatWorkflowAgentFactory
{
    public GroupChatWorkflowAgentFactory(IChatClient chatClient, AnagramTools tools);

    public AIAgent CreateOrchestratorAgent(string? model = null);
    public AIAgent CreateFirstPlayerAgent(string? model = null);
    public AIAgent CreateSecondPlayerAgent(string? model = null);
    public AIAgent CreateReviewerAgent(string? model = null);
}
```

Notes:
1. Follow existing pattern with `ConfigureOptionsChatClient` for per-agent model overrides.
2. Keep tools minimal per role to prevent secret leakage or role bypass.

### 4.4 Core Models
File: `AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatAgentRole.cs`

```csharp
public enum GroupChatAgentRole
{
    Orchestrator,
    FirstPlayer,
    SecondPlayer,
    Reviewer,
    Completed
}
```

File: `AnagramMsAgentFramework.Console/Workflows/GroupChat/Models/GroupChatConversationState.cs`

```csharp
public sealed record GroupChatConversationState
{
    public int TurnNumber { get; init; }
    public int CurrentRound { get; init; }
    public GroupChatAgentRole ActiveRole { get; init; }
    public string? SecretWord { get; init; }
    public string? ProducedAnagram { get; init; }
    public string? LatestGuess { get; init; }
    public bool? IsGuessCorrect { get; init; }
}
```

File: `AnagramMsAgentFramework.Console/Workflows/GroupChat/Models/OrchestratorDecision.cs`

```csharp
public sealed record OrchestratorDecision
{
    public GroupChatAgentRole NextRole { get; init; }
    public string Reason { get; init; } = string.Empty;
    public double Confidence { get; init; }
}
```

File: `AnagramMsAgentFramework.Console/Workflows/GroupChat/Models/GroupChatTurnResult.cs`

```csharp
public sealed record GroupChatTurnResult(
    string FinalMessage,
    GroupChatAgentRole RoutedRole,
    GroupChatConversationState State,
    bool UsedFallback);
```

### 4.5 Streaming Contracts
File: `AnagramMsAgentFramework.Console/Workflows/GroupChat/Streaming/GroupChatStreamEvent.cs`

```csharp
public enum GroupChatStreamEvent
{
    Orchestrator,
    FirstPlayer,
    SecondPlayer,
    Reviewer
}
```

File: `AnagramMsAgentFramework.Console/Workflows/GroupChat/Streaming/IGroupChatStreamWriter.cs`

```csharp
public interface IGroupChatStreamWriter
{
    Task WriteUpdateAsync(GroupChatStreamEvent streamEvent, string text, CancellationToken ct = default);
    Task WriteCompletedAsync(GroupChatStreamEvent streamEvent, CancellationToken ct = default);
}
```

## 5. Host Integration Plan

### 5.1 Workflow Mode Expansion
Modify `AnagramMsAgentFramework.Console/Program.cs`:
1. Add `WorkflowMode.GroupChat`.
2. Extend `ParseWorkflowMode(...)` to map `"groupchat"` and `"group-chat"`.
3. Register Group Chat services in DI:
   - `GroupChatWorkflowAgentFactory`
   - `IGroupChatStreamWriter`
   - `GroupChatWorkflowOptions` binder and value clamps
   - `IGroupChatWorkflow`
4. Resolve `IGroupChatWorkflow` from service provider.
5. Extend runtime switch expression to dispatch to Group Chat execution.
6. Add `reset` handling for Group Chat state (same style as Handoff).

### 5.2 App Settings
Modify `AnagramMsAgentFramework.Console/appsettings.json`:
1. Add `Workflows.GroupChat` section.
2. Add per-role model settings and guard settings.
3. Keep `Workflows.ActiveWorkflow` compatible with all 3 modes.

## 6. Orchestration and Safety Design

### 6.1 Per-Turn State Machine
`GroupChatWorkflow.ExecuteAsync(...)` will:
1. Validate input and cancellation.
2. Snapshot state.
3. Run orchestrator to determine first role for this turn.
4. Execute role-specific turn handler.
5. Enforce `MaxRoleHopsPerTurn` to avoid loops.
6. Persist updated state and return `GroupChatTurnResult`.

### 6.2 Deterministic Integrity Rules
Host-side checks (must not rely only on LLM output):
1. First player output must be one token and a true anagram of the secret word.
2. First player output must not equal the secret word.
3. Reviewer verdict must be cross-checked by deterministic comparison.
4. Any malformed output triggers safe fallback message and state-safe completion.

### 6.3 Secret Isolation Rules
1. `SecretWord` remains host-owned state.
2. Second player receives only the anagram token and game context, never the secret word.
3. Reviewer receives secret word and guess via structured payload, not free-form transcript parsing.

### 6.4 Cancellation and Timeout Rules
1. Mirror existing timeout guard behavior per role stage.
2. Do not swallow cancellation:
   - explicitly rethrow `OperationCanceledException` when caller token is canceled.
3. Use deterministic fallback only for non-cancellation failures.

## 7. Numbered Implementation Tasks

### 1. Foundation
1.1 Create `Workflows/GroupChat` folders (`Models`, `Streaming`).
1.2 Add `GroupChatAgentRole` enum.
1.3 Add `IGroupChatWorkflow` interface and `GroupChatTurnResult` model.

### 2. Configuration and DI
2.1 Create `GroupChatWorkflowOptions` with defaults and section constant.
2.2 Add `Workflows.GroupChat` section in `appsettings.json`.
2.3 Register Group Chat services in `Program.cs` DI.

### 3. Agent Factory
3.1 Create `GroupChatWorkflowAgentFactory`.
3.2 Implement role-specific agent creation methods.
3.3 Implement per-role model override capture for tests.

### 4. State and Contracts
4.1 Create `GroupChatConversationState`.
4.2 Create `OrchestratorDecision`, `FirstPlayerTurnResult`, `SecondPlayerTurnResult`, `ReviewerTurnResult`.
4.3 Add JSON parse/validation helpers for structured model outputs.

### 5. Streaming Layer
5.1 Create `GroupChatStreamEvent` enum.
5.2 Create `IGroupChatStreamWriter`.
5.3 Implement `ConsoleGroupChatStreamWriter` with labeled updates and completion markers.

### 6. Workflow Core
6.1 Implement `GroupChatWorkflow.ExecuteAsync(...)` shell and input validation.
6.2 Implement orchestrator stage and fallback route logic.
6.3 Implement first-player stage with deterministic anagram validation.
6.4 Implement second-player stage with strict payload parsing.
6.5 Implement reviewer stage with host cross-check and final verdict.
6.6 Implement role-hop guard and deterministic fallback responses.
6.7 Implement `ResetAsync(...)` for in-memory game state.

### 7. Host Wiring
7.1 Extend `WorkflowMode` and `ParseWorkflowMode(...)` for Group Chat.
7.2 Add Group Chat dispatch branch in main loop.
7.3 Add `reset` handling for Group Chat.

### 8. Test Implementation
8.1 Add unit tests for orchestrator route validity and loop guard.
8.2 Add unit tests for first-player one-word and anagram correctness constraints.
8.3 Add unit tests for reviewer deterministic correctness checks.
8.4 Add streaming tests for labels/completion counts/timeouts.
8.5 Add integration tests for happy path and incorrect guess path.
8.6 Add cancellation tests to verify propagation and no state corruption.

### 9. Verification and Hardening
9.1 Run Group Chat-focused tests.
9.2 Run existing Handoff and Sequential suites to ensure no regression.
9.3 Validate all fallback messages remain user-safe and deterministic.

## 8. Endpoints and Request/Response Models
No HTTP endpoints are applicable for this task.

Runtime surface:
1. Input: `string userInput` to `IGroupChatWorkflow.ExecuteAsync(...)`.
2. Output: `GroupChatTurnResult` with message, routed role, updated state, and fallback flag.

## 9. Test Plan Matrix

### 9.1 Unit Tests
1. `ExecuteAsync_WhenInputIsEmpty_ShouldReturnValidationMessage`.
2. `ExecuteAsync_WhenOrchestratorReturnsInvalidRole_ShouldUseSafeFallback`.
3. `FirstPlayer_WhenOutputIsNotSingleWord_ShouldFailSafe`.
4. `FirstPlayer_WhenOutputIsNotAnagram_ShouldFailSafe`.
5. `Reviewer_WhenGuessMatchesSecret_ShouldReturnCorrect`.
6. `Reviewer_WhenGuessDoesNotMatchSecret_ShouldReturnIncorrect`.
7. `ExecuteAsync_WhenMaxRoleHopsExceeded_ShouldStopWithFallback`.
8. `ResetAsync_ShouldClearGameState`.

### 9.2 Streaming Tests
1. Labeled updates emitted for each executed role.
2. Exactly one completion event per executed role.
3. Timeout emits warning and still returns deterministic final result.

### 9.3 Contract/Parsing Tests
1. Orchestrator JSON parse accepts valid payload.
2. Orchestrator JSON parse rejects malformed payload.
3. Reviewer payload parse accepts object/array payload variants where expected.
4. Malformed structured payload triggers fallback, not crash.

### 9.4 Integration Tests
1. Happy path game flow:
   - Orchestrator -> FirstPlayer -> SecondPlayer -> Reviewer -> Completed.
2. Incorrect guess flow with explicit reviewer rejection.
3. Cancellation during role execution propagates cancellation and preserves state integrity.
4. Host mode switch selects Group Chat when `Workflows:ActiveWorkflow=GroupChat`.

## 10. Acceptance Criteria
1. Group Chat workflow exists under `AnagramMsAgentFramework.Console/Workflows/GroupChat`.
2. Host can switch between `Sequential`, `Handoff`, and `GroupChat` using configuration.
3. Four-agent flow executes with orchestrator-driven turn selection.
4. Secret word is never exposed to second player through workflow payload.
5. Reviewer verdict is deterministic and safe against malformed model output.
6. Timeout and cancellation handling match existing reliability expectations.
7. Group Chat tests pass and existing workflow tests remain green.