namespace ResourcePulse.Services.Capacity;

public static class CapacitySegments
{
    // Compresses a date-ordered daily series into inclusive [From, To] runs of
    // equal hours. Zero-hour days are dropped: the absence of a segment means
    // zero capacity, so weekends/closures become gaps rather than 0h runs.
    public static IReadOnlyList<CapacitySegmentDto> Compress(IReadOnlyList<DailyCapacityDto> days)
    {
        var segments = new List<CapacitySegmentDto>();
        DateOnly runStart = default;
        DateOnly runEnd = default;
        TimeSpan runHours = default;
        var open = false;

        void Emit() => segments.Add(new CapacitySegmentDto { From = runStart, To = runEnd, HoursPerDay = runHours });

        foreach (var day in days)
        {
            if (day.Hours <= TimeSpan.Zero)
            {
                if (open) { Emit(); open = false; }
                continue;
            }

            if (open && day.Hours == runHours && day.Date == runEnd.AddDays(1))
            {
                runEnd = day.Date;
                continue;
            }

            if (open) Emit();
            runStart = runEnd = day.Date;
            runHours = day.Hours;
            open = true;
        }

        if (open) Emit();
        return segments;
    }
}
