using System.Text.Json.Nodes;

using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Configuration;

var apiUrl = Environment.GetEnvironmentVariable("OpenFga__ApiUrl")
    ?? throw new InvalidOperationException("OpenFga__ApiUrl is required.");
var storeName = Environment.GetEnvironmentVariable("OpenFga__StoreName")
    ?? throw new InvalidOperationException("OpenFga__StoreName is required.");
var superuserSubjects = ReadSubjects("OpenFga__BootstrapSuperuserSubjects");
var standardUserSubjects = ReadSubjects("OpenFga__BootstrapStandardUserSubjects");

var modelPath = Path.Combine(
    AppContext.BaseDirectory,
    "OpenFga",
    "authorization-model.json");
var modelJson = await File.ReadAllTextAsync(modelPath);
var authorizationModel = ClientWriteAuthorizationModelRequest.FromJson(modelJson);

var client = new OpenFgaClient(new ClientConfiguration
{
    ApiUrl = apiUrl
});

var stores = await client.ListStores(new ClientListStoresRequest
{
    Name = storeName
});
var store = stores.Stores.SingleOrDefault(candidate =>
    string.Equals(candidate.Name, storeName, StringComparison.Ordinal));

string storeId;

if (store is null)
{
    var created = await client.CreateStore(new ClientCreateStoreRequest
    {
        Name = storeName
    });
    storeId = created.Id;
    Console.WriteLine("Created OpenFGA store '{0}' ({1}).", created.Name, storeId);
}
else
{
    storeId = store.Id;
    Console.WriteLine("Reusing OpenFGA store '{0}' ({1}).", store.Name, storeId);
}

var storeClient = new OpenFgaClient(new ClientConfiguration
{
    ApiUrl = apiUrl,
    StoreId = storeId
});

var authorizationModels = await storeClient.ReadAuthorizationModels(
    new ClientReadAuthorizationModelsOptions
    {
        PageSize = 1
    });
var latest = authorizationModels.AuthorizationModels.FirstOrDefault();
var desiredModel = ParseModel(modelJson);
var latestModel = latest is null ? null : ParseModel(latest.ToJson());

if (latest is not null
    && JsonNode.DeepEquals(desiredModel, latestModel))
{
    Console.WriteLine(
        "Authorization model {0} is already current in store {1}.",
        latest.Id,
        storeId);
}
else
{
    var written = await storeClient.WriteAuthorizationModel(authorizationModel);
    Console.WriteLine("Published authorization model {0} to store {1}.", written.AuthorizationModelId, storeId);
}

var bootstrapTuples = superuserSubjects
    .Select(subject => new ClientTupleKey
    {
        User = $"user:{subject}",
        Relation = "superuser",
        Object = "system:global"
    })
    .Concat(standardUserSubjects.Select(subject => new ClientTupleKey
    {
        User = $"user:{subject}",
        Relation = "standard_user",
        Object = "system:global"
    }))
    .ToList();

if (bootstrapTuples.Count > 0)
{
    await storeClient.Write(
        new ClientWriteRequest { Writes = bootstrapTuples },
        new ClientWriteOptions
        {
            Conflict = new ConflictOptions
            {
                OnDuplicateWrites = OnDuplicateWrites.Ignore
            }
        });
    Console.WriteLine("Ensured {0} bootstrap authorization tuple(s).", bootstrapTuples.Count);
}

static IReadOnlyList<string> ReadSubjects(string variableName) =>
    (Environment.GetEnvironmentVariable(variableName) ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

static JsonNode ParseModel(string json)
{
    var model = JsonNode.Parse(json)?.AsObject()
        ?? throw new InvalidOperationException("Authorization model JSON must be an object.");

    NormalizeModel(model);
    return Canonicalize(model);
}

static void NormalizeModel(JsonNode node)
{
    switch (node)
    {
        case JsonObject objectNode:
            foreach (var property in objectNode.ToList())
            {
                if (property.Value is not null)
                {
                    NormalizeModel(property.Value);
                }
            }

            objectNode.Remove("id");
            RemoveEmptyObject(objectNode, "conditions");
            RemoveEmptyObject(objectNode, "relations");
            RemoveEmptyArray(objectNode, "directly_related_user_types");
            RemoveNull(objectNode, "metadata");
            RemoveNull(objectNode, "source_info");
            RemoveEmptyString(objectNode, "object");
            RemoveEmptyString(objectNode, "condition");
            RemoveEmptyString(objectNode, "module");
            break;

        case JsonArray arrayNode:
            foreach (var item in arrayNode)
            {
                if (item is not null)
                {
                    NormalizeModel(item);
                }
            }
            break;
    }
}

static void RemoveEmptyObject(JsonObject objectNode, string propertyName)
{
    if (objectNode[propertyName] is JsonObject { Count: 0 })
    {
        objectNode.Remove(propertyName);
    }
}

static void RemoveNull(JsonObject objectNode, string propertyName)
{
    if (objectNode[propertyName] is null)
    {
        objectNode.Remove(propertyName);
    }
}

static void RemoveEmptyArray(JsonObject objectNode, string propertyName)
{
    if (objectNode[propertyName] is JsonArray { Count: 0 })
    {
        objectNode.Remove(propertyName);
    }
}

static void RemoveEmptyString(JsonObject objectNode, string propertyName)
{
    if (objectNode[propertyName]?.GetValue<string>() is "")
    {
        objectNode.Remove(propertyName);
    }
}

static JsonNode Canonicalize(JsonNode node) => node switch
{
    JsonObject objectNode => new JsonObject(
        objectNode
            .OrderBy(property => property.Key, StringComparer.Ordinal)
            .Select(property => new KeyValuePair<string, JsonNode?>(
                property.Key,
                property.Value is null ? null : Canonicalize(property.Value)))),
    JsonArray arrayNode => new JsonArray(
        arrayNode.Select(item => item is null ? null : Canonicalize(item)).ToArray()),
    _ => node.DeepClone()
};
