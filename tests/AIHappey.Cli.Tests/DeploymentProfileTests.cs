using AIHappey.Cli.Core;
using Xunit;

namespace AIHappey.Cli.Tests;

public sealed class DeploymentProfileTests
{
    [Fact]
    public void FromValuesBindsHeaderProfileAndFixedHeaders()
    {
        var profile = DeploymentProfile.FromValues(new Dictionary<string, string>
        {
            ["AuthMode"] = "Header",
            ["AiBaseUrl"] = "https://ai.example/",
            ["AgentsBaseUrl"] = "https://agents.example/",
            ["AzureCredentialMode"] = "DefaultAzure",
            ["FixedHeaders"] = "X-One=one;X-Two=two",
            ["AllowUrlOverride"] = "true",
            ["AllowHeaderOverride"] = "true",
            ["AllowBearerOverride"] = "true"
        });

        Assert.Equal(AuthenticationMode.Header, profile.Authentication);
        Assert.Equal("one", profile.FixedHeaders["X-One"]);
        Assert.True(profile.AllowUrlOverride);
        Assert.Equal(AzureCredentialMode.DefaultAzure, profile.AzureCredential);
    }

    [Fact]
    public void AzureProfileRequiresBothScopes()
    {
        var values = new Dictionary<string, string>
        {
            ["AuthMode"] = "Azure",
            ["AiBaseUrl"] = "https://ai.example/",
            ["AgentsBaseUrl"] = "https://agents.example/",
            ["AzureCredentialMode"] = "InteractiveBrowser",
            ["TokenCacheName"] = "test-cache",
            ["AllowInteractiveLogin"] = "true"
        };

        Assert.Throws<InvalidOperationException>(() => DeploymentProfile.FromValues(values));
    }

    [Fact]
    public void AzureProfileBindsInteractiveDefaultAndCacheIdentity()
    {
        var profile = DeploymentProfile.FromValues(new Dictionary<string, string>
        {
            ["AuthMode"] = "Azure",
            ["AzureCredentialMode"] = "InteractiveBrowser",
            ["TokenCacheName"] = "company-production",
            ["AiBaseUrl"] = "https://ai.example/",
            ["AgentsBaseUrl"] = "https://agents.example/",
            ["AiScope"] = "api://ai/AI.use",
            ["AgentsScope"] = "api://agents/Agents.use",
            ["AllowInteractiveLogin"] = "true"
        });

        Assert.Equal(AzureCredentialMode.InteractiveBrowser, profile.AzureCredential);
        Assert.Equal("company-production", profile.TokenCacheName);
    }

    [Fact]
    public void InteractiveDefaultRequiresInteractiveLoginPermission()
    {
        var values = new Dictionary<string, string>
        {
            ["AuthMode"] = "Azure",
            ["AzureCredentialMode"] = "InteractiveBrowser",
            ["TokenCacheName"] = "test-cache",
            ["AiBaseUrl"] = "https://ai.example/",
            ["AgentsBaseUrl"] = "https://agents.example/",
            ["AiScope"] = "api://ai/AI.use",
            ["AgentsScope"] = "api://agents/Agents.use"
        };

        Assert.Throws<InvalidOperationException>(() => DeploymentProfile.FromValues(values));
    }
}

