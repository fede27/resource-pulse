using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Capacity;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Persistence;
using ResourcePulse.Services.Allocations;
using ResourcePulse.Services.Capacity;
using ResourcePulse.Services.Configuration;

namespace ResourcePulse.Services.Plan;

// Plan mutation under one mechanism (ADR-0018). Dispatches on the runtime
// command type, applies the domain kernel (ADR-0019), and either commits or —
// when DryRun — returns the computed consequence WITHOUT persisting.
//
// dryRun mechanism (ADR-0018 §2): pure in-memory application of the domain
// primitives, projecting PlanBlockChange from the aggregates; SaveChanges runs
// only when !DryRun. New entities are added / removed only on commit; on a
// dryRun the ChangeTracker is cleared so tracked mutations are discarded.
//
// Cross-aggregate invariants ported verbatim from the retired AllocationService
// write paths: I1 (planning-level node), I3 (resource active — unconditional
// since ADR-0025: a coverage always has one),
// I4 (root not Closed/Cancelled), I6 (Hard ⇒ root committed). Overlap is never
// re-checked: it sums and is surfaced, not enforced (ADR-0014, ADR-0019 §4).
public sealed class PlanCommandService(
    ResourcePulseDbContext db,
    ICapacityQueryService capacity,
    ICommitmentPolicyService commitmentPolicy) : IPlanCommandService
{
    public Task<ServiceResult<PlanCommandResult>> ExecuteAsync(
        PlanCommand command, CancellationToken ct = default) =>
        command switch
        {
            CreateCommand c => CreateAsync(c, ct),
            CreateByHoursCommand c => CreateByHoursAsync(c, ct),
            CoverInferredCommand c => CoverInferredAsync(c, ct),
            EditCommand c => EditAsync(c, ct),
            SplitAtCommand c => SplitAtAsync(c, ct),
            ChangeRateFromCommand c => ChangeRateFromAsync(c, ct),
            MoveCommand c => MoveAsync(c, ct),
            RetargetCommand c => RetargetAsync(c, ct),
            ResizeCommand c => ResizeAsync(c, ct),
            ShiftFromCommand c => ShiftFromAsync(c, ct),
            ReassignCommand c => ReassignAsync(c, ct),
            ChangeStatusCommand c => ChangeStatusAsync(c, ct),
            DeleteCommand c => DeleteAsync(c, ct),
            CreateDemandCommand c => CreateDemandAsync(c, ct),
            EditDemandCommand c => EditDemandAsync(c, ct),
            DeleteDemandCommand c => DeleteDemandAsync(c, ct),
            SetAnchorCommand c => SetAnchorAsync(c, ct),
            PinCommand c => PinAsync(c, ct),
            ReplanNodeCommand c => ReplanNodeAsync(c, ct),
            MoveSubtreeCommand c => MoveSubtreeAsync(c, ct),
            SetAvailabilityCommand c => SetAvailabilityAsync(c, ct),
            MoveConstraintCommand c => MoveConstraintAsync(c, ct),
            _ => Task.FromResult(Fail(ServiceError.Validation(new Dictionary<string, string[]>
            {
                ["kind"] = [$"Unknown command type {command.GetType().Name}."]
            })))
        };

    // ── Creation (coverage against a demand) ─────────────────────────────────

    private async Task<ServiceResult<PlanCommandResult>> CreateAsync(CreateCommand c, CancellationToken ct)
    {
        var (demandErr, node) = await LoadDemandNodeAsync(c.DemandId, ct);
        if (demandErr is { } e0) return Fail(e0);
        if (await CheckResourceActiveAsync(c.ResourceId, ct) is { } e2) return Fail(e2);
        if (await CheckProjectStatusByNodeAsync(node, ct) is { } e3) return Fail(e3);
        if (c.Status == AllocationStatus.Hard && await CheckHardCommitmentAsync(node, ct) is { } e4)
            return Fail(e4);

        var (snapErr, snap) = await SnapToAnchorsAsync(c.StartAnchor, c.EndAnchor, node, c.ResourceId, c.PeriodStart, c.PeriodEnd, ct);
        if (snapErr is { } e5) return Fail(e5);

        return await CreateCoverageAsync(
            c, c.DemandId, node, c.ResourceId, snap, c.Percent, c.Status, c.Notes, "create", ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> CreateByHoursAsync(CreateByHoursCommand c, CancellationToken ct)
    {
        var (demandErr, node) = await LoadDemandNodeAsync(c.DemandId, ct);
        if (demandErr is { } e0) return Fail(e0);
        if (await CheckResourceActiveAsync(c.ResourceId, ct) is { } e2) return Fail(e2);
        if (await CheckProjectStatusByNodeAsync(node, ct) is { } e3) return Fail(e3);
        if (c.Status == AllocationStatus.Hard && await CheckHardCommitmentAsync(node, ct) is { } e4)
            return Fail(e4);

        // Snap first: the hours are spread over the window the anchors define.
        var (snapErr, snap) = await SnapToAnchorsAsync(c.StartAnchor, c.EndAnchor, node, c.ResourceId, c.PeriodStart, c.PeriodEnd, ct);
        if (snapErr is { } e5) return Fail(e5);

        var pct = await ResolvePercentForHoursAsync(c.ResourceId, snap.Start, snap.End, c.TargetHours, ct);
        if (pct.IsFailure) return Fail(pct.Error!);

        return await CreateCoverageAsync(
            c, c.DemandId, node, c.ResourceId, snap, pct.Value, c.Status, c.Notes, "createByHours", ct);
    }

    // coverInferred (attach-first, amendment C3). See CoverInferredCommand.
    private async Task<ServiceResult<PlanCommandResult>> CoverInferredAsync(CoverInferredCommand c, CancellationToken ct)
    {
        var (nodeErr, _) = await LoadPlanningNodeAsync(c.ProjectNodeId, ct);
        if (nodeErr is { } e1) return Fail(e1);
        if (await CheckRoleAndOwnerAsync(c.RoleId, c.OwnerResourceId, ct) is { } e2) return Fail(e2);
        if (await CheckResourceActiveAsync(c.ResourceId, ct) is { } e3) return Fail(e3);
        if (await CheckProjectStatusByNodeAsync(c.ProjectNodeId, ct) is { } e4) return Fail(e4);
        if (c.Status == AllocationStatus.Hard && await CheckHardCommitmentAsync(c.ProjectNodeId, ct) is { } e5)
            return Fail(e5);

        var (snapErr, snap) = await SnapToAnchorsAsync(c.StartAnchor, c.EndAnchor, c.ProjectNodeId, c.ResourceId, c.PeriodStart, c.PeriodEnd, ct);
        if (snapErr is { } e6) return Fail(e6);

        // Find uncovered demands on (node, role): best-effort, or covered < required.
        var candidates = await UncoveredDemandsAsync(c.ProjectNodeId, c.RoleId, ct);

        if (candidates.Count > 1)
        {
            // Ambiguous: return the candidate list, commit NOTHING (C3 branch 4).
            // The user disambiguates and re-issues a plain create with a demandId.
            db.ChangeTracker.Clear();
            var list = candidates.Select(d => ToDemandChange(d, PlanChangeKind.Candidate)).ToList();
            return ServiceResult<PlanCommandResult>.Success(new PlanCommandResult
            {
                CommandKind = "coverInferred",
                DryRun = c.DryRun,
                Committed = false,
                Changes = [],
                DemandChanges = list
            });
        }

        if (candidates.Count == 1)
        {
            // Attach to the existing uncovered demand; provenance unchanged.
            var target = candidates[0];
            return await CreateCoverageAsync(
                c, target.Id, target.ProjectNodeId, c.ResourceId,
                snap, c.Percent, c.Status, c.Notes, "coverInferred", ct);
        }

        // Fallback: materialize an Inferred, best-effort demand and cover it.
        Demand demand;
        Allocation a;
        try
        {
            demand = Demand.Create(
                c.ProjectNodeId, c.RoleId, requiredHours: null, DemandProvenance.Inferred, c.OwnerResourceId);
            a = Allocation.CreateCoverage(
                demand.Id, c.ProjectNodeId, c.ResourceId, snap.Start, snap.End, c.Percent, c.Notes, c.Status);
            snap.ApplyTo(a, "coverInferred");
        }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "coverInferred",
            [ToChange(a, PlanChangeKind.Created)], toAdd: [a], toRemove: null, ct,
            demandChanges: [ToDemandChange(demand, PlanChangeKind.Created)], demandsToAdd: [demand]);
    }

    private async Task<ServiceResult<PlanCommandResult>> CreateCoverageAsync(
        PlanCommand cmd, Guid demandId, Guid projectNodeId, Guid resourceId, SnappedWindow window,
        decimal percent, AllocationStatus status, string? notes, string kind, CancellationToken ct)
    {
        Allocation a;
        try
        {
            a = Allocation.CreateCoverage(demandId, projectNodeId, resourceId, window.Start, window.End, percent, notes, status);
            window.ApplyTo(a, kind);
        }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(cmd, kind, [ToChange(a, PlanChangeKind.Created)], toAdd: [a], toRemove: null, ct);
    }

    // Resolves a target-hours quantity to a percent against the resource's window
    // capacity, gating zero-capacity and the 1000% cap (shared by createByHours).
    private async Task<ServiceResult<decimal>> ResolvePercentForHoursAsync(
        Guid resourceId, DateOnly start, DateOnly end, TimeSpan targetHours, CancellationToken ct)
    {
        var cap = await CapacityInWindowAsync(resourceId, start, end, ct);
        if (cap.IsFailure) return ServiceResult<decimal>.Failure(cap.Error!);
        if (cap.Value <= TimeSpan.Zero)
            return ServiceResult<decimal>.Conflict(
                "Cannot allocate hours: resource has zero capacity in the requested window.");

        decimal percent;
        try { percent = AllocationResolver.PercentForHours(targetHours, cap.Value); }
        catch (DomainException ex) { return ServiceResult<decimal>.Conflict(ex.Message); }

        if (percent > Allocation.MaxAllocationPercent)
            return ServiceResult<decimal>.Conflict(
                $"Resolved percent {percent} exceeds the {Allocation.MaxAllocationPercent}% cap. " +
                "Widen the window or reduce target hours.");

        return ServiceResult<decimal>.Success(percent);
    }

    // ── Edit in place ────────────────────────────────────────────────────────

    private async Task<ServiceResult<PlanCommandResult>> EditAsync(EditCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);

        try
        {
            a.ChangePeriod(c.PeriodStart, c.PeriodEnd);
            a.ChangePercent(c.AllocationPercent);
            a.Annotate(c.Notes);
        }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "edit", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    // ── Span operations (ADR-0019) ───────────────────────────────────────────

    private async Task<ServiceResult<PlanCommandResult>> SplitAtAsync(SplitAtCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e) return Fail(e);

        Allocation second;
        try { second = a.SplitAt(c.Date, "Split"); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "splitAt",
            [ToChange(a, PlanChangeKind.Modified), ToChange(second, PlanChangeKind.Created)],
            toAdd: [second], toRemove: null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> ChangeRateFromAsync(ChangeRateFromCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e) return Fail(e);

        Allocation second;
        try { second = a.ChangeRateFrom(c.Date, c.NewRate, "ChangeRateFrom"); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "changeRateFrom",
            [ToChange(a, PlanChangeKind.Modified), ToChange(second, PlanChangeKind.Created)],
            toAdd: [second], toRemove: null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> MoveAsync(MoveCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e) return Fail(e);

        try { a.Shift(c.DeltaDays, "Move"); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "move", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    // Retarget — re-point the coverage to ANOTHER demand (amendment C1). Re-check
    // guards on the NEW target: I4 (new root not Closed/Cancelled) and, if the
    // coverage is Hard, I6 (new root commitment admits Hard). No silent demotion.
    private async Task<ServiceResult<PlanCommandResult>> RetargetAsync(RetargetCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);

        var (demandErr, newNode) = await LoadDemandNodeAsync(c.DemandId, ct);
        if (demandErr is { } e0) return Fail(e0);
        if (await CheckProjectStatusByNodeAsync(newNode, ct) is { } e1) return Fail(e1);
        if (a.Status == AllocationStatus.Hard && await CheckHardCommitmentAsync(newNode, ct) is { } e2)
            return Fail(e2);

        try { a.RetargetToDemand(c.DemandId, newNode); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        // I10 on the new target: a node anchor whose referent is not in the new
        // root no longer means anything to this coverage — it breaks (ADR-0034 §6).
        if (await PinAnchorsOutsideRootAsync(a, newNode, "Retarget", ct) is { } e3) return Fail(e3);

        return await FinalizeAsync(c, "retarget", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> ResizeAsync(ResizeCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e) return Fail(e);

        try { a.Resize(c.NewPeriodStart, c.NewPeriodEnd, "Resize"); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "resize", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> ShiftFromAsync(ShiftFromCommand c, CancellationToken ct)
    {
        // Lane = resource × project_node, downstream in time. Tracked load so the
        // mutations persist. Every coverage has a resource (ADR-0025), so the
        // ResourceId filter selects the lane and excludes nothing else.
        var lane = await db.Allocations
            .Where(a => a.ResourceId == c.ResourceId
                     && a.ProjectNodeId == c.ProjectNodeId
                     && a.PeriodStart >= c.FromDate)
            .OrderBy(a => a.PeriodStart)
            .ToListAsync(ct);

        if (lane.Count == 0)
            return await FinalizeAsync(c, "shiftFrom", [], null, null, ct);

        if (await CheckProjectStatusByNodeAsync(c.ProjectNodeId, ct) is { } e) return Fail(e);

        try
        {
            // Same delta on every block ⇒ relative gaps preserved. Any resulting
            // overlap with blocks before FromDate is allowed and sums.
            foreach (var a in lane) a.Shift(c.DeltaDays, "ShiftFrom");
        }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        var changes = lane.Select(a => ToChange(a, PlanChangeKind.Modified)).ToList();
        return await FinalizeAsync(c, "shiftFrom", changes, null, null, ct);
    }

    // ── Resource & status transitions ────────────────────────────────────────

    // Reassign — swap the covering resource on the same demand (amendment C1).
    private async Task<ServiceResult<PlanCommandResult>> ReassignAsync(ReassignCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (await CheckResourceActiveAsync(c.ResourceId, ct) is { } e1) return Fail(e1);
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e2) return Fail(e2);

        try { a.Reassign(c.ResourceId); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "reassign", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> ChangeStatusAsync(ChangeStatusCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (c.Status == AllocationStatus.Hard && await CheckHardCommitmentAsync(a.ProjectNodeId, ct) is { } e)
            return Fail(e);

        try { a.ChangeStatus(c.Status, c.Reason); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "changeStatus", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> DeleteAsync(DeleteCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);

        a.MarkDeleted();
        // Project the pre-delete state before removal.
        var change = ToChange(a, PlanChangeKind.Deleted);
        return await FinalizeAsync(c, "delete", [change], toAdd: null, toRemove: [a], ct);
    }

    // ── Boundaries (ADR-0034) ────────────────────────────────────────────────

    // setAnchor — tie an edge to a referent and snap it there (I9). The referent
    // is checked for scope (I10) and for having a date; the span rule may still
    // refuse the snap (start > end) and that is a Conflict, not a silent resize.
    private async Task<ServiceResult<PlanCommandResult>> SetAnchorAsync(SetAnchorCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e) return Fail(e);

        var (err, resolved) = await ResolveAnchorAsync(c.Anchor, c.Edge, a.ProjectNodeId, a.ResourceId, "Anchor", ct);
        if (err is { } e1) return Fail(e1);

        try { a.Anchor(c.Edge, resolved!.Anchor, resolved.Date, "SetAnchor"); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "setAnchor", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    // pin — release an edge, date untouched. No project-status guard: nothing in
    // the plan moves, the block only stops following.
    private async Task<ServiceResult<PlanCommandResult>> PinAsync(PinCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);

        try { a.Pin(c.Edge, "Pin"); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "pin", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    // ── Referent movement: replanNode / moveSubtree (ADR-0034 §5) ───────────

    private async Task<ServiceResult<PlanCommandResult>> ReplanNodeAsync(ReplanNodeCommand c, CancellationToken ct)
    {
        var node = await db.ProjectNodes.FindAsync([c.NodeId], ct);
        if (node is null) return NodeNotFound(c.NodeId);
        if (await CheckProjectStatusByNodeAsync(node.Id, ct) is { } e) return Fail(e);

        var before = NodeWindow.Of(node);
        try { node.Replan(c.PlannedStart, c.PlannedEnd); }
        catch (DomainException ex) { db.ChangeTracker.Clear(); return Fail(ServiceError.Conflict(ex.Message)); }

        var (propErr, dragged) = await PropagateAsync(new Dictionary<Guid, ProjectNode> { [node.Id] = node }, "ReplanNode", ct);
        if (propErr is { } e2) { db.ChangeTracker.Clear(); return Fail(e2); }

        return await FinalizeAsync(c, "replanNode", dragged, null, null, ct,
            referentChanges: [ToNodeChange(node, before)]);
    }

    private async Task<ServiceResult<PlanCommandResult>> MoveSubtreeAsync(MoveSubtreeCommand c, CancellationToken ct)
    {
        var node = await db.ProjectNodes.FindAsync([c.NodeId], ct);
        if (node is null) return NodeNotFound(c.NodeId);
        if (await CheckProjectStatusByNodeAsync(node.Id, ct) is { } e) return Fail(e);

        // The node and every descendant (Path prefix), tracked. Only nodes that
        // carry a planned date move; the others have nothing to translate.
        var prefix = node.Path + "/";
        var subtree = await db.ProjectNodes
            .Where(p => p.Id == node.Id || p.Path.StartsWith(prefix))
            .OrderBy(p => p.Path)
            .ToListAsync(ct);
        var dated = subtree.Where(p => p.PlannedStart is not null || p.PlannedEnd is not null).ToList();

        var befores = dated.ToDictionary(p => p.Id, NodeWindow.Of);
        try
        {
            foreach (var p in dated)
                p.Replan(p.PlannedStart?.AddDays(c.DeltaDays), p.PlannedEnd?.AddDays(c.DeltaDays));
        }
        catch (DomainException ex) { db.ChangeTracker.Clear(); return Fail(ServiceError.Conflict(ex.Message)); }

        var (propErr, dragged) = await PropagateAsync(dated.ToDictionary(p => p.Id), "MoveSubtree", ct);
        if (propErr is { } e2) { db.ChangeTracker.Clear(); return Fail(e2); }

        return await FinalizeAsync(c, "moveSubtree", dragged, null, null, ct,
            referentChanges: dated.Select(p => ToNodeChange(p, befores[p.Id])).ToList());
    }

    // Re-snaps every boundary anchored to one of `movedNodes` (I9). A block
    // anchored on both edges follows both at once (FollowReferents), so a shift
    // longer than the block does not invert it half-way. Returns the blocks whose
    // window actually changed — that count is the confirmation §8 asks for.
    //
    // Two refusals, both Conflict, nothing applied: a moved node lost the date
    // some boundary follows (clearing a date with dependants — pin them first),
    // and a re-snap that would invert a block (the phase shrank past the block's
    // other edge — no silent shortening).
    private async Task<(ServiceError? err, IReadOnlyList<PlanBlockChange> dragged)> PropagateAsync(
        IReadOnlyDictionary<Guid, ProjectNode> movedNodes, string reason, CancellationToken ct)
    {
        var ids = movedNodes.Keys.ToList();
        var dependants = await db.Allocations
            .Where(a => (a.StartAnchor.NodeId != null && ids.Contains(a.StartAnchor.NodeId.Value))
                     || (a.EndAnchor.NodeId != null && ids.Contains(a.EndAnchor.NodeId.Value)))
            .OrderBy(a => a.PeriodStart)
            .ToListAsync(ct);

        var dragged = new List<PlanBlockChange>();
        foreach (var a in dependants)
        {
            var (startErr, newStart) = FollowingDate(a.StartAnchor, movedNodes, dependants);
            if (startErr is not null) return (startErr, []);
            var (endErr, newEnd) = FollowingDate(a.EndAnchor, movedNodes, dependants);
            if (endErr is not null) return (endErr, []);

            var oldStart = a.PeriodStart;
            var oldEnd = a.PeriodEnd;
            try { a.FollowReferents(newStart, newEnd, reason); }
            catch (DomainException ex)
            {
                return (ServiceError.Conflict(
                    $"Coverage {a.Id} ({oldStart:yyyy-MM-dd} to {oldEnd:yyyy-MM-dd}) cannot follow its referent: {ex.Message} " +
                    "Pin or shorten the block first."), []);
            }

            if (a.PeriodStart != oldStart || a.PeriodEnd != oldEnd)
                dragged.Add(ToChange(a, PlanChangeKind.Modified));
        }
        return (null, dragged);
    }

    // The date an anchored edge must take after its referent moved, or null when
    // the edge does not follow one of the moved nodes. Error when the referent
    // lost the date this edge follows.
    private static (ServiceError? err, DateOnly? date) FollowingDate(
        BoundaryAnchor anchor, IReadOnlyDictionary<Guid, ProjectNode> movedNodes,
        IReadOnlyList<Allocation> dependants)
    {
        if (!anchor.RequiresNode || !movedNodes.TryGetValue(anchor.NodeId!.Value, out var referent))
            return (null, null);

        var date = anchor.Kind == AnchorKind.NodeStart ? referent.PlannedStart : referent.PlannedEnd;
        if (date is not null) return (null, date);

        var following = dependants.Count(d => d.StartAnchor.Equals(anchor)) + dependants.Count(d => d.EndAnchor.Equals(anchor));
        var which = anchor.Kind == AnchorKind.NodeStart ? "start" : "end";
        return (ServiceError.Conflict(
            $"ProjectNode {referent.Id} ('{referent.Name}'): {following} anchored boundary(ies) follow its planned {which}, " +
            "which cannot be cleared. Pin them first."), null);
    }

    // setAvailability — the person's boundary moves; the blocks anchored to it
    // follow (start edges -> AvailableFrom, end edges -> AvailableUntil). Same
    // refusals as the node case: clearing a followed date, or a re-snap that
    // would invert a block.
    private async Task<ServiceResult<PlanCommandResult>> SetAvailabilityAsync(SetAvailabilityCommand c, CancellationToken ct)
    {
        var person = await db.Resources.FindAsync([c.ResourceId], ct);
        if (person is null) return ServiceResult<PlanCommandResult>.NotFound($"Resource {c.ResourceId} not found.");

        var before = new NodeWindow(person.AvailableFrom, person.AvailableUntil);
        try { person.SetAvailability(c.AvailableFrom, c.AvailableUntil); }
        catch (DomainException ex) { db.ChangeTracker.Clear(); return Fail(ServiceError.Conflict(ex.Message)); }

        var dependants = await db.Allocations
            .Where(a => a.ResourceId == c.ResourceId
                     && (a.StartAnchor.Kind == AnchorKind.ResourceAvailability
                      || a.EndAnchor.Kind == AnchorKind.ResourceAvailability))
            .OrderBy(a => a.PeriodStart)
            .ToListAsync(ct);

        var startFollowers = dependants.Count(a => a.StartAnchor.Kind == AnchorKind.ResourceAvailability);
        var endFollowers = dependants.Count(a => a.EndAnchor.Kind == AnchorKind.ResourceAvailability);
        if (startFollowers > 0 && person.AvailableFrom is null)
        {
            db.ChangeTracker.Clear();
            return Fail(ServiceError.Conflict(
                $"Resource {person.Id} ('{person.Name}'): {startFollowers} anchored boundary(ies) follow AvailableFrom, which cannot be cleared. Pin them first."));
        }
        if (endFollowers > 0 && person.AvailableUntil is null)
        {
            db.ChangeTracker.Clear();
            return Fail(ServiceError.Conflict(
                $"Resource {person.Id} ('{person.Name}'): {endFollowers} anchored boundary(ies) follow AvailableUntil, which cannot be cleared. Pin them first."));
        }

        var dragged = new List<PlanBlockChange>();
        foreach (var a in dependants)
        {
            var newStart = a.StartAnchor.Kind == AnchorKind.ResourceAvailability ? person.AvailableFrom : null;
            var newEnd = a.EndAnchor.Kind == AnchorKind.ResourceAvailability ? person.AvailableUntil : null;
            var oldStart = a.PeriodStart;
            var oldEnd = a.PeriodEnd;
            try { a.FollowReferents(newStart, newEnd, "SetAvailability"); }
            catch (DomainException ex)
            {
                db.ChangeTracker.Clear();
                return Fail(ServiceError.Conflict(
                    $"Coverage {a.Id} ({oldStart:yyyy-MM-dd} to {oldEnd:yyyy-MM-dd}) cannot follow its referent: {ex.Message} " +
                    "Pin or shorten the block first."));
            }
            if (a.PeriodStart != oldStart || a.PeriodEnd != oldEnd)
                dragged.Add(ToChange(a, PlanChangeKind.Modified));
        }

        return await FinalizeAsync(c, "setAvailability", dragged, null, null, ct,
            referentChanges:
            [
                new PlanReferentChange
                {
                    Kind = PlanChangeKind.Modified,
                    Referent = ReferentKind.Resource,
                    Id = person.Id,
                    Name = person.Name,
                    OldStart = before.Start,
                    OldEnd = before.End,
                    NewStart = person.AvailableFrom,
                    NewEnd = person.AvailableUntil
                }
            ]);
    }

    private sealed record NodeWindow(DateOnly? Start, DateOnly? End)
    {
        public static NodeWindow Of(ProjectNode n) => new(n.PlannedStart, n.PlannedEnd);
    }

    private static PlanReferentChange ToNodeChange(ProjectNode n, NodeWindow before) => new()
    {
        Kind = PlanChangeKind.Modified,
        Referent = ReferentKind.Node,
        Id = n.Id,
        Name = n.Name,
        NodeType = n.NodeType,
        OldStart = before.Start,
        OldEnd = before.End,
        NewStart = n.PlannedStart,
        NewEnd = n.PlannedEnd
    };

    private static ServiceResult<PlanCommandResult> NodeNotFound(Guid id) =>
        ServiceResult<PlanCommandResult>.NotFound($"ProjectNode {id} not found.");

    private sealed record ResolvedAnchor(BoundaryAnchor Anchor, DateOnly Date);

    // The window a creation command lands on once its anchors are resolved: the
    // submitted dates, overridden per edge by the referent's date. ApplyTo ties
    // the (already snapped) edges after the aggregate is built, so the anchor is
    // set without a second move.
    private sealed record SnappedWindow(DateOnly Start, DateOnly End, ResolvedAnchor? StartAnchor, ResolvedAnchor? EndAnchor)
    {
        public void ApplyTo(Allocation a, string reason)
        {
            if (StartAnchor is { } s) a.Anchor(BoundaryEdge.Start, s.Anchor, s.Date, reason);
            if (EndAnchor is { } e) a.Anchor(BoundaryEdge.End, e.Anchor, e.Date, reason);
        }
    }

    private async Task<(ServiceError? err, SnappedWindow window)> SnapToAnchorsAsync(
        AnchorSpec? startSpec, AnchorSpec? endSpec, Guid coverageNodeId, Guid resourceId, DateOnly start, DateOnly end, CancellationToken ct)
    {
        ResolvedAnchor? s = null, e = null;
        if (startSpec is not null)
        {
            var (err, r) = await ResolveAnchorAsync(startSpec, BoundaryEdge.Start, coverageNodeId, resourceId, "StartAnchor", ct);
            if (err is not null) return (err, null!);
            s = r;
        }
        if (endSpec is not null)
        {
            var (err, r) = await ResolveAnchorAsync(endSpec, BoundaryEdge.End, coverageNodeId, resourceId, "EndAnchor", ct);
            if (err is not null) return (err, null!);
            e = r;
        }

        var snappedStart = s?.Date ?? start;
        var snappedEnd = e?.Date ?? end;
        if (snappedStart > snappedEnd)
            return (ServiceError.Conflict(
                $"Anchoring would invert the span: start {snappedStart:yyyy-MM-dd} is after end {snappedEnd:yyyy-MM-dd}."), null!);

        return (null, new SnappedWindow(snappedStart, snappedEnd, s, e));
    }

    // Resolves a wire anchor to (domain anchor, referent date) for a coverage on
    // `coverageNodeId` by `resourceId`. I10: a node referent must be
    // planning-level and in the same root; the availability referent is the
    // block's own person. I9: the referent must carry the date the kind reads —
    // a phase without PlannedEnd, a person without AvailableUntil, cannot be
    // anchored to (Conflict, not a guessed date).
    private async Task<(ServiceError? err, ResolvedAnchor? resolved)> ResolveAnchorAsync(
        AnchorSpec spec, BoundaryEdge edge, Guid coverageNodeId, Guid resourceId, string field, CancellationToken ct)
    {
        BoundaryAnchor anchor;
        try { anchor = BoundaryAnchor.Of(spec.Kind, spec.NodeId, spec.ConstraintId); }
        catch (DomainException ex) { return (FieldError(field, ex.Message), null); }

        switch (anchor.Kind)
        {
            case AnchorKind.NodeStart:
            case AnchorKind.NodeEnd:
            {
                var referent = await db.ProjectNodes.AsNoTracking()
                    .Where(p => p.Id == anchor.NodeId)
                    .Select(p => new { p.NodeType, p.Path, p.PlannedStart, p.PlannedEnd })
                    .FirstOrDefaultAsync(ct);
                if (referent is null)
                    return (FieldError(field, $"ProjectNode {anchor.NodeId} does not exist."), null);
                if (referent.NodeType != ProjectNodeType.Project && referent.NodeType != ProjectNodeType.Phase)
                    return (FieldError(field, $"Anchors target Project or Phase nodes (got {referent.NodeType})."), null);

                var (rootErr, coverageRoot) = await RootIdOfNodeAsync(coverageNodeId, ct);
                if (rootErr is not null) return (rootErr, null);
                if (!ProjectNodePath.TryGetRootId(referent.Path, out var referentRoot))
                    return (ServiceError.Failure("ProjectNode has an invalid materialized path."), null);
                if (referentRoot != coverageRoot)
                    return (FieldError(field, "Anchor referent must belong to the same root project as the coverage (I10)."), null);

                var date = anchor.Kind == AnchorKind.NodeStart ? referent.PlannedStart : referent.PlannedEnd;
                if (date is null)
                {
                    var which = anchor.Kind == AnchorKind.NodeStart ? "start" : "end";
                    return (ServiceError.Conflict(
                        $"ProjectNode {anchor.NodeId} has no planned {which} to anchor the {edge} boundary to."), null);
                }

                return (null, new ResolvedAnchor(anchor, date.Value));
            }
            case AnchorKind.ResourceAvailability:
            {
                var person = await db.Resources.AsNoTracking()
                    .Where(r => r.Id == resourceId)
                    .Select(r => new { r.AvailableFrom, r.AvailableUntil })
                    .FirstOrDefaultAsync(ct);
                if (person is null)
                    return (FieldError(field, $"Resource {resourceId} does not exist."), null);

                var date = edge == BoundaryEdge.Start ? person.AvailableFrom : person.AvailableUntil;
                if (date is null)
                {
                    var which = edge == BoundaryEdge.Start ? "AvailableFrom" : "AvailableUntil";
                    return (ServiceError.Conflict(
                        $"Resource {resourceId} has no declared {which} to anchor the {edge} boundary to."), null);
                }
                return (null, new ResolvedAnchor(anchor, date.Value));
            }
            case AnchorKind.External:
            {
                var constraint = await db.ExternalConstraints.AsNoTracking()
                    .Where(x => x.Id == anchor.ConstraintId)
                    .Select(x => new { x.RootProjectId, x.Date })
                    .FirstOrDefaultAsync(ct);
                if (constraint is null)
                    return (FieldError(field, $"External constraint {anchor.ConstraintId} does not exist."), null);

                var (rootErr, coverageRoot) = await RootIdOfNodeAsync(coverageNodeId, ct);
                if (rootErr is not null) return (rootErr, null);
                if (constraint.RootProjectId != coverageRoot)
                    return (FieldError(field, "Anchor referent must belong to the same root project as the coverage (I10)."), null);

                // An imposed date always has a date: nothing to refuse on I9.
                return (null, new ResolvedAnchor(anchor, constraint.Date));
            }
            default:
                return (ServiceError.Conflict($"Anchor kind {anchor.Kind} cannot be resolved."), null);
        }
    }

    // After a retarget: node and constraint anchors whose referent lies outside
    // the new root are pinned (I10 re-checked on the new target). Returns an
    // error only if a referent is unreadable.
    private async Task<ServiceError?> PinAnchorsOutsideRootAsync(Allocation a, Guid newNodeId, string reason, CancellationToken ct)
    {
        static bool Scoped(BoundaryAnchor x) => x.RequiresNode || x.RequiresConstraint;
        if (!Scoped(a.StartAnchor) && !Scoped(a.EndAnchor)) return null;

        var (rootErr, newRoot) = await RootIdOfNodeAsync(newNodeId, ct);
        if (rootErr is not null) return rootErr;

        foreach (var edge in new[] { BoundaryEdge.Start, BoundaryEdge.End })
        {
            var anchor = a.AnchorOf(edge);
            if (!Scoped(anchor)) continue;

            Guid? referentRoot;
            if (anchor.RequiresNode)
            {
                var (err, root) = await RootIdOfNodeAsync(anchor.NodeId!.Value, ct);
                if (err is not null) return err;
                referentRoot = root;
            }
            else
            {
                referentRoot = await db.ExternalConstraints.AsNoTracking()
                    .Where(x => x.Id == anchor.ConstraintId)
                    .Select(x => (Guid?)x.RootProjectId)
                    .FirstOrDefaultAsync(ct);
                if (referentRoot is null)
                    return ServiceError.Failure($"External constraint {anchor.ConstraintId} disappeared while resolving its root.");
            }

            if (referentRoot != newRoot) a.Pin(edge, reason);
        }
        return null;
    }

    // moveConstraint — the imposed date moves; the boundaries anchored to it
    // follow. Same refusal as the other referents on a re-snap that would
    // invert a block. I4 on the constraint's root.
    private async Task<ServiceResult<PlanCommandResult>> MoveConstraintAsync(MoveConstraintCommand c, CancellationToken ct)
    {
        var constraint = await db.ExternalConstraints.FindAsync([c.ConstraintId], ct);
        if (constraint is null)
            return ServiceResult<PlanCommandResult>.NotFound($"External constraint {c.ConstraintId} not found.");
        if (await CheckProjectStatusByNodeAsync(constraint.RootProjectId, ct) is { } e) return Fail(e);

        var before = constraint.Date;
        constraint.MoveTo(c.Date);

        var dependants = await db.Allocations
            .Where(a => a.StartAnchor.ConstraintId == c.ConstraintId || a.EndAnchor.ConstraintId == c.ConstraintId)
            .OrderBy(a => a.PeriodStart)
            .ToListAsync(ct);

        var dragged = new List<PlanBlockChange>();
        foreach (var a in dependants)
        {
            var newStart = a.StartAnchor.ConstraintId == c.ConstraintId ? c.Date : (DateOnly?)null;
            var newEnd = a.EndAnchor.ConstraintId == c.ConstraintId ? c.Date : (DateOnly?)null;
            var oldStart = a.PeriodStart;
            var oldEnd = a.PeriodEnd;
            try { a.FollowReferents(newStart, newEnd, "MoveConstraint"); }
            catch (DomainException ex)
            {
                db.ChangeTracker.Clear();
                return Fail(ServiceError.Conflict(
                    $"Coverage {a.Id} ({oldStart:yyyy-MM-dd} to {oldEnd:yyyy-MM-dd}) cannot follow its referent: {ex.Message} " +
                    "Pin or shorten the block first."));
            }
            if (a.PeriodStart != oldStart || a.PeriodEnd != oldEnd)
                dragged.Add(ToChange(a, PlanChangeKind.Modified));
        }

        return await FinalizeAsync(c, "moveConstraint", dragged, null, null, ct,
            referentChanges:
            [
                new PlanReferentChange
                {
                    Kind = PlanChangeKind.Modified,
                    Referent = ReferentKind.ExternalConstraint,
                    Id = constraint.Id,
                    Name = constraint.Name,
                    OldStart = before,
                    OldEnd = before,
                    NewStart = constraint.Date,
                    NewEnd = constraint.Date
                }
            ]);
    }

    private async Task<(ServiceError? err, Guid rootId)> RootIdOfNodeAsync(Guid nodeId, CancellationToken ct)
    {
        var path = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == nodeId).Select(p => p.Path).FirstOrDefaultAsync(ct);
        if (path is null) return (ServiceError.Failure($"ProjectNode {nodeId} disappeared while resolving its root."), Guid.Empty);
        if (!ProjectNodePath.TryGetRootId(path, out var rootId))
            return (ServiceError.Failure("ProjectNode has an invalid materialized path."), Guid.Empty);
        return (null, rootId);
    }

    private static ServiceError FieldError(string field, string message) =>
        ServiceError.Validation(new Dictionary<string, string[]> { [field] = [message] });

    // ── Demand mutation (Phase 5.0) ──────────────────────────────────────────

    private async Task<ServiceResult<PlanCommandResult>> CreateDemandAsync(CreateDemandCommand c, CancellationToken ct)
    {
        var (nodeErr, _) = await LoadPlanningNodeAsync(c.ProjectNodeId, ct);
        if (nodeErr is { } e1) return Fail(e1);
        if (await CheckProjectStatusByNodeAsync(c.ProjectNodeId, ct) is { } e2) return Fail(e2);
        if (await CheckRoleAndOwnerAsync(c.RoleId, c.OwnerResourceId, ct) is { } e3) return Fail(e3);

        Demand d;
        try
        {
            d = Demand.Create(
                c.ProjectNodeId, c.RoleId, c.RequiredHours, DemandProvenance.Declared, c.OwnerResourceId, c.Notes, c.DecideBy);
        }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "createDemand", [], null, null, ct,
            demandChanges: [ToDemandChange(d, PlanChangeKind.Created)], demandsToAdd: [d]);
    }

    private async Task<ServiceResult<PlanCommandResult>> EditDemandAsync(EditDemandCommand c, CancellationToken ct)
    {
        var d = await db.Demands.FindAsync([c.Id], ct);
        if (d is null) return DemandNotFound(c.Id);

        // Validate references that are actually changing.
        if (c.RoleId is Guid newRole && await CheckRoleAndOwnerAsync(newRole, null, ct) is { } eRole)
            return Fail(eRole);
        if (c.OwnerResourceIdSet && c.OwnerResourceId is Guid newOwner
            && await CheckRoleAndOwnerAsync(d.RoleId, newOwner, ct) is { } eOwner)
            return Fail(eOwner);

        try
        {
            if (c.RoleId is Guid role) d.ChangeRole(role);
            if (c.RequiredHoursSet) d.ChangeRequiredHours(c.RequiredHours);
            if (c.OwnerResourceIdSet) d.ChangeOwner(c.OwnerResourceId);
            if (c.NotesSet) d.Annotate(c.Notes);
            if (c.DecideBySet) d.ChangeDecideBy(c.DecideBy);
        }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "editDemand", [], null, null, ct,
            demandChanges: [ToDemandChange(d, PlanChangeKind.Modified)]);
    }

    private async Task<ServiceResult<PlanCommandResult>> DeleteDemandAsync(DeleteDemandCommand c, CancellationToken ct)
    {
        var d = await db.Demands.FindAsync([c.Id], ct);
        if (d is null) return DemandNotFound(c.Id);

        // A demand with coverage on it cannot be deleted (mirrors the FK Restrict).
        // Deallocation (delete the coverage) is how you free it first.
        if (await db.Allocations.AnyAsync(a => a.DemandId == c.Id, ct))
            return Fail(ServiceError.Conflict(
                "Demand has coverage on it and cannot be deleted. Remove the coverage first."));

        d.MarkDeleted();
        var change = ToDemandChange(d, PlanChangeKind.Deleted);
        return await FinalizeAsync(c, "deleteDemand", [], null, null, ct,
            demandChanges: [change], demandsToRemove: [d]);
    }

    // ── Persistence boundary ─────────────────────────────────────────────────

    // Commits (Add/Remove + SaveChanges) only when !DryRun. On a dryRun the
    // ChangeTracker is cleared so tracked mutations are discarded and nothing is
    // persisted (verified explicitly in the integration tests). `changes` /
    // `demandChanges` are built by the caller from the in-memory aggregates BEFORE
    // this point, so they reflect the would-be state in both modes.
    private async Task<ServiceResult<PlanCommandResult>> FinalizeAsync(
        PlanCommand cmd, string kind, IReadOnlyList<PlanBlockChange> changes,
        IReadOnlyList<Allocation>? toAdd, IReadOnlyList<Allocation>? toRemove, CancellationToken ct,
        IReadOnlyList<PlanDemandChange>? demandChanges = null,
        IReadOnlyList<Demand>? demandsToAdd = null,
        IReadOnlyList<Demand>? demandsToRemove = null,
        IReadOnlyList<PlanReferentChange>? referentChanges = null)
    {
        if (!cmd.DryRun)
        {
            if (demandsToAdd is not null)
                foreach (var d in demandsToAdd) await db.Demands.AddAsync(d, ct);
            if (toAdd is not null)
                foreach (var a in toAdd) await db.Allocations.AddAsync(a, ct);
            if (toRemove is not null)
                db.Allocations.RemoveRange(toRemove);
            if (demandsToRemove is not null)
                db.Demands.RemoveRange(demandsToRemove);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            db.ChangeTracker.Clear();
        }

        return ServiceResult<PlanCommandResult>.Success(new PlanCommandResult
        {
            CommandKind = kind,
            DryRun = cmd.DryRun,
            Committed = !cmd.DryRun,
            Changes = changes,
            DemandChanges = demandChanges ?? [],
            ReferentChanges = referentChanges ?? []
        });
    }

    private async Task<Allocation?> LoadAsync(Guid id, CancellationToken ct) =>
        await db.Allocations.FindAsync([id], ct);

    private static ServiceResult<PlanCommandResult> NotFound(Guid id) =>
        ServiceResult<PlanCommandResult>.NotFound($"Allocation {id} not found.");

    private static ServiceResult<PlanCommandResult> DemandNotFound(Guid id) =>
        ServiceResult<PlanCommandResult>.NotFound($"Demand {id} not found.");

    private static PlanDemandChange ToDemandChange(Demand d, PlanChangeKind kind) => new()
    {
        Kind = kind,
        Id = d.Id,
        ProjectNodeId = d.ProjectNodeId,
        RoleId = d.RoleId,
        RequiredHours = d.RequiredHours,
        Provenance = d.Provenance,
        OwnerResourceId = d.OwnerResourceId,
        Notes = d.Notes,
        DecideBy = d.DecideBy
    };

    private static ServiceResult<PlanCommandResult> Fail(ServiceError e) =>
        ServiceResult<PlanCommandResult>.Failure(e);

    private static PlanBlockChange ToChange(Allocation a, PlanChangeKind kind) => new()
    {
        Kind = kind,
        Id = a.Id,
        DemandId = a.DemandId,
        ResourceId = a.ResourceId,
        ProjectNodeId = a.ProjectNodeId,
        PeriodStart = a.PeriodStart,
        PeriodEnd = a.PeriodEnd,
        AllocationPercent = a.AllocationPercent,
        Status = a.Status,
        Notes = a.Notes,
        StartAnchor = BoundaryAnchorDto.From(a.StartAnchor),
        EndAnchor = BoundaryAnchorDto.From(a.EndAnchor)
    };

    // ── Cross-aggregate guards (ported from AllocationService) ────────────────

    // I1: node exists and is at capacity-planning level. Returns its Path.
    private async Task<(ServiceError? err, string? path)> LoadPlanningNodeAsync(Guid nodeId, CancellationToken ct)
    {
        var n = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == nodeId)
            .Select(p => new { p.NodeType, p.Path })
            .FirstOrDefaultAsync(ct);

        if (n is null)
            return (ServiceError.Validation(new Dictionary<string, string[]>
            {
                ["ProjectNodeId"] = [$"ProjectNode {nodeId} does not exist."]
            }), null);

        if (n.NodeType != ProjectNodeType.Project && n.NodeType != ProjectNodeType.Phase)
            return (ServiceError.Validation(new Dictionary<string, string[]>
            {
                ["ProjectNodeId"] = [$"Allocations are only allowed on Project or Phase nodes (got {n.NodeType})."]
            }), null);

        return (null, n.Path);
    }

    // I4: root project not Closed/Cancelled. By node id (resolves the path).
    private async Task<ServiceError?> CheckProjectStatusByNodeAsync(Guid nodeId, CancellationToken ct)
    {
        var path = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == nodeId).Select(p => p.Path).FirstOrDefaultAsync(ct);
        if (path is null) return null; // node vanished; surfaced elsewhere

        if (!ProjectNodePath.TryGetRootId(path, out var rootId))
            return ServiceError.Failure("ProjectNode has an invalid materialized path.");

        var status = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == rootId).Select(p => p.Status).FirstOrDefaultAsync(ct);

        if (status == ProjectStatus.Closed || status == ProjectStatus.Cancelled)
            return ServiceError.Conflict($"Project root is {status} and cannot accept new or modified allocations.");

        return null;
    }

    // I3: resource exists and is active.
    private async Task<ServiceError?> CheckResourceActiveAsync(Guid resourceId, CancellationToken ct)
    {
        var info = await db.Resources.AsNoTracking()
            .Where(r => r.Id == resourceId).Select(r => new { r.IsActive }).FirstOrDefaultAsync(ct);

        if (info is null)
            return ServiceError.Validation(new Dictionary<string, string[]>
            {
                ["ResourceId"] = [$"Resource {resourceId} does not exist."]
            });
        if (!info.IsActive)
            return ServiceError.Conflict($"Resource {resourceId} is inactive and cannot be allocated.");
        return null;
    }

    // I6: Hard requires the root project committed (Committed/Critical).
    private async Task<ServiceError?> CheckHardCommitmentAsync(Guid nodeId, CancellationToken ct)
    {
        var path = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == nodeId).Select(p => p.Path).FirstOrDefaultAsync(ct);
        if (path is null) return ServiceError.Failure($"ProjectNode {nodeId} disappeared while validating I6.");

        if (!ProjectNodePath.TryGetRootId(path, out var rootId))
            return ServiceError.Failure("ProjectNode has an invalid materialized path.");

        var level = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == rootId).Select(p => (CommitmentLevel?)p.CommitmentLevel).FirstOrDefaultAsync(ct);

        // I6 threshold read from CommitmentPolicy (ADR-0020) — no longer cabled.
        var policy = await commitmentPolicy.GetConfigurationAsync(ct);
        if (!policy.IsHardCommitted(level))
        {
            var allowed = string.Join(", ", policy.HardCommitLevels);
            return ServiceError.Conflict(
                $"Allocation cannot be set to Hard: the project root commitment level is " +
                $"'{level?.ToString() ?? "Unspecified"}'. Hard requires one of: {allowed}.");
        }
        return null;
    }

    // Loads a demand and returns its (denormalized) ProjectNodeId. Coverage reads
    // the node from here (I8) — the client never supplies it. I1 was enforced at
    // demand creation, so the node is guaranteed planning-level.
    private async Task<(ServiceError? err, Guid node)> LoadDemandNodeAsync(Guid demandId, CancellationToken ct)
    {
        var node = await db.Demands.AsNoTracking()
            .Where(d => d.Id == demandId)
            .Select(d => (Guid?)d.ProjectNodeId)
            .FirstOrDefaultAsync(ct);

        if (node is null)
            return (ServiceError.Validation(new Dictionary<string, string[]>
            {
                ["DemandId"] = [$"Demand {demandId} does not exist."]
            }), Guid.Empty);

        return (null, node.Value);
    }

    // Demands on (node, role) that can still take coverage (amendment C3): a
    // best-effort demand (RequiredHours null) always qualifies; a targeted demand
    // qualifies while its covered hours are below the target. Covered hours are the
    // reconciliation truth (% × capacity), so this consults the capacity service.
    private async Task<IReadOnlyList<Demand>> UncoveredDemandsAsync(Guid nodeId, Guid roleId, CancellationToken ct)
    {
        var demands = await db.Demands.AsNoTracking()
            .Where(d => d.ProjectNodeId == nodeId && d.RoleId == roleId)
            .ToListAsync(ct);

        var result = new List<Demand>();
        foreach (var d in demands)
        {
            if (d.RequiredHours is null) { result.Add(d); continue; }
            var covered = await CoveredHoursForDemandAsync(d.Id, ct);
            if (covered < d.RequiredHours.Value) result.Add(d);
        }
        return result;
    }

    // Sum of resolved coverage hours (% × capacity over each block's window) on a
    // demand. Sequential capacity loads (pooled DbContext, ADR-0010).
    private async Task<TimeSpan> CoveredHoursForDemandAsync(Guid demandId, CancellationToken ct)
    {
        var blocks = await db.Allocations.AsNoTracking()
            .Where(a => a.DemandId == demandId)
            .Select(a => new { a.ResourceId, a.PeriodStart, a.PeriodEnd, a.AllocationPercent })
            .ToListAsync(ct);

        var total = TimeSpan.Zero;
        foreach (var b in blocks)
        {
            var cap = await CapacityInWindowAsync(b.ResourceId, b.PeriodStart, b.PeriodEnd, ct);
            if (cap.IsFailure || cap.Value <= TimeSpan.Zero) continue;
            total += AllocationResolver.HoursForPercent(b.AllocationPercent, cap.Value);
        }
        return total;
    }

    // Demand references exist (RoleId required; OwnerResourceId optional). Both
    // target existing catalogue rows. Used by createDemand/editDemand (Phase 5.0).
    private async Task<ServiceError?> CheckRoleAndOwnerAsync(Guid roleId, Guid? ownerResourceId, CancellationToken ct)
    {
        if (!await db.Roles.AnyAsync(r => r.Id == roleId, ct))
            return ServiceError.Validation(new Dictionary<string, string[]>
            {
                ["RoleId"] = [$"Role {roleId} does not exist."]
            });

        if (ownerResourceId is Guid o && !await db.Resources.AnyAsync(r => r.Id == o, ct))
            return ServiceError.Validation(new Dictionary<string, string[]>
            {
                ["OwnerResourceId"] = [$"Resource {o} does not exist."]
            });
        return null;
    }

    private async Task<ServiceResult<TimeSpan>> CapacityInWindowAsync(
        Guid resourceId, DateOnly from, DateOnly toInclusive, CancellationToken ct)
    {
        var cap = await capacity.GetForResourceAsync(resourceId, from, toInclusive, ct);
        if (cap.IsFailure) return ServiceResult<TimeSpan>.Failure(cap.Error!);

        var total = TimeSpan.Zero;
        foreach (var d in cap.Value) total += d.Hours;
        return ServiceResult<TimeSpan>.Success(total);
    }
}
