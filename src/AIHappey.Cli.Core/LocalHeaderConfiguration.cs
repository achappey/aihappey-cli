using System.Runtime.InteropServices;
using System.Text.Json;

namespace AIHappey.Cli.Core;

public interface ILocalHeaderConfiguration
{
    Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken cancellationToken);
}

public sealed class LocalHeaderConfiguration : ILocalHeaderConfiguration
{
    private readonly string path;

    public LocalHeaderConfiguration()
        : this(GetDefaultPath())
    {
    }

    public LocalHeaderConfiguration(string path)
    {
        this.path = path;
    }

    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("The root value must be a JSON object.");

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String)
                    throw new JsonException($"Header '{property.Name}' must have a string value.");
                headers[property.Name] = property.Value.GetString()!;
            }
            return headers;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException)
        {
            throw new InvalidOperationException($"Failed to load local HTTP headers from '{path}': {exception.Message}", exception);
        }
    }

    public static string GetDefaultPath()
    {
        if (OperatingSystem.IsWindows())
        {
            return ResolvePath(
                OSPlatform.Windows,
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                null);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return ResolvePath(
            OperatingSystem.IsMacOS() ? OSPlatform.OSX : OSPlatform.Linux,
            null,
            home,
            Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"));
    }

    public static string ResolvePath(
        OSPlatform platform,
        string? localApplicationData,
        string userProfile,
        string? xdgConfigHome)
    {
        string configDirectory;
        if (platform == OSPlatform.Windows)
        {
            configDirectory = string.IsNullOrWhiteSpace(localApplicationData)
                ? throw new InvalidOperationException("The local application-data directory is unavailable.")
                : localApplicationData;
        }
        else if (platform == OSPlatform.OSX)
        {
            configDirectory = Path.Combine(userProfile, "Library", "Application Support");
        }
        else
        {
            configDirectory = string.IsNullOrWhiteSpace(xdgConfigHome)
                ? Path.Combine(userProfile, ".config")
                : xdgConfigHome;
        }

        return Path.Combine(configDirectory, "aihappey", "headers.json");
    }
}
