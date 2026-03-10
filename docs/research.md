# Research: Group Chat Workflow for AnagramMsAgentFramework.Console

## Scope
Research target: `AnagramMsAgentFramework.Console` and related tests.

Requested future change:
1. Add a new workflow under `AnagramMsAgentFramework.Console/Workflows`.
2. New workflow type is Group Chat with 4 agents:
   - `Orchestrator`: decides which agent speaks next.
   - `First player`: picks one dictionary word and sends one-word anagram.
   - `Second player`: guesses original word.
   - `Reviewer`: verifies whether the guess is correct.
3. Workflow must be switchable like existing workflows.

This document is research and analysis only, no implementation code.

## Current Architecture

### Workflow switching in host
`AnagramMsAgentFramework.Console/Program.cs:20` reads `Workflows:ActiveWorkflow` and maps it through `ParseWorkflowMode`.

`AnagramMsAgentFramework.Console/Program.cs:182` currently supports only:
- `Handoff`
- default `Sequential`

`AnagramMsAgentFramework.Console/Program.cs:87` dispatches the user turn through a `switch` expression to either:
- `ISequentialAnagramWorkflow.ExecuteAsync(...)`
- `IHandoffWorkflow.ExecuteAsync(...)`

`AnagramMsAgentFramework.Console/appsettings.json:17` confirms this pattern via:
- `Workflows.ActiveWorkflow`
- `Workflows.SequentialAnagram` section
- `Workflows.Handoff` section

Conclusion: workflow switch infrastructure already exists and can be extended to a third mode.

### Existing workflow patterns
Two patterns are already in place:

1. Sequential stage workflow
- Files: `AnagramMsAgentFramework.Console/Workflows/SequentialAnagram/*`
- Factory creates role-specific agents (`Finder`, `Analyzer`, `Presenter`) with `AsAIAgent(...)` and constrained tools.
- Workflow keeps deterministic host logic for final shaping and fallback (`BuildFallbackPresentation`).

2. Handoff workflow
- Files: `AnagramMsAgentFramework.Console/Workflows/Handoff/*`
- Uses explicit routing state and role handoff guards.
- Includes confidence threshold and max depth safeguards (`RouteConfidenceThreshold`, `MaxHandoffDepthPerTurn`) in `AnagramMsAgentFramework.Console/Workflows/Handoff/HandoffWorkflow.cs:91` and `AnagramMsAgentFramework.Console/Workflows/Handoff/HandoffWorkflow.cs:106`.

Conclusion: Handoff workflow is the closest architectural baseline for Group Chat because it already models multi-role turn control and host-enforced safety checks.

## Technology and Library Findings

### Runtime and dependencies
From `AnagramMsAgentFramework.Console/AnagramMsAgentFramework.Console.csproj`:
- `net10.0`
- `Microsoft.Agents.AI` `1.0.0-rc3`
- `Microsoft.Extensions.AI` `10.3.0`
- `Microsoft.Extensions.AI.OpenAI` `10.3.0`
- `OpenAI` `2.9.1`

### Agent construction conventions
- Chat client is created via `OpenAIClient(...).GetChatClient(model).AsIChatClient()` in `AnagramMsAgentFramework.Console/Program.cs:27`.
- Agents are created with `IChatClient.AsAIAgent(...)` in both factories.
- Tool exposure is explicit via `AIFunctionFactory.Create(...)`.

Repository memory confirms important constraints:
- Must use `.AsIChatClient()` adapter before `.AsAIAgent(...)`.
- Per-agent model override should use `ConfigureOptionsChatClient` and `ChatOptions.ModelId` (already used in current factories).
- Base `AIAgent.RunStreamingAsync` expects chat-message input in this package family.

## Domain Capabilities Already Available

From `AnagramMsAgentFramework.Console/AnagramTools.cs`:
- `FindAnagramsStructuredAsync(...)`
- `CountAnagramsAsync(...)`
- `AnalyzeWordFrequencyStructured(...)`
- `GetCurrentTime()`

For requested Group Chat game:
- First player needs deterministic dictionary word selection and anagram generation. Current tools can find anagrams for a given word but do not expose a direct dictionary word picker tool.
- Reviewer needs deterministic correctness checking. Current workflow patterns favor host validation over trusting free-form model statements.

## Test Baseline and Quality Signals

The codebase already has strong workflow tests for routing, state, streaming, and fallback behavior:
- `AnagramSolver.Tests/HandoffWorkflowRoutingTests.cs:39` (clarification on low confidence)
- `AnagramSolver.Tests/HandoffWorkflowRoutingTests.cs:55` (prevent invalid role transitions)
- `AnagramSolver.Tests/HandoffWorkflowStreamingTests.cs:16` (labeled stream events)
- `AnagramSolver.Tests/HandoffWorkflowStreamingTests.cs:40` (timeout fallback)
- `AnagramSolver.Tests/HandoffWorkflowContractIntegrationTests.cs:17` (per-role model overrides)
- `AnagramSolver.Tests/HandoffWorkflowContractIntegrationTests.cs:67` (multi-turn scenario)

