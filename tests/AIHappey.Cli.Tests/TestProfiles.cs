using AIHappey.Cli.Core;

namespace AIHappey.Cli.Tests;

internal static class TestProfiles
{
    internal static DeploymentProfile Header(
        bool allowUrl = true,
        bool allowHeaders = true,
        bool allowBearer = true,
        IReadOnlyDictionary<string, string>? fixedHeaders = null)
        => new(
            AuthenticationMode.Header,
            AzureCredentialMode.DefaultAzure,
            null,
            new Uri("https://ai.test/"),
            new Uri("https://agents.test/"),
            null,
            null,
            null,
            null,
            fixedHeaders ?? new Dictionary<string, string>(),
            allowUrl,
            allowHeaders,
            allowBearer,
            false,
            false);

    internal static DeploymentProfile Azure(AzureCredentialMode credential = AzureCredentialMode.InteractiveBrowser)
        => new(
            AuthenticationMode.Azure,
            credential,
            "aihappey-cli-tests",
            new Uri("https://ai.test/"),
            new Uri("https://agents.test/"),
            "api://ai/AI.use",
            "api://agents/Agents.use",
            null,
            null,
            new Dictionary<string, string>(),
            false,
            true,
            false,
            true,
            true);
}

