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

builder
    .AddProject<Projects.symphony_test_1_Api>("api")
    .WithReference(database)
    .WaitForCompletion(migrations)
    .WithHttpHealthCheck("/api/health");

builder.Build().Run();
