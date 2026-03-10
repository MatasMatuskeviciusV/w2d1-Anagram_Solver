using AnagramSolver.BusinessLogic;
using AnagramSolver.Contracts;
using AnagramMsAgentFramework.Console.Workflows.GroupChat;
using AnagramMsAgentFramework.Console.Workflows.GroupChat.Streaming;
using AnagramMsAgentFramework.Console.Workflows.Handoff;
using AnagramMsAgentFramework.Console.Workflows.Handoff.Streaming;
using AnagramMsAgentFramework.Console.Workflows.SequentialAnagram;
using AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Streaming;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace AnagramMsAgentFramework.Console;

internal static class Program
{
	private static async Task Main()
	{
		var configuration = BuildConfiguration();
		var workflowMode = ParseWorkflowMode(configuration["Workflows:ActiveWorkflow"]);

		var apiKey = GetRequired(configuration, "OpenAI:ApiKey", "OPENAI_API_KEY");
		var model = configuration["OpenAI:Model"] ?? "gpt-5.2";

		var services = new ServiceCollection();
		ConfigureAnagramServices(services, configuration);

		var chatClient = new OpenAIClient(apiKey)
			.GetChatClient(model)
			.AsIChatClient();

		services.AddSingleton<IChatClient>(chatClient);
		services.AddSingleton<SequentialWorkflowAgentFactory>();
		services.AddSingleton<IWorkflowStreamWriter, ConsoleWorkflowStreamWriter>();
		services.AddSingleton(sp =>
		{
			var options = new SequentialAnagramWorkflowOptions();
			configuration.GetSection(SequentialAnagramWorkflowOptions.SectionName).Bind(options);
			options.MaxPresentedItems = Math.Max(1, options.MaxPresentedItems);
			return options;
		});
		services.AddSingleton<ISequentialAnagramWorkflow, SequentialAnagramWorkflow>();

		services.AddSingleton<HandoffWorkflowAgentFactory>();
		services.AddSingleton<IHandoffStreamWriter, ConsoleHandoffStreamWriter>();
		services.AddSingleton(sp =>
		{
			var options = new HandoffWorkflowOptions();
			configuration.GetSection(HandoffWorkflowOptions.SectionName).Bind(options);
			options.MaxPresentedItems = Math.Max(1, options.MaxPresentedItems);
			options.MaxHandoffDepthPerTurn = Math.Max(1, options.MaxHandoffDepthPerTurn);
			options.StreamingStageTimeoutSeconds = Math.Max(1, options.StreamingStageTimeoutSeconds);
			options.RouteConfidenceThreshold = Math.Clamp(options.RouteConfidenceThreshold, 0.0, 1.0);
			return options;
		});
		services.AddSingleton<IHandoffWorkflow, HandoffWorkflow>();

		services.AddSingleton<GroupChatWorkflowAgentFactory>();
		services.AddSingleton<IGroupChatStreamWriter, ConsoleGroupChatStreamWriter>();
		services.AddSingleton(sp =>
		{
			var options = new GroupChatWorkflowOptions();
			configuration.GetSection(GroupChatWorkflowOptions.SectionName).Bind(options);
			options.StreamingStageTimeoutSeconds = Math.Max(1, options.StreamingStageTimeoutSeconds);
			options.MaxRoleHopsPerTurn = Math.Max(1, options.MaxRoleHopsPerTurn);
			options.MaxRoundsPerGame = Math.Max(1, options.MaxRoundsPerGame);
			options.MinWordLength = Math.Max(2, options.MinWordLength);
			return options;
		});
		services.AddSingleton<IGroupChatWorkflow, GroupChatWorkflow>();

		using var serviceProvider = services.BuildServiceProvider();
		var sequentialWorkflow = serviceProvider.GetRequiredService<ISequentialAnagramWorkflow>();
		var handoffWorkflow = serviceProvider.GetRequiredService<IHandoffWorkflow>();
		var groupChatWorkflow = serviceProvider.GetRequiredService<IGroupChatWorkflow>();

		System.Console.WriteLine($"Anagram Agent ready. Workflow: {workflowMode}. Type 'exit' to quit.");
		while (true)
		{
			System.Console.Write("You: ");
			var input = System.Console.ReadLine();

			if (string.IsNullOrWhiteSpace(input))
			{
				continue;
			}

			if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
			{
				break;
			}

			if (workflowMode == WorkflowMode.Handoff && string.Equals(input, "reset", StringComparison.OrdinalIgnoreCase))
			{
				await handoffWorkflow.ResetAsync();
				System.Console.WriteLine("Agent: Handoff conversation state reset.");
				continue;
			}

			if (workflowMode == WorkflowMode.GroupChat && string.Equals(input, "reset", StringComparison.OrdinalIgnoreCase))
			{
				await groupChatWorkflow.ResetAsync();
				System.Console.WriteLine("Agent: Group Chat conversation state reset.");
				continue;
			}

			try
			{
				var responseMessage = workflowMode switch
				{
					WorkflowMode.Handoff => (await handoffWorkflow.ExecuteAsync(input)).FinalMessage,
					WorkflowMode.GroupChat => (await groupChatWorkflow.ExecuteAsync(input)).FinalMessage,
					_ => (await sequentialWorkflow.ExecuteAsync(input)).FinalMessage
				};

				System.Console.WriteLine($"Agent: {responseMessage}");
			}
			catch (OperationCanceledException)
			{
				System.Console.WriteLine("Agent: Operation canceled.");
			}
			catch (Exception ex)
			{
				System.Console.WriteLine($"Agent: Workflow failed - {ex.Message}");
			}
		}
	}

