# Code Review: Group Chat Workflow

Reviewed against: `docs/plan.md`

## Findings (ordered by severity)

### 1. Medium: Orchestrator decisions are validated syntactically, not against current state prerequisites
- Evidence: `AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatWorkflow.cs:215`
- Evidence: `AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatWorkflow.cs:668`
- Evidence: `AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatWorkflow.cs:172`
- Evidence: `AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatWorkflow.cs:176`
- Plan reference: `docs/plan.md` sections 6.1 and 6.2
- Issue: `SelectNextRoleAsync` only rejects malformed role payloads (reason/confidence/enum), but it does not reject context-invalid choices (for example `Reviewer` before `SecretWord` and `LatestGuess` exist). The workflow then fails later with a deterministic error message instead of rerouting safely.
- Risk: valid-but-context-wrong orchestrator output can prematurely terminate a turn even though deterministic routing data is available.
- Suggested fix:
1. Add state-aware role validation in `SelectNextRoleAsync` (or a dedicated validator) so a role is considered invalid when required prerequisites are missing.
2. When context validation fails, fall back to `DetermineOrchestratorDecision(state)` directly instead of entering the stage and failing.
3. Add a test where orchestrator returns `Reviewer` on an empty turn state and assert the workflow reroutes to `FirstPlayer` (or deterministic fallback path) rather than ending with "Reviewer stage requires both...".

### 2. Medium: First-player output validation does not enforce dictionary-word constraint for produced anagram
- Evidence: `AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatWorkflow.cs:688`
- Evidence: `AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatWorkflow.cs:711`
- Evidence: `AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatWorkflow.cs:403`
- Plan reference: `docs/plan.md` objective section 1 (dictionary-based game flow)
- Issue: `ValidateFirstPlayerOutput` checks one-token shape and character signature, but does not verify the produced anagram is a dictionary word.
- Risk: model output like a non-word permutation can pass validation and degrade game integrity.
- Suggested fix:
1. Add dictionary membership validation for `ProducedAnagram` (for example through `AnagramTools`/repository lookup).
2. Keep the existing signature validation as a secondary guard.
3. Add a test where first-player returns a non-dictionary permutation with matching signature and assert fail-safe fallback.

## Test Coverage Gaps

### 1. Missing regression test for context-invalid orchestrator role with deterministic reroute
- Current tests cover malformed/invalid role values and rerouting behavior, but not the context-invalid case where role enum is valid yet impossible for current state.
- Suggested test name: `ExecuteAsync_WhenOrchestratorChoosesReviewerBeforeGuess_ShouldRerouteDeterministically`.

### 2. Missing regression test for dictionary constraint on first-player produced anagram
- Existing tests cover "not single word" and "not anagram" cases, but not "anagram signature valid but not a dictionary word".
- Suggested test name: `FirstPlayer_WhenProducedAnagramIsNotDictionaryWord_ShouldFailSafe`.

## Security and Validation Notes
- Secret isolation is implemented: second-player prompt includes only the anagram token and explicit instruction not to output unknown secret (`AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatWorkflow.cs:785`).
- Reviewer verdict is cross-checked deterministically and conflicting/malformed model output falls back safely (`AnagramMsAgentFramework.Console/Workflows/GroupChat/GroupChatWorkflow.cs:465`).

## Test Run Status
- Executed: `AnagramSolver.Tests/GroupChatWorkflowTests.cs`
- Executed: `AnagramSolver.Tests/GroupChatWorkflowRoutingTests.cs`
- Executed: `AnagramSolver.Tests/GroupChatWorkflowStreamingTests.cs`
- Executed: `AnagramSolver.Tests/GroupChatWorkflowContractIntegrationTests.cs`
- Executed: `AnagramSolver.Tests/ProgramWorkflowModeTests.cs`
- Result: 28 passed, 0 failed.

## Overall Assessment
- Implementation is largely aligned with the plan and includes strong deterministic fallback and cancellation behavior.
- Main remaining risks are state-aware routing validation and dictionary-level validation of first-player output.
