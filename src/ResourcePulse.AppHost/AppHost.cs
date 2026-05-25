var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("resourcepulse-db")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var api = builder.AddProject<Projects.ResourcePulse_Hosting>("api")
    .WithReference(postgres)
    .WaitFor(postgres);

builder.AddNpmApp("frontend", "../frontend", "dev")
    .WaitFor(api)
    // Vite dev server binds to PORT directly; the browser reaches it on the same
    // port (Vite's own /api proxy forwards to the API). For non-container
    // resources Aspire rejects port == targetPort when proxied, so opt out of
    // the proxy and let the npm process own the endpoint.
    .WithHttpEndpoint(port: 5173, targetPort: 5173, env: "PORT", isProxied: false)
    .WithExternalHttpEndpoints();

builder.Build().Run();
