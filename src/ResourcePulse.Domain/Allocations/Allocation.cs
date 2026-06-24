using ResourcePulse.Common.Domain;
using ResourcePulse.Domain.Events;

namespace ResourcePulse.Domain.Allocations;

// A commitment over an inclusive date window on a project node. Aggregate root
// with its own lifecycle.
//
// Status di forma (ADR-0016, I7 — XOR sull'atomo):
//   - Assegnato:    ResourceId valorizzato, campi placeholder nulli.
//   - Placeholder:  ResourceId nullo, RoleSkillId valorizzato (ruolo scoperto).
// La transizione Assegnato ↔ Placeholder conserva Id, span, rate%, status.
//
// Status di impegno (ADR-0015):
//   - Tentative (default): ipotesi di pianificazione, libera in tutti i casi.
//   - Hard:                commitment che richiede fondamento. L'invariante I6
//                          (service-level) ammette Hard solo se il Project
//                          radice del nodo ha CommitmentLevel ∈ {Committed,
//                          Critical}; l'invariante NON è espressa qui — è
//                          enforced da AllocationService.
//
// Local invariants only:
//   - period start <= period end
//   - rate% in (0, 1000] — typo safeguard, non un cap di overcommitment (ADR-0013)
//   - XOR di forma (ResourceId vs placeholder fields)
//
// Cross-aggregate (I1, I3, I4, I6) live nel service layer (AllocationService).
// I3 (risorsa attiva) si applica solo allo stato Assegnato — il placeholder
// non ha risorsa per definizione (ADR-0016).
//
// Overlap stesso (resource, project_node): prima classe, le rate% sommano
// (ADR-0014).
public sealed class Allocation : Entity<Guid>, IAuditable
{
    public Guid? ResourceId { get; private set; }
    public Guid ProjectNodeId { get; private set; }
    public DateOnly PeriodStart { get; private set; }
    public DateOnly PeriodEnd { get; private set; }
    public decimal AllocationPercent { get; private set; }
    public AllocationStatus Status { get; private set; }
    public string? Notes { get; private set; }

    // Placeholder fields — valorizzati iff ResourceId is null.
    public Guid? RoleSkillId { get; private set; }
    public Guid? OwnerResourceId { get; private set; }

    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    // Convenience predicate. Source of truth resta i campi.
    public bool IsPlaceholder => ResourceId is null;

    private Allocation() { }

    // Crea un blocco assegnato. Default Status = Tentative (ADR-0015 §1).
    public static Allocation Create(
        Guid resourceId,
        Guid projectNodeId,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal allocationPercent,
        string? notes = null,
        AllocationStatus status = AllocationStatus.Tentative)
    {
        if (resourceId == Guid.Empty)
            throw new DomainException("Allocation must reference a resource.");
        AssertCommonInputs(projectNodeId, periodStart, periodEnd, allocationPercent);
        AssertStatusKnown(status);

        var id = Guid.NewGuid();
        var allocation = new Allocation
        {
            Id = id,
            ResourceId = resourceId,
            ProjectNodeId = projectNodeId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            AllocationPercent = allocationPercent,
            Status = status,
            Notes = NormalizeNotes(notes),
            RoleSkillId = null,
            OwnerResourceId = null
        };

        allocation.RaiseEvent(new AllocationCreated(
            id, resourceId, projectNodeId, periodStart, periodEnd, allocationPercent, DateTimeOffset.UtcNow));
        return allocation;
    }

    // Crea direttamente un placeholder (ruolo scoperto). ADR-0016 §2:
    // ResourceId è null, RoleSkillId è il ruolo richiesto, OwnerResourceId è
    // chi presidia la riassegnazione (opzionale).
    public static Allocation CreatePlaceholder(
        Guid projectNodeId,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal allocationPercent,
        Guid roleSkillId,
        Guid? ownerResourceId,
        string? notes = null,
        AllocationStatus status = AllocationStatus.Tentative)
    {
        if (roleSkillId == Guid.Empty)
            throw new DomainException("Placeholder allocation must reference a role skill.");
        if (ownerResourceId is Guid o && o == Guid.Empty)
            throw new DomainException("OwnerResourceId, when provided, must not be Guid.Empty.");
        AssertCommonInputs(projectNodeId, periodStart, periodEnd, allocationPercent);
        AssertStatusKnown(status);

        var id = Guid.NewGuid();
        var allocation = new Allocation
        {
            Id = id,
            ResourceId = null,
            ProjectNodeId = projectNodeId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            AllocationPercent = allocationPercent,
            Status = status,
            Notes = NormalizeNotes(notes),
            RoleSkillId = roleSkillId,
            OwnerResourceId = ownerResourceId
        };

        // No AllocationCreated event for placeholders: it carries a ResourceId
        // in its schema (Phase 4) and adapting it would force a breaking change
        // before any consumer exists. The conversion/assign events carry the
        // placeholder lifecycle for now; a future revision can introduce a
        // PlaceholderCreated event if/when needed (ADR-0016 §"Domande aperte").
        return allocation;
    }

