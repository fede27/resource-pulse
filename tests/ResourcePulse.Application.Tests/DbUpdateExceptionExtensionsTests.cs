using Microsoft.EntityFrameworkCore;
using ResourcePulse.Persistence;

namespace ResourcePulse.Application.Tests;

// One classification of the database errors services turn into a ServiceResult,
// where there used to be eight copies of the unique check and three DIFFERENT
// foreign-key checks. Pinned against the wording Postgres actually produces —
// message inspection is the documented convention, so the messages are the
// contract until ADR-0008's SqlState follow-up lands.
public class DbUpdateExceptionExtensionsTests
{
    private static DbUpdateException Wrapping(string message) =>
        new("An error occurred while saving the entity changes.", new InvalidOperationException(message));

    [Fact]
    public void AUniqueIndexViolationIsRecognised()
    {
        Wrapping("23505: duplicate key value violates unique constraint \"ix_teams_name\"")
            .IsUniqueViolation().Should().BeTrue();
    }

    [Fact]
    public void AForeignKeyViolationOnInsertIsRecognised()
    {
        Wrapping("23503: insert or update on table \"allocations\" violates foreign key " +
                 "constraint \"fk_allocations_demands_demand_id\"")
            .IsForeignKeyViolation().Should().BeTrue();
    }

    [Fact]
    public void AForeignKeyViolationOnDeleteIsRecognised()
    {
        // The shape the project-node delete hits: the row being removed is still
        // referenced. Different wording from the insert case, same classification.
        Wrapping("23503: update or delete on table \"project_nodes\" violates foreign key " +
                 "constraint \"fk_demands_project_nodes_project_node_id\" on table \"demands\"")
            .IsForeignKeyViolation().Should().BeTrue();
    }

    [Fact]
    public void TheTwoClassificationsDoNotOverlap()
    {
        var unique = Wrapping("23505: duplicate key value violates unique constraint \"ix_roles_name\"");
        var foreignKey = Wrapping("23503: insert or update on table \"demands\" violates foreign key " +
                                  "constraint \"fk_demands_roles_role_id\"");

        unique.IsForeignKeyViolation().Should().BeFalse();
        foreignKey.IsUniqueViolation().Should().BeFalse();
    }

    [Fact]
    public void ACheckConstraintIsNeither()
    {
        // It must fall through to the exception handler: a violated CHECK is a bug
        // in the service that let the value past, not a conflict to report as 409.
        var ex = Wrapping("23514: new row for relation \"demands\" violates check constraint " +
                          "\"ck_demands_required_hours_positive\"");

        ex.IsUniqueViolation().Should().BeFalse();
        ex.IsForeignKeyViolation().Should().BeFalse();
    }

    [Fact]
    public void AnExceptionWithNoInnerCauseIsNeither()
    {
        var ex = new DbUpdateException("no inner exception");

        ex.IsUniqueViolation().Should().BeFalse();
        ex.IsForeignKeyViolation().Should().BeFalse();
    }
}
