using AIHappey.Cli.Core;
using Xunit;

namespace AIHappey.Cli.Tests;

public sealed class EndpointCatalogTests
{
    [Fact]
    public void EndpointKeysAreUnique()
        => Assert.Equal(EndpointCatalog.All.Count, EndpointCatalog.All.Select(endpoint => endpoint.Key).Distinct().Count());

    [Theory]
    [InlineData("ai.chat.create", "api/chat")]
    [InlineData("ai.responses.create", "v1/responses")]
    [InlineData("ai.audio.transcriptions.create", "v1/audio/transcriptions")]
    [InlineData("ai.videos.status", "api/videos/{provider}/{task}")]
    [InlineData("agents.responses.delete", "v1/responses/{response}")]
    public void ContainsExpectedPublicRoutes(string key, string route)
        => Assert.Equal(route, Assert.Single(EndpointCatalog.All.Where(endpoint => endpoint.Key == key)).Route);

    [Fact]
    public void CatalogContainsEveryAuditedControllerOperation()
        => Assert.Equal(31, EndpointCatalog.All.Count);
}

