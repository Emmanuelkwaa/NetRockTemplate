var builder = DistributedApplication.CreateBuilder(args);

var frontendPort = int.TryParse(builder.Configuration["Ports:Frontend"], out var fp) ? fp : 13000;
// @init:if backend=yes
var apiPort = int.TryParse(builder.Configuration["Ports:Api"], out var ap) ? ap : 13002;

// Derive infrastructure ports from the base (frontend) port so the entire
// stack lives in one predictable range - no collisions between projects.
// @init:if database=managed
var pgAdminPort = frontendPort + 3;
var postgresPort = frontendPort + 4;
// @init:end
var minioPort = frontendPort + 5;
var minioConsolePort = frontendPort + 6;
var mailpitSmtpPort = frontendPort + 7;
var mailpitHttpPort = frontendPort + 8;
// @init:end

// @init:if deploy=aspire uncomment
// // `aspire publish` writes docker-compose.yaml and .env for this environment.
// // See docs/deployment.md.
// builder.AddDockerComposeEnvironment("compose");
//
// @init:end
// @init:if backend=yes
// ── Infrastructure ──────────────────────────────────────────────────────────
// Container resources use session lifetime (default) - containers stop on
// Ctrl+C and restart on next run. Named data volumes persist across restarts,
// so database and file data survive. Explicit passwords ensure new containers
// can mount existing volumes without credential mismatch.

// @init:if database=managed
var pgPassword = builder.AddParameter("postgres-password", secret: true);
// @init:end
// @init:if auth=jwt
var jwtSecret = builder.AddParameter("jwt-secret", secret: true);
// @init:end
// @init:if auth=keycloak uncomment
// var keycloakClientSecret = builder.AddParameter("keycloak-client-secret", secret: true);
// @init:end

// @init:if database=managed
var postgres = builder.AddPostgres("db", password: pgPassword)
    .WithEndpoint("tcp", e => e.Port = postgresPort)
    .WithDataVolume("net-rock-template-db-data");

// pgAdmin only for local development
if (builder.ExecutionContext.IsRunMode)
{
    postgres.WithPgAdmin(pgAdmin => pgAdmin.WithEndpoint("http", e => e.Port = pgAdminPort));
}

var db = postgres.AddDatabase("Database");
// @init:end
// @init:if database=external uncomment
// // External PostgreSQL: the connection string comes from AppHost user secrets in development
// // (ConnectionStrings:Database) and from the environment in production (ConnectionStrings__Database).
// var db = builder.AddConnectionString("Database");
// @init:end

// ── API ─────────────────────────────────────────────────────────────────────
// Migrations run on startup in development. Elsewhere they run when
// Database:MigrateOnStartup is true. See: ApplicationBuilderExtensions.InitializeDatabaseAsync

var api = builder.AddProject<Projects.NetRockTemplate_WebApi>("api")
    .WithEndpoint("http", e =>
    {
        e.Port = apiPort;
        e.IsProxied = false;
    })
    .WithReference(db);
// @init:if database=managed
api.WaitFor(db);
// @init:end
// @init:if auth=jwt
api.WithEnvironment("Authentication__Jwt__Key", jwtSecret);
// @init:end
// @init:if auth=keycloak uncomment
// api.WithEnvironment("Authentication__Keycloak__ClientSecret", keycloakClientSecret);
// @init:end

