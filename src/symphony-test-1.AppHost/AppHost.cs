var builder = DistributedApplication.CreateBuilder(args);

var keycloak = builder
    .AddKeycloak("keycloak")
    .WithRealmImport("./Realms")
    .WithDockerfile(".");

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
    .AddProject<Projects.symphony_test_1_Api>("api")
    .WithOtlpExporter()
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
    .AddBlazorWasmProject<Projects.symphony_test_1_Web>("web")
    .WithReference(api.GetEndpoint("https"));

var gateway = builder
    .AddProject<Projects.symphony_test_1_Gateway>("gateway")
    .WithHttpEndpoint(name: "http")
    .WithHttpsEndpoint(name: "https")
    .WaitFor(api)
    .WithOtlpExporter(OtlpProtocol.HttpProtobuf)
    .WithHttpHealthCheck("/health");

gateway.WithBlazorClientApp(web, proxyTelemetry: true);

gateway.WithUrlForEndpoint("https", url => url.DisplayText = "Symphony administration")
    .WithUrlForEndpoint("http", url => url.DisplayText = "Symphony administration");

builder
    .AddJavaScriptApp("docs", "../../docs")
    .WithRunScript("dev")
    .WithEnvironment("API_BASE_URL", api.GetEndpoint("https"))
    .WaitFor(api)
    .WithHttpEndpoint(name: "http", env: "PORT")
    .WithUrlForEndpoint("http", url =>
    {
        url.DisplayText = "Mintlify documentation";
    })
    .ExcludeFromManifest();

builder.Build().Run();
