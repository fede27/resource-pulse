namespace ResourcePulse.Domain.Capacity;

public readonly record struct DailyCapacity(DateOnly Date, TimeSpan Hours);
