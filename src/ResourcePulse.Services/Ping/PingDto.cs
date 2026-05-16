namespace ResourcePulse.Services.Ping;

public sealed class PingDto
{
    public Guid Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed record CreatePingDto(string Message);

public sealed record UpdatePingDto(string Message);
