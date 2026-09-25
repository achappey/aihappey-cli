using System.Runtime.InteropServices;
using AIHappey.Cli.Core;
using Xunit;

namespace AIHappey.Cli.Tests;

public sealed class LocalHeaderConfigurationTests
{
    [Fact]
    public void ResolvesWindowsPath()
    {
        var path = LocalHeaderConfiguration.ResolvePath(OSPlatform.Windows, @"C:\Users\test\AppData\Local", @"C:\Users\test", null);

        Assert.Equal(Path.Combine(@"C:\Users\test\AppData\Local", "aihappey", "headers.json"), path);
    }

    [Fact]
    public void ResolvesMacOsPath()
    {
        var path = LocalHeaderConfiguration.ResolvePath(OSPlatform.OSX, null, "/Users/test", null);

        Assert.Equal(Path.Combine("/Users/test", "Library", "Application Support", "aihappey", "headers.json"), path);
    }

    [Fact]
    public void ResolvesLinuxXdgPath()
    {
        var path = LocalHeaderConfiguration.ResolvePath(OSPlatform.Linux, null, "/home/test", "/custom/config");

        Assert.Equal(Path.Combine("/custom/config", "aihappey", "headers.json"), path);
    }

    [Fact]
    public void ResolvesLinuxFallbackPath()
    {
        var path = LocalHeaderConfiguration.ResolvePath(OSPlatform.Linux, null, "/home/test", null);

        Assert.Equal(Path.Combine("/home/test", ".config", "aihappey", "headers.json"), path);
    }

    [Fact]
    public async Task MissingFileReturnsEmptyHeaders()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "headers.json");

        var headers = await new LocalHeaderConfiguration(path).LoadAsync(CancellationToken.None);

        Assert.Empty(headers);
    }

    [Fact]
    public async Task LoadsRootObjectAsCaseInsensitiveHeaders()
    {
        var path = await WriteTemporaryFileAsync("""{"X-Api-Key":"secret","Authorization":"Bearer token"}""");
        try
        {
            var headers = await new LocalHeaderConfiguration(path).LoadAsync(CancellationToken.None);

            Assert.Equal("secret", headers["x-api-key"]);
            Assert.Equal("Bearer token", headers["AUTHORIZATION"]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{\"X-Api-Key\":42}")]
    public async Task InvalidFileFailsWithPath(string contents)
    {
        var path = await WriteTemporaryFileAsync(contents);
        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => new LocalHeaderConfiguration(path).LoadAsync(CancellationToken.None));

            Assert.Contains(path, exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<string> WriteTemporaryFileAsync(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"aihappey-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, contents);
        return path;
    }
}
