using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Client.Extensions.Msal;

namespace AIHappey.Cli.Core;

public interface IAzureAuthenticationState
{
    TokenCachePersistenceOptions CreateCacheOptions();
    Task<AuthenticationRecord?> LoadRecordAsync(CancellationToken cancellationToken);
    Task SaveRecordAsync(AuthenticationRecord record, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}

public sealed class AzureAuthenticationState : IAzureAuthenticationState
{
    private const string CacheDirectoryName = ".IdentityService";
    private readonly string cacheName;
    private readonly string recordPath;

    public AzureAuthenticationState(DeploymentProfile profile)
    {
        cacheName = profile.TokenCacheName
            ?? throw new InvalidOperationException("The compiled Azure profile has no token cache name.");

        var stateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AIHappey",
            "Cli",
            Sanitize(cacheName));
        recordPath = Path.Combine(stateDirectory, "authentication-record.json");
    }

    public TokenCachePersistenceOptions CreateCacheOptions() => new() { Name = cacheName };

    public async Task<AuthenticationRecord?> LoadRecordAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(recordPath))
            return null;

        try
        {
            await using var stream = File.OpenRead(recordPath);
            return await AuthenticationRecord.DeserializeAsync(stream, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return null;
        }
    }

    public async Task SaveRecordAsync(AuthenticationRecord record, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(recordPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = recordPath + ".tmp";

        await using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            await record.SerializeAsync(stream, cancellationToken);

        File.Move(temporaryPath, recordPath, true);
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(recordPath))
            File.Delete(recordPath);

        var cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            CacheDirectoryName);
        var storage = new StorageCreationPropertiesBuilder(cacheName, cacheDirectory)
            .WithMacKeyChain("Microsoft.Developer.IdentityService", cacheName)
            .WithLinuxKeyring(
                "msal.cache",
                "default",
                "MSAL token cache",
                new KeyValuePair<string, string>("MsalClientID", "Microsoft.Developer.IdentityService"),
                new KeyValuePair<string, string>("Microsoft.Developer.IdentityService", "1.0.0.0"))
            .Build();
        var helper = await MsalCacheHelper.CreateAsync(storage);
        helper.Clear();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
    }
}

public interface IAzureTokenProvider
{
    Task<AccessToken> GetTokenAsync(
        AzureCredentialMode mode,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken);
}

public sealed class AzureTokenProvider(DeploymentProfile profile, IAzureAuthenticationState state) : IAzureTokenProvider
{
    public async Task<AccessToken> GetTokenAsync(
        AzureCredentialMode mode,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken)
    {
        var context = new TokenRequestContext([.. scopes]);
        if (mode == AzureCredentialMode.DefaultAzure)
        {
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                TenantId = profile.AzureTenantId,
                ManagedIdentityClientId = profile.AzureClientId
            });
            return await credential.GetTokenAsync(context, cancellationToken);
        }

        var record = await state.LoadRecordAsync(cancellationToken);
        var credentialOptions = new InteractiveBrowserCredentialOptions
        {
            TenantId = profile.AzureTenantId,
            ClientId = profile.AzureClientId,
            AuthenticationRecord = record,
            TokenCachePersistenceOptions = state.CreateCacheOptions(),
            DisableAutomaticAuthentication = true
        };
        var interactiveCredential = new InteractiveBrowserCredential(credentialOptions);

        try
        {
            return await interactiveCredential.GetTokenAsync(context, cancellationToken);
        }
        catch (AuthenticationRequiredException)
        {
            record = await interactiveCredential.AuthenticateAsync(context, cancellationToken);
            await state.SaveRecordAsync(record, cancellationToken);
            return await interactiveCredential.GetTokenAsync(context, cancellationToken);
        }
    }
}
