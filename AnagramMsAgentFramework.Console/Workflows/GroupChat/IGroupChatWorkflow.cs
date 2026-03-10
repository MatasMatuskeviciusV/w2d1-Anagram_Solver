using AnagramMsAgentFramework.Console.Workflows.GroupChat.Models;

namespace AnagramMsAgentFramework.Console.Workflows.GroupChat;

public interface IGroupChatWorkflow
{
	Task<GroupChatTurnResult> ExecuteAsync(string userInput, CancellationToken ct = default);
	Task ResetAsync(CancellationToken ct = default);
}
