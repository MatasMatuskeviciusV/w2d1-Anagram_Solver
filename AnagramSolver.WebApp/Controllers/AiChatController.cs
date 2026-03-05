namespace AnagramSolver.WebApp.Controllers;

using AnagramSolver.WebApp.Models;
using AnagramSolver.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel.ChatCompletion;

[ApiController]
[Route("api/[controller]")]
public class AiChatController : ControllerBase
{
    private readonly IAiChatService _chatService;
    private readonly IChatHistoryRepository _chatHistoryRepository;
    private readonly ILogger<AiChatController> _logger;

    public AiChatController(
        IAiChatService chatService,
        IChatHistoryRepository chatHistoryRepository,
        ILogger<AiChatController> logger)
    {
        _chatService = chatService;
        _chatHistoryRepository = chatHistoryRepository;
        _logger = logger;
    }

    /// <summary>
    /// Sends a message to the AI agent and returns the response.
    /// Uses ASP.NET Core session management automatically via HttpContext.Session.
    /// </summary>
    /// <param name="request">The chat request containing the user message.</param>
    /// <returns>The AI agent response with session ID.</returns>
    [HttpPost("chat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponseDto>> Chat([FromBody] ChatRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var sessionId = HttpContext.Session.Id;

        // CRITICAL: Activate session by storing a marker value
        // Without this, the session cookie won't be sent to the client and each request gets a new session ID
        HttpContext.Session.SetString("_chatInitialized", "true");

        _logger.LogInformation("Chat request received for session {SessionId}", sessionId);

        var response = await _chatService.SendMessageAsync(request.Message, sessionId);

        if (!response.Success)
        {
            _logger.LogWarning(
                "Chat request failed for session {SessionId}: {ErrorMessage}",
                sessionId,
                response.ErrorMessage);

            return BadRequest(new ChatResponseDto
            {
                Response = response.ErrorMessage ?? "An error occurred.",
                SessionId = sessionId
            });
        }

        return Ok(new ChatResponseDto
        {
            Response = response.Message,
            SessionId = sessionId
        });
    }

    /// <summary>
    /// Retrieves chat history for the current session.
    /// </summary>
    /// <returns>Chat history with all messages for the session.</returns>
    [HttpGet("chat/history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ChatHistoryResponseDto> GetChatHistory()
    {
        var sessionId = HttpContext.Session.Id;

        // CRITICAL: Activate session by storing a marker value
        // This ensures the session cookie is sent to subsequent requests
        HttpContext.Session.SetString("_chatInitialized", "true");

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            _logger.LogWarning("GetChatHistory called with empty sessionId");
            return BadRequest(new { error = "Session not initialized" });
        }

        try
        {
            var chatHistory = _chatHistoryRepository.GetChatHistory(sessionId);

            if (chatHistory == null)
            {
                _logger.LogInformation("No chat history found for session {SessionId}", sessionId);
                return Ok(new ChatHistoryResponseDto
                {
                    SessionId = sessionId,
                    Messages = new List<ChatHistoryMessageDto>(),
                    TotalMessages = 0,
                    RetrievedAt = DateTime.UtcNow
                });
            }

            // Convert ChatHistory to DTO (skip system prompt which is first message)
            var messages = chatHistory
                .Skip(1)
                .Select(m => new ChatHistoryMessageDto
                {
                    Role = m.Role == AuthorRole.User ? "user" : "assistant",
                    Content = m.Content
                })
                .ToList();

            var response = new ChatHistoryResponseDto
            {
                SessionId = sessionId,
                Messages = messages,
                TotalMessages = messages.Count,
                RetrievedAt = DateTime.UtcNow
            };

            _logger.LogInformation(
                "Retrieved {MessageCount} messages from chat history for session {SessionId}",
                messages.Count,
                sessionId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving chat history for session {SessionId}", sessionId);
            return StatusCode(500, new { error = "An error occurred while retrieving chat history" });
        }
    }
}

/// <summary>
/// DTO for a single message in chat history.
/// </summary>
public class ChatHistoryMessageDto
{
    public required string Role { get; set; }
    public required string Content { get; set; }
}

/// <summary>
/// DTO for complete chat history response.
/// </summary>
public class ChatHistoryResponseDto
{
    public required string SessionId { get; set; }
    public required List<ChatHistoryMessageDto> Messages { get; set; }
    public int TotalMessages { get; set; }
    public DateTime RetrievedAt { get; set; }
}
