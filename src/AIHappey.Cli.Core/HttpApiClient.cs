using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace AIHappey.Cli.Core;

public sealed record ApiInvocation(
    EndpointDescriptor Endpoint,
    JsonObject? Body,
    IReadOnlyDictionary<string, string> RouteValues,
    IReadOnlyDictionary<string, string> Query,
    IReadOnlyList<string> Headers,
    IReadOnlyList<string> Files,
    string? Url,
    string? Bearer,
    bool Azure,
    bool AzureLogin,
    IReadOnlyList<string> Scopes,
    string? Output);

public sealed class HttpApiClient(
    DeploymentProfile profile,
    HttpClient httpClient,
    IRequestAuthenticator authenticator,
    ILocalHeaderConfiguration? localHeaderConfiguration = null)
{
    public async Task<int> SendAsync(ApiInvocation invocation, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(invocation, cancellationToken);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await WriteResponseAsync(response, invocation.Output, cancellationToken);
        return response.IsSuccessStatusCode ? 0 : Math.Clamp((int)response.StatusCode, 1, 255);
    }

    public async Task<HttpRequestMessage> CreateRequestAsync(ApiInvocation invocation, CancellationToken cancellationToken)
    {
        var endpoint = invocation.Endpoint;
        var route = endpoint.Route;
        foreach (var parameter in endpoint.RouteParameters ?? [])
        {
            if (!invocation.RouteValues.TryGetValue(parameter, out var value) || string.IsNullOrWhiteSpace(value))
                throw new ArgumentException($"Route value '{parameter}' is required.");
            route = route.Replace($"{{{parameter}}}", Uri.EscapeDataString(value), StringComparison.Ordinal);
        }

        var baseUrl = ResolveBaseUrl(endpoint.Area, invocation.Url);
        var uri = new Uri(baseUrl, route);
        if (invocation.Query.Count > 0)
        {
            var query = string.Join('&', invocation.Query
                .Where(item => !string.IsNullOrWhiteSpace(item.Value))
                .Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
            if (query.Length > 0)
                uri = new UriBuilder(uri) { Query = query }.Uri;
        }

        var request = new HttpRequestMessage(endpoint.Method, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        ApplyHeaders(request, profile.FixedHeaders);
        if (profile.Authentication == AuthenticationMode.Header && profile.AllowHeaderOverride)
        {
            var localHeaders = await (localHeaderConfiguration ?? new LocalHeaderConfiguration())
                .LoadAsync(cancellationToken);
            ApplyHeaders(request, localHeaders);
        }
        ApplyInvocationHeaders(request, invocation.Headers);

        request.Content = endpoint.Encoding switch
        {
            RequestEncoding.Json when endpoint.Method != HttpMethod.Get && endpoint.Method != HttpMethod.Delete
                => new StringContent((invocation.Body ?? new JsonObject()).ToJsonString(), Encoding.UTF8, "application/json"),
            RequestEncoding.Multipart => CreateMultipart(invocation),
            _ => null
        };

        await authenticator.AuthenticateAsync(request,
            new AuthenticationRequest(endpoint.Area, invocation.Bearer, invocation.Azure, invocation.AzureLogin, invocation.Scopes),
            cancellationToken);
        return request;
    }

    private Uri ResolveBaseUrl(ApiArea area, string? overrideUrl)
    {
        if (overrideUrl is null)
            return EnsureTrailingSlash(profile.BaseUrl(area));
        if (!profile.AllowUrlOverride)
            throw new ArgumentException("URL overrides are disabled by the compiled deployment profile.");
        if (!Uri.TryCreate(overrideUrl, UriKind.Absolute, out var url))
            throw new ArgumentException("--url must be an absolute base URL.");
        return EnsureTrailingSlash(url);
    }

    private void ApplyInvocationHeaders(HttpRequestMessage request, IReadOnlyList<string> values)
    {
        if (values.Count > 0 && !profile.AllowHeaderOverride)
            throw new ArgumentException("Header overrides are disabled by the compiled deployment profile.");
        foreach (var value in values)
        {
            var separator = value.IndexOf(':');
            if (separator <= 0)
                throw new ArgumentException("--header values must use 'Name: Value' syntax.");
            ApplyHeader(request, value[..separator].Trim(), value[(separator + 1)..].Trim());
        }
    }

    private static void ApplyHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string> headers)
    {
        foreach (var (name, value) in headers)
            ApplyHeader(request, name, value);
    }

    private static void ApplyHeader(HttpRequestMessage request, string name, string value)
    {
        request.Headers.Remove(name);
        if (!request.Headers.TryAddWithoutValidation(name, value))
        {
            request.Content ??= new ByteArrayContent([]);
            request.Content.Headers.Remove(name);
            request.Content.Headers.TryAddWithoutValidation(name, value);
        }
    }

    private static MultipartFormDataContent CreateMultipart(ApiInvocation invocation)
    {
        var multipart = new MultipartFormDataContent();
        foreach (var property in invocation.Body ?? new JsonObject())
        {
            if (property.Value is null)
                continue;
            var value = property.Value is JsonValue scalar && scalar.TryGetValue<string>(out var text)
                ? text
                : property.Value.ToJsonString();
            multipart.Add(new StringContent(value), property.Key);
        }

        var defaultField = invocation.Endpoint.Key.Contains("transcriptions", StringComparison.Ordinal) ? "file" : "image";
        foreach (var specification in invocation.Files)
        {
            var separator = specification.IndexOf('=');
            var field = separator > 0 ? specification[..separator] : defaultField;
            var path = separator > 0 ? specification[(separator + 1)..] : specification;
            var content = new StreamContent(File.OpenRead(path));
            content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypes.ForPath(path));
            multipart.Add(content, field, Path.GetFileName(path));
        }
        return multipart;
    }

    private static async Task WriteResponseAsync(HttpResponseMessage response, string? output, CancellationToken cancellationToken)
    {
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (output is not null)
        {
            var fullPath = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await using var destination = File.Create(fullPath);
            await source.CopyToAsync(destination, cancellationToken);
            return;
        }

        var stdout = Console.OpenStandardOutput();
        await source.CopyToAsync(stdout, cancellationToken);
        await stdout.FlushAsync(cancellationToken);
    }

    private static Uri EnsureTrailingSlash(Uri uri)
        => uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
}