Conclusion: future Group Chat should follow this testing style: deterministic assertions, role-transition safety, and timeout/cancellation coverage.

## Gap Analysis for Requested Group Chat

### What exists already
1. Switchable workflow host loop.
2. DI + options binding per workflow.
3. Agent factory pattern with role-isolated tools.
4. Streaming writer abstractions for role-labeled progress.
5. Deterministic fallback policy in host.

### What does not exist yet
1. No Group Chat workflow in `AnagramMsAgentFramework.Console/Workflows`.
2. No Group Chat-specific role contracts (`Orchestrator`, `FirstPlayer`, `SecondPlayer`, `Reviewer`).
3. No game-state model for secret word, generated anagram, guess, verdict.
4. No host-level deterministic reviewer logic for strict correctness checks.
5. No third workflow value in `WorkflowMode` or `ParseWorkflowMode`.

## Risks and Edge Cases

### 1. Secret leakage to guessing agent
If conversation payload is shared naively, second player may see secret/original word.

Mitigation:
- Use strict per-role prompts and role-specific payload contracts.
- Pass only anagram token to second player.
- Keep secret word in host-owned state, not in shared text context.

### 2. Invalid anagram generation
First player could output non-anagram or same word.

Mitigation:
- Host verifies generated anagram deterministically (sorted letters check, inequality check).
- Retry or fallback when generated value is invalid.

### 3. Non-deterministic reviewer decisions
LLM reviewer may mark wrong guess as correct or vice versa.

Mitigation:
- Reviewer should be host-validated or host-authoritative.
- If using model output, parse into strict contract and cross-check with deterministic comparison.

### 4. Orchestrator loops / dead turns
Orchestrator might repeatedly select the same role or never terminate.

Mitigation:
- Enforce max per-turn role hops similar to `MaxHandoffDepthPerTurn`.
- Require terminal state after reviewer verdict.

### 5. Empty or low-quality dictionary selection
Selected word may have no meaningful anagrams, too short, or unsupported by current filters.

Mitigation:
- Selection criteria should enforce minimum length and at least one alternate anagram candidate.
- Reuse existing normalization and validation constraints from `AnagramTools` and business logic.

### 6. Switching ergonomics and reset semantics
Current reset command is only for Handoff (`Program.cs:78`). Group Chat may need its own state reset behavior.

Mitigation:
- Define reset behavior consistently for stateful workflows.
- Keep command handling explicit per workflow mode.

## Best Practices for Future Implementation

1. Mirror existing folder conventions:
- `AnagramMsAgentFramework.Console/Workflows/GroupChat/`
- `.../Models/`
- `.../Streaming/`

2. Follow existing abstraction style:
- `IGroupChatWorkflow` interface.
- `GroupChatWorkflowOptions` with section name under `Workflows`.
- `GroupChatWorkflowAgentFactory` for role agent creation.

3. Keep deterministic authority in host:
- Use agents for language and orchestration hints.
- Use host code for correctness checks (anagram validity and reviewer verdict guardrails).

4. Keep role tooling minimal:
- First player gets only required anagram-related tools.
- Second player should generally not get dictionary tools that could bypass game intent.
- Reviewer should rely on structured inputs and deterministic comparison.

5. Preserve existing resilience model:
- Timeout guards per stage/role.
- Cancellation propagation.
- Safe fallback final messages.

6. Extend workflow switching in one place:
- `WorkflowMode` enum in `Program.cs`.
- `ParseWorkflowMode` mapping.
- Main dispatch switch expression.
- appsettings with `Workflows.GroupChat` settings.

## Suggested Test Plan for Group Chat Workflow

### Unit tests
1. Orchestrator selects valid next role and terminates at reviewer.
2. First player produces one-word anagram; host rejects invalid output.
3. Second player guess contract parsing and fallback behavior.
4. Reviewer correctness for both correct and incorrect guesses.
5. Max-hop safety guard prevents infinite loop.
6. Reset clears game state.

### Integration tests
1. Happy path: first player -> second player -> reviewer (correct guess).
2. Incorrect guess path with explicit reviewer rejection.
3. Timeout in one role still returns deterministic safe result.
4. Workflow mode switch in host routes to Group Chat workflow.

## Implementation Readiness Summary
The project is well-prepared for adding a third switchable workflow. The current Handoff architecture provides the strongest template for multi-agent role control, state, and safeguards. The main engineering challenge for Group Chat is not basic wiring; it is enforcing deterministic game integrity (secret isolation, anagram validity, and reviewer correctness) while keeping orchestration flexible.