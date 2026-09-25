# aihappey cli

A deliberately thin .NET 10 CLI over the existing aihappey AI and Agents HTTP APIs:

```text
CLI input -> existing HTTP API -> output
```

It contains no provider, model, runtime, or agent logic. All endpoint commands, request composition, HTTP handling, streaming, media handling, and output behavior live in the shared `AIHappey.Cli.Core` project. The executable project is only a composition root.

## Compile-time deployment profiles

Authentication mode and deployment identity are selected at build, publish, or pack time through an MSBuild profile. They are not selected by an end user on every command.

Generic examples are provided at:

- `profiles/header-auth/sample.props`
- `profiles/azure-entra/sample.props`

Copy a sample to a deployment-controlled location before adding real URLs, scopes, headers, package names, or executable names. Do not commit secrets. The profile controls:

- global-tool package ID and executable command name;
- AI and Agents base URLs;
- `Header` or `Azure` authentication strategy;
- fixed request headers;
- Azure tenant, client, AI scope, and Agents scope;
- the default Azure credential flow (`InteractiveBrowser` or `DefaultAzure`) and a deployment-specific token-cache name;
- whether runtime URL, header, bearer, interactive-login, and scope overrides are allowed.

The build validates that a profile exists and that its required fields are present. The selected settings are compiled into assembly metadata and loaded as an immutable `DeploymentProfile` at startup.

## Verified local workflow

The repository uses .NET SDK 10.0.204 via `global.json`.

In PowerShell, from the repository root:

```powershell
cd .\aihappey-cli
$headerProfile = (Resolve-Path .\profiles\header-auth\sample.props).Path
$azureProfile = (Resolve-Path .\profiles\azure-entra\sample.props).Path
```

Build the header variant:

```powershell
dotnet build .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile"
```

Build the Azure/Entra variant:

```powershell
dotnet build .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$azureProfile"
```

Run tests, which do not need live services or credentials:

```powershell
dotnet test .\tests\AIHappey.Cli.Tests\AIHappey.Cli.Tests.csproj
```

Inspect the generated command tree:

```powershell
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile" -- --help
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile" -- ai --help
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile" -- agents responses --help
```

For local header-auth development, the repository also includes `run-local.ps1`, which supplies the sample profile automatically:

```powershell
.\run-local.ps1 --help
.\run-local.ps1 ai responses create --model openai/gpt-5.6-luna --input "Say hello" --bearer $env:OPENAI_API_KEY
```

The equivalent command without the helper is:

```powershell
$profile = (Resolve-Path .\profiles\header-auth\sample.props).Path
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$profile" -- ai responses create --model openai/gpt-5.6-luna --input "Say hello" --bearer $env:OPENAI_API_KEY
```

`-p:CliProfile=...` is an MSBuild option and must appear before the `--` separator. Everything after `--` is passed to the CLI. In PowerShell, set a real token in an environment variable; do not type placeholder values such as `<your-key>`, because `<` and `>` are parsed as redirection operators.

### Local backends

The existing header-auth AI sample runs at `http://localhost:5015`:

```powershell
dotnet run --project ..\aihappey-ai\Samples\AIHappey.HeaderAuth\AIHappey.HeaderAuth.csproj
```

For a quick call, the generic header profile permits a base-URL override:

```powershell
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile" -- ai models list --url http://localhost:5015/
```

For your sample Responses request against that local backend:

```powershell
.\run-local.ps1 ai responses create --url http://localhost:5015/ --model openai/gpt-5.6-luna --input "Say hello" --bearer $env:OPENAI_API_KEY
```

For repeatable local integration testing, copy the header profile outside the repository and compile the local URLs into that copy instead.

## Common input and transport options

All endpoint commands share the same thin transport options where applicable:

- `--model`
- `--input`
- `--prompt`
- `--json '<object>'`
- `--json-file path.json`
- `--stdin`
- repeatable `--file path` or multipart `--file field=path`
- repeatable `--header 'Name: Value'`
- `--bearer token`
- `--url base-url`
- repeatable `--query name=value`
- `--stream`
- `--output path`
- `--azure`
- `--azure-login`
- repeatable `--scope scope`

