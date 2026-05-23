var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("resourcepulse-db")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var api = builder.AddProject<Projects.ResourcePulse_Hosting>("api")
    .WithReference(postgres)
    .WaitFor(postgres);

builder.AddNpmApp("frontend", "../frontend", "dev")
    .WaitFor(api)
    .WithHttpEndpoint(port: 5173, targetPort: 5173, env: "PORT")
    .WithExternalHttpEndpoints();

builder.Build().Run();