// @init:end
// @init:if frontend=blazor uncomment
// // ── Frontend (Blazor SSR) ───────────────────────────────────────────────────
// // The "http" launch profile keeps the stock HTTPS redirection from sending browsers to the
// // template's random https port in development.
// var frontend = builder.AddProject<Projects.NetRockTemplate_UI>("frontend", launchProfileName: "http")
//     .WithEndpoint("http", e =>
//     {
//         e.Port = frontendPort;
//         e.IsProxied = false;
//     });
// @init:if backend=yes
// frontend.WithReference(api).WaitFor(api);
// @init:end
//
// @init:end
// @init:if backend=yes
// Local development services - production uses real SMTP and S3-compatible storage
// (configured via environment variables).
if (builder.ExecutionContext.IsRunMode)
{
    var storageUser = builder.AddParameter("storage-user");
    var storagePassword = builder.AddParameter("storage-password", secret: true);
    var storage = builder.AddMinioContainer("storage", rootUser: storageUser, rootPassword: storagePassword)
        .WithEndpoint("http", e => e.Port = minioPort)
        .WithEndpoint("console", e => e.Port = minioConsolePort)
        .WithDataVolume("net-rock-template-storage-data");

    var mailpit = builder.AddMailPit("mailpit", httpPort: mailpitHttpPort, smtpPort: mailpitSmtpPort);

    api.WaitFor(storage)
        .WaitFor(mailpit)
        .WithEnvironment("FileStorage__Endpoint", storage.GetEndpoint("http"))
        .WithEnvironment("FileStorage__AccessKey", storage.Resource.RootUser)
        .WithEnvironment("FileStorage__SecretKey", storage.Resource.PasswordParameter)
        .WithEnvironment("FileStorage__BucketName", "net-rock-template-files")
        .WithEnvironment("FileStorage__UseSSL", "false")
        .WithEnvironment("Email__Smtp__Host", mailpit.Resource.Host)
        .WithEnvironment("Email__Smtp__Port", () => mailpitSmtpPort.ToString())
        .WithEnvironment("Email__Smtp__UseSsl", "false");
// @init:if auth=jwt

    // Superuser for local development, read from AppHost user secrets (written by init).
    // Set it with: dotnet user-secrets set Parameters:seed-user-email <email> (and seed-user-password)
    if (!string.IsNullOrEmpty(builder.Configuration["Parameters:seed-user-email"]))
    {
        api.WithEnvironment("Seed__Users__0__Email", builder.AddParameter("seed-user-email"))
            .WithEnvironment("Seed__Users__0__Password", builder.AddParameter("seed-user-password", secret: true))
            .WithEnvironment("Seed__Users__0__Role", "Superuser");
    }
// @init:end
// @init:if frontend=svelte uncomment
//
//     // ── Frontend (SvelteKit) ────────────────────────────────────────────────
//     builder.AddViteApp("frontend", "../../../src/frontend")
//         .WithPnpm()
//         .WithEndpoint("http", e =>
//         {
//             e.Port = frontendPort;
//             e.IsProxied = false;
//         })
//         .WithEnvironment("API_URL", "http://127.0.0.1:" + apiPort)
//         .WaitFor(api);
// @init:end
}
// @init:end
// @init:if deploy=aspire uncomment
// @init:if backend=yes
// else
// @init:else
// if (builder.ExecutionContext.IsPublishMode)
// @init:end
// {
// @init:if backend=yes
//     // Production values come from the .env file next to the published docker-compose.yaml.
//     var publicUrl = builder.AddParameter("public-url");
//
//     api.WithExternalHttpEndpoints()
//         .WithEnvironment("FileStorage__Endpoint", builder.AddParameter("storage-endpoint"))
//         .WithEnvironment("FileStorage__AccessKey", builder.AddParameter("storage-access-key"))
//         .WithEnvironment("FileStorage__SecretKey", builder.AddParameter("storage-secret-key", secret: true))
//         .WithEnvironment("FileStorage__BucketName", builder.AddParameter("storage-bucket"))
//         .WithEnvironment("FileStorage__UseSSL", builder.AddParameter("storage-use-ssl"))
//         .WithEnvironment("Email__FrontendBaseUrl", publicUrl)
//         .WithEnvironment("Cors__AllowedOrigins__0", publicUrl);
// @init:end
// @init:if database=managed
//     api.WithEnvironment("Database__MigrateOnStartup", "true");
// @init:end
// @init:if auth=keycloak frontend=blazor
//
//     // Keycloak redirects back to the API, which is public next to the Blazor frontend.
//     api.WithEnvironment("Authentication__Keycloak__CallbackBaseUrl", builder.AddParameter("api-public-url"));
// @init:end
// @init:if frontend=svelte
//
//     // Production image from src/frontend/Dockerfile (adapter-node on port 3000).
//     builder.AddDockerfile("frontend", "../../../src/frontend")
//         .WithHttpEndpoint(targetPort: 3000, env: "PORT")
//         .WithExternalHttpEndpoints()
//         .WithEnvironment("API_URL", api.GetEndpoint("http"))
//         .WithEnvironment("ORIGIN", publicUrl)
//         .WaitFor(api);
// @init:end
// @init:if frontend=blazor
// @init:if backend=yes
//
// @init:end
//     frontend.WithExternalHttpEndpoints();
// @init:end
// }
// @init:end

builder.Build().Run();
