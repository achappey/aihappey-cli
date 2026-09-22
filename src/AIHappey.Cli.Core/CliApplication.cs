using System.CommandLine;
using System.CommandLine.Invocation;

namespace AIHappey.Cli.Core;

public static class CliApplication
{
    public static Task<int> RunAsync(
        string[] args,
        DeploymentProfile profile,
        HttpClient? httpClient = null,
        IAzureAuthenticationState? authenticationState = null,
        IAzureTokenProvider? azureTokenProvider = null)
    {
        var root = BuildRootCommand(profile, httpClient, authenticationState, azureTokenProvider);
        return root.Parse(args).InvokeAsync();
    }

    public static RootCommand BuildRootCommand(
        DeploymentProfile profile,
        HttpClient? httpClient = null,
        IAzureAuthenticationState? authenticationState = null,
        IAzureTokenProvider? azureTokenProvider = null)
    {
        var root = new RootCommand("Thin client for the AIHappey AI and Agents HTTP APIs.");
        var ai = new Command("ai", "Call the AI gateway.");
        var agents = new Command("agents", "Call the Agents API.");
        root.Subcommands.Add(ai);
        root.Subcommands.Add(agents);

        foreach (var endpoint in EndpointCatalog.All)
            AddEndpoint(endpoint.Area == ApiArea.Ai ? ai : agents, endpoint, profile, httpClient, azureTokenProvider);

        AddAuthenticationCommands(root, profile, authenticationState);

        return root;
    }

    private static void AddEndpoint(
        Command area,
        EndpointDescriptor endpoint,
        DeploymentProfile profile,
        HttpClient? httpClient,
        IAzureTokenProvider? azureTokenProvider)
    {
        var segments = endpoint.Key.Split('.')[1..];
        var parent = area;
        for (var index = 0; index < segments.Length; index++)
        {
            var name = segments[index];
            var existing = parent.Subcommands.FirstOrDefault(command => command.Name == name);
            if (existing is null)
            {
                existing = new Command(name);
                parent.Subcommands.Add(existing);
            }
            parent = existing;
        }

        ConfigureLeaf(parent, endpoint, profile, httpClient, azureTokenProvider);
    }

