namespace AnagramSolver.WebApp.Services;

using AnagramSolver.WebApp.Models;

public interface IAiChatService
{
    Task<ChatResponse> SendMessageAsync(string userMessage, string sessionId);
}
