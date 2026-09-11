using FluentValidation;
using Mapster;
using MapsterMapper;
using ResourcePulse.Services;
using ResourcePulse.Services.Access;
using ResourcePulse.Services.Allocations;
using ResourcePulse.Services.BusinessCalendars;
using ResourcePulse.Services.Capacity;
using ResourcePulse.Services.CompanyClosures;
using ResourcePulse.Services.Configuration;
using ResourcePulse.Services.Demands;
using ResourcePulse.Services.ExternalConstraints;
using ResourcePulse.Services.Identity;
using ResourcePulse.Services.Load;
using ResourcePulse.Services.Plan;
using ResourcePulse.Services.Projects;
using ResourcePulse.Services.Resources;
using ResourcePulse.Services.Roles;
using ResourcePulse.Services.Signals;
using ResourcePulse.Services.Skills;
using ResourcePulse.Services.Tags;
using ResourcePulse.Services.Teams;
using ResourcePulse.Services.Tenancy;

namespace ResourcePulse.Hosting.Setup;

/// <summary>
/// The application layer: mapping, validation, and one registration per service.
/// </summary>
/// <remarks>
/// A flat list on purpose. It is long because the domain is, and grouping it by
/// phase or aggregate would only invite the question of which group a new service
/// belongs to — a question with no useful answer. Adding a service here should
/// stay a one-line edit in an obvious place.
/// </remarks>
public static class ApplicationServicesSetup
{
    public static void AddResourcePulseApplicationServices(this WebApplicationBuilder builder)
    {
        // Mapster — scan Services assembly for IRegister implementations
        var mapsterConfig = TypeAdapterConfig.GlobalSettings;
        mapsterConfig.Scan(typeof(ServicesAssemblyMarker).Assembly);
        builder.Services.AddSingleton(mapsterConfig);
        builder.Services.AddScoped<IMapper, ServiceMapper>();

        // FluentValidation — scan Services assembly for validators
        builder.Services.AddValidatorsFromAssembly(typeof(ServicesAssemblyMarker).Assembly);

        builder.Services.AddScoped<IBusinessCalendarService, BusinessCalendarService>();
        builder.Services.AddScoped<ICompanyClosureService, CompanyClosureService>();
        builder.Services.AddScoped<IResourceService, ResourceService>();
        builder.Services.AddScoped<ICapacityQueryService, LiveCapacityQueryService>();
        builder.Services.AddScoped<ITeamService, TeamService>();
        builder.Services.AddScoped<IRoleService, RoleService>();
        builder.Services.AddScoped<ISkillService, SkillService>();
        builder.Services.AddScoped<ITagService, TagService>();
        builder.Services.AddScoped<IProjectNodeService, ProjectNodeService>();
        builder.Services.AddScoped<IExternalConstraintService, ExternalConstraintService>();
        builder.Services.AddScoped<IAllocationService, AllocationService>();
        builder.Services.AddScoped<IDemandService, DemandService>();
        builder.Services.AddScoped<IPlanCommandService, PlanCommandService>();
        builder.Services.AddScoped<ILoadQueryService, LiveLoadQueryService>();
        builder.Services.AddScoped<IMeService, MeService>();
        builder.Services.AddScoped<ITenantResolver, TenantResolver>();

        // Access control (ADR-0030): membership resolution + administration.
        builder.Services.AddScoped<IAccessResolver, AccessResolver>();
        builder.Services.AddScoped<IMembershipService, MembershipService>();
        if (builder.Environment.IsDevelopment())
            builder.Services.AddScoped<IDevAccessService, DevAccessService>();

        // Org-level configuration singletons (ADR-0020): boundaries & thresholds.
        builder.Services.AddScoped<ILoadBandConfigurationService, LoadBandConfigurationService>();
        builder.Services.AddScoped<ITimeFenceConfigurationService, TimeFenceConfigurationService>();
        builder.Services.AddScoped<IBucketingDefaultsService, BucketingDefaultsService>();
        builder.Services.AddScoped<ICommitmentPolicyService, CommitmentPolicyService>();
        builder.Services.AddScoped<ISignalPolicyService, SignalPolicyService>();

        // Triage (ADR-0032). The detector is registered here too — the API never runs it
        // on a schedule (that is the dedicated worker's job, so N replicas cannot mean N
        // concurrent sweeps on the same tenant), but the resolve-only hook on the plan
        // envelope resolves it per request.
        builder.Services.AddScoped<ISignalDetectionService, SignalDetectionService>();
        builder.Services.AddScoped<ISignalService, SignalService>();
        // Operational, not organizational (ADR-0032 §12): the cadence belongs to the
        // worker's settings. The API only echoes the tolerance so no client invents one.
        builder.Services.AddSingleton<ISweepCadence>(_ => new SweepCadence(
            builder.Configuration.GetValue("Signals:StaleAfterHours", SweepCadence.DefaultStaleAfterHours)));
    }
}