The compiled profile can disable security-sensitive overrides. Azure authentication happens automatically in an Azure-compiled executable. `--azure` explicitly overrides the compiled choice with `DefaultAzureCredential`; `--azure-login` explicitly overrides it with persistent `InteractiveBrowserCredential`. The two overrides cannot be combined.

### Per-user default headers

Executables compiled for `Header` authentication can read default HTTP headers from a per-user `headers.json` file when the deployment profile enables header overrides:

- Windows: `%LOCALAPPDATA%\aihappey\headers.json`
- macOS: `~/Library/Application Support/aihappey/headers.json`
- Linux: `${XDG_CONFIG_HOME:-~/.config}/aihappey/headers.json`

The file is a JSON object whose property names and string values are arbitrary HTTP header names and values. The CLI does not interpret provider names or API-key formats. For example:

```json
{
  "X-Api-Key": "your-secret-value",
  "X-Tenant": "your-tenant"
}
```

Header precedence is compiled fixed headers, then per-user `headers.json`, then explicit `--header` values. Matching is case-insensitive, so an explicit `--header "x-api-key: temporary-value"` overrides `X-Api-Key` from the local file. If the file is absent, behavior is unchanged. If it exists but cannot be read, is malformed, is not a JSON object, or contains a non-string value, the command fails with an error that identifies the file path.

The local file is never read or applied by Azure/Entra builds, and it is also ignored when the compiled profile disables header overrides. Values are stored as plaintext: restrict the file and containing directory to the current OS user, and do not commit the file to source control. After configuring it, callers such as Zoo Code can invoke normal endpoint commands without putting secrets in command-line arguments:

```powershell
aihappey ai responses create --model openai/gpt-5.4-mini --input "Say hi"
```

### Persistent Azure browser login

Set these values in an Azure profile to make browser login the build-time default:

```xml
<CliAzureCredentialMode>InteractiveBrowser</CliAzureCredentialMode>
<CliTokenCacheName>your-company-your-deployment</CliTokenCacheName>
<CliAllowInteractiveLogin>true</CliAllowInteractiveLogin>
```

The first API command opens the browser. The Azure Identity token cache is then encrypted for the current OS user and reused across later CLI processes and shell sessions. The small serialized account record is stored under the current user's local application-data directory; it contains account metadata needed to select the cached account, not access or refresh tokens. A browser can open again when consent or conditional-access policy requires it, the refresh token expires, or the cache is removed.

Use the compiled default without adding an authentication option:

```powershell
aihappey ai models list
aihappey ai responses create --model openai/gpt-5.4-mini --input "Say hi"
```

Remove only this compiled CLI deployment's persisted sign-in:

```powershell
aihappey auth logout
```

This does not sign out Azure CLI, the browser, or other applications. Encrypted persistence requires the platform's protected credential storage (Windows DPAPI, macOS Keychain, or Linux Secret Service/keyring); the CLI does not opt into plaintext cache fallback.

Only one of `--json`, `--json-file`, and `--stdin` may be used. Convenience values overlay the raw JSON object, so every backend property remains available without adding a dedicated CLI option.

## Examples

Header-auth Responses call:

```powershell
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile" -- ai responses create --model openai/gpt-5.4-mini --input "Say hi" --bearer $env:OPENAI_API_KEY
```

Raw JSON stream:

```powershell
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile" -- ai responses create --bearer $env:OPENAI_API_KEY --json '{"model":"openai/gpt-5.4-mini","input":"Say hi","stream":true}'
```

JSON through stdin:

```powershell
'{"model":"openai/gpt-5.4-mini","input":"Say hi"}' | dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile" -- ai responses create --bearer $env:OPENAI_API_KEY --stdin
```

Provider-specific header:

```powershell
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile" -- ai responses create --model openai/gpt-5.4-mini --input "Say hi" --header "X-OpenAI-Key: $env:OPENAI_API_KEY"
```

Multipart transcription:

```powershell
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile" -- ai audio transcriptions create --model openai/whisper-1 --file .\sample.mp3 --bearer $env:OPENAI_API_KEY
```

Binary speech output:

