namespace ResourcePulse.Domain.Tests.Builders;

// Tiny construction helpers for ProjectNode trees in tests.
// Encourages readable test setup without hiding the production factory signatures.
public static class ProjectNodes
{
    public static ProjectNode Project(
        string name = "Test Project",
        string? code = null,
        ProjectType type = ProjectType.Internal,
        CommitmentLevel commitment = CommitmentLevel.Planned,
        Guid? leadResourceId = null) =>
        ProjectNode.CreateRoot(name, code, type, commitment, leadResourceId);

    public static ProjectNode Phase(ProjectNode parent, string name = "Test Phase", string? code = null) =>
        ProjectNode.CreateChild(parent, ProjectNodeType.Phase, name, code);

    public static ProjectNode WorkPackage(ProjectNode parent, string name = "Test WP", string? code = null) =>
        ProjectNode.CreateChild(parent, ProjectNodeType.WorkPackage, name, code);
}
