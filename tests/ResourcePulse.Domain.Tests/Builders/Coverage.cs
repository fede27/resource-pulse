using ResourcePulse.Domain.Allocations;

namespace ResourcePulse.Domain.Tests.Builders;

// Test convenience for building a coverage Allocation (Phase 5.1, ADR-0025).
// Mirrors the OLD Allocation.Create positional signature (resource, node, ...)
// so calculator tests that only care about (resource, node, period, percent) can
// swap Allocation.Create → Coverage.Cov with no other change. A fresh demand id
// is synthesized when the caller doesn't care which demand is covered.
public static class Coverage
{
    public static Allocation Cov(
        Guid resourceId,
        Guid projectNodeId,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal allocationPercent,
        string? notes = null,
        AllocationStatus status = AllocationStatus.Tentative) =>
        Allocation.CreateCoverage(
            Guid.NewGuid(), projectNodeId, resourceId, periodStart, periodEnd, allocationPercent, notes, status);

    // Explicit-demand overload for tests that assert on DemandId / retarget.
    public static Allocation CovOn(
        Guid demandId,
        Guid resourceId,
        Guid projectNodeId,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal allocationPercent,
        AllocationStatus status = AllocationStatus.Tentative) =>
        Allocation.CreateCoverage(
            demandId, projectNodeId, resourceId, periodStart, periodEnd, allocationPercent, notes: null, status);
}
