using Microsoft.EntityFrameworkCore;
using ResourcePulse.Common.Domain;
using ResourcePulse.Common.Results;
using ResourcePulse.Domain.Allocations;
using ResourcePulse.Domain.Capacity;
using ResourcePulse.Domain.Demands;
using ResourcePulse.Domain.Projects;
using ResourcePulse.Persistence;
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
// write paths: I1 (planning-level node), I3 (resource active — assigned only),
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

        return await CreateCoverageAsync(
            c, c.DemandId, node, c.ResourceId, c.PeriodStart, c.PeriodEnd, c.Percent, c.Status, c.Notes, "create", ct);
    }

    private async Task<ServiceResult<PlanCommandResult>> CreateByHoursAsync(CreateByHoursCommand c, CancellationToken ct)
    {
        var (demandErr, node) = await LoadDemandNodeAsync(c.DemandId, ct);
        if (demandErr is { } e0) return Fail(e0);
        if (await CheckResourceActiveAsync(c.ResourceId, ct) is { } e2) return Fail(e2);
        if (await CheckProjectStatusByNodeAsync(node, ct) is { } e3) return Fail(e3);
        if (c.Status == AllocationStatus.Hard && await CheckHardCommitmentAsync(node, ct) is { } e4)
            return Fail(e4);

        var pct = await ResolvePercentForHoursAsync(c.ResourceId, c.PeriodStart, c.PeriodEnd, c.TargetHours, ct);
        if (pct.IsFailure) return Fail(pct.Error!);

        return await CreateCoverageAsync(
            c, c.DemandId, node, c.ResourceId, c.PeriodStart, c.PeriodEnd, pct.Value, c.Status, c.Notes, "createByHours", ct);
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
                c.PeriodStart, c.PeriodEnd, c.Percent, c.Status, c.Notes, "coverInferred", ct);
        }

        // Fallback: materialize an Inferred, best-effort demand and cover it.
        Demand demand;
        Allocation a;
        try
        {
            demand = Demand.Create(
                c.ProjectNodeId, c.RoleId, requiredHours: null, DemandProvenance.Inferred, c.OwnerResourceId);
            a = Allocation.CreateCoverage(
                demand.Id, c.ProjectNodeId, c.ResourceId, c.PeriodStart, c.PeriodEnd, c.Percent, c.Notes, c.Status);
        }
        catch (DomainException ex) { return Fail(ServiceError.Conflict(ex.Message)); }

        return await FinalizeAsync(c, "coverInferred",
            [ToChange(a, PlanChangeKind.Created)], toAdd: [a], toRemove: null, ct,
            demandChanges: [ToDemandChange(demand, PlanChangeKind.Created)], demandsToAdd: [demand]);
    }

    private async Task<ServiceResult<PlanCommandResult>> CreateCoverageAsync(
        PlanCommand cmd, Guid demandId, Guid projectNodeId, Guid resourceId, DateOnly start, DateOnly end,
        decimal percent, AllocationStatus status, string? notes, string kind, CancellationToken ct)
    {
        Allocation a;
        try { a = Allocation.CreateCoverage(demandId, projectNodeId, resourceId, start, end, percent, notes, status); }
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
        // mutations persist. Placeholders (ResourceId null) are excluded by the
        // ResourceId filter — per-resource lane = assigned offer (ADR-0016 §5).
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
                c.ProjectNodeId, c.RoleId, c.RequiredHours, DemandProvenance.Declared, c.OwnerResourceId, c.Notes);
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
        IReadOnlyList<Demand>? demandsToRemove = null)
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
            DemandChanges = demandChanges ?? []
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
        Notes = d.Notes
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
        Notes = a.Notes
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

        var rootIdStr = path.TrimStart('/').Split('/').FirstOrDefault();
        if (!Guid.TryParse(rootIdStr, out var rootId))
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

        var rootIdStr = path.TrimStart('/').Split('/').FirstOrDefault();
        if (!Guid.TryParse(rootIdStr, out var rootId))
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
    // target existing catalogue rows. Used by createDemand/editDemand (Phase 5.0);
    // supersedes CheckPlaceholderRefsAsync once the placeholder is retired (5.1).
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