    // reason is optional decision-level provenance (ADR-0017), propagated to
    // AllocationPeriodChanged. The span operations (Shift, Resize, cascade)
    // pass it; Update / Move leave it null.
    public void ChangePeriod(DateOnly newStart, DateOnly newEnd, string? reason = null)
    {
        if (newStart > newEnd)
            throw new DomainException("PeriodStart must be on or before PeriodEnd.");
        if (PeriodStart == newStart && PeriodEnd == newEnd) return; // no-op suppresses event

        var oldStart = PeriodStart;
        var oldEnd = PeriodEnd;
        PeriodStart = newStart;
        PeriodEnd = newEnd;
        RaiseEvent(new AllocationPeriodChanged(
            Id, oldStart, oldEnd, newStart, newEnd, NormalizeReason(reason), DateTimeOffset.UtcNow));
    }

    // ── Span operations (ADR-0017) ───────────────────────────────────────────
    // The friction of planning is in the update, not the insert (§12), so these
    // are first-class. All preserve identity, form (assigned/placeholder),
    // status and node; only the window (and, for ChangeRateFrom, the rate of the
    // new block) changes.

    // SplitAt — structural, NON-destructive. Cuts the span at `date` into two
    // adjacent, NON-overlapping blocks: this becomes [PeriodStart, date-1], and
    // the returned new sibling covers [date, PeriodEnd]. Both carry the same
    // rate%, status, project node and form. Because the blocks do not overlap at
    // the boundary, the per-day rate% sum is identical to the original on every
    // date — the split is equivalent to the original (ADR-0014, ADR-0017 §1).
    //
    // `date` must be strictly interior: PeriodStart < date <= PeriodEnd, so both
    // sides are non-empty. (date == PeriodEnd is the only valid cut of a 2-day
    // span; date == PeriodStart would leave the first side empty.)
    //
    // The split does NOT change the rate — that is a separate, composable gesture
    // (ChangeRateFrom). Keeping "add" (stack a summing block) and "correct"
    // (split then edit) distinct is the UX constraint of ADR-0014.
    public Allocation SplitAt(DateOnly date, string? reason = null)
    {
        if (date <= PeriodStart || date > PeriodEnd)
            throw new DomainException(
                "Split date must be strictly inside the allocation span (PeriodStart < date <= PeriodEnd).");

        // Second block: [date, PeriodEnd], identical form/rate/status/node.
        var second = CloneForSplit(date, PeriodEnd);

        // First block (this): [PeriodStart, date-1]. Set directly rather than via
        // ChangePeriod — the AllocationSplit event carries the provenance for the
        // whole operation, so we suppress the redundant AllocationPeriodChanged.
        PeriodEnd = date.AddDays(-1);

        RaiseEvent(new AllocationSplit(Id, date, second.Id, NormalizeReason(reason), DateTimeOffset.UtcNow));
        return second;
    }

    // ChangeRateFrom — "change the rate from `date` onward". Composition of
    // SplitAt + ChangePercent on the new (second) block (ADR-0017 §2): the
    // original keeps its rate on [start, date-1]; the second block [date, end]
    // takes newRate. Yields the piecewise-constant profile the user means by
    // "edit the rate mid-span" (§8). Returns the new block for the caller to
    // persist.
    public Allocation ChangeRateFrom(DateOnly date, decimal newRate, string? reason = null)
    {
        var second = SplitAt(date, reason ?? "ChangeRateFrom");
        second.ChangePercent(newRate);
        return second;
    }

    // Shift — translate the whole span by `deltaDays` (may be negative),
    // preserving duration and rate. Reuses ChangePeriod; raises
    // AllocationPeriodChanged with the provenance reason. delta == 0 is a
    // suppressed no-op.
    public void Shift(int deltaDays, string? reason = null) =>
        ChangePeriod(PeriodStart.AddDays(deltaDays), PeriodEnd.AddDays(deltaDays), reason ?? "Shift");

    // Resize — move a single edge to an explicit date, leaving the other fixed.
    // At least one of newStart/newEnd must be provided. Reuses ChangePeriod,
    // which enforces start <= end.
    public void Resize(DateOnly? newStart, DateOnly? newEnd, string? reason = null)
    {
        if (newStart is null && newEnd is null)
            throw new DomainException("Resize requires at least one of newStart or newEnd.");
        ChangePeriod(newStart ?? PeriodStart, newEnd ?? PeriodEnd, reason ?? "Resize");
    }

    // Copies the current block into a new aggregate over [start, end], preserving
    // form (assigned -> same resource; placeholder -> same role/owner), rate,
    // status and notes. New Id; no creation event (provenance lives on the
    // AllocationSplit raised by SplitAt — same convention as CreatePlaceholder).
    private Allocation CloneForSplit(DateOnly start, DateOnly end) =>
        new()
        {
            Id = Guid.NewGuid(),
            ResourceId = ResourceId,
            ProjectNodeId = ProjectNodeId,
            PeriodStart = start,
            PeriodEnd = end,
            AllocationPercent = AllocationPercent,
            Status = Status,
            Notes = Notes,
            RoleSkillId = RoleSkillId,
            OwnerResourceId = OwnerResourceId
        };

