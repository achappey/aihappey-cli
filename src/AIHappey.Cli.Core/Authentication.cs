using System.Net.Http.Headers;

namespace AIHappey.Cli.Core;

public sealed record AuthenticationRequest(
    ApiArea Area,
    string? Bearer,
    bool Azure,
    bool AzureLogin,
    IReadOnlyList<string> Scopes);

public interface IRequestAuthenticator
{
    Task AuthenticateAsync(HttpRequestMessage request, AuthenticationRequest authentication, CancellationToken cancellationToken);
}

public static class RequestAuthenticatorFactory
{
    public static IRequestAuthenticator Create(DeploymentProfile profile, IAzureTokenProvider? azureTokenProvider = null) => profile.Authentication switch
    {
        AuthenticationMode.Header => new HeaderRequestAuthenticator(profile),
        AuthenticationMode.Azure => new AzureRequestAuthenticator(
            profile,
            azureTokenProvider ?? new AzureTokenProvider(profile, new AzureAuthenticationState(profile))),
        _ => throw new InvalidOperationException($"Unsupported compiled authentication mode '{profile.Authentication}'.")
    };
}

public sealed class HeaderRequestAuthenticator(DeploymentProfile profile) : IRequestAuthenticator
{
    public Task AuthenticateAsync(HttpRequestMessage request, AuthenticationRequest authentication, CancellationToken cancellationToken)
    {
        if (authentication.Azure || authentication.AzureLogin || authentication.Scopes.Count > 0)
            throw new ArgumentException("This executable was built for header authentication; Azure credential options are unavailable.");
        if (authentication.Bearer is not null)
        {
            if (!profile.AllowBearerOverride)
                throw new ArgumentException("Bearer overrides are disabled by the compiled deployment profile.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authentication.Bearer);
        }
        return Task.CompletedTask;
    }
}

public sealed class AzureRequestAuthenticator(DeploymentProfile profile, IAzureTokenProvider tokenProvider) : IRequestAuthenticator
{
    public async Task AuthenticateAsync(HttpRequestMessage request, AuthenticationRequest authentication, CancellationToken cancellationToken)
    {
        if (authentication.Bearer is not null)
        {
            if (!profile.AllowBearerOverride)
                throw new ArgumentException("Bearer overrides are disabled by the compiled deployment profile.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authentication.Bearer);
            return;
        }

        if (authentication.AzureLogin && !profile.AllowInteractiveLogin)
            throw new ArgumentException("Interactive browser login is disabled by the compiled deployment profile.");
        if (authentication.Azure && authentication.AzureLogin)
            throw new ArgumentException("--azure and --azure-login cannot be used together.");
        if (authentication.Scopes.Count > 0 && !profile.AllowScopeOverride)
            throw new ArgumentException("Scope overrides are disabled by the compiled deployment profile.");

        var scopes = authentication.Scopes.Count > 0
            ? authentication.Scopes
            : [profile.Scope(authentication.Area) ?? throw new InvalidOperationException("No compiled scope exists for this API.")];

        var credentialMode = authentication.AzureLogin
            ? AzureCredentialMode.InteractiveBrowser
            : authentication.Azure
                ? AzureCredentialMode.DefaultAzure
                : profile.AzureCredential;
        var token = await tokenProvider.GetTokenAsync(credentialMode, scopes, cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
    }
}

