namespace AnagramMsAgentFramework.Console.Workflows.SequentialAnagram.Models;

public sealed record PresenterInput(
	string OriginalInput,
	FinderResult Finder,
	AnalyzerResult Analyzer);
