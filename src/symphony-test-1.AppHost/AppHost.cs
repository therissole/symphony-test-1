var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImageTag("18-alpine")
    .WithDataVolume();

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
    .WaitForCompletion(migrations)
    .WithHttpHealthCheck("/api/health");

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