    public void ChangePercent(decimal newPercent)
    {
        AssertPercentInRange(newPercent);
        if (AllocationPercent == newPercent) return; // no-op suppresses event

        var oldPercent = AllocationPercent;
        AllocationPercent = newPercent;
        RaiseEvent(new AllocationPercentChanged(Id, oldPercent, newPercent, DateTimeOffset.UtcNow));
    }

    public void Annotate(string? notes) => Notes = NormalizeNotes(notes);

    // Cambia lo status. Cross-aggregate gating (I6 — Hard richiede progetto
    // committato) resta a carico di AllocationService. Reason è opzionale e
    // si propaga nell'evento — usato dalla cascade demotion (ADR-0015 §4)
    // per registrare la causale "ProjectCommitmentDowngrade".
    public void ChangeStatus(AllocationStatus newStatus, string? reason = null)
    {
        AssertStatusKnown(newStatus);
        if (Status == newStatus) return; // no-op suppresses event

        var oldStatus = Status;
        Status = newStatus;
        RaiseEvent(new AllocationStatusChanged(Id, oldStatus, newStatus, NormalizeReason(reason), DateTimeOffset.UtcNow));
    }

    // Transizione Assegnato → Placeholder (ADR-0016 §4). Conserva tutto
    // tranne il puntatore alla risorsa, che viene "convertito" in ruolo
    // scoperto + owner.
    public void ConvertToPlaceholder(Guid roleSkillId, Guid? ownerResourceId)
    {
        if (IsPlaceholder)
            throw new DomainException("Allocation is already a placeholder.");
        if (roleSkillId == Guid.Empty)
            throw new DomainException("Placeholder allocation must reference a role skill.");
        if (ownerResourceId is Guid o && o == Guid.Empty)
            throw new DomainException("OwnerResourceId, when provided, must not be Guid.Empty.");

        var oldResourceId = ResourceId!.Value;
        ResourceId = null;
        RoleSkillId = roleSkillId;
        OwnerResourceId = ownerResourceId;

        RaiseEvent(new AllocationConvertedToPlaceholder(
            Id, oldResourceId, ProjectNodeId, roleSkillId, ownerResourceId, DateTimeOffset.UtcNow));
    }

    // Transizione Placeholder → Assegnato (ADR-0016 §4). Inverso di
    // ConvertToPlaceholder: conserva Id, span, rate%, status; valorizza la
    // risorsa e azzera i campi placeholder.
    public void AssignTo(Guid resourceId)
    {
        if (!IsPlaceholder)
            throw new DomainException("Only a placeholder allocation can be assigned to a resource.");
        if (resourceId == Guid.Empty)
            throw new DomainException("Allocation must reference a resource.");

        ResourceId = resourceId;
        RoleSkillId = null;
        OwnerResourceId = null;

        RaiseEvent(new PlaceholderAssignedToResource(Id, resourceId, ProjectNodeId, DateTimeOffset.UtcNow));
    }

    // Chiamato dal service layer subito prima di repository.Remove. Si applica
    // a entrambi gli stati di forma — l'evento AllocationDeleted è
    // semanticamente "il blocco esce dalla collezione" (ADR-0012, ristretto da
    // ADR-0016).
    public void MarkDeleted() =>
        RaiseEvent(new AllocationDeleted(Id, DateTimeOffset.UtcNow));

    // Upper bound is 1000% — a deliberate overcommitment signal for a single
    // allocation (see ADR-0013). The cap is a typo safeguard, not a domain
    // ceiling on overcommitment severity.
    public const decimal MaxAllocationPercent = 1000m;

    // ── helpers ─────────────────────────────────────────────────────────────

    private static void AssertCommonInputs(
        Guid projectNodeId, DateOnly periodStart, DateOnly periodEnd, decimal allocationPercent)
    {
        if (projectNodeId == Guid.Empty)
            throw new DomainException("Allocation must reference a project node.");
        if (periodStart > periodEnd)
            throw new DomainException("PeriodStart must be on or before PeriodEnd.");
        AssertPercentInRange(allocationPercent);
    }

    private static void AssertPercentInRange(decimal percent)
    {
        if (percent <= 0m || percent > MaxAllocationPercent)
            throw new DomainException(
                $"AllocationPercent must be in the range (0, {MaxAllocationPercent}].");
    }

    private static void AssertStatusKnown(AllocationStatus status)
    {
        if (!Enum.IsDefined(status))
            throw new DomainException($"Invalid allocation status '{status}'.");
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (notes is null) return null;
        var trimmed = notes.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string? NormalizeReason(string? reason)
    {
        if (reason is null) return null;
        var trimmed = reason.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