    private static void ConfigureLeaf(
        Command command,
        EndpointDescriptor endpoint,
        DeploymentProfile profile,
        HttpClient? httpClient,
        IAzureTokenProvider? azureTokenProvider)
    {
        command.Description = $"{endpoint.Method} /{endpoint.Route}";

        var model = new Option<string?>("--model") { Description = "Model or agent identifier." };
        var input = new Option<string?>("--input") { Description = "Simple input value overlaid onto the JSON body." };
        var prompt = new Option<string?>("--prompt") { Description = "Simple prompt value overlaid onto the JSON body." };
        var json = new Option<string?>("--json") { Description = "Raw JSON request object." };
        var jsonFile = new Option<string?>("--json-file") { Description = "Read the raw JSON request object from a file." };
        var stdin = new Option<bool>("--stdin") { Description = "Read the raw JSON request object from stdin." };
        var files = new Option<string[]>("--file") { Description = "Media file. Repeat for multiple files. Multipart commands accept field=path." };
        files.Arity = ArgumentArity.ZeroOrMore;
        var url = new Option<string?>("--url") { Description = "Override the compiled API base URL when allowed by the deployment profile." };
        var headers = new Option<string[]>("--header") { Description = "Additional 'Name: Value' HTTP header. Repeatable." };
        headers.Arity = ArgumentArity.ZeroOrMore;
        var bearer = new Option<string?>("--bearer") { Description = "Explicit bearer token when allowed by the deployment profile." };
        var azure = new Option<bool>("--azure") { Description = "Override the compiled Azure credential mode with DefaultAzureCredential." };
        var azureLogin = new Option<bool>("--azure-login") { Description = "Override the compiled Azure credential mode with persistent interactive browser login." };
        var scopes = new Option<string[]>("--scope") { Description = "Azure/Entra scope override. Repeatable when allowed." };
        scopes.Arity = ArgumentArity.ZeroOrMore;
        var stream = new Option<bool>("--stream") { Description = "Set stream=true in the JSON request." };
        var output = new Option<string?>("--output") { Description = "Write the response body to a file instead of stdout." };
        var queries = new Option<string[]>("--query") { Description = "Query parameter as name=value. Repeatable." };
        queries.Arity = ArgumentArity.ZeroOrMore;

        foreach (var option in new Option[] { model, input, prompt, json, jsonFile, stdin, files, url, headers, bearer, azure, azureLogin, scopes, stream, output, queries })
            command.Options.Add(option);

        var routeArguments = new Dictionary<string, Argument<string>>(StringComparer.Ordinal);
        foreach (var parameter in endpoint.RouteParameters ?? [])
        {
            var argument = new Argument<string>(parameter) { Description = $"Route value for {parameter}." };
            command.Arguments.Add(argument);
            routeArguments.Add(parameter, argument);
        }

        command.SetAction(async (ParseResult parseResult, CancellationToken cancellationToken) =>
        {
            try
            {
                var body = endpoint.Encoding == RequestEncoding.None
                    ? null
                    : await RequestBodyComposer.ComposeAsync(
                        parseResult.GetValue(json),
                        parseResult.GetValue(jsonFile),
                        parseResult.GetValue(stdin),
                        parseResult.GetValue(model),
                        parseResult.GetValue(input),
                        parseResult.GetValue(prompt),
                        parseResult.GetValue(stream),
                        cancellationToken);

                var fileValues = parseResult.GetValue(files) ?? [];
                if (body is not null && endpoint.Encoding == RequestEncoding.Json)
                    RequestBodyComposer.AddMediaFiles(body, fileValues);

                var invocation = new ApiInvocation(
                    endpoint,
                    body,
                    routeArguments.ToDictionary(item => item.Key, item => parseResult.GetValue(item.Value)!),
                    ParsePairs(parseResult.GetValue(queries) ?? [], "--query", '='),
                    parseResult.GetValue(headers) ?? [],
                    fileValues,
                    parseResult.GetValue(url),
                    parseResult.GetValue(bearer),
                    parseResult.GetValue(azure),
                    parseResult.GetValue(azureLogin),
                    parseResult.GetValue(scopes) ?? [],
                    parseResult.GetValue(output));

                var client = new HttpApiClient(
                    profile,
                    httpClient ?? SharedHttpClient.Instance,
                    RequestAuthenticatorFactory.Create(profile, azureTokenProvider));
                return await client.SendAsync(invocation, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return 130;
            }
            catch (Exception exception)
            {
                await Console.Error.WriteLineAsync(exception.Message);
                return 1;
            }
        });
    }

    private static void AddAuthenticationCommands(
        RootCommand root,
        DeploymentProfile profile,
        IAzureAuthenticationState? authenticationState)
    {
        if (profile.Authentication != AuthenticationMode.Azure)
            return;

        var auth = new Command("auth", "Manage this CLI deployment's persisted Azure sign-in.");
        var logout = new Command("logout", "Remove this CLI deployment's local Azure account and token cache.");
        logout.SetAction(async (_, cancellationToken) =>
        {
            try
            {
                await (authenticationState ?? new AzureAuthenticationState(profile)).ClearAsync(cancellationToken);
                await Console.Out.WriteLineAsync("Signed out of this AIHappey CLI deployment.");
                return 0;
            }
            catch (OperationCanceledException)
            {
                return 130;
            }
            catch (Exception exception)
            {
                await Console.Error.WriteLineAsync(exception.Message);
                return 1;
            }
        });
        auth.Subcommands.Add(logout);
        root.Subcommands.Add(auth);
    }

    private static class SharedHttpClient
    {
        internal static readonly HttpClient Instance = new();
    }

    private static IReadOnlyDictionary<string, string> ParsePairs(IEnumerable<string> values, string option, char separator)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            var index = value.IndexOf(separator);
            if (index <= 0)
                throw new ArgumentException($"{option} values must use name{separator}value syntax.");
            result[value[..index]] = value[(index + 1)..];
        }
        return result;
    }
}
