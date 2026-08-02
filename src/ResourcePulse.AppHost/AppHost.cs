var builder = DistributedApplication.CreateBuilder(args);

// ── Secrets as parameters (never literals) ───────────────────────────────────
// Each carries a GENERATED default, persisted to user-secrets on first run. A
// bare AddParameter(secret: true) has no default, so Aspire prompts for a value
// — and a hand-typed one silently violates the constraints below.

// Zitadel requires the masterkey to be EXACTLY 32 bytes and fails setup
// otherwise ("masterkey must be 32 bytes, but is N"). MinLength drives the
// generated length, so 32 in means 32 out.
var zitadelMasterKey = builder.AddParameter(
    "zitadel-masterkey",
    new GenerateParameterDefault
    {
        MinLength = 32,
        Lower = true,
        Upper = true,
        Numeric = true,
        Special = true,
        MinLower = 1,
        MinUpper = 1,
        MinNumeric = 1,
        MinSpecial = 1
    },
    secret: true,
    persist: true);

var zitadelAdminPassword = builder.AddParameter(
    "zitadel-admin-password",
    new GenerateParameterDefault
    {
        MinLength = 16,
        Lower = true,
        Upper = true,
        Numeric = true,
        Special = true,
        MinLower = 1,
        MinUpper = 1,
        MinNumeric = 1,
        MinSpecial = 1
    },
    secret: true,
    persist: true);

// Deliberately alphanumeric: this value is interpolated into a SQL string
// literal by the container init script (CREATE ROLE ... PASSWORD '...'), where
// a generated quote character would break the statement.
var appDbPassword = builder.AddParameter(
    "app-db-password",
    new GenerateParameterDefault
    {
        MinLength = 24,
        Lower = true,
        Upper = true,
        Numeric = true,
        Special = false,
        MinLower = 1,
        MinUpper = 1,
        MinNumeric = 1
    },
    secret: true,
    persist: true);

// The role the API connects as. It is NOT the Postgres superuser: superusers
// bypass row-level security unconditionally, which would make every tenant
// policy inert (ADR-0029).
const string appDbRole = "resourcepulse_app";

var postgres = builder.AddPostgres("resourcepulse-db")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent)
    // Runs once at container first-init; creates the non-superuser app role.
    .WithInitFiles("postgres-init")
    .WithEnvironment("APP_DB_ROLE", appDbRole)
    .WithEnvironment("APP_DB_PASSWORD", appDbPassword);

// ── Dev mail sink ────────────────────────────────────────────────────────────
// Zitadel sends real mail on registration (the InitCode / verification message).
// With no SMTP configured it fails with Errors.SMTPConfig.NotFound and the user
// is left uninitialised. In development that mail must land somewhere
// inspectable and must NEVER reach the internet — a real relay would email
// actual addresses typed into a dev login form.
//
// Ports are pinned so the SMTP address Zitadel is configured with stays stable:
// SMTP 1025, web inbox on http://localhost:8025.
var mailpit = builder.AddMailPit("mailpit", httpPort: 8025, smtpPort: 1025)
    .WithLifetime(ContainerLifetime.Persistent)
    // Bind the container's ports straight to the host instead of going through
    // Aspire's proxy: the proxied endpoints never became reachable here, which
    // left the inbox readable only from inside the container network.
    .WithEndpoint("http", e => e.IsProxied = false)
    .WithEndpoint("smtp", e => e.IsProxied = false);

// ── Identity provider ────────────────────────────────────────────────────────
// Zitadel gets its OWN logical database on the shared server. It owns its schema,
// its migrations and its projection tables; sharing a database with the
// EF-managed domain schema would risk name collisions and would make a routine
// `ef database drop` destroy identity state as collateral damage.
// The certificate APIs are still evaluation-only; the diagnostic is attributed
// to the start of the whole fluent statement, so the suppression has to wrap it.
#pragma warning disable ASPIRECERTIFICATES001
var zitadel = builder.AddZitadel(
        "zitadel",
        port: 8080,
        password: zitadelAdminPassword,
        masterKey: zitadelMasterKey)
    .WithDatabase(postgres, "zitadel-db")
    // Pinned host AND port so the issuer is one stable string, identical as seen
    // from the browser, from the API process and from the bootstrap.
    //
    // Deliberately plain `localhost`, NOT the integration's default
    // `{name}.dev.localhost`: the `.localhost` TLD is special-cased by browsers
    // (and by curl), but the Windows OS resolver does not resolve its
    // subdomains, so .NET's HttpClient fails with "unknown host". An issuer only
    // the browser can reach is not an issuer. The multi-instance isolation that
    // default buys is irrelevant here — there is exactly one instance.
    .WithExternalDomain("localhost")
    // Plain HTTP in dev, and this takes BOTH calls.
    //
    // The integration registers an HTTPS certificate configuration, so Aspire's
    // developer-certificate service flips the endpoint to HTTPS and injects
    // ZITADEL_TLS_* / ZITADEL_EXTERNALSECURE=true at start time — overriding a
    // plain environment variable set here, because the certificate callback runs
    // later. Opting the resource out of the developer certificate is what
    // actually keeps the scheme http; the env var alone is not enough.
    //
    // It matters because the issuer must be ONE stable string, identical from the
    // browser and from the API. A silent flip to https invalidates every token
    // already minted against the http issuer.
    .WithoutHttpsCertificate()
    // Both are required, and for different reasons:
    //   EXTERNALSECURE=false  -> the issuer Zitadel advertises is http://
    //   TLS_ENABLED=false     -> Zitadel does not demand a cert/key pair
    // Opting out of the developer certificate removes the callback that used to
    // set ZITADEL_TLS_*, and Zitadel's own default for TLS is ENABLED — so
    // without this second line it starts, provisions the first instance, then
    // dies with "TLS is enabled: please specify a key (path) and a cert (path)".
    .WithEnvironment("ZITADEL_EXTERNALSECURE", "false")
    .WithEnvironment("ZITADEL_TLS_ENABLED", "false")
    // First-instance provisioning: a machine user whose personal access token is
    // written to a mounted file, which is what the bootstrap step authenticates
    // with. This is the only way to get a credential out of a fresh Zitadel
    // without a human clicking through the console.
    // The very first admin is created with its address already verified, so
    // provisioning never blocks on a mail nobody can read.
    // The human administrator, spelled out so the console login is predictable
    // instead of being derived from defaults. The address is pre-verified and the
    // password is not flagged for change, so the very first console login works
    // without any mail round-trip.
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_EMAIL_ADDRESS", "admin@resourcepulse.local")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_EMAIL_VERIFIED", "true")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_FIRSTNAME", "Resource Pulse")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_HUMAN_LASTNAME", "Admin")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_NAME", "resource-pulse-dev")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_MACHINE_MACHINE_USERNAME", "bootstrap")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_MACHINE_MACHINE_NAME", "Bootstrap Service User")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_ORG_MACHINE_PAT_EXPIRATIONDATE", "2030-01-01T00:00:00Z")
    .WithEnvironment("ZITADEL_FIRSTINSTANCE_PATPATH", "/machinekey/pat.txt")
    .WithBindMount("zitadel-machinekey", "/machinekey");
