using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SymphonyTest1.Api.Features.Greetings;
using SymphonyTest1.Api.Features.Languages;
using SymphonyTest1.IntegrationTests.Infrastructure;

namespace SymphonyTest1.IntegrationTests.Features.Languages;

[TestFixture]
public class LanguageSliceTests
{
    private IntegrationTestWebAppFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new IntegrationTestWebAppFactory();
        await _factory.StartAsync();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _client?.Dispose();

        if (_factory is not null)
        {
            await _factory.StopAsync();
            await _factory.DisposeAsync();
        }
    }

    [Test]
    public async Task ListLanguages_ReturnsSeededLanguages()
    {
        var response = await _client.GetAsync("/api/languages");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var languages = await response.Content.ReadFromJsonAsync<List<ListLanguages.Response>>();
        Assert.That(languages, Is.Not.Null);
        Assert.That(languages, Has.Some.Property(nameof(ListLanguages.Response.Code)).EqualTo("en"));
    }

    [Test]
    public async Task GetLanguage_WhenLanguageDoesNotExist_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/languages/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task CreateLanguage_WithValidRequest_ReturnsCreatedLanguage()
    {
        var request = new CreateLanguage.Request("French", "fr");

        var response = await _client.PostAsJsonAsync("/api/languages", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That(response.Headers.Location?.ToString(), Does.StartWith("/api/languages/"));

        var language = await response.Content.ReadFromJsonAsync<CreateLanguage.Response>();
        Assert.Multiple(() =>
        {
            Assert.That(language, Is.Not.Null);
            Assert.That(language!.Name, Is.EqualTo("French"));
            Assert.That(language.Code, Is.EqualTo("fr"));
        });
    }

    [Test]
    public async Task CreateLanguage_WithInvalidRequest_ReturnsValidationProblem()
    {
        var request = new CreateLanguage.Request("", "");

        var response = await _client.PostAsJsonAsync("/api/languages", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Multiple(() =>
        {
            Assert.That(problem, Is.Not.Null);
            Assert.That(problem!.Errors, Does.ContainKey("name"));
            Assert.That(problem.Errors, Does.ContainKey("code"));
        });
    }

    [Test]
    public async Task CreateLanguage_WithDuplicateCode_ReturnsConflict()
    {
        var request = new CreateLanguage.Request("Another English", "en");

        var response = await _client.PostAsJsonAsync("/api/languages", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem?.Title, Is.EqualTo("Language already exists"));
    }

    [Test]
    public async Task GetAndUpdateLanguage_WithExistingId_ReturnsUpdatedLanguage()
    {
        var created = await CreateLanguageAsync("Italian", "it");

        var getResponse = await _client.GetAsync($"/api/languages/{created.Id}");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var retrieved = await getResponse.Content.ReadFromJsonAsync<GetLanguage.Response>();
        Assert.That(retrieved?.Id, Is.EqualTo(created.Id));

        var updateRequest = new UpdateLanguage.Request("Italiano", "it");
        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/languages/{created.Id}",
            updateRequest);

        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var updated = await updateResponse.Content.ReadFromJsonAsync<UpdateLanguage.Response>();
        Assert.That(updated?.Name, Is.EqualTo("Italiano"));
    }

    [Test]
    public async Task UpdateLanguage_WhenLanguageDoesNotExist_ReturnsNotFound()
    {
        var request = new UpdateLanguage.Request("Missing", "xx");

        var response = await _client.PutAsJsonAsync($"/api/languages/{Guid.NewGuid()}", request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteLanguage_WithExistingId_ThenReturnsNotFound()
    {
        var created = await CreateLanguageAsync("Portuguese", "pt");

        var deleteResponse = await _client.DeleteAsync($"/api/languages/{created.Id}");
        var getResponse = await _client.GetAsync($"/api/languages/{created.Id}");

        Assert.Multiple(() =>
        {
            Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
            Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task DeleteLanguage_BlocksConcurrentGreetingCreationBeforeEnumeratingTupleDeletes()
    {
        var language = await CreateLanguageAsync("Concurrent language", "con");
        await using var controlConnection = new NpgsqlConnection(_factory.ConnectionString);
        await using var monitorConnection = new NpgsqlConnection(_factory.ConnectionString);
        await controlConnection.OpenAsync();
        await monitorConnection.OpenAsync();
        await using var controlTransaction = await controlConnection.BeginTransactionAsync();
        await controlConnection.ExecuteAsync(new CommandDefinition(
            "SELECT id FROM languages WHERE id = @Id FOR UPDATE",
            new { Id = language.Id },
            transaction: controlTransaction));

        Task<HttpResponseMessage>? deleteTask = null;
        Task<HttpResponseMessage>? createTask = null;
        var controlTransactionCompleted = false;
        try
        {
            deleteTask = _client.DeleteAsync($"/api/languages/{language.Id}");
            await WaitForBlockedCommandAsync(
                monitorConnection,
                "SELECT id FROM languages WHERE id",
                CancellationToken.None);

            createTask = _client.PostAsJsonAsync(
                "/api/greetings",
                new CreateGreeting.Request(language.Id, "Too late", false));
            await WaitForBlockedCommandAsync(
                monitorConnection,
                "INSERT INTO greetings",
                CancellationToken.None);

            await controlTransaction.CommitAsync();
            controlTransactionCompleted = true;

            var deleteResponse = await deleteTask;
            var createResponse = await createTask;
            var remainingGreetingCount = await controlConnection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM greetings WHERE greeting_text = 'Too late'");

            Assert.Multiple(() =>
            {
                Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
                Assert.That(createResponse.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
                Assert.That(remainingGreetingCount, Is.Zero);
            });
        }
        finally
        {
            if (!controlTransactionCompleted)
            {
                await controlTransaction.RollbackAsync();
            }

            if (deleteTask is not null)
            {
                await deleteTask;
            }

            if (createTask is not null)
            {
                await createTask;
            }
        }
    }

    [Test]
    public async Task DeleteLanguage_LocksExistingGreetingsBeforeAConcurrentMove()
    {
        var sourceLanguage = await CreateLanguageAsync("Move source", "mvs");
        var destinationLanguage = await CreateLanguageAsync("Move destination", "mvd");
        var createGreetingResponse = await _client.PostAsJsonAsync(
            "/api/greetings",
            new CreateGreeting.Request(sourceLanguage.Id, "Move me", false));
        createGreetingResponse.EnsureSuccessStatusCode();
        var greeting = (await createGreetingResponse.Content
            .ReadFromJsonAsync<CreateGreeting.Response>())!;

        await using var controlConnection = new NpgsqlConnection(_factory.ConnectionString);
        await using var monitorConnection = new NpgsqlConnection(_factory.ConnectionString);
        await controlConnection.OpenAsync();
        await monitorConnection.OpenAsync();

        const string installDeleteBarrierSql = """
            CREATE OR REPLACE FUNCTION block_language_delete_for_test()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                PERFORM pg_advisory_xact_lock(731946821);
                RETURN OLD;
            END;
            $$;

            CREATE TRIGGER block_language_delete_for_test
            BEFORE DELETE ON languages
            FOR EACH ROW
            EXECUTE FUNCTION block_language_delete_for_test();
            """;
        const string removeDeleteBarrierSql = """
            DROP TRIGGER IF EXISTS block_language_delete_for_test ON languages;
            DROP FUNCTION IF EXISTS block_language_delete_for_test();
            """;
        await controlConnection.ExecuteAsync(installDeleteBarrierSql);
        await using var controlTransaction = await controlConnection.BeginTransactionAsync();
        await controlConnection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(731946821)",
            transaction: controlTransaction));

        Task<HttpResponseMessage>? deleteTask = null;
        Task<HttpResponseMessage>? updateTask = null;
        var controlTransactionCompleted = false;
        try
        {
            deleteTask = _client.DeleteAsync($"/api/languages/{sourceLanguage.Id}");
            await WaitForBlockedCommandAsync(
                monitorConnection,
                "DELETE FROM languages",
                CancellationToken.None);

            updateTask = _client.PutAsJsonAsync(
                $"/api/greetings/{greeting.Id}",
                new UpdateGreeting.Request(
                    destinationLanguage.Id,
                    greeting.GreetingText,
                    greeting.Formal));
            await WaitForBlockedCommandAsync(
                monitorConnection,
                "UPDATE greetings",
                CancellationToken.None);

            await controlTransaction.CommitAsync();
            controlTransactionCompleted = true;

            var deleteResponse = await deleteTask;
            var updateResponse = await updateTask;
            var remainingGreetingCount = await controlConnection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM greetings WHERE id = @Id",
                new { greeting.Id });

            Assert.Multiple(() =>
            {
                Assert.That(deleteResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
                Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                Assert.That(remainingGreetingCount, Is.Zero);
            });
        }
        finally
        {
            try
            {
                if (!controlTransactionCompleted)
                {
                    await controlTransaction.RollbackAsync();
                }
            }
            finally
            {
                try
                {
                    var pendingRequests = new List<Task>();
                    if (deleteTask is not null)
                    {
                        pendingRequests.Add(deleteTask);
                    }

                    if (updateTask is not null)
                    {
                        pendingRequests.Add(updateTask);
                    }

                    await Task.WhenAll(pendingRequests);
                }
                finally
                {
                    await controlConnection.ExecuteAsync(removeDeleteBarrierSql);
                }
            }
        }
    }

    private async Task<CreateLanguage.Response> CreateLanguageAsync(string name, string code)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/languages",
            new CreateLanguage.Request(name, code));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CreateLanguage.Response>())!;
    }

    private static async Task WaitForBlockedCommandAsync(
        NpgsqlConnection connection,
        string commandFragment,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_stat_activity
                WHERE datname = current_database()
                  AND pid <> pg_backend_pid()
                  AND wait_event_type = 'Lock'
                  AND query LIKE @Pattern)
            """;
        var timeProvider = TimeProvider.System;
        var deadline = timeProvider.GetUtcNow().AddSeconds(10);
        while (timeProvider.GetUtcNow() < deadline)
        {
            var blocked = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                sql,
                new { Pattern = $"%{commandFragment}%" },
                cancellationToken: cancellationToken));
            if (blocked)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }

        throw new TimeoutException($"The expected blocked command containing '{commandFragment}' was not observed.");
    }
}
