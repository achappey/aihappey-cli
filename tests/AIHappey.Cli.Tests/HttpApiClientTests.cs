using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using AIHappey.Cli.Core;
using Xunit;

namespace AIHappey.Cli.Tests;

public sealed class HttpApiClientTests
{
    [Fact]
    public async Task CreatesJsonRequestWithRouteHeadersBearerAndBody()
    {
        var endpoint = EndpointCatalog.All.Single(item => item.Key == "agents.responses.get");
        var profile = TestProfiles.Header(fixedHeaders: new Dictionary<string, string> { ["X-Fixed"] = "fixed" });
        var client = new HttpApiClient(profile, new HttpClient(new StubHandler()), new HeaderRequestAuthenticator(profile));
        var request = await client.CreateRequestAsync(new ApiInvocation(
            endpoint, null,
            new Dictionary<string, string> { ["response"] = "response/1" },
            new Dictionary<string, string>(),
            ["X-Test: value"], [], null, "token", false, false, [], null), CancellationToken.None);

        Assert.Equal("https://agents.test/v1/responses/response%2F1", request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("token", request.Headers.Authorization.Parameter);
        Assert.Equal("fixed", request.Headers.GetValues("X-Fixed").Single());
        Assert.Equal("value", request.Headers.GetValues("X-Test").Single());
    }

    [Fact]
    public async Task CreatesMultipartFileRequest()
    {
        var path = Path.GetTempFileName() + ".mp3";
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        try
        {
            var endpoint = EndpointCatalog.All.Single(item => item.Key == "ai.audio.transcriptions.create");
            var profile = TestProfiles.Header();
            var client = new HttpApiClient(profile, new HttpClient(new StubHandler()), new HeaderRequestAuthenticator(profile));
            using var request = await client.CreateRequestAsync(new ApiInvocation(
                endpoint, new JsonObject { ["model"] = "whisper" },
                new Dictionary<string, string>(), new Dictionary<string, string>(), [], [path],
                null, null, false, false, [], null), CancellationToken.None);

            var payload = await request.Content!.ReadAsStringAsync();
            Assert.Contains("name=model", payload);
            Assert.Contains("whisper", payload);
            Assert.Contains("name=file", payload);
            Assert.Contains("audio/mpeg", payload);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task DeniesUrlOverrideWhenProfileForbidsIt()
    {
        var endpoint = EndpointCatalog.All.Single(item => item.Key == "ai.models.list");
        var profile = TestProfiles.Header(allowUrl: false);
        var client = new HttpApiClient(profile, new HttpClient(new StubHandler()), new HeaderRequestAuthenticator(profile));

        await Assert.ThrowsAsync<ArgumentException>(() => client.CreateRequestAsync(new ApiInvocation(
            endpoint, null, new Dictionary<string, string>(), new Dictionary<string, string>(), [], [],
            "https://other.test/", null, false, false, [], null), CancellationToken.None));
    }

    [Fact]
    public async Task SendPassesThroughSseResponse()
    {
        var endpoint = EndpointCatalog.All.Single(item => item.Key == "ai.responses.create");
        var profile = TestProfiles.Header();
        var output = Path.GetTempFileName();
        try
        {
            var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("data: {\"type\":\"response.output_text.delta\"}\n\ndata: [DONE]\n\n", Encoding.UTF8, "text/event-stream")
            });
            var client = new HttpApiClient(profile, new HttpClient(handler), new HeaderRequestAuthenticator(profile));
            var exit = await client.SendAsync(new ApiInvocation(
                endpoint, new JsonObject { ["input"] = "hi", ["stream"] = true },
                new Dictionary<string, string>(), new Dictionary<string, string>(), [], [],
                null, null, false, false, [], output), CancellationToken.None);

            Assert.Equal(0, exit);
            Assert.Contains("data: [DONE]", await File.ReadAllTextAsync(output));
        }
        finally
        {
            File.Delete(output);
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage>? response = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response?.Invoke(request) ?? new HttpResponseMessage(HttpStatusCode.OK));
    }
}
