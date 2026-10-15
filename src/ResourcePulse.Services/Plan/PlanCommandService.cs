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
//
// Boundaries (ADR-0035): the only referent is the coverage's own node, so this
// service is the ONLY surface that can drag a block by moving something else —
// replanNode and moveSubtree. Availability and external constraints are read
// nowhere here: they are displayed against the plan, never propagated into it.
public sealed class PlanCommandService(
    ResourcePulseDbContext db,
    ICapacityQueryService capacity,
    ICommitmentPolicyService commitmentPolicy,
    IPlanConsequenceService consequences) : IPlanCommandService
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
            FollowNodeCommand c => FollowNodeAsync(c, ct),
            PinCommand c => PinAsync(c, ct),
            ReplanNodeCommand c => ReplanNodeAsync(c, ct),
            MoveSubtreeCommand c => MoveSubtreeAsync(c, ct),
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

        var (winErr, snap) = await ResolveWindowAsync(node, c.PeriodStart, c.PeriodEnd, ct);
        if (winErr is { } e5) return Fail(e5);

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

        // Resolve the window first: the hours are spread over it, and a
        // following edge takes the node's boundary rather than a submitted date.
        var (winErr, snap) = await ResolveWindowAsync(node, c.PeriodStart, c.PeriodEnd, ct);
        if (winErr is { } e5) return Fail(e5);

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

        var (winErr, snap) = await ResolveWindowAsync(c.ProjectNodeId, c.PeriodStart, c.PeriodEnd, ct);
        if (winErr is { } e6) return Fail(e6);

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
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

        return await FinalizeAsync(c, "coverInferred",
            [ToChange(a, PlanChangeKind.Created)], toAdd: [a], toRemove: null, ct,
            demandChanges: [ToDemandChange(demand, PlanChangeKind.Created)], demandsToAdd: [demand]);
    }

    private async Task<ServiceResult<PlanCommandResult>> CreateCoverageAsync(
        PlanCommand cmd, Guid demandId, Guid projectNodeId, Guid resourceId, CoverageWindow window,
        decimal percent, AllocationStatus status, string? notes, string kind, CancellationToken ct)
    {
        Allocation a;
        try
        {
            a = Allocation.CreateCoverage(demandId, projectNodeId, resourceId, window.Start, window.End, percent, notes, status);
            window.ApplyTo(a, kind);
        }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

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
            return ServiceResult<decimal>.Failure(ServiceError.Conflict(
                ProblemCode.ZeroCapacityInWindow,
                "Cannot allocate hours: resource has zero capacity in the requested window.",
                new Dictionary<string, object?>
                {
                    ["resource"] = await ResourceNameAsync(resourceId, ct),
                    ["from"] = start,
                    ["to"] = end,
                }));

        decimal percent;
        try { percent = AllocationResolver.PercentForHours(targetHours, cap.Value); }
        catch (DomainException ex) { return ServiceResult<decimal>.Conflict(ex); }

        if (percent > Allocation.MaxAllocationPercent)
            return ServiceResult<decimal>.Failure(ServiceError.Conflict(
                ProblemCode.HoursAbovePercentCap,
                $"Resolved percent {percent} exceeds the {Allocation.MaxAllocationPercent}% cap. " +
                "Widen the window or reduce target hours.",
                new Dictionary<string, object?>
                {
                    ["percent"] = percent,
                    ["cap"] = Allocation.MaxAllocationPercent,
                }));

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
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

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
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

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
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

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
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

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
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

        // The referent moved with the demand: boundaries that follow now follow
        // the NEW node and re-snap to its dates (ADR-0035 §6).
        if (await ResnapAfterRetargetAsync(a, newNode, ct) is { } e3) return Fail(e3);

        return await FinalizeAsync(c, "retarget", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> ResizeAsync(ResizeCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e) return Fail(e);

        try { a.Resize(c.NewPeriodStart, c.NewPeriodEnd, "Resize"); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

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
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

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
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

        return await FinalizeAsync(c, "reassign", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> ChangeStatusAsync(ChangeStatusCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (c.Status == AllocationStatus.Hard && await CheckHardCommitmentAsync(a.ProjectNodeId, ct) is { } e)
            return Fail(e);

        try { a.ChangeStatus(c.Status, c.Reason); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

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

    // ── Boundaries (ADR-0035) ────────────────────────────────────────────────

    // followNode — re-attach an edge to its own node and snap it there (I9).
    // Nothing to resolve and nothing to scope: the node is the one this coverage
    // sits on. The node may still have no such planned date (Conflict), and the
    // snap may still invert the span (Conflict) — never a silent resize.
    private async Task<ServiceResult<PlanCommandResult>> FollowNodeAsync(FollowNodeCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);
        if (await CheckProjectStatusByNodeAsync(a.ProjectNodeId, ct) is { } e) return Fail(e);

        var (err, date) = await NodeBoundaryAsync(a.ProjectNodeId, c.Edge, ct);
        if (err is { } e1) return Fail(e1);

        try { a.FollowNode(c.Edge, date!.Value, "FollowNode"); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

        return await FinalizeAsync(c, "followNode", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    // pin — release an edge, date untouched. No project-status guard: nothing in
    // the plan moves, the block only stops following.
    private async Task<ServiceResult<PlanCommandResult>> PinAsync(PinCommand c, CancellationToken ct)
    {
        var a = await LoadAsync(c.Id, ct);
        if (a is null) return NotFound(c.Id);

        try { a.Pin(c.Edge, "Pin"); }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

        return await FinalizeAsync(c, "pin", [ToChange(a, PlanChangeKind.Modified)], null, null, ct);
    }

    // ── Moving the node: replanNode / moveSubtree (ADR-0035 §5) ─────────────

    private async Task<ServiceResult<PlanCommandResult>> ReplanNodeAsync(ReplanNodeCommand c, CancellationToken ct)
    {
        var node = await db.ProjectNodes.FindAsync([c.NodeId], ct);
        if (node is null) return NodeNotFound(c.NodeId);
        if (await CheckProjectStatusByNodeAsync(node.Id, ct) is { } e) return Fail(e);

        // Before the first mutation: see IPlanConsequenceService.BeginAsync.
        var pricing = await consequences.BeginAsync(ct);

        var before = NodeWindow.Of(node);
        try { node.Replan(c.PlannedStart, c.PlannedEnd); }
        catch (DomainException ex) { db.ChangeTracker.Clear(); return Fail(ServiceError.Conflict(ex)); }

        var (propErr, dragged, moved) = await PropagateAsync(new Dictionary<Guid, ProjectNode> { [node.Id] = node }, "ReplanNode", ct);
        if (propErr is { } e2) { db.ChangeTracker.Clear(); return Fail(e2); }

        return await FinalizeMoveAsync(c, "replanNode", dragged, moved, [ToNodeChange(node, before)], pricing, ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> MoveSubtreeAsync(MoveSubtreeCommand c, CancellationToken ct)
    {
        var node = await db.ProjectNodes.FindAsync([c.NodeId], ct);
        if (node is null) return NodeNotFound(c.NodeId);
        if (await CheckProjectStatusByNodeAsync(node.Id, ct) is { } e) return Fail(e);

        // The node and every descendant (Path prefix), tracked. Only nodes that
        // carry a planned date move; the others have nothing to translate.
        // Before the first mutation: see IPlanConsequenceService.BeginAsync.
        var pricing = await consequences.BeginAsync(ct);

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
        catch (DomainException ex) { db.ChangeTracker.Clear(); return Fail(ServiceError.Conflict(ex)); }

        var (propErr, dragged, moved) = await PropagateAsync(dated.ToDictionary(p => p.Id), "MoveSubtree", ct);
        if (propErr is { } e2) { db.ChangeTracker.Clear(); return Fail(e2); }

        return await FinalizeMoveAsync(c, "moveSubtree", dragged, moved,
            dated.Select(p => ToNodeChange(p, befores[p.Id])).ToList(), pricing, ct);
    }

    // The tail shared by the two node moves: price the consequence while the
    // database still holds the "before" (ADR-0036), then commit or discard. One
    // path for dryRun and execution, so the estimate IS the receipt.
    private async Task<ServiceResult<PlanCommandResult>> FinalizeMoveAsync(
        PlanCommand cmd, string kind, IReadOnlyList<PlanBlockChange> dragged, IReadOnlyList<Allocation> moved,
        IReadOnlyList<PlanReferentChange> referentChanges, PlanPricing pricing, CancellationToken ct)
    {
        var priced = await consequences.PriceAsync(pricing, moved, referentChanges, ct);
        if (priced.IsFailure) { db.ChangeTracker.Clear(); return Fail(priced.Error!); }

        return await FinalizeAsync(cmd, kind, dragged, null, null, ct,
            referentChanges: referentChanges, consequence: priced.Value);
    }

    // Re-snaps every boundary that still FOLLOWS one of `movedNodes` (I9).
    // Dependants are the coverages sitting ON those nodes — the referent is
    // implicit, so this is a plain ProjectNodeId filter, no anchor referent to
    // join. A block following on both edges follows both at once (FollowNodes),
    // so a shift longer than the block does not invert it half-way. Returns the
    // blocks whose window actually changed: that count IS the confirmation —
    // as projections (`dragged`) and as the tracked aggregates (`moved`), which
    // is what the consequence is priced from.
    //
    // Two refusals, both Conflict with nothing applied: a moved node lost a
    // planned date that some boundary follows (pin them first), and a re-snap
    // that would invert a block (the phase shrank past a pinned edge — no silent
    // shortening).
    private async Task<(ServiceError? err, IReadOnlyList<PlanBlockChange> dragged, IReadOnlyList<Allocation> moved)> PropagateAsync(
        IReadOnlyDictionary<Guid, ProjectNode> movedNodes, string reason, CancellationToken ct)
    {
        var ids = movedNodes.Keys.ToList();
        var dependants = await db.Allocations
            .Where(a => ids.Contains(a.ProjectNodeId)
                     && (a.StartAnchor == AnchorKind.FollowsNode || a.EndAnchor == AnchorKind.FollowsNode))
            .OrderBy(a => a.PeriodStart)
            .ToListAsync(ct);

        foreach (var node in movedNodes.Values)
        {
            if (ClearedDateError(node, BoundaryEdge.Start, dependants) is { } se) return (se, [], []);
            if (ClearedDateError(node, BoundaryEdge.End, dependants) is { } ee) return (ee, [], []);
        }

        var dragged = new List<PlanBlockChange>();
        var moved = new List<Allocation>();
        foreach (var a in dependants)
        {
            var node = movedNodes[a.ProjectNodeId];
            var newStart = a.Follows(BoundaryEdge.Start) ? node.PlannedStart : null;
            var newEnd = a.Follows(BoundaryEdge.End) ? node.PlannedEnd : null;

            var oldStart = a.PeriodStart;
            var oldEnd = a.PeriodEnd;
            try { a.FollowNodes(newStart, newEnd, reason); }
            catch (DomainException ex)
            {
                return (ServiceError.Conflict(
                    ProblemCode.FollowWouldInvertCoverage,
                    $"Coverage {a.Id} ({oldStart:yyyy-MM-dd} to {oldEnd:yyyy-MM-dd}) cannot follow " +
                    $"'{node.Name}': {ex.Message} Pin or shorten the block first.",
                    await InvertArgsAsync(node.Name, a.ResourceId, oldStart, oldEnd, ct)), [], []);
            }

            if (a.PeriodStart != oldStart || a.PeriodEnd != oldEnd)
            {
                dragged.Add(ToChange(a, PlanChangeKind.Modified));
                moved.Add(a);
            }
        }
        return (null, dragged, moved);
    }

    // A planned date cannot be cleared while boundaries take it: that would leave
    // them with no date to carry. Refused with the count, as everywhere else.
    private static ServiceError? ClearedDateError(
        ProjectNode node, BoundaryEdge edge, IReadOnlyList<Allocation> dependants)
    {
        var date = edge == BoundaryEdge.Start ? node.PlannedStart : node.PlannedEnd;
        if (date is not null) return null;

        var following = dependants.Count(d => d.ProjectNodeId == node.Id && d.Follows(edge));
        if (following == 0) return null;

        var which = edge == BoundaryEdge.Start ? "start" : "end";
        return ServiceError.Conflict(
            ProblemCode.PlannedDateFollowed,
            $"ProjectNode {node.Id} ('{node.Name}'): {following} coverage boundary(ies) follow its planned {which}, " +
            "which cannot be cleared. Pin them first.",
            new Dictionary<string, object?>
            {
                ["node"] = node.Name,
                ["edge"] = edge,
                ["count"] = following,
            });
    }

    private sealed record NodeWindow(DateOnly? Start, DateOnly? End)
    {
        public static NodeWindow Of(ProjectNode n) => new(n.PlannedStart, n.PlannedEnd);
    }

    private static PlanReferentChange ToNodeChange(ProjectNode n, NodeWindow before) => new()
    {
        Kind = PlanChangeKind.Modified,
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

    // The window a creation command lands on (ADR-0035 §2). A supplied date PINS
    // that edge; a null date — the normal case — makes the edge FOLLOW the node
    // and take its planned boundary. ApplyTo ties the (already correct) edges
    // after the aggregate is built, so the tie is set without a second move.
    private sealed record CoverageWindow(DateOnly Start, DateOnly End, bool StartFollows, bool EndFollows)
    {
        public void ApplyTo(Allocation a, string reason)
        {
            if (StartFollows) a.FollowNode(BoundaryEdge.Start, Start, reason);
            if (EndFollows) a.FollowNode(BoundaryEdge.End, End, reason);
        }
    }

    private async Task<(ServiceError? err, CoverageWindow window)> ResolveWindowAsync(
        Guid nodeId, DateOnly? start, DateOnly? end, CancellationToken ct)
    {
        var node = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == nodeId)
            .Select(p => new { p.Name, p.PlannedStart, p.PlannedEnd })
            .FirstOrDefaultAsync(ct);
        if (node is null)
            return (ServiceError.Failure($"ProjectNode {nodeId} disappeared while resolving the coverage window."), null!);

        var resolvedStart = start ?? node.PlannedStart;
        if (resolvedStart is null) return (NoPlannedDate(nodeId, node.Name, BoundaryEdge.Start), null!);

        var resolvedEnd = end ?? node.PlannedEnd;
        if (resolvedEnd is null) return (NoPlannedDate(nodeId, node.Name, BoundaryEdge.End), null!);

        if (resolvedStart > resolvedEnd)
            return (ServiceError.Conflict(
                $"The coverage window is inverted: start {resolvedStart:yyyy-MM-dd} is after end {resolvedEnd:yyyy-MM-dd}."), null!);

        return (null, new CoverageWindow(
            resolvedStart.Value, resolvedEnd.Value, StartFollows: start is null, EndFollows: end is null));
    }

    // The planned date one edge of a block on `nodeId` would follow.
    private async Task<(ServiceError? err, DateOnly? date)> NodeBoundaryAsync(
        Guid nodeId, BoundaryEdge edge, CancellationToken ct)
    {
        var node = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == nodeId)
            .Select(p => new { p.Name, p.PlannedStart, p.PlannedEnd })
            .FirstOrDefaultAsync(ct);
        if (node is null)
            return (ServiceError.Failure($"ProjectNode {nodeId} disappeared while reading its planned dates."), null);

        var date = edge == BoundaryEdge.Start ? node.PlannedStart : node.PlannedEnd;
        return date is null ? (NoPlannedDate(nodeId, node.Name, edge), null) : (null, date);
    }

    private static ServiceError NoPlannedDate(Guid nodeId, string name, BoundaryEdge edge)
    {
        var which = edge == BoundaryEdge.Start ? "start" : "end";
        return ServiceError.Conflict(
            ProblemCode.NodeHasNoPlannedDate,
            $"ProjectNode {nodeId} ('{name}') has no planned {which} for the coverage's {edge} boundary to follow. " +
            $"Plan the node, or give the coverage an explicit {which} date.",
            new Dictionary<string, object?>
            {
                ["node"] = name,
                ["edge"] = edge,
            });
    }

    // After a retarget the following boundaries follow a DIFFERENT node: re-snap
    // them to the new node's dates, as ONE span change so a long move cannot
    // invert the block half-way. An edge whose new node has no such planned date
    // stops following and keeps the date it had — pinning is the honest fallback,
    // and dryRun shows exactly where the block ended up.
    private async Task<ServiceError?> ResnapAfterRetargetAsync(Allocation a, Guid newNodeId, CancellationToken ct)
    {
        if (!a.Follows(BoundaryEdge.Start) && !a.Follows(BoundaryEdge.End)) return null;

        var node = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == newNodeId)
            .Select(p => new { p.Name, p.PlannedStart, p.PlannedEnd })
            .FirstOrDefaultAsync(ct);
        if (node is null)
            return ServiceError.Failure($"ProjectNode {newNodeId} disappeared while re-snapping the coverage.");

        DateOnly? newStart = null, newEnd = null;
        if (a.Follows(BoundaryEdge.Start))
        {
            if (node.PlannedStart is { } ps) newStart = ps;
            else a.Pin(BoundaryEdge.Start, "Retarget");
        }
        if (a.Follows(BoundaryEdge.End))
        {
            if (node.PlannedEnd is { } pe) newEnd = pe;
            else a.Pin(BoundaryEdge.End, "Retarget");
        }

        var (oldStart, oldEnd) = (a.PeriodStart, a.PeriodEnd);
        try { a.FollowNodes(newStart, newEnd, "Retarget"); }
        catch (DomainException ex)
        {
            return ServiceError.Conflict(
                ProblemCode.FollowWouldInvertCoverage,
                $"Coverage {a.Id} cannot follow '{node.Name}' after the retarget: {ex.Message} " +
                "Pin the boundary you want to keep first.",
                await InvertArgsAsync(node.Name, a.ResourceId, oldStart, oldEnd, ct));
        }
        return null;
    }

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
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

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
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex)); }

        return await FinalizeAsync(c, "editDemand", [], null, null, ct,
            demandChanges: [ToDemandChange(d, PlanChangeKind.Modified)]);
    }

    private async Task<ServiceResult<PlanCommandResult>> DeleteDemandAsync(DeleteDemandCommand c, CancellationToken ct)
    {
        var d = await db.Demands.FindAsync([c.Id], ct);
        if (d is null) return DemandNotFound(c.Id);

        // A demand with coverage on it cannot be deleted (mirrors the FK Restrict).
        // Deallocation (delete the coverage) is how you free it first.
        var coverages = await db.Allocations.CountAsync(a => a.DemandId == c.Id, ct);
        if (coverages > 0)
            return Fail(ServiceError.Conflict(
                ProblemCode.DemandHasCoverage,
                "Demand has coverage on it and cannot be deleted. Remove the coverage first.",
                new Dictionary<string, object?> { ["count"] = coverages }));

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
        IReadOnlyList<PlanReferentChange>? referentChanges = null,
        PlanConsequence? consequence = null)
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
            ReferentChanges = referentChanges ?? [],
            Consequence = consequence
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
        StartAnchor = a.StartAnchor,
        EndAnchor = a.EndAnchor
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

    // The pieces of a refusal name people, not ids (ADR-0037). Read only on the
    // refusal path, so the happy path pays nothing.
    private async Task<string> ResourceNameAsync(Guid resourceId, CancellationToken ct) =>
        await db.Resources.AsNoTracking()
            .Where(r => r.Id == resourceId).Select(r => r.Name).FirstOrDefaultAsync(ct) ?? string.Empty;

    private async Task<IReadOnlyDictionary<string, object?>> InvertArgsAsync(
        string nodeName, Guid resourceId, DateOnly from, DateOnly to, CancellationToken ct) =>
        new Dictionary<string, object?>
        {
            ["node"] = nodeName,
            ["resource"] = await ResourceNameAsync(resourceId, ct),
            ["from"] = from,
            ["to"] = to,
        };

    // I4: root project not Closed/Cancelled. By node id (resolves the path).
    private async Task<ServiceError?> CheckProjectStatusByNodeAsync(Guid nodeId, CancellationToken ct)
    {
        var path = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == nodeId).Select(p => p.Path).FirstOrDefaultAsync(ct);
        if (path is null) return null; // node vanished; surfaced elsewhere

        if (!ProjectNodePath.TryGetRootId(path, out var rootId))
            return ServiceError.Failure("ProjectNode has an invalid materialized path.");

        var root = await db.ProjectNodes.AsNoTracking()
            .Where(p => p.Id == rootId).Select(p => new { p.Status, p.Name }).FirstOrDefaultAsync(ct);
        var status = root?.Status;

        if (status == ProjectStatus.Closed || status == ProjectStatus.Cancelled)
            return ServiceError.Conflict(
                ProblemCode.ProjectRootClosed,
                $"Project root is {status} and cannot accept new or modified allocations.",
                new Dictionary<string, object?>
                {
                    ["project"] = root!.Name,
                    ["status"] = status,
                });

        return null;
    }

    // I3: resource exists and is active.
    private async Task<ServiceError?> CheckResourceActiveAsync(Guid resourceId, CancellationToken ct)
    {
        var info = await db.Resources.AsNoTracking()
            .Where(r => r.Id == resourceId).Select(r => new { r.IsActive, r.Name }).FirstOrDefaultAsync(ct);

        if (info is null)
            return ServiceError.Validation(new Dictionary<string, string[]>
            {
                ["ResourceId"] = [$"Resource {resourceId} does not exist."]
            });
        if (!info.IsActive)
            return ServiceError.Conflict(
                ProblemCode.ResourceInactive,
                $"Resource {resourceId} is inactive and cannot be allocated.",
                new Dictionary<string, object?> { ["resource"] = info.Name });
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
                ProblemCode.HardRequiresCommitment,
                $"Allocation cannot be set to Hard: the project root commitment level is " +
                $"'{level?.ToString() ?? "Unspecified"}'. Hard requires one of: {allowed}.",
                new Dictionary<string, object?>
                {
                    ["level"] = level,
                    ["required"] = policy.HardCommitLevels.Order().ToArray(),
                });
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
