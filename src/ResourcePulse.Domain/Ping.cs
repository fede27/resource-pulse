namespace ResourcePulse.Domain;

public class Ping : Entity<Guid>, IAuditable
{
    // Required by EF Core
    protected Ping() { }

    public Ping(Guid id) { Id = id; }

    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
