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

var openFgaDatabase = postgres.AddDatabase(
    name: "openfga-database",
    databaseName: "openfga");

var openFgaMigration = builder
    .AddContainer("openfga-migrate", "openfga/openfga", "v1.18.1")
    .WithArgs("migrate")
    .WithEnvironment("OPENFGA_DATASTORE_ENGINE", "postgres")
    .WithEnvironment(
        "OPENFGA_DATASTORE_URI",
        $"{openFgaDatabase.Resource.UriExpression}?sslmode=disable")
    .WaitFor(openFgaDatabase);

var openFga = builder
    .AddContainer("openfga", "openfga/openfga", "v1.18.1")
    .WithArgs("run")
    .WithEnvironment("OPENFGA_DATASTORE_ENGINE", "postgres")
    .WithEnvironment(
        "OPENFGA_DATASTORE_URI",
        $"{openFgaDatabase.Resource.UriExpression}?sslmode=disable")
    .WithEnvironment("OPENFGA_PLAYGROUND_ENABLED", "false")
    .WithEnvironment("OPENFGA_LIST_OBJECTS_DEADLINE", "5s")
    .WithHttpEndpoint(targetPort: 8080, name: "http")
    .WaitForCompletion(openFgaMigration)
    .WithHttpHealthCheck("/healthz");

var openFgaProvisioning = builder
    .AddProject<Projects.symphony_test_1_OpenFgaProvisioning>("openfga-provisioning")
    .WithReference(database)
    .WithEnvironment("OpenFga__ApiUrl", openFga.GetEndpoint("http"))
    .WithEnvironment("OpenFga__StoreName", "administration-authorization")
    .WithEnvironment("OpenFga__BootstrapSuperuserSubjects", "b612a21b-b2e7-4a97-a2b7-5c3d77d0342c,f5a0f69c-8cc7-4f68-9b45-6ff5b6f8b730")
    .WithEnvironment("OpenFga__BootstrapStandardUserSubjects", "907718f8-0d56-421e-8a45-aac8d9679075")
    .WaitFor(openFga);

var migrations = builder
    .AddProject<Projects.symphony_test_1_DatabaseMigrations>("database-migrations")
    .WithReference(database)
    .WaitFor(database);

openFgaProvisioning = openFgaProvisioning.WaitForCompletion(migrations);

var api = builder
    .AddProject<Projects.symphony_test_1_Api>("api")
    .WithOtlpExporter()
    .WithReference(database)
    .WithReference(keycloak)
    .WithEnvironment("OpenFga__ApiUrl", openFga.GetEndpoint("http"))
    .WithEnvironment("OpenFga__StoreName", "administration-authorization")
    .WithEnvironment(
        "Authentication__Authority",
        $"{keycloak.GetEndpoint("http")}/realms/symphony")
    .WithEnvironment("Authentication__RequireHttpsMetadata", "false")
    .WaitForCompletion(migrations)
    .WaitForCompletion(openFgaProvisioning)
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
