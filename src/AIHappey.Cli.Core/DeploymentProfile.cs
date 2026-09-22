using System.Reflection;

namespace AIHappey.Cli.Core;

public enum AuthenticationMode
{
    Header,
    Azure
}

public enum AzureCredentialMode
{
    DefaultAzure,
    InteractiveBrowser
}

public sealed record DeploymentProfile(
    AuthenticationMode Authentication,
    AzureCredentialMode AzureCredential,
    string? TokenCacheName,
    Uri AiBaseUrl,
    Uri AgentsBaseUrl,
    string? AiScope,
    string? AgentsScope,
    string? AzureTenantId,
    string? AzureClientId,
    IReadOnlyDictionary<string, string> FixedHeaders,
    bool AllowUrlOverride,
    bool AllowHeaderOverride,
    bool AllowBearerOverride,
    bool AllowInteractiveLogin,
    bool AllowScopeOverride)
{
    public static DeploymentProfile FromEntryAssembly()
    {
        var assembly = Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("The entry assembly is unavailable.");
        var values = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(attribute => attribute.Key.StartsWith("AIHappey.Cli.", StringComparison.Ordinal))
            .ToDictionary(
                attribute => attribute.Key[13..],
                attribute => attribute.Value ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        return FromValues(values);
    }

    public static DeploymentProfile FromValues(IReadOnlyDictionary<string, string> values)
    {
        var auth = Enum.Parse<AuthenticationMode>(Required(values, "AuthMode"), true);
        var azureCredential = Enum.TryParse<AzureCredentialMode>(Optional(values, "AzureCredentialMode"), true, out var parsedCredential)
            ? parsedCredential
            : AzureCredentialMode.DefaultAzure;
        var profile = new DeploymentProfile(
            auth,
            azureCredential,
            Optional(values, "TokenCacheName"),
            AbsoluteUri(values, "AiBaseUrl"),
            AbsoluteUri(values, "AgentsBaseUrl"),
            Optional(values, "AiScope"),
            Optional(values, "AgentsScope"),
            Optional(values, "AzureTenantId"),
            Optional(values, "AzureClientId"),
            ParseHeaders(Optional(values, "FixedHeaders")),
            Boolean(values, "AllowUrlOverride"),
            Boolean(values, "AllowHeaderOverride"),
            Boolean(values, "AllowBearerOverride"),
            Boolean(values, "AllowInteractiveLogin"),
            Boolean(values, "AllowScopeOverride"));

        if (auth == AuthenticationMode.Azure
            && (string.IsNullOrWhiteSpace(profile.AiScope) || string.IsNullOrWhiteSpace(profile.AgentsScope)))
        {
            throw new InvalidOperationException("Azure profiles require both AI and Agents scopes.");
        }

        if (auth == AuthenticationMode.Azure && string.IsNullOrWhiteSpace(profile.TokenCacheName))
            throw new InvalidOperationException("Azure profiles require a stable token cache name.");

        if (auth == AuthenticationMode.Azure
            && profile.AzureCredential == AzureCredentialMode.InteractiveBrowser
            && !profile.AllowInteractiveLogin)
        {
            throw new InvalidOperationException("An InteractiveBrowser default requires interactive login to be allowed.");
        }

        return profile;
    }

    public Uri BaseUrl(ApiArea area) => area == ApiArea.Ai ? AiBaseUrl : AgentsBaseUrl;

    public string? Scope(ApiArea area) => area == ApiArea.Ai ? AiScope : AgentsScope;

    private static string Required(IReadOnlyDictionary<string, string> values, string key)
        => Optional(values, key) ?? throw new InvalidOperationException($"Compiled deployment setting '{key}' is required.");

    private static string? Optional(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    private static Uri AbsoluteUri(IReadOnlyDictionary<string, string> values, string key)
        => Uri.TryCreate(Required(values, key), UriKind.Absolute, out var uri)
            ? uri
            : throw new InvalidOperationException($"Compiled deployment setting '{key}' must be an absolute URL.");

    private static bool Boolean(IReadOnlyDictionary<string, string> values, string key)
        => bool.TryParse(Optional(values, key), out var value) && value;

    private static IReadOnlyDictionary<string, string> ParseHeaders(string? value)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in (value ?? string.Empty).Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = item.IndexOf('=');
            if (separator <= 0)
                throw new InvalidOperationException("CliFixedHeaders entries must use Name=Value syntax.");
            headers[item[..separator].Trim()] = item[(separator + 1)..].Trim();
        }
        return headers;
    }
}

