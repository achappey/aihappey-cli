namespace AIHappey.Cli.Core;

public enum ApiArea { Ai, Agents }
public enum RequestEncoding { None, Json, Multipart }

public sealed record EndpointDescriptor(
    string Key,
    ApiArea Area,
    HttpMethod Method,
    string Route,
    RequestEncoding Encoding = RequestEncoding.Json,
    bool AlwaysStreaming = false,
    string[]? RouteParameters = null);

public static class EndpointCatalog
{
    public static readonly IReadOnlyList<EndpointDescriptor> All =
    [
        new("ai.models.list", ApiArea.Ai, HttpMethod.Get, "v1/models", RequestEncoding.None),
        new("ai.chat.create", ApiArea.Ai, HttpMethod.Post, "api/chat", AlwaysStreaming: true),
        new("ai.chat.completions.create", ApiArea.Ai, HttpMethod.Post, "v1/chat/completions"),
        new("ai.responses.create", ApiArea.Ai, HttpMethod.Post, "v1/responses"),
        new("ai.messages.create", ApiArea.Ai, HttpMethod.Post, "v1/messages"),
        new("ai.embeddings.create", ApiArea.Ai, HttpMethod.Post, "api/embeddings"),
        new("ai.embeddings.openai.create", ApiArea.Ai, HttpMethod.Post, "v1/embeddings"),
        new("ai.images.create", ApiArea.Ai, HttpMethod.Post, "api/images"),
        new("ai.images.generations.create", ApiArea.Ai, HttpMethod.Post, "v1/images/generations"),
        new("ai.images.edits.create", ApiArea.Ai, HttpMethod.Post, "v1/images/edits", RequestEncoding.Multipart),
        new("ai.speech.create", ApiArea.Ai, HttpMethod.Post, "api/speech"),
        new("ai.audio.speech.create", ApiArea.Ai, HttpMethod.Post, "v1/audio/speech"),
        new("ai.transcriptions.create", ApiArea.Ai, HttpMethod.Post, "api/transcriptions"),
        new("ai.transcriptions.stream", ApiArea.Ai, HttpMethod.Post, "api/transcriptions/stream", AlwaysStreaming: true),
        new("ai.audio.transcriptions.create", ApiArea.Ai, HttpMethod.Post, "v1/audio/transcriptions", RequestEncoding.Multipart),
        new("ai.videos.create", ApiArea.Ai, HttpMethod.Post, "api/videos"),
        new("ai.videos.status", ApiArea.Ai, HttpMethod.Get, "api/videos/{provider}/{task}", RequestEncoding.None, RouteParameters: ["provider", "task"]),
        new("ai.rerank.create", ApiArea.Ai, HttpMethod.Post, "api/rerank"),
        new("ai.generate.create", ApiArea.Ai, HttpMethod.Post, "api/generate"),
        new("ai.realtime.client-secrets.create", ApiArea.Ai, HttpMethod.Post, "v1/realtime/client_secrets"),
        new("ai.skills.list", ApiArea.Ai, HttpMethod.Get, "v1/skills", RequestEncoding.None),
        new("ai.skills.versions.list", ApiArea.Ai, HttpMethod.Get, "v1/skills/{provider}/{skill}/versions", RequestEncoding.None, RouteParameters: ["provider", "skill"]),
        new("ai.skills.content.get", ApiArea.Ai, HttpMethod.Get, "v1/skills/{provider}/{skill}/content", RequestEncoding.None, RouteParameters: ["provider", "skill"]),
        new("ai.skills.version-content.get", ApiArea.Ai, HttpMethod.Get, "v1/skills/{provider}/{skill}/versions/{version}/content", RequestEncoding.None, RouteParameters: ["provider", "skill", "version"]),
        new("ai.callbacks.create", ApiArea.Ai, HttpMethod.Post, "api/callbacks/{provider}", RouteParameters: ["provider"]),
        new("agents.models.list", ApiArea.Agents, HttpMethod.Get, "v1/models", RequestEncoding.None),
        new("agents.chat.create", ApiArea.Agents, HttpMethod.Post, "api/chat", AlwaysStreaming: true),
        new("agents.responses.create", ApiArea.Agents, HttpMethod.Post, "v1/responses"),
        new("agents.responses.list", ApiArea.Agents, HttpMethod.Get, "v1/responses", RequestEncoding.None),
        new("agents.responses.get", ApiArea.Agents, HttpMethod.Get, "v1/responses/{response}", RequestEncoding.None, RouteParameters: ["response"]),
        new("agents.responses.delete", ApiArea.Agents, HttpMethod.Delete, "v1/responses/{response}", RequestEncoding.None, RouteParameters: ["response"])
    ];
}

