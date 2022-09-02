using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.NET.Build.Containers;

record Label(string name, string value);

public class Image
{
    public JsonNode manifest;
    public JsonNode config;

    public readonly string OriginatingName;
    internal readonly Registry? originatingRegistry;

    internal List<Layer> newLayers = new();

    private HashSet<Label> labels;

    public Image(JsonNode manifest, JsonNode config, string name, Registry? registry)
    {
        this.manifest = manifest;
        this.config = config;
        this.OriginatingName = name;
        this.originatingRegistry = registry;
        // labels are inherited from the parent image, so we need to seed our new image with them.
        this.labels = ReadLabelsFromConfig(config);
    }

    public IEnumerable<Descriptor> LayerDescriptors
    {
        get
        {
            JsonNode? layersNode = manifest["layers"];

            if (layersNode is null)
            {
                throw new NotImplementedException("Tried to get layer information but there is no layer node?");
            }

            foreach (JsonNode? descriptorJson in layersNode.AsArray())
            {
                if (descriptorJson is null)
                {
                    throw new NotImplementedException("Null layer descriptor in the list?");
                }

                yield return descriptorJson.Deserialize<Descriptor>();
            }
        }
    }

    public void AddLayer(Layer l)
    {
        newLayers.Add(l);
        manifest["layers"]!.AsArray().Add(l.Descriptor);
        config["rootfs"]!["diff_ids"]!.AsArray().Add(l.Descriptor.Digest); // TODO: this should be the descriptor of the UNCOMPRESSED tarball (once we turn on compression)
        RecalculateDigest();
    }

    private void RecalculateDigest()
    {
        config["created"] = DateTime.UtcNow;

        manifest["config"]!["digest"] = GetDigest(config);
    }

    private static HashSet<Label> ReadLabelsFromConfig(JsonNode inputConfig)
    {
        if (inputConfig is JsonObject config && config["Labels"] is JsonObject labelsJson)
        {
            // read label mappings from object
            var labels = new HashSet<Label>();
            foreach (var property in labelsJson)
            {
                if (property.Key is { } propertyName && property.Value is JsonValue propertyValue)
                {
                    labels.Add(new(propertyName, propertyValue.ToString()));
                }
            }
            return labels;
        }
        else
        {
            // initialize and empty labels map
            return new HashSet<Label>();
        }
    }

    private JsonObject CreateLabelMap()
    {
        var container = new JsonObject();
        foreach (var label in labels)
        {
            container.Add(label.name, label.value);
        }
        return container;
    }

    static JsonArray ToJsonArray(string[] items) => new JsonArray(items.Where(s => !string.IsNullOrEmpty(s)).Select(s => JsonValue.Create(s)).ToArray());

    public void SetEntrypoint(string[] executableArgs, string[]? args = null)
    {
        JsonObject? configObject = config["config"]!.AsObject();

        if (configObject is null)
        {
            throw new NotImplementedException("Expected base image to have a config node");
        }

        configObject["Entrypoint"] = ToJsonArray(executableArgs);

        if (args is null)
        {
            configObject.Remove("Cmd");
        }
        else
        {
            configObject["Cmd"] = ToJsonArray(args);
        }

        RecalculateDigest();
    }

    public string WorkingDirectory
    {
        get => (string?)config["config"]!["WorkingDir"] ?? "";
        set
        {
            config["config"]!["WorkingDir"] = value;
            RecalculateDigest();
        }
    }

    private JsonNode? UserNode() => config["config"]!["User"];
    private void SetUserNode(string value) => config["config"]!["User"] = value;

    private bool GetUserAndGroup([NotNullWhen(true)] out string? user, out string? group) {
        var userValue = UserNode();
        if (userValue is null) {
            user = null;
            group = null;
            return false;
        } else {
            var parts = userValue.GetValue<string>().Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts is [string u, string g]) {
                user = u;
                group = g;
                return true;
            } else if (parts is [ string userOnly ]) {
                user = userOnly;
                group = null;
                return true;
            } else {
                user = null;
                group = null;
                return false;
            }
        }
    }

    void SetUser(string? inUser) {
        if (GetUserAndGroup(out var user, out var group)) {
            if (user is not null && group is not null) {
                SetUserNode($"{inUser}:{group}");
            } else {
                SetUserNode(inUser ?? "");
            }
        } else {
            SetUserNode(inUser ?? "");
        }
        RecalculateDigest();
    }

    void SetGroup(string? inGroup) {
        if (GetUserAndGroup(out var user, out var group)) {
            if (user is not null) {
                SetUserNode($"{user}:{inGroup}");
            } else {
                throw new ArgumentException("Cannot set a group without a user.");
            }
        } else {
            throw new ArgumentException("Cannot set a group without a user.");
        }
        RecalculateDigest();
    }

    public string? User {
        get => GetUserAndGroup(out var user, out var _) ? user : null;
        set => SetUser(value);
    }

    public string? Group {
        get => GetUserAndGroup(out var _, out var group) ? group : null;
        set => SetGroup(value);
    }

    public void Label(string name, string value)
    {
        labels.Add(new(name, value));
        config["config"]!["Labels"] = CreateLabelMap();
        RecalculateDigest();
    }

    public string GetDigest(JsonNode json)
    {
        string hashString;

        hashString = GetSha(json);

        return $"sha256:{hashString}";
    }

    public static string GetSha(JsonNode json)
    {
        Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(Encoding.UTF8.GetBytes(json.ToJsonString()), hash);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
