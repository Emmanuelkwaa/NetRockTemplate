// NETrock project generator.
// Copies the template into a new folder and applies the selected options.
// The template folder is never modified.
//
// Run through ./init.sh or ./init.ps1, or directly:
//   dotnet run tools/init/init.cs -- --name MyApp --yes

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

var templateRoot = Path.GetFullPath(Path.Combine(
    AppContext.GetData("EntryPointFileDirectoryPath") as string ?? Directory.GetCurrentDirectory(), "..", ".."));

return Cli.Run(args, templateRoot);

/// <summary>Option values and flags collected from the command line or prompts.</summary>
sealed class Settings
{
    public string? Name { get; set; }
    public int? Port { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? Output { get; set; }
    public string? Auth { get; set; }
    public string? Deploy { get; set; }
    public string? Database { get; set; }
    public string? Stack { get; set; }
    public string? KeycloakAuthority { get; set; }
    public string? KeycloakClientId { get; set; }
    public string? KeycloakClientSecret { get; set; }
    public bool Yes { get; set; }
    public bool? Migration { get; set; }
    public bool? Commit { get; set; }
    public bool? Aspire { get; set; }

    public int ApiPort => Port!.Value + 2;

    /// <summary>The selected options as one line, for the initial commit message.</summary>
    public string Describe() => HasBackend
        ? $"stack={Stack}, auth={Auth}, deploy={Deploy}, database={Database}"
        : $"stack={Stack}, deploy={Deploy}";
    public string Slug => Regex.Replace(Name!, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();

    /// <summary>True for every stack except <c>blazor</c>, which has no backend API.</summary>
    public bool HasBackend => Stack != "blazor";

    /// <summary>The frontend kind derived from the stack: <c>svelte</c>, <c>blazor</c> or <c>none</c>.</summary>
    public string Frontend => Stack switch
    {
        "api-svelte" => "svelte",
        "api-blazor" or "blazor" => "blazor",
        _ => "none"
    };

    /// <summary>Values that <c>@init:if</c> conditions and manifest rules are evaluated against.</summary>
    public IReadOnlyDictionary<string, string> Options => new Dictionary<string, string>
    {
        ["auth"] = HasBackend ? Auth! : "none",
        ["deploy"] = Deploy!,
        ["database"] = HasBackend ? Database! : "none",
        ["frontend"] = Frontend,
        ["backend"] = HasBackend ? "yes" : "no"
    };
}

static class Cli
{
    /// <summary>Values accepted on the command line and in prompts.</summary>
    public static readonly Dictionary<string, string[]> Choices = new()
    {
        ["stack"] = ["api-svelte", "api-blazor", "api", "blazor"],
        ["auth"] = ["jwt", "keycloak", "none"],
        ["deploy"] = ["aspire", "compose"],
        ["database"] = ["managed", "external"]
    };

    /// <summary>
    /// Keys and values that conditions may use. <c>frontend</c> and <c>backend</c> are derived from the stack,
    /// and <c>blazor</c> projects evaluate with <c>auth=none</c> and <c>database=none</c>.
    /// </summary>
    public static readonly Dictionary<string, string[]> ConditionKeys = new()
    {
        ["auth"] = ["jwt", "keycloak", "none"],
        ["deploy"] = ["aspire", "compose"],
        ["database"] = ["managed", "external", "none"],
        ["frontend"] = ["svelte", "blazor", "none"],
        ["backend"] = ["yes", "no"]
    };

    public static int Run(string[] args, string templateRoot)
    {
        Settings settings;
        try
        {
            settings = Parse(args);
        }
        catch (HelpRequested)
        {
            PrintHelp();
            return 0;
        }
        catch (ArgumentException ex)
        {
            Ui.Error(ex.Message);
            Console.WriteLine("Run with --help for usage.");
            return 1;
        }

        try
        {
            Ui.Header("NETrock project setup");
            Prompts.Complete(settings, templateRoot);
            Prerequisites.Check(settings);
            Ui.Summary(settings);

            if (!settings.Yes && !Ui.Confirm("Proceed?", true))
            {
                Ui.Warn("Aborted");
                return 0;
            }

            var stopwatch = Stopwatch.StartNew();
            var output = Generator.Generate(settings, templateRoot);
            Ui.Header("Done");
            Console.WriteLine($"  Project created at {output} in {stopwatch.Elapsed.TotalSeconds:F0}s");
            NextSteps.Print(settings, output);

            if (settings.Aspire == true)
            {
                Ui.Step("Launching Aspire (Ctrl+C to stop)");
                return Shell.RunInteractive("dotnet", ["run", "--project", Path.Combine("src", "backend", $"{settings.Name}.AppHost")], output);
            }

            return 0;
        }
        catch (InitException ex)
        {
            Ui.Error(ex.Message);
            return 1;
        }
    }

    private static Settings Parse(string[] args)
    {
        var s = new Settings();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string Value()
            {
                if (i + 1 >= args.Length) throw new ArgumentException($"{arg} needs a value");
                return args[++i];
            }

            switch (arg)
            {
                case "-n" or "--name": s.Name = Value(); break;
                case "-p" or "--port":
                    s.Port = int.TryParse(Value(), out var port) ? port : throw new ArgumentException("--port must be a number");
                    break;
                case "-e" or "--email": s.Email = Value(); break;
                case "--password": s.Password = Value(); break;
                case "-o" or "--output": s.Output = Value(); break;
                case "--auth": s.Auth = Choice("auth", Value()); break;
                case "--deploy": s.Deploy = Choice("deploy", Value()); break;
                case "--database": s.Database = Choice("database", Value()); break;
                case "--stack": s.Stack = Choice("stack", Value()); break;
                case "--keycloak-authority": s.KeycloakAuthority = Value(); break;
                case "--keycloak-client-id": s.KeycloakClientId = Value(); break;
                case "--keycloak-client-secret": s.KeycloakClientSecret = Value(); break;
                case "-y" or "--yes": s.Yes = true; break;
                case "--no-migration": s.Migration = false; break;
                case "--no-commit": s.Commit = false; break;
                case "--no-aspire": s.Aspire = false; break;
                case "-h" or "--help": throw new HelpRequested();
                default: throw new ArgumentException($"Unknown option: {arg}");
            }
        }

        return s;
    }

    private static string Choice(string option, string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return Choices[option].Contains(normalized)
            ? normalized
            : throw new ArgumentException($"--{option} must be one of: {string.Join(", ", Choices[option])}");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            NETrock project setup

            Usage:
              ./init.sh                  Interactive mode
              ./init.sh [options]        Prompts only for values not passed

            Project:
              -n, --name NAME            Project name in PascalCase (e.g. MyAwesomeApi)
              -p, --port PORT            Base port (default: 13000). Frontend = PORT, API = PORT + 2
              -o, --output DIR           Output folder (default: ../NAME). Must be empty or missing

            Options:
                  --stack TYPE           api-svelte | api-blazor | api | blazor   (default: api-svelte)
                                           api-svelte  Backend API with the SvelteKit frontend
                                           api-blazor  Backend API with a Blazor SSR frontend (stock .NET template)
                                           api         Backend API only
                                           blazor      Blazor SSR only, no backend
                  --auth MODE            jwt | keycloak | none          (default: jwt, not for blazor)
                  --deploy MODE          aspire | compose               (default: aspire)
                  --database MODE        managed | external             (default: managed, not for blazor)

            JWT:
              -e, --email EMAIL          Superuser email (default: superuser@test.com)
                  --password PASS        Superuser password (default: Superuser123!)

            Keycloak:
                  --keycloak-authority URL      Realm URL, e.g. https://auth.example.com/realms/my-realm
                  --keycloak-client-id ID       Confidential client ID
                  --keycloak-client-secret S    Client secret (stored in user secrets, never in files)

            Behavior:
              -y, --yes                  Accept defaults without prompting
                  --no-migration         Skip creating the initial migration
                  --no-commit            Skip git init and the initial commit
                  --no-aspire            Don't launch Aspire after setup
              -h, --help                 Show this help

            Examples:
              ./init.sh --name MyApi --yes
              ./init.sh -n MyApi --stack api --auth none --deploy compose -y
              ./init.sh -n MySite --stack blazor -y
            """);
    }

    private sealed class HelpRequested : Exception;
}

static class Prompts
{
    public static void Complete(Settings s, string templateRoot)
    {
        while (!IsValidName(s.Name))
        {
            if (s.Name is not null) Ui.Error("Name must start with an uppercase letter and contain only letters and digits (e.g. MyAwesomeApi)");
            if (s.Yes && s.Name is not null) throw new InitException("Invalid --name");
            if (s.Yes) throw new InitException("--name is required with --yes");
            s.Name = Ui.Ask("Project name (PascalCase)", null);
        }

        s.Port ??= s.Yes ? 13000 : AskPort();
        if (s.Port is < 1024 or > 65527) throw new InitException("Port must be between 1024 and 65527");

        s.Stack ??= s.Yes ? "api-svelte" : Ui.Choose("Project type", Cli.Choices["stack"], "api-svelte");
        if (!s.HasBackend)
        {
            if (s.Auth is not null) throw new InitException("--auth does not apply to --stack blazor (it has no backend)");
            if (s.Database is not null) throw new InitException("--database does not apply to --stack blazor (it has no backend)");
        }
        else
        {
            s.Auth ??= s.Yes ? "jwt" : Ui.Choose("Authentication", Cli.Choices["auth"], "jwt");
        }

        s.Deploy ??= s.Yes ? "aspire" : Ui.Choose("Production deployment", Cli.Choices["deploy"], "aspire");
        if (s.HasBackend) s.Database ??= s.Yes ? "managed" : Ui.Choose("PostgreSQL", Cli.Choices["database"], "managed");

        if (s.Auth == "jwt")
        {
            s.Email ??= s.Yes ? "superuser@test.com" : Ui.Ask("Superuser email", "superuser@test.com");
            s.Password ??= s.Yes ? "Superuser123!" : Ui.AskSecret("Superuser password", "Superuser123!");
        }
        else if (s.Email is not null || s.Password is not null)
        {
            Ui.Warn("--email and --password only apply to --auth jwt and are ignored");
            s.Email = s.Password = null;
        }

        if (s.Auth != "keycloak" && (s.KeycloakAuthority ?? s.KeycloakClientId ?? s.KeycloakClientSecret) is not null)
        {
            Ui.Warn("--keycloak-* options only apply to --auth keycloak and are ignored");
            s.KeycloakAuthority = s.KeycloakClientId = s.KeycloakClientSecret = null;
        }

        if (s.Auth == "keycloak")
        {
            s.KeycloakAuthority ??= s.Yes ? "" : Ui.Ask("Keycloak realm URL (e.g. https://auth.example.com/realms/my-realm)", "");
            s.KeycloakClientId ??= s.Yes ? "" : Ui.Ask("Keycloak client ID", "");
            s.KeycloakClientSecret ??= s.Yes ? "" : Ui.AskSecret("Keycloak client secret (leave empty to set later)", "");
            if (s.KeycloakAuthority.Length > 0 &&
                (!Uri.TryCreate(s.KeycloakAuthority, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
            {
                throw new InitException("--keycloak-authority must be an absolute http(s) URL");
            }
        }

        var defaultOutput = Path.GetFullPath(Path.Combine(templateRoot, "..", s.Name!));
        s.Output = Path.GetFullPath(s.Output ?? (s.Yes ? defaultOutput : Ui.Ask("Output folder", defaultOutput)));

        if (s.HasBackend) s.Migration ??= s.Yes || Ui.Confirm("Create the initial database migration?", true);
        else s.Migration = false;
        s.Commit ??= s.Yes || Ui.Confirm("Run git init and commit?", true);
        s.Aspire ??= s.Yes || Ui.Confirm("Launch Aspire after setup?", true);
    }

    private static bool IsValidName(string? name) => name is not null && Regex.IsMatch(name, "^[A-Z][A-Za-z0-9]*$");

    private static int AskPort()
    {
        while (true)
        {
            var value = Ui.Ask("Base port", "13000");
            if (int.TryParse(value, out var port) && port is >= 1024 and <= 65527) return port;
            Ui.Error("Port must be a number between 1024 and 65527");
        }
    }
}

static class Prerequisites
{
    public static void Check(Settings s)
    {
        var required = new List<string> { "git", "dotnet", "docker" };
        if (s.Frontend == "svelte") required.AddRange(["node", "pnpm"]);

        var missing = required.Where(tool => !Shell.Exists(tool)).ToList();
        if (missing.Count > 0)
        {
            var hint = missing.Contains("pnpm") ? " (pnpm is managed via corepack: run 'corepack enable')" : "";
            throw new InitException($"Missing required tools: {string.Join(", ", missing)}{hint}");
        }
    }
}

static class Generator
{
    private const string TemplateName = "NetRockTemplate";
    private const string TemplateSlug = "net-rock-template";

    public static string Generate(Settings s, string templateRoot)
    {
        var output = s.Output!;
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        {
            throw new InitException($"Output folder is not empty: {output}");
        }

        if (IsInside(output, templateRoot))
        {
            throw new InitException("Output folder must be outside the template folder");
        }

        var manifest = Manifest.Load(Path.Combine(templateRoot, "template-options", "manifest.json"));
        var options = s.Options;

        Ui.Step("Copying template");
        var copied = Copy(templateRoot, output, manifest);
        Ui.Sub($"{copied} files copied");

        Ui.Step("Applying options");
        foreach (var rule in manifest.Rules.Where(r => Conditions.Matches(r.When, options)))
        {
            Ui.Sub($"{rule.When}");
            ApplyRule(rule, templateRoot, output);
        }

        var textFiles = TextFiles(output).ToList();
        foreach (var file in textFiles)
        {
            Markers.ApplyToFile(file, options);
        }

        if (s.Auth == "keycloak")
        {
            var keycloak = new Dictionary<string, JsonNode?>();
            if (!string.IsNullOrEmpty(s.KeycloakAuthority)) keycloak["Authentication:Keycloak:Authority"] = s.KeycloakAuthority;
            if (!string.IsNullOrEmpty(s.KeycloakClientId)) keycloak["Authentication:Keycloak:ClientId"] = s.KeycloakClientId;
            if (keycloak.Count > 0)
            {
                var file = $"src/backend/{TemplateName}.WebApi/appsettings.json";
                JsonEdits.Apply(Path.Combine(output, file), new JsonEdit(file, [], keycloak));
            }
        }

        if (s.Frontend == "blazor")
        {
            Ui.Step("Creating the Blazor project (dotnet new blazor)");
            BlazorProject.Create(s, output);
            textFiles = TextFiles(output).ToList();
        }

        Ui.Step("Renaming project");
        Rename(output, s, textFiles);

        Ui.Step("Generating secrets");
        ReplaceSecrets(output, manifest);

        if (s.Frontend == "svelte")
        {
            var envExample = Path.Combine(output, "src", "frontend", ".env.example");
            if (File.Exists(envExample)) File.Copy(envExample, Path.Combine(output, "src", "frontend", ".env.local"));
        }

        Validate(output);

        if (s.HasBackend)
        {
            var appHost = Path.Combine(output, "src", "backend", $"{s.Name}.AppHost");
            Ui.Step("Writing development user secrets");
            WriteUserSecrets(s, appHost);

            Ui.Step("Preparing database migrations");
            Migrations.Regenerate(s, output);
        }

        if (s.Commit == true)
        {
            Ui.Step("Creating git repository");
            Shell.Run("git", ["init", "--quiet"], output);
            Shell.Run("git", ["add", "-A"], output);
            Shell.Run("git", ["-c", "commit.gpgsign=false", "commit", "--quiet", "-m",
                $"chore: initialize {s.Name} from NETrock template\n\nOptions: {s.Describe()}"], output);
        }

        return output;
    }

    private static int Copy(string source, string destination, Manifest manifest)
    {
        var count = 0;
        foreach (var relative in ListTemplateFiles(source))
        {
            if (manifest.IsExcluded(relative)) continue;

            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(Path.Combine(source, relative), target);
            count++;
        }

        return count;
    }

    // Uses git to honor .gitignore (keeps local files such as .env.local out); falls back to a full walk.
    private static IEnumerable<string> ListTemplateFiles(string root)
    {
        if (Directory.Exists(Path.Combine(root, ".git")) && Shell.Exists("git"))
        {
            var result = Shell.Capture("git", ["ls-files", "--cached", "--others", "--exclude-standard", "-z"], root);
            return result.Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Where(path => File.Exists(Path.Combine(root, path)));
        }

        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'));
    }

    private static void ApplyRule(Rule rule, string templateRoot, string output)
    {
        foreach (var pattern in rule.Delete)
        {
            var regex = Glob.ToRegex(pattern);
            var removed = 0;
            foreach (var file in Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories).ToList())
            {
                var relative = Path.GetRelativePath(output, file).Replace('\\', '/');
                if (Glob.MatchesPathOrAncestor(regex, relative))
                {
                    File.Delete(file);
                    removed++;
                }
            }

            if (removed == 0) Ui.Warn($"Delete pattern matched nothing: {pattern}");
        }

        DeleteEmptyDirectories(output);

        if (rule.Overlay is not null)
        {
            var overlayRoot = Path.Combine(templateRoot, "template-options", rule.Overlay);
            if (!Directory.Exists(overlayRoot)) throw new InitException($"Overlay folder missing: {rule.Overlay}");

            foreach (var file in Directory.EnumerateFiles(overlayRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(overlayRoot, file);
                if (Path.GetFileName(relative) == ".DS_Store") continue;
                var target = Path.Combine(output, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: true);
            }
        }

        foreach (var edit in rule.Json)
        {
            JsonEdits.Apply(Path.Combine(output, edit.File), edit);
        }
    }

    private static void Rename(string output, Settings s, List<string> textFiles)
    {
        var replacements = new (Regex Pattern, string Value)[]
        {
            (new Regex(TemplateName), s.Name!),
            (new Regex(TemplateSlug), s.Slug),
            (new Regex(TemplateName.ToLowerInvariant()), s.Name!.ToLowerInvariant()),
            // Ports only after ':', whitespace or a quote, so digits inside hashes never match.
            (new Regex(@"(?<=[:\s`""'])13000(?![0-9A-Za-z])"), s.Port!.Value.ToString()),
            (new Regex(@"(?<=[:\s`""'])13002(?![0-9A-Za-z])"), s.ApiPort.ToString())
        };

        foreach (var file in textFiles.Where(File.Exists).Where(f => !IsLockFile(f)))
        {
            var content = File.ReadAllText(file);
            var updated = replacements.Aggregate(content, (text, r) => r.Pattern.Replace(text, r.Value));
            if (!ReferenceEquals(updated, content) && updated != content) File.WriteAllText(file, updated);
        }

        // Deepest paths first so parent renames do not invalidate child paths.
        var entries = Directory.EnumerateFileSystemEntries(output, "*", SearchOption.AllDirectories)
            .Where(p => Path.GetFileName(p).Contains(TemplateName, StringComparison.Ordinal))
            .OrderByDescending(p => p.Length)
            .ToList();
        foreach (var path in entries)
        {
            var renamed = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileName(path).Replace(TemplateName, s.Name));
            if (Directory.Exists(path)) Directory.Move(path, renamed);
            else File.Move(path, renamed);
        }
    }

    private static bool IsLockFile(string path) =>
        Path.GetFileName(path) is "pnpm-lock.yaml" or "package-lock.json" or "yarn.lock" or "packages.lock.json";

    private static void ReplaceSecrets(string output, Manifest manifest)
    {
        var generated = manifest.Secrets.ToDictionary(secret => secret, _ => RandomSecret(64));
        foreach (var file in TextFiles(output))
        {
            var content = File.ReadAllText(file);
            var updated = generated.Aggregate(content, (text, pair) => text.Replace(pair.Key, pair.Value, StringComparison.Ordinal));
            if (updated != content) File.WriteAllText(file, updated);
        }
    }

    private static void WriteUserSecrets(Settings s, string appHost)
    {
        var secrets = new List<(string Key, string Value)>();
        if (s.Auth == "jwt")
        {
            secrets.Add(("Parameters:seed-user-email", s.Email!));
            secrets.Add(("Parameters:seed-user-password", s.Password!));
        }

        if (s.Auth == "keycloak" && !string.IsNullOrEmpty(s.KeycloakClientSecret))
        {
            secrets.Add(("Parameters:keycloak-client-secret", s.KeycloakClientSecret));
        }

        if (s.Database == "external")
        {
            Ui.Sub("Set the dev connection string with:");
            Ui.Sub($"  dotnet user-secrets set ConnectionStrings:Database \"Host=...;Database=...;Username=...;Password=...\" --project src/backend/{s.Name}.AppHost");
        }

        Shell.Run("dotnet", ["user-secrets", "init", "--project", appHost], appHost);
        foreach (var (key, value) in secrets)
        {
            Shell.Run("dotnet", ["user-secrets", "set", key, value, "--project", appHost], appHost, echoArgs: false);
        }

        Ui.Sub($"{secrets.Count} value(s) stored in AppHost user secrets");
    }

    private static void Validate(string output)
    {
        var problems = new List<string>();
        foreach (var file in TextFiles(output))
        {
            var content = File.ReadAllText(file);
            var relative = Path.GetRelativePath(output, file);
            if (content.Contains("@init:", StringComparison.Ordinal)) problems.Add($"leftover @init: marker in {relative}");
            if (content.Contains(TemplateName, StringComparison.Ordinal)) problems.Add($"leftover {TemplateName} text in {relative}");
        }

        if (problems.Count > 0) throw new InitException("Generated project failed validation:\n  " + string.Join("\n  ", problems));
    }

    public static IEnumerable<string> TextFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}"))
            .Where(IsText);

    private static bool IsText(string path)
    {
        Span<byte> buffer = stackalloc byte[8000];
        using var stream = File.OpenRead(path);
        var read = stream.Read(buffer);
        return !buffer[..read].Contains((byte)0);
    }

    private static void DeleteEmptyDirectories(string root)
    {
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
        }
    }

    private static bool IsInside(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative == "." || (!relative.StartsWith("..") && !Path.IsPathRooted(relative));
    }

    private static string RandomSecret(int length)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return RandomNumberGenerator.GetString(alphabet, length);
    }
}

/// <summary>
/// Creates the Blazor SSR project from the stock <c>dotnet new blazor</c> template, so it matches the
/// installed SDK, then wires it to Aspire. Generated pages are left as they are.
/// </summary>
static class BlazorProject
{
    // Renamed with the rest of the project afterwards.
    private const string ProjectName = "NetRockTemplate.UI";

    private const string ApiClient = """


        // Named client for the backend API. Aspire service discovery resolves "http://api"
        // (services__api__http__0 in own compose). Use IHttpClientFactory.CreateClient("api").
        builder.Services.AddHttpClient("api", client => client.BaseAddress = new Uri("http://api"));
        """;

    public static void Create(Settings s, string output)
    {
        var relative = Path.Combine("src", "frontend", ProjectName);
        Shell.Run("dotnet", ["new", "blazor", "--name", ProjectName, "--output", relative,
            "--interactivity", "None", "--auth", "None", "--no-restore"], output);

        var directory = Path.Combine(output, relative);
        Edit(Path.Combine(directory, "Program.cs"), code =>
        {
            code = Insert(code, "var builder = WebApplication.CreateBuilder(args);", after: true,
                "\n\nbuilder.AddServiceDefaults();" + (s.HasBackend ? ApiClient : ""));
            return Insert(code, "app.Run();", after: false, "app.MapDefaultEndpoints();\n\n");
        });

        Edit(Path.Combine(directory, $"{ProjectName}.csproj"), project => Insert(project, "</Project>", after: false, """
              <ItemGroup>
                <ProjectReference Include="../../backend/NetRockTemplate.ServiceDefaults/NetRockTemplate.ServiceDefaults.csproj" />
              </ItemGroup>


            """));
        Ui.Sub($"{ProjectName} created and connected to ServiceDefaults");
    }

    private static void Edit(string path, Func<string, string> change)
    {
        if (!File.Exists(path)) throw new InitException($"dotnet new blazor did not create {Path.GetFileName(path)}");
        var content = File.ReadAllText(path);
        var newline = content.Contains("\r\n") ? "\r\n" : "\n";
        File.WriteAllText(path, change(content.Replace("\r\n", "\n")).Replace("\n", newline));
    }

    // Fails loudly when the stock template changes shape, instead of producing a half-wired project.
    private static string Insert(string content, string anchor, bool after, string text)
    {
        var index = content.IndexOf(anchor, StringComparison.Ordinal);
        if (index < 0) throw new InitException($"The Blazor template no longer contains '{anchor}'. Update tools/init/init.cs.");
        var at = after ? index + anchor.Length : index;
        return content.Insert(at, text.Replace("\r\n", "\n"));
    }
}

static class Migrations
{
    public static void Regenerate(Settings s, string output)
    {
        var backend = Path.Combine(output, "src", "backend");
        var migrations = Path.Combine(backend, $"{s.Name}.Infrastructure", "Persistence", "Migrations");
        if (Directory.Exists(migrations)) Directory.Delete(migrations, recursive: true);
        Ui.Sub("Cleared template migrations");

        if (s.Migration != true)
        {
            Ui.Warn("No migration created. Create one before running the API:");
            Ui.Sub(Command(s));
            return;
        }

        try
        {
            Shell.Run("dotnet", ["tool", "restore"], output);
            Shell.Run("dotnet", ["build", Path.Combine(backend, $"{s.Name}.WebApi"), "-v", "q", "-nologo"], output);
            Shell.Run("dotnet", ["ef", "migrations", "add", "Initial",
                "--project", Path.Combine(backend, $"{s.Name}.Infrastructure"),
                "--startup-project", Path.Combine(backend, $"{s.Name}.WebApi"),
                "--output-dir", Path.Combine("Persistence", "Migrations"),
                "--no-build"], output);
            Ui.Sub("Migration 'Initial' created");
        }
        catch (InitException ex)
        {
            Ui.Warn($"Migration creation failed: {ex.Message}");
            Ui.Sub($"Fix the error, then run: {Command(s)}");
        }
    }

    private static string Command(Settings s) =>
        $"dotnet ef migrations add Initial --project src/backend/{s.Name}.Infrastructure --startup-project src/backend/{s.Name}.WebApi --output-dir Persistence/Migrations";
}

static class NextSteps
{
    public static void Print(Settings s, string output)
    {
        Console.WriteLine();
        Console.WriteLine($"  cd {output}");
        Console.WriteLine($"  dotnet run --project src/backend/{s.Name}.AppHost");
        Console.WriteLine();

        if (s.Database == "external")
        {
            Ui.Warn("External PostgreSQL: the database needs the pg_trgm extension (used for fuzzy search).");
            Ui.Sub("The migration runs CREATE EXTENSION IF NOT EXISTS pg_trgm. On PostgreSQL 13+ that needs the CREATE privilege");
            Ui.Sub("on the database; older versions need a superuser. Otherwise ask an admin to run it before migrating.");
            Ui.Sub("Migrations do not run automatically against an external database. See docs/deployment.md.");
        }

        if (s.Auth == "keycloak")
        {
            var baseUrl = s.Frontend == "svelte" ? $"http://localhost:{s.Port}" : $"http://localhost:{s.ApiPort}";
            Ui.Warn("Keycloak: create a confidential OpenID Connect client in your realm with:");
            Ui.Sub("Client authentication on, Standard flow on, PKCE method S256");
            Ui.Sub($"Valid redirect URI:  {baseUrl}/api/auth/keycloak/callback");
            Ui.Sub($"Web origin:          {(s.Frontend == "none" ? baseUrl : $"http://localhost:{s.Port}")}");
            Ui.Sub("An audience mapper that adds the client ID to the access token 'aud' claim");
            if (string.IsNullOrEmpty(s.KeycloakAuthority) || string.IsNullOrEmpty(s.KeycloakClientId))
            {
                Ui.Sub($"Then set Authentication:Keycloak:Authority and ClientId in src/backend/{s.Name}.WebApi/appsettings.json");
            }

            if (string.IsNullOrEmpty(s.KeycloakClientSecret))
            {
                Ui.Sub($"Store the secret: dotnet user-secrets set Parameters:keycloak-client-secret <secret> --project src/backend/{s.Name}.AppHost");
            }

            Ui.Sub("Full guide: docs/keycloak.md");
        }

        if (s.Deploy == "compose")
        {
            Ui.Sub("Production: cp .env.example .env, fill it in, then docker compose up -d --build (see docs/deployment.md)");
        }
        else
        {
            Ui.Sub("Production: aspire publish or aspire deploy (see docs/deployment.md)");
        }
    }
}

/// <summary>
/// Evaluates option conditions such as <c>auth=jwt,keycloak frontend=svelte</c> (all terms must match).
/// The term <c>template</c> is always false, for template-only content.
/// </summary>
static class Conditions
{
    public static bool Matches(string condition, IReadOnlyDictionary<string, string> options)
    {
        foreach (var term in condition.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            // "template" marks content that only exists in the template, never in a generated project.
            if (term == "template") return false;

            var negate = term.Contains("!=");
            var parts = term.Split(negate ? "!=" : "=", 2);
            if (parts.Length != 2 || !Cli.ConditionKeys.TryGetValue(parts[0], out var allowed))
            {
                throw new InitException($"Invalid condition term '{term}'");
            }

            var values = parts[1].Split(',');
            var unknown = values.FirstOrDefault(v => !allowed.Contains(v));
            if (unknown is not null) throw new InitException($"Unknown value '{unknown}' for {parts[0]} in '{term}'");

            if (values.Contains(options[parts[0]]) == negate) return false;
        }

        return true;
    }
}

/// <summary>
/// Processes <c>@init:if cond [uncomment]</c> / <c>@init:else</c> / <c>@init:end</c> marker lines.
/// Marker lines are always removed. Inactive branches are dropped. A branch flagged
/// <c>uncomment</c> (<c>@init:if cond uncomment</c> or <c>@init:else uncomment</c>) loses one
/// leading comment prefix (<c>// </c>, <c># </c>, <c>&lt;!-- --&gt;</c>) per line, so the template
/// can carry code for other variants without compiling it.
/// </summary>
static class Markers
{
    private static readonly Regex Marker = new(@"@init:(if|else|end)\b(.*?)(?:-->|\*/)?\s*$");

    public static void ApplyToFile(string path, IReadOnlyDictionary<string, string> options)
    {
        var content = File.ReadAllText(path);
        if (!content.Contains("@init:", StringComparison.Ordinal)) return;

        try
        {
            File.WriteAllText(path, Apply(content, options));
        }
        catch (InitException ex)
        {
            throw new InitException($"{path}: {ex.Message}");
        }
    }

    public static string Apply(string content, IReadOnlyDictionary<string, string> options)
    {
        var newline = content.Contains("\r\n") ? "\r\n" : "\n";
        var lines = content.Split(newline);
        var result = new List<string>(lines.Length);
        var stack = new Stack<Block>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var match = Marker.Match(line);
            if (!match.Success)
            {
                if (stack.All(b => b.Active)) result.Add(stack.Any(b => b.Uncomment) ? Uncomment(line) : line);
                continue;
            }

            var argsText = match.Groups[2].Value.Trim();
            switch (match.Groups[1].Value)
            {
                case "if":
                    var uncomment = argsText == "uncomment" || argsText.EndsWith(" uncomment", StringComparison.Ordinal);
                    var condition = uncomment ? argsText[..^"uncomment".Length].Trim() : argsText;
                    var active = Conditions.Matches(condition, options);
                    stack.Push(new Block(active, uncomment, false));
                    break;
                case "else":
                    if (stack.Count == 0) throw new InitException($"line {i + 1}: @init:else without @init:if");
                    var top = stack.Pop();
                    if (top.SeenElse) throw new InitException($"line {i + 1}: duplicate @init:else");
                    if (argsText is not ("" or "uncomment")) throw new InitException($"line {i + 1}: @init:else takes no condition");
                    stack.Push(new Block(!top.Active, argsText == "uncomment", true));
                    break;
                case "end":
                    if (stack.Count == 0) throw new InitException($"line {i + 1}: @init:end without @init:if");
                    stack.Pop();
                    break;
            }
        }

        if (stack.Count > 0) throw new InitException("unclosed @init:if block");
        return string.Join(newline, CollapseBlankRuns(result));
    }

    // Removed blocks often leave two blank lines next to each other; keep one.
    private static IEnumerable<string> CollapseBlankRuns(List<string> lines)
    {
        var previousBlank = false;
        foreach (var line in lines)
        {
            var blank = line.Trim().Length == 0;
            if (blank && previousBlank) continue;
            previousBlank = blank;
            yield return line;
        }
    }

    private static string Uncomment(string line)
    {
        var xml = Regex.Match(line, @"^(\s*)<!-- ?(.*?) ?-->\s*$");
        if (xml.Success) return xml.Groups[1].Value + xml.Groups[2].Value;

        var match = Regex.Match(line, @"^(\s*)(//|#) ?(.*)$");
        return match.Success ? match.Groups[1].Value + match.Groups[3].Value : line;
    }

    private sealed record Block(bool Active, bool Uncomment, bool SeenElse);
}

static class Glob
{
    public static Regex ToRegex(string pattern)
    {
        var builder = new StringBuilder("^");
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
            {
                builder.Append(".*");
                i++;
                if (i + 1 < pattern.Length && pattern[i + 1] == '/') i++;
            }
            else if (c == '*') builder.Append("[^/]*");
            else if (c == '?') builder.Append("[^/]");
            else builder.Append(Regex.Escape(c.ToString()));
        }

        return new Regex(builder.Append('$').ToString().Replace("/$", "$"));
    }

    public static bool MatchesPathOrAncestor(Regex regex, string relative)
    {
        if (regex.IsMatch(relative)) return true;
        var index = relative.LastIndexOf('/');
        while (index > 0)
        {
            relative = relative[..index];
            if (regex.IsMatch(relative)) return true;
            index = relative.LastIndexOf('/');
        }

        return false;
    }
}

static class JsonEdits
{
    public static void Apply(string path, JsonEdit edit)
    {
        if (!File.Exists(path)) throw new InitException($"JSON edit target missing: {edit.File}");
        var text = File.ReadAllText(path);
        var root = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })
                   ?? throw new InitException($"Empty JSON: {edit.File}");

        foreach (var pointer in edit.Remove)
        {
            var (parent, key) = Resolve(root, pointer, create: false);
            if (parent is null || !parent.Remove(key)) Ui.Warn($"JSON path not found in {edit.File}: {pointer}");
        }

        foreach (var (pointer, value) in edit.Set)
        {
            var (parent, key) = Resolve(root, pointer, create: true);
            parent![key] = value?.DeepClone();
        }

        // Keep the file's indentation style (the frontend's Prettier config uses tabs).
        var tabs = Regex.IsMatch(text, "^\t", RegexOptions.Multiline);
        var json = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            IndentCharacter = tabs ? '\t' : ' ',
            IndentSize = tabs ? 1 : 2,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        File.WriteAllText(path, json + "\n");
    }

    private static (JsonObject? Parent, string Key) Resolve(JsonNode root, string path, bool create)
    {
        var segments = path.Split(':');
        var current = root.AsObject();
        foreach (var segment in segments[..^1])
        {
            if (current[segment] is JsonObject next)
            {
                current = next;
            }
            else if (create)
            {
                var created = new JsonObject();
                current[segment] = created;
                current = created;
            }
            else
            {
                return (null, segments[^1]);
            }
        }

        return (current, segments[^1]);
    }
}

sealed record Rule(string When, string[] Delete, string? Overlay, JsonEdit[] Json);

sealed record JsonEdit(string File, string[] Remove, Dictionary<string, JsonNode?> Set);

sealed class Manifest
{
    public required string[] ExcludeNames { get; init; }
    public required string[] ExcludePaths { get; init; }
    public required string[] Secrets { get; init; }
    public required Rule[] Rules { get; init; }

    public bool IsExcluded(string relative)
    {
        var segments = relative.Split('/');
        if (segments.Any(segment => ExcludeNames.Contains(segment))) return true;
        return ExcludePaths.Any(path => relative == path || relative.StartsWith(path + "/", StringComparison.Ordinal));
    }

    public static Manifest Load(string path)
    {
        if (!File.Exists(path)) throw new InitException($"Manifest not found: {path}");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();

        static string[] Strings(JsonNode? node) => node?.AsArray().Select(n => n!.GetValue<string>()).ToArray() ?? [];

        return new Manifest
        {
            ExcludeNames = Strings(root["excludeNames"]),
            ExcludePaths = Strings(root["excludePaths"]),
            Secrets = Strings(root["secrets"]),
            Rules = root["rules"]!.AsArray().Select(rule => new Rule(
                rule!["when"]!.GetValue<string>(),
                Strings(rule["delete"]),
                rule["overlay"]?.GetValue<string>(),
                rule["json"]?.AsArray().Select(edit => new JsonEdit(
                    edit!["file"]!.GetValue<string>(),
                    Strings(edit["remove"]),
                    edit["set"]?.AsObject().ToDictionary(p => p.Key, p => p.Value) ?? [])).ToArray() ?? [])).ToArray()
        };
    }
}

static class Shell
{
    public static bool Exists(string tool)
    {
        try
        {
            return Capture(OperatingSystem.IsWindows() ? "where" : "which", [tool], Directory.GetCurrentDirectory()).Length > 0;
        }
        catch (InitException)
        {
            return false;
        }
    }

    public static string Capture(string file, string[] args, string cwd)
    {
        var (code, stdout, stderr) = Execute(file, args, cwd);
        if (code != 0) throw new InitException($"{file} {string.Join(' ', args)} failed: {stderr.Trim()}");
        return stdout;
    }

    public static void Run(string file, string[] args, string cwd, bool echoArgs = true)
    {
        var (code, stdout, stderr) = Execute(file, args, cwd);
        if (code != 0)
        {
            var shown = echoArgs ? string.Join(' ', args) : args.FirstOrDefault() ?? "";
            var detail = (stderr.Length > 0 ? stderr : stdout).Trim();
            if (detail.Length > 4000) detail = detail[^4000..];
            throw new InitException($"{file} {shown} failed (exit {code}):\n{detail}");
        }
    }

    public static int RunInteractive(string file, string[] args, string cwd)
    {
        var info = new ProcessStartInfo(file) { WorkingDirectory = cwd };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        process.WaitForExit();
        return process.ExitCode;
    }

    private static (int Code, string Stdout, string Stderr) Execute(string file, string[] args, string cwd)
    {
        var info = new ProcessStartInfo(file)
        {
            WorkingDirectory = cwd,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        info.Environment["DOTNET_NOLOGO"] = "1";

        try
        {
            using var process = Process.Start(info)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            return (process.ExitCode, stdout.Result, stderr.Result);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InitException($"Could not start {file}: {ex.Message}");
        }
    }
}

static class Ui
{
    private static readonly bool Color = !Console.IsOutputRedirected && Environment.GetEnvironmentVariable("NO_COLOR") is null;

    private static string Paint(string code, string text) => Color ? $"\u001b[{code}m{text}\u001b[0m" : text;

    public static void Header(string text) => Console.WriteLine($"\n{Paint("1;36", "== " + text + " ==")}\n");
    public static void Step(string text) => Console.WriteLine(Paint("1", "> " + text));
    public static void Sub(string text) => Console.WriteLine($"    {text}");
    public static void Warn(string text) => Console.WriteLine(Paint("33", "  ! " + text));
    public static void Error(string text) => Console.Error.WriteLine(Paint("31", "  x " + text));

    public static string Ask(string question, string? defaultValue)
    {
        Console.Write(defaultValue is { Length: > 0 } ? $"{question} [{defaultValue}]: " : $"{question}: ");
        var answer = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(answer) ? defaultValue ?? "" : answer;
    }

    public static string AskSecret(string question, string defaultValue)
    {
        Console.Write(defaultValue.Length > 0 ? $"{question} [{new string('*', 8)}]: " : $"{question}: ");
        if (Console.IsInputRedirected)
        {
            var piped = Console.ReadLine()?.Trim();
            return string.IsNullOrEmpty(piped) ? defaultValue : piped;
        }

        var builder = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) break;
            if (key.Key == ConsoleKey.Backspace) { if (builder.Length > 0) builder.Length--; }
            else if (!char.IsControl(key.KeyChar)) builder.Append(key.KeyChar);
        }

        Console.WriteLine();
        return builder.Length == 0 ? defaultValue : builder.ToString();
    }

    public static string Choose(string question, string[] choices, string defaultValue)
    {
        while (true)
        {
            var answer = Ask($"{question} ({string.Join("/", choices)})", defaultValue).ToLowerInvariant();
            if (choices.Contains(answer)) return answer;
            Error($"Choose one of: {string.Join(", ", choices)}");
        }
    }

    public static bool Confirm(string question, bool defaultValue)
    {
        var answer = Ask($"{question} ({(defaultValue ? "Y/n" : "y/N")})", "").ToLowerInvariant();
        return answer.Length == 0 ? defaultValue : answer.StartsWith('y');
    }

    public static void Summary(Settings s)
    {
        Header("Summary");
        Console.WriteLine($"  Name:        {s.Name} (slug {s.Slug})");
        Console.WriteLine($"  Stack:       {s.Stack}");
        Console.WriteLine(s.HasBackend ? $"  Ports:       frontend {s.Port}, API {s.ApiPort}" : $"  Port:        {s.Port}");
        if (s.HasBackend) Console.WriteLine($"  Auth:        {s.Auth}");
        Console.WriteLine($"  Deploy:      {s.Deploy}");
        if (s.HasBackend) Console.WriteLine($"  Database:    {s.Database}");
        if (s.Auth == "jwt") Console.WriteLine($"  Superuser:   {s.Email}");
        if (s.Auth == "keycloak") Console.WriteLine($"  Keycloak:    {(string.IsNullOrEmpty(s.KeycloakAuthority) ? "(set later)" : s.KeycloakAuthority)}");
        Console.WriteLine($"  Output:      {s.Output}");
        var migration = s.HasBackend ? $"Migration:   {(s.Migration == true ? "yes" : "no")}, " : "";
        Console.WriteLine($"  {migration}git: {(s.Commit == true ? "yes" : "no")}, launch Aspire: {(s.Aspire == true ? "yes" : "no")}");
        Console.WriteLine();
    }
}

sealed class InitException(string message) : Exception(message);
