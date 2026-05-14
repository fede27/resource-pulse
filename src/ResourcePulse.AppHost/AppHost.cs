var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("resourcepulse-db")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

builder.AddProject<Projects.ResourcePulse_Hosting>("api")
    .WithReference(postgres)
    .WaitFor(postgres);

builder.Build().Run();
