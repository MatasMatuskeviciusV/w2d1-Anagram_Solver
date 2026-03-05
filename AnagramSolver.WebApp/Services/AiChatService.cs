namespace AnagramSolver.WebApp.Services;

using AnagramSolver.WebApp.Models;
using AnagramSolver.WebApp.Plugins;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Collections.Concurrent;
using System.Text.Json;

/// <summary>
/// Interface for managing chat history storage per session.
/// </summary>
public interface IChatHistoryRepository
{
    /// <summary>Gets chat history for a session.</summary>
    ChatHistory? GetChatHistory(string sessionId);

    /// <summary>Saves chat history for a session.</summary>
    void SaveChatHistory(string sessionId, ChatHistory chatHistory);

    /// <summary>Removes chat history for a session.</summary>
    void RemoveChatHistory(string sessionId);
}

/// <summary>
/// Thread-safe in-memory implementation using ConcurrentDictionary.
/// Stores chat histories for all sessions during application lifetime.
/// </summary>
public class InMemoryChatHistoryRepository : IChatHistoryRepository
{
    private readonly ConcurrentDictionary<string, ChatHistory> _chatHistories;
    private readonly ILogger<InMemoryChatHistoryRepository> _logger;

    public InMemoryChatHistoryRepository(ILogger<InMemoryChatHistoryRepository> logger)
    {
        _chatHistories = new ConcurrentDictionary<string, ChatHistory>();
        _logger = logger;
    }

    public ChatHistory? GetChatHistory(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        if (_chatHistories.TryGetValue(sessionId, out var chatHistory))
        {
            _logger.LogInformation("Chat history retrieved from memory for session {SessionId}.", sessionId);
            return chatHistory;
        }

        _logger.LogInformation("No chat history in memory for session {SessionId}.", sessionId);
        return null;
    }

    public void SaveChatHistory(string sessionId, ChatHistory chatHistory)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || chatHistory == null)
            return;

        _chatHistories.AddOrUpdate(sessionId, chatHistory, (key, oldValue) => chatHistory);
        _logger.LogInformation("Chat history saved to memory for session {SessionId}.", sessionId);
    }

    public void RemoveChatHistory(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return;

        if (_chatHistories.TryRemove(sessionId, out _))
        {
            _logger.LogInformation("Chat history removed from memory for session {SessionId}.", sessionId);
        }
    }
}

public class AiChatService : IAiChatService
{
    private const string ChatHistoryKeyPrefix = "chat_history_";
    private const string SystemPrompt = """
        You are an expert anagram assistant. Your role is to help users find and understand anagrams.

        You have access to the following functions:
        - GetAnagrams: Finds all anagrams for a given word
        - CountAnagrams: Counts how many anagrams exist for a word
        - GetCurrentTime: Gets the current date and time

        When a user asks for anagrams, automatically use the GetAnagrams or CountAnagrams function to provide accurate results.
        Always be helpful, clear, and provide relevant context about the anagrams you find.
        If the user's input is invalid or too short, guide them on how to use the service effectively.
        If the user's input is in Lithuanian, you should still process it and provide anagrams, respond in Lithuanian.
        """;

    private readonly Kernel _kernel;
    private readonly IDistributedCache _cache;
    private readonly IChatHistoryRepository _chatHistoryRepository;
    private readonly ILogger<AiChatService> _logger;
    private readonly AnagramPlugin _anagramPlugin;
    private readonly TimePlugin _timePlugin;

    public AiChatService(
        Kernel kernel,
        IDistributedCache cache,
        IChatHistoryRepository chatHistoryRepository,
        ILogger<AiChatService> logger,
        AnagramPlugin anagramPlugin,
        TimePlugin timePlugin)
    {
        _kernel = kernel;
        _cache = cache;
        _chatHistoryRepository = chatHistoryRepository;
        _logger = logger;
        _anagramPlugin = anagramPlugin;
        _timePlugin = timePlugin;
    }