	private static IConfiguration BuildConfiguration()
	{
		return new ConfigurationBuilder()
			// Use app base directory so config resolution is stable regardless of launch cwd.
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
			.AddUserSecrets(typeof(Program).Assembly, optional: true)
			.AddEnvironmentVariables()
			.Build();
	}

	private static void ConfigureAnagramServices(IServiceCollection services, IConfiguration configuration)
	{
		var maxResults = GetRequiredInt(configuration, "Settings:MaxResults", 1000);
		var maxWords = GetRequiredInt(configuration, "Settings:MaxWordsInAnagram", 50);
		var minUserWordLength = GetRequiredInt(configuration, "Settings:MinUserWordLength", 4);

		var dictionaryPath = ResolveDictionaryPath(configuration["Dictionary:WordFilePath"]);

		services.AddSingleton<WordNormalizer>();
		services.AddSingleton(new UserProcessor(minUserWordLength));
		services.AddSingleton<IWordRepository>(_ => new FileWordRepository(dictionaryPath));
		services.AddSingleton<IAnagramSolver>(sp =>
		{
			var repo = sp.GetRequiredService<IWordRepository>();
			return new DefaultAnagramSolver(repo, maxResults, maxWords);
		});

		var stopWords = configuration.GetSection("Analysis:StopWords").Get<string[]>() ?? Array.Empty<string>();
		services.AddSingleton<IWordFrequencyAnalyzer>(_ => new WordFrequencyAnalyzer(stopWords));

		services.AddSingleton<AnagramTools>();
	}

	private static string ResolveDictionaryPath(string? configuredPath)
	{
		var fallback = Path.Combine("Data", "zodynas.txt");
		var candidate = string.IsNullOrWhiteSpace(configuredPath) ? fallback : configuredPath;

		var fullPath = Path.GetFullPath(candidate, AppContext.BaseDirectory);
		if (!File.Exists(fullPath))
		{
			throw new FileNotFoundException(
				$"Dictionary file not found. Checked path: '{fullPath}'. Set Dictionary:WordFilePath in appsettings.json.");
		}

		return fullPath;
	}

	private static string GetRequired(IConfiguration configuration, string key, string envFallback)
	{
		var value = configuration[key] ?? Environment.GetEnvironmentVariable(envFallback);
		if (string.IsNullOrWhiteSpace(value))
		{
			throw new InvalidOperationException($"Missing configuration '{key}' or environment variable '{envFallback}'.");
		}

		return value;
	}

	private static int GetRequiredInt(IConfiguration configuration, string key, int fallback)
	{
		var raw = configuration[key];
		if (string.IsNullOrWhiteSpace(raw))
		{
			return fallback;
		}

		if (!int.TryParse(raw, out var parsed))
		{
			throw new InvalidOperationException($"Configuration '{key}' must be an integer.");
		}

		return parsed;
	}

	internal static WorkflowMode ParseWorkflowMode(string? configuredMode)
	{
		return configuredMode?.Trim().ToLowerInvariant() switch
		{
			"handoff" => WorkflowMode.Handoff,
			"groupchat" or "group-chat" => WorkflowMode.GroupChat,
			_ => WorkflowMode.Sequential
		};
	}

	internal enum WorkflowMode
	{
		Sequential,
		Handoff,
		GroupChat
	}
}