```powershell
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile" -- ai audio speech create --json-file .\speech-request.json --output .\speech.mp3 --bearer $env:OPENAI_API_KEY
```

Agents response lifecycle:

```powershell
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile" -- agents responses create --model OpenAIAgent --input "Say hi" --bearer $env:OPENAI_API_KEY
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile" -- agents responses list --bearer $env:OPENAI_API_KEY
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile" -- agents responses get response-id --bearer $env:OPENAI_API_KEY
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$headerProfile" -- agents responses delete response-id --bearer $env:OPENAI_API_KEY
```

Azure/Entra with the compiled scope and default credential chain:

```powershell
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$azureProfile" -- ai models list
```

Azure/Entra browser login:

```powershell
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$azureProfile" -- ai models list --azure-login
```

When the profile's `CliAzureCredentialMode` is `InteractiveBrowser`, the equivalent normal command is shorter and reuses the persisted login:

```powershell
dotnet run --project .\src\AIHappey.Cli\AIHappey.Cli.csproj -p:CliProfile="$azureProfile" -- ai models list
```

## Publish and global-tool packaging

Publish a profile-bound executable:

```powershell
dotnet publish .\src\AIHappey.Cli\AIHappey.Cli.csproj -c Release -p:CliProfile="$headerProfile" -o .\artifacts\header
.\artifacts\header\aihappey.exe --help
```

Pack the profile-bound .NET tool:

```powershell
dotnet pack .\src\AIHappey.Cli\AIHappey.Cli.csproj -c Release -p:CliProfile="$headerProfile"
dotnet tool install AIHappey.Cli.Header.Sample --tool-path .\artifacts\tools --add-source .\artifacts\packages
.\artifacts\tools\aihappey.exe --help
```

Deployment builds should replace both the sample package ID and command name in their external profile.

## Endpoint parity matrix

| Commands | HTTP API |
| --- | --- |
| `ai models list` | `GET /v1/models` |
| `ai chat create` | `POST /api/chat` |
| `ai chat completions create` | `POST /v1/chat/completions` |
| `ai responses create` | `POST /v1/responses` |
| `ai messages create` | `POST /v1/messages` |
| `ai embeddings create` | `POST /api/embeddings` |
| `ai embeddings openai create` | `POST /v1/embeddings` |
| `ai images create` | `POST /api/images` |
| `ai images generations create` | `POST /v1/images/generations` |
| `ai images edits create` | `POST /v1/images/edits` |
| `ai speech create` | `POST /api/speech` |
| `ai audio speech create` | `POST /v1/audio/speech` |
| `ai transcriptions create` | `POST /api/transcriptions` |
| `ai transcriptions stream` | `POST /api/transcriptions/stream` |
| `ai audio transcriptions create` | `POST /v1/audio/transcriptions` |
| `ai videos create` | `POST /api/videos` |
| `ai videos status PROVIDER TASK` | `GET /api/videos/{providerId}/{taskId}` |
| `ai rerank create` | `POST /api/rerank` |
| `ai generate create` | `POST /api/generate` |
| `ai realtime client-secrets create` | `POST /v1/realtime/client_secrets` |
| `ai skills list` | `GET /v1/skills` |
| `ai skills versions list PROVIDER SKILL` | `GET /v1/skills/{providerId}/{skillId}/versions` |
| `ai skills content get PROVIDER SKILL` | `GET /v1/skills/{providerId}/{skillId}/content` |
| `ai skills version-content get PROVIDER SKILL VERSION` | `GET /v1/skills/{providerId}/{skillId}/versions/{version}/content` |
| `ai callbacks create PROVIDER` | `POST /api/callbacks/{provider}` |
| `agents models list` | `GET /v1/models` |
| `agents chat create` | `POST /api/chat` |
| `agents responses create` | `POST /v1/responses` |
| `agents responses list` | `GET /v1/responses` |
| `agents responses get RESPONSE` | `GET /v1/responses/{responseId}` |
| `agents responses delete RESPONSE` | `DELETE /v1/responses/{responseId}` |

Streaming and binary responses are copied directly without interpreting provider or agent events. HTTP failure response bodies are preserved and the CLI returns a nonzero exit code.