    public async Task<ChatResponse> SendMessageAsync(string userMessage, string sessionId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return new ChatResponse
                {
                    Message = "User message cannot be empty.",
                    Success = false,
                    ErrorMessage = "User message cannot be empty."
                };
            }

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                return new ChatResponse
                {
                    Message = "Session ID is required.",
                    Success = false,
                    ErrorMessage = "Session ID is required."
                };
            }

            _logger.LogInformation(
                "Processing chat message for session {SessionId}: {Message}",
                sessionId,
                userMessage);

            // Import plugins into the kernel (safe to do per-request with scoped lifetime)
            _kernel.ImportPluginFromObject(_anagramPlugin, "anagram");
            _kernel.ImportPluginFromObject(_timePlugin, "time");

            // Load chat history: try in-memory first, then distributed cache
            var chatHistory = LoadChatHistoryFromMemory(sessionId) 
                ?? await LoadChatHistoryFromCacheAsync(sessionId);

            _logger.LogInformation(
                "Loaded chat history for session {SessionId} with {MessageCount} messages (including system prompt).",
                sessionId,
                chatHistory.Count);

            // Get chat completion service
            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

            // Add user message to history
            chatHistory.AddUserMessage(userMessage);

            // Configure OpenAI settings with auto function calling enabled
            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            _logger.LogInformation(
                "Invoking kernel with auto function calling enabled for session {SessionId}.",
                sessionId);

            // Invoke kernel with chat history and auto function calling
            var result = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                settings,
                _kernel);

            // Extract response text
            var responseText = result.Content ?? "No response generated.";

            // Add assistant response to history
            chatHistory.AddAssistantMessage(responseText);

            // Save chat history to both in-memory and distributed cache
            await SaveChatHistoryAsync(sessionId, chatHistory);

            _logger.LogInformation(
                "Chat message processed successfully for session {SessionId}.",
                sessionId);

            return new ChatResponse
            {
                Message = responseText,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing chat message for session {SessionId}: {ErrorMessage}",
                sessionId,
                ex.Message);

            return new ChatResponse
            {
                Message = "An error occurred while processing your request. Please try again.",
                Success = false,
                ErrorMessage = "An error occurred while processing your request. Please try again."
            };
        }
    }

    private ChatHistory? LoadChatHistoryFromMemory(string sessionId)
    {
        return _chatHistoryRepository.GetChatHistory(sessionId);
    }

    private async Task<ChatHistory> LoadChatHistoryFromCacheAsync(string sessionId)
    {
        var cacheKey = $"{ChatHistoryKeyPrefix}{sessionId}";
        var cachedHistory = await _cache.GetStringAsync(cacheKey);

        var chatHistory = new ChatHistory(SystemPrompt);

        if (!string.IsNullOrEmpty(cachedHistory))
        {
            try
            {
                var messages = JsonSerializer.Deserialize<List<ChatMessageDto>>(cachedHistory);
                if (messages != null)
                {
                    foreach (var message in messages)
                    {
                        var authorRole = message.Role == "user" ? AuthorRole.User : AuthorRole.Assistant;
                        chatHistory.AddMessage(authorRole, message.Content);
                    }
                }

                _logger.LogInformation("Chat history loaded from distributed cache for session {SessionId}.", sessionId);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to deserialize chat history from cache for session {SessionId}. Starting fresh.",
                    sessionId);
            }
        }
        else
        {
            _logger.LogInformation("No chat history found in cache. Starting fresh for session {SessionId}.", sessionId);
        }

        return chatHistory;
    }

    private async Task SaveChatHistoryAsync(string sessionId, ChatHistory chatHistory)
    {
        try
        {
            var messages = chatHistory
                .Skip(1) // Skip system prompt
                .Select(m => new ChatMessageDto 
                { 
                    Role = m.Role == AuthorRole.User ? "user" : "assistant", 
                    Content = m.Content 
                })
                .ToList();

            // Save to in-memory repository (fast access for subsequent requests in same session)
            _chatHistoryRepository.SaveChatHistory(sessionId, chatHistory);

            // Also save to distributed cache (persistence and scalability)
            await SaveToCacheAsync(sessionId, messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save chat history for session {SessionId}.",
                sessionId);
        }
    }

    private async Task SaveToCacheAsync(string sessionId, List<ChatMessageDto> messages)
    {
        var cacheKey = $"{ChatHistoryKeyPrefix}{sessionId}";

        try
        {
            var serialized = JsonSerializer.Serialize(messages);

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
            };

            await _cache.SetStringAsync(cacheKey, serialized, options);
            _logger.LogInformation("Chat history saved to distributed cache for session {SessionId}.", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save to distributed cache for session {SessionId}.",
                sessionId);
        }
    }

    private class ChatMessageDto
    {
        public required string Role { get; set; }
        public required string Content { get; set; }
    }
}
