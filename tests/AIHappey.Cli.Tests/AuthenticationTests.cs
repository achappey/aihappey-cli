using System.Net;
using Azure.Core;
using Azure.Identity;
using AIHappey.Cli.Core;
using Xunit;

namespace AIHappey.Cli.Tests;

public sealed class AuthenticationTests
{
    [Theory]
    [InlineData(false, false, AzureCredentialMode.InteractiveBrowser)]
    [InlineData(true, false, AzureCredentialMode.DefaultAzure)]
    [InlineData(false, true, AzureCredentialMode.InteractiveBrowser)]
    public async Task AzureCredentialSelectionUsesOverridesThenCompiledDefault(
        bool azure,
        bool azureLogin,
        AzureCredentialMode expected)
    {
        var provider = new RecordingTokenProvider();
        var authenticator = new AzureRequestAuthenticator(TestProfiles.Azure(), provider);
        using var request = new HttpRequestMessage();

        await authenticator.AuthenticateAsync(
            request,
            new AuthenticationRequest(ApiArea.Ai, null, azure, azureLogin, []),
            CancellationToken.None);

        Assert.Equal(expected, provider.Mode);
        Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
    }

    [Fact]
    public async Task AzureOverridesAreMutuallyExclusive()
    {
        var authenticator = new AzureRequestAuthenticator(TestProfiles.Azure(), new RecordingTokenProvider());
        using var request = new HttpRequestMessage();

        await Assert.ThrowsAsync<ArgumentException>(() => authenticator.AuthenticateAsync(
            request,
            new AuthenticationRequest(ApiArea.Ai, null, true, true, []),
            CancellationToken.None));
    }

    [Fact]
    public async Task LogoutCommandClearsOnlyInjectedAuthenticationState()
    {
        var state = new RecordingAuthenticationState();

        var exitCode = await CliApplication.RunAsync(
            ["auth", "logout"],
            TestProfiles.Azure(),
            authenticationState: state);

        Assert.Equal(0, exitCode);
        Assert.True(state.WasCleared);
    }

    private sealed class RecordingTokenProvider : IAzureTokenProvider
    {
        public AzureCredentialMode? Mode { get; private set; }

        public Task<AccessToken> GetTokenAsync(
            AzureCredentialMode mode,
            IReadOnlyList<string> scopes,
            CancellationToken cancellationToken)
        {
            Mode = mode;
            return Task.FromResult(new AccessToken("test-token", DateTimeOffset.UtcNow.AddHours(1)));
        }
    }

    private sealed class RecordingAuthenticationState : IAzureAuthenticationState
    {
        public bool WasCleared { get; private set; }

        public TokenCachePersistenceOptions CreateCacheOptions() => new();
        public Task<AuthenticationRecord?> LoadRecordAsync(CancellationToken cancellationToken) => Task.FromResult<AuthenticationRecord?>(null);
        public Task SaveRecordAsync(AuthenticationRecord record, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken)
        {
            WasCleared = true;
            return Task.CompletedTask;
        }
    }
}
