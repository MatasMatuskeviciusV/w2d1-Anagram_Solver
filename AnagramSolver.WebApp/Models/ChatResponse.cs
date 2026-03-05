namespace AnagramSolver.WebApp.Models;

public class ChatResponse
{
    public required string Message { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}
