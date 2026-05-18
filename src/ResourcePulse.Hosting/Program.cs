using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ResourcePulse.Common.Auth;
using ResourcePulse.Domain;
using ResourcePulse.Hosting;
using ResourcePulse.Hosting.Auth;
using ResourcePulse.Http;
using ResourcePulse.Persistence;
using ResourcePulse.Services;
using ResourcePulse.Services.Allocations;
using ResourcePulse.Services.BusinessCalendars;
using ResourcePulse.Services.Capacity;
using ResourcePulse.Services.CompanyClosures;
using ResourcePulse.Services.Load;
using ResourcePulse.Services.Projects;
using ResourcePulse.Services.Resources;
using ResourcePulse.Services.Skills;
using ResourcePulse.Services.Tags;
using ResourcePulse.Services.Teams;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration));

builder.AddServiceDefaults();

// ProblemDetails + global exception handler
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Auth — FakeAuth for Development only; fail fast if misconfigured in Production
if (builder.Environment.IsProduction() && builder.Configuration.GetSection("FakeAuth").Exists())
    throw new InvalidOperationException("FakeAuth must not be configured in the Production environment.");

if (builder.Environment.IsDevelopment())
{
    builder.Services
        .AddAuthentication(FakeAuthenticationDefaults.SchemeName)
        .AddScheme<FakeAuthenticationOptions, FakeAuthenticationHandler>(
            FakeAuthenticationDefaults.SchemeName,
            opts => builder.Configuration.GetSection("FakeAuth").Bind(opts));
}

builder.Services.AddAuthorization(opts =>
    opts.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.AddHttpContextAccessor();
// Singleton required: Aspire's AddNpgsqlDbContext uses AddDbContextPool.
// Pooled DbContexts are resolved from the root provider, so constructor-injected
// dependencies must be singletons. HttpContextCurrentUserAccessor reads
// IHttpContextAccessor.HttpContext at call time, so singleton lifetime is safe.
builder.Services.AddSingleton<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();

// Mapster — scan Services assembly for IRegister implementations
var mapsterConfig = TypeAdapterConfig.GlobalSettings;
mapsterConfig.Scan(typeof(ServicesAssemblyMarker).Assembly);
builder.Services.AddSingleton(mapsterConfig);
builder.Services.AddScoped<IMapper, ServiceMapper>();

// FluentValidation — scan Services assembly for validators
builder.Services.AddValidatorsFromAssembly(typeof(ServicesAssemblyMarker).Assembly);

// Persistence
// AuditInterceptor is singleton (safe — reads IHttpContextAccessor.HttpContext at call time).
// Wired into the DbContextPool's shared options via IDbContextOptionsConfiguration<T>,
// which is the EF Core-supported way to add interceptors when pooling is active.
builder.Services.AddSingleton<AuditInterceptor>();
builder.Services.AddSingleton<IDbContextOptionsConfiguration<ResourcePulseDbContext>, ResourcePulseDbContextOptionsConfiguration>();
builder.Services.AddScoped(typeof(IRepository<,>), typeof(Repository<,>));

builder.AddNpgsqlDbContext<ResourcePulseDbContext>("resourcepulse-db");

// Services
builder.Services.AddScoped<IBusinessCalendarService, BusinessCalendarService>();
builder.Services.AddScoped<ICompanyClosureService, CompanyClosureService>();
builder.Services.AddScoped<IResourceService, ResourceService>();
builder.Services.AddScoped<ICapacityQueryService, LiveCapacityQueryService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<ISkillService, SkillService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IProjectNodeService, ProjectNodeService>();
builder.Services.AddScoped<IAllocationService, AllocationService>();
builder.Services.AddScoped<ILoadQueryService, LiveLoadQueryService>();

// MVC + global validation filter
builder.Services.AddControllers(opts => opts.Filters.Add<DtoValidationFilter>());

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

// Apply pending migrations on startup in Development
// (Production migrations are applied out-of-band via CI/CD)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ResourcePulseDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serilog request logging enriched with current user Sub
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        var userAccessor = httpContext.RequestServices.GetService<ICurrentUserAccessor>();
        if (userAccessor?.IsAuthenticated == true)
            diagnosticContext.Set("UserSub", userAccessor.User.Sub);
    };
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
