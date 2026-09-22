using AIHappey.Cli.Core;
using Xunit;

namespace AIHappey.Cli.Tests;

public sealed class RequestBodyComposerTests
{
    [Fact]
    public async Task ConvenienceOptionsOverlayRawJson()
    {
        var body = await RequestBodyComposer.ComposeAsync(
            "{\"model\":\"old\",\"temperature\":0.2}", null, false,
            "new", "hello", null, true, CancellationToken.None);

        Assert.Equal("new", body["model"]!.GetValue<string>());
        Assert.Equal("hello", body["input"]!.GetValue<string>());
        Assert.True(body["stream"]!.GetValue<bool>());
        Assert.Equal(0.2, body["temperature"]!.GetValue<double>());
    }

    [Fact]
    public async Task RejectsMultipleRawSources()
        => await Assert.ThrowsAsync<ArgumentException>(() => RequestBodyComposer.ComposeAsync(
            "{}", "request.json", false, null, null, null, false, CancellationToken.None));
}

