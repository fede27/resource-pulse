using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResourcePulse.Common.Tenancy;

namespace ResourcePulse.Persistence.Tenancy;

/// <summary>
/// Tenant isolation at the EF layer (ADR-0029).
/// <para>
/// <c>TenantId</c> is a <b>shadow property</b>: it exists in the EF model and in
/// every table, but nowhere in <c>ResourcePulse.Domain</c>. The planning
/// aggregates stay unaware that tenancy exists — tenancy is an infrastructure
/// concern, not a modelling one.
/// </para>
/// <para>
/// The global query filter here is a convenience and a second line of defence.
/// The <i>enforcement</i> is Postgres row-level security, which holds even for
/// raw SQL and for anything that calls <c>IgnoreQueryFilters()</c>.
/// </para>
/// </summary>
public static class TenantModel
{
    public const string TenantIdProperty = "TenantId";
    public const string TenantIdColumn = "tenant_id";

    /// <summary>
    /// Declares the tenant discriminator on an aggregate up front, so that
    /// per-tenant composite unique indexes can reference it by name from within
    /// the entity configuration.
    /// </summary>
    public static EntityTypeBuilder<T> HasTenantId<T>(this EntityTypeBuilder<T> builder)
        where T : class
    {
        builder.Property<Guid>(TenantIdProperty).IsRequired();
        return builder;
    }

    /// <summary>
    /// Back-fills the tenant discriminator onto every mapped entity type that
    /// does not already declare it — owned collections included, since RLS is
    /// applied per table and a child table without the column could not be
    /// protected — and installs the tenant query filter on every root type.
    /// </summary>
    /// <remarks>
    /// Takes the <b>context</b>, not the tenant accessor: the filter must be
    /// rooted at the DbContext or EF inlines the tenant id as a SQL literal and
    /// caches the compiled query with it — see
    /// <see cref="ResourcePulseDbContext.CurrentTenantId"/>.
    /// </remarks>
    public static void ApplyTenantIsolation(this ModelBuilder modelBuilder, ResourcePulseDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Apply(modelBuilder, context);
    }

    /// <summary>
    /// Schema only: the discriminator column and its index, with no query filter.
    /// Used at design time (migrations) and by provider-agnostic unit tests,
    /// where there is no ambient request to scope to.
    /// </summary>
    public static void ApplyTenantColumnsOnly(this ModelBuilder modelBuilder) =>
        Apply(modelBuilder, context: null);

    private static void Apply(ModelBuilder modelBuilder, ResourcePulseDbContext? context)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            // Owned types that share their owner's table (OwnsOne value objects
            // such as TimeFenceConfiguration's horizons) already carry the
            // owner's tenant column — adding another would duplicate it.
            if (entityType.IsOwned())
            {
                var ownership = entityType.FindOwnership();
                if (ownership is not null &&
                    ownership.PrincipalEntityType.GetTableName() == entityType.GetTableName())
                    continue;
            }

            if (entityType.FindProperty(TenantIdProperty) is null)
            {
                var property = entityType.AddProperty(TenantIdProperty, typeof(Guid));
                property.IsNullable = false;
            }

            // Every table gets an index on the discriminator: RLS predicates are
            // evaluated on every row touched, so an unindexed tenant_id turns
            // each query into a scan.
            if (entityType.FindIndex(entityType.FindProperty(TenantIdProperty)!) is null)
                entityType.AddIndex(entityType.FindProperty(TenantIdProperty)!);

            // Query filters belong to root types only; owned types inherit the
            // filter of the aggregate that owns them, and derived types inherit
            // from their base.
            if (context is not null && !entityType.IsOwned() && entityType.BaseType is null)
                entityType.SetQueryFilter(BuildTenantFilter(entityType, context));
        }
    }

    private static LambdaExpression BuildTenantFilter(IMutableEntityType entityType, ResourcePulseDbContext context)
    {
        var parameter = Expression.Parameter(entityType.ClrType, "e");

        var entityTenantId = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            [typeof(Guid)],
            parameter,
            Expression.Constant(TenantIdProperty));

        // Rooted at the DbContext, NOT at the tenant accessor. EF treats a
        // subtree that reaches the context as a query PARAMETER
        // (`__ef_filter__…`), re-evaluated on every execution. A subtree that
        // reaches anything else is "evaluatable": EF computes it once, writes the
        // result into the SQL as a literal, and caches that compiled query — so
        // every subsequent execution, for every other tenant, silently reuses the
        // first tenant's id.
        //
        // The model is built once and the context is pooled, which is exactly why
        // the value must come from a property that resolves the ambient tenant at
        // call time rather than from anything captured at model-building time.
        var currentTenantId = Expression.Property(
            Expression.Constant(context, typeof(ResourcePulseDbContext)),
            nameof(ResourcePulseDbContext.CurrentTenantId));

        return Expression.Lambda(Expression.Equal(entityTenantId, currentTenantId), parameter);
    }
}
