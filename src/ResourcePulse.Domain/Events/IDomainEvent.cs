namespace ResourcePulse.Domain.Events;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
