using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIHappey.Cli.Core;

public static class RequestBodyComposer
{
    public static async Task<JsonObject> ComposeAsync(
        string? json,
        string? jsonFile,
        bool stdin,
        string? model,
        string? input,
        string? prompt,
        bool stream,
        CancellationToken cancellationToken)
    {
        var sourceCount = (json is null ? 0 : 1) + (jsonFile is null ? 0 : 1) + (stdin ? 1 : 0);
        if (sourceCount > 1)
            throw new ArgumentException("Use only one of --json, --json-file, or --stdin.");

        string? source = json;
        if (jsonFile is not null)
            source = await File.ReadAllTextAsync(jsonFile, cancellationToken);
        if (stdin)
            source = await Console.In.ReadToEndAsync(cancellationToken);

        var body = string.IsNullOrWhiteSpace(source)
            ? new JsonObject()
            : JsonNode.Parse(source)?.AsObject()
                ?? throw new JsonException("The request body must be a JSON object.");

        if (model is not null) body["model"] = model;
        if (input is not null) body["input"] = input;
        if (prompt is not null) body["prompt"] = prompt;
        if (stream) body["stream"] = true;
        return body;
    }

    public static void AddMediaFiles(JsonObject body, IReadOnlyList<string> files)
    {
        if (files.Count == 0)
            return;

        var values = new JsonArray();
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            values.Add(new JsonObject
            {
                ["type"] = "file",
                ["mediaType"] = MediaTypes.ForPath(file),
                ["data"] = Convert.ToBase64String(bytes)
            });
        }
        body["files"] = values;
    }
}

public static class MediaTypes
{
    public static string ForPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".json" => "application/json",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".wav" => "audio/wav",
        ".mp3" => "audio/mpeg",
        ".m4a" => "audio/mp4",
        ".ogg" or ".oga" => "audio/ogg",
        ".flac" => "audio/flac",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };
}
