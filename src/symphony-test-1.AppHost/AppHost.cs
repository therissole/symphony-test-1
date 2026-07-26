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
    .AddProject<Projects.symphony_test_1_Api>("api")
    .WithReference(database)
    .WithReference(keycloak)
    .WithEnvironment(
        "Authentication__Authority",
        $"{keycloak.GetEndpoint("http")}/realms/symphony")
    .WithEnvironment("Authentication__RequireHttpsMetadata", "false")
    .WaitForCompletion(migrations)
    .WaitFor(keycloak)
    .WithHttpHealthCheck("/api/health");

api.WithUrlForEndpoint("https", url => url.DisplayText = "Symphony administration")
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
