using AnagramMsAgentFramework.Console;
using FluentAssertions;

namespace AnagramSolver.Tests;

public class ProgramWorkflowModeTests
{
	[Theory]
	[InlineData("GroupChat")]
	[InlineData("groupchat")]
	[InlineData("group-chat")]
	public void ParseWorkflowMode_WhenConfiguredForGroupChat_ShouldReturnGroupChat(string configuredMode)
	{
		var mode = Program.ParseWorkflowMode(configuredMode);

		mode.Should().Be(Program.WorkflowMode.GroupChat);
	}
}