#pragma warning restore ASPIRECERTIFICATES001

// ── Deterministic, idempotent dev bootstrap ──────────────────────────────────
// Creates the project + SPA/API applications in Zitadel, registers the default
// tenant in OUR control plane, and writes the resulting issuer/client/project ids
// where the API and the SPA read them. Re-running it must never duplicate.
var bootstrap = builder.AddProject<Projects.ResourcePulse_DevBootstrap>("zitadel-bootstrap")
    .WithReference(postgres)
    // ZitadelResource carries no connection string; the issuer is passed
    // explicitly below and the wait is what actually matters here.
    .WaitFor(zitadel)
    .WaitFor(postgres)
    // Deliberately NOT WaitFor(mailpit): the bootstrap never opens an SMTP
    // connection, it only hands the address to Zitadel's admin API. Waiting on it
    // made the entire environment hostage to Mailpit's health probe — when that
    // did not go healthy, the bootstrap never ran and, through
    // WaitForCompletion, neither the API nor the frontend ever started.
    // A gate must express a real dependency, not a vague ordering preference.
    // The address as seen FROM THE ZITADEL CONTAINER: Aspire gives each container
    // a network alias equal to its resource name, so this is the in-network
    // name and the TARGET port (1025) — not the host-published one. The
    // bootstrap only relays this string to Zitadel's admin API; it never
    // connects to it itself.
    .WithEnvironment("Bootstrap__SmtpHost", "mailpit:1025")
    .WithEnvironment("Bootstrap__SmtpSenderAddress", "noreply@resourcepulse.local")
    .WithEnvironment("Bootstrap__SmtpSenderName", "Resource Pulse (dev)")
    .WithEnvironment("Zitadel__MachineKeyPath",
        Path.Combine(builder.AppHostDirectory, "zitadel-machinekey", "pat.txt"))
    .WithEnvironment("Zitadel__Issuer", "http://localhost:8080")
    // Where the generated identity configuration lands: a gitignored JSON the
    // API reads as an optional config source, plus the SPA's .env.local.
    .WithEnvironment("Bootstrap__ApiConfigPath",
        Path.Combine(builder.AppHostDirectory, "..", "ResourcePulse.Hosting", "zitadel-dev.json"))
    .WithEnvironment("Bootstrap__SpaEnvPath",
        Path.Combine(builder.AppHostDirectory, "..", "frontend", ".env.local"))
    .WithEnvironment("Bootstrap__SpaRedirectUri", "http://localhost:5173/auth/callback")
    .WithEnvironment("Bootstrap__SpaSilentRenewUri", "http://localhost:5173/auth/silent-renew")
    .WithEnvironment("Bootstrap__SpaPostLogoutUri", "http://localhost:5173/");

var api = builder.AddProject<Projects.ResourcePulse_Hosting>("api")
    .WithReference(postgres)
    .WaitFor(postgres)
    // The API must not start before the identity configuration exists on disk.
    .WaitForCompletion(bootstrap)
    .WithEnvironment("Tenancy__AppDbRole", appDbRole)
    .WithEnvironment("Tenancy__AppDbPassword", appDbPassword);

builder.AddNpmApp("frontend", "../frontend", "dev")
    .WaitFor(api)
    .WaitForCompletion(bootstrap)
    // Vite dev server binds to PORT directly; the browser reaches it on the same
    // port (Vite's own /api proxy forwards to the API). For non-container
    // resources Aspire rejects port == targetPort when proxied, so opt out of
    // the proxy and let the npm process own the endpoint.
    .WithHttpEndpoint(port: 5173, targetPort: 5173, env: "PORT", isProxied: false)
    .WithExternalHttpEndpoints();

builder.Build().Run();
