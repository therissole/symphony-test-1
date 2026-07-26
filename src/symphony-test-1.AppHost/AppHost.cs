var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder
    .AddKeycloak("keycloak")
    .WithRealmImport("./Realms")
    .WithBindMount("./Themes", "/opt/keycloak/themes", isReadOnly: true);

var postgres = builder.AddPostgres("postgres")
    .WithImageTag("18-alpine");

var database = postgres.AddDatabase(
    name: "DefaultConnection",
    databaseName: "symphony_test_1");

var migrations = builder
    .AddProject<Projects.symphony_test_1_DatabaseMigrations>("database-migrations")
    .WithReference(database)
    .WaitFor(database);

var api = builder
    .AddExecutable(
        "api",
        "dotnet",
        "../symphony-test-1.Api",
        "watch",
        "--non-interactive",
        "--project",
        "symphony-test-1.Api.csproj",
        "--no-launch-profile")
    .WithHttpEndpoint(name: "http", env: "ASPNETCORE_HTTP_PORTS")
    .WithOtlpExporter()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("DOTNET_WATCH_RESTART_ON_RUDE_EDIT", "1")
    .WithReference(database)
    .WithReference(keycloak)
    .WithEnvironment(
        "Authentication__Authority",
        $"{keycloak.GetEndpoint("http")}/realms/symphony")
    .WithEnvironment("Authentication__RequireHttpsMetadata", "false")
    .WaitForCompletion(migrations)
    .WaitFor(keycloak)
    .WithHttpHealthCheck("/api/health")
    .ExcludeFromManifest();

api.WithUrlForEndpoint("http", url => url.DisplayText = "Symphony API");

var web = builder
    .AddExecutable(
        "web",
        "dotnet",
        "../symphony-test-1.Web",
        "watch",
        "--non-interactive",
        "--project",
        "symphony-test-1.Web.csproj",
        "--no-launch-profile")
    .WithHttpEndpoint(name: "http", env: "ASPNETCORE_HTTP_PORTS")
    .WithOtlpExporter()
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
    .WithEnvironment("DOTNET_WATCH_RESTART_ON_RUDE_EDIT", "1")
    .ExcludeFromManifest();

web.WithUrlForEndpoint("http", url => url.DisplayText = "Web development server");

var gateway = builder
    .AddProject<Projects.symphony_test_1_Gateway>("gateway")
    .WithHttpEndpoint(name: "http")
    .WithHttpsEndpoint(name: "https")
    .WithEnvironment("Gateway__ApiBaseUrl", api.GetEndpoint("http"))
    .WithEnvironment("Gateway__WebBaseUrl", web.GetEndpoint("http"))
    .WaitFor(api)
    .WaitFor(web)
    .WithHttpHealthCheck("/health");

gateway.WithUrlForEndpoint("https", url => url.DisplayText = "Symphony administration")
    .WithUrlForEndpoint("http", url => url.DisplayText = "Symphony administration");

builder
    .AddJavaScriptApp("docs", "../../docs")
    .WithRunScript("dev")
    .WithEnvironment("API_BASE_URL", api.GetEndpoint("http"))
    .WaitFor(api)
    .WithHttpEndpoint(name: "http", env: "PORT")
    .WithUrlForEndpoint("http", url =>
    {
        url.DisplayText = "Mintlify documentation";
    })
    .ExcludeFromManifest();

builder.Build().Run();
