using release_builder.Model;
using release_builder.Models;
using release_builder.Services;
using System.Diagnostics;
using System.Text.Json;

namespace release_builder;

public class Program
{
    private const string AppFolderName = "release-builder";
    private const string ConfigFileName = "appsettings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions TemplateWriteOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<int> Main(string[] args)
    {
        var parsedArgs = ParseArgs(args);

        if (parsedArgs.ShowConfigPath)
        {
            Console.WriteLine(GetDefaultConfigPath());
            return 0;
        }

        if (parsedArgs.Version is null)
        {
            PrintUsage();
            return 1;
        }

        ConsoleLogger.Header($"Release Builder — version {parsedArgs.Version}");

        var configPath = parsedArgs.ConfigPath ?? GetDefaultConfigPath();
        var config = LoadConfig(configPath, parsedArgs.ConfigPath is not null);

        if (config is null)
        {
            return 2;
        }

        if (!Directory.Exists(config.RootPath))
        {
            ConsoleLogger.Error($"Root path does not exist: {config.RootPath}");
            return 1;
        }

        ConsoleLogger.Info($"Root path: {config.RootPath}");
        ConsoleLogger.Info($"Repositories: {config.Repositories.Count}");
        ConsoleLogger.Info($"Target branch: release/{parsedArgs.Version}");
        ConsoleLogger.Info($"Stop on error: {config.StopOnError}");
        ConsoleLogger.Info($"Config loaded from: {configPath}");

        var gitService = new GitService();
        var buildService = new BuildService();
        var results = new List<BuildResult>();
        var totalStopwatch = Stopwatch.StartNew();

        for (var i = 0; i < config.Repositories.Count; i++)
        {
            var repo = config.Repositories[i];
            var repoPath = Path.Combine(config.RootPath, repo.Name);
            var solutionPath = Path.Combine(repoPath, repo.SolutionFile);

            ConsoleLogger.SubHeader($"[{i + 1}/{config.Repositories.Count}] {repo.Name}");

            var result = await ProcessRepositoryAsync(
                gitService, buildService, repo, repoPath, solutionPath, parsedArgs.Version);

            results.Add(result);

            if (!result.IsSuccess && config.StopOnError)
            {
                ConsoleLogger.Error("Stopping due to StopOnError=true");
                break;
            }
        }

        totalStopwatch.Stop();

        PrintReport(results, totalStopwatch.Elapsed);

        return results.All(r => r.IsSuccess) ? 0 : 1;
    }

    private static async Task<BuildResult> ProcessRepositoryAsync(
        GitService gitService,
        BuildService buildService,
        RepositoryEntry repo,
        string repoPath,
        string solutionPath,
        string version)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new BuildResult { RepositoryName = repo.Name };

        if (!Directory.Exists(repoPath))
        {
            result.ErrorMessage = $"Repository folder not found: {repoPath}";
            ConsoleLogger.Error(result.ErrorMessage);
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            return result;
        }

        ConsoleLogger.Info("Checking working tree status...");
        var dirtyCheck = await gitService.CheckForDirtyWorkingTreeAsync(repoPath);

        if (!dirtyCheck.Success)
        {
            var stash = await gitService.StashWorkTree(repoPath);
        }

        ConsoleLogger.Info("Fetching from origin...");
        var fetchResult = await gitService.FetchAsync(repoPath);

        if (!fetchResult.Success)
        {
            result.ErrorMessage = $"Git fetch failed: {fetchResult.Output}";
            ConsoleLogger.Error(result.ErrorMessage);
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            return result;
        }

        ConsoleLogger.Info($"Checking out release/{version}...");
        var checkoutResult = await gitService.CheckoutBranchAsync(repoPath, version);

        if (!checkoutResult.Success)
        {
            result.ErrorMessage = $"Checkout failed: {checkoutResult.Output}";
            ConsoleLogger.Error(result.ErrorMessage);
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            return result;
        }

        ConsoleLogger.Info("Pulling latest changes...");
        var pullResult = await gitService.PullAsync(repoPath, version);

        if (!pullResult.Success)
        {
            result.ErrorMessage = $"Git pull failed: {pullResult.Output}";
            ConsoleLogger.Error(result.ErrorMessage);
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            return result;
        }

        result.GitSuccess = true;
        ConsoleLogger.Success("Git operations completed");

        ConsoleLogger.Info($"Rebuilding {repo.SolutionFile}...");
        var buildResult = await buildService.RebuildAsync(solutionPath);

        if (!buildResult.Success)
        {
            result.ErrorMessage = $"Build failed: {buildResult.Output}";
            ConsoleLogger.Error(result.ErrorMessage);
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            return result;
        }

        result.BuildSuccess = true;
        ConsoleLogger.Success($"Build succeeded");

        stopwatch.Stop();
        result.Duration = stopwatch.Elapsed;
        return result;
    }

    private record CliArgs(string? Version, string? ConfigPath, bool ShowConfigPath);

    private static CliArgs ParseArgs(string[] args)
    {
        string? version = null;
        string? configPath = null;
        var showConfigPath = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--version" or "-v" when i + 1 < args.Length:
                    version = args[++i];
                    break;
                case "--config" or "-c" when i + 1 < args.Length:
                    configPath = args[++i];
                    break;
                case "--config-path":
                    showConfigPath = true;
                    break;
                default:
                    if (version is null && !arg.StartsWith('-'))
                    {
                        version = arg;
                    }
                    break;
            }
        }

        return new CliArgs(version, configPath, showConfigPath);
    }

    private static string GetDefaultConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, AppFolderName, ConfigFileName);
    }

    private static BuildConfig? LoadConfig(string configPath, bool configPathExplicit)
    {
        if (!File.Exists(configPath))
        {
            if (configPathExplicit)
            {
                ConsoleLogger.Error($"Config file not found at: {configPath}");
                return null;
            }

            CreateTemplateConfig(configPath);
            ConsoleLogger.Warning($"Config criada em: {configPath}");
            ConsoleLogger.Warning("Edite o arquivo com seus repositórios e rode novamente.");
            return null;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<BuildConfig>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            ConsoleLogger.Error($"Failed to parse config: {ex.Message}");
            return null;
        }
    }

    private static void CreateTemplateConfig(string configPath)
    {
        var directory = Path.GetDirectoryName(configPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var template = new BuildConfig
        {
            RootPath = "C:\\Repos",
            StopOnError = false,
            Repositories =
            [
                new RepositoryEntry { Name = "Core.Library", SolutionFile = "Core.Library.sln" },
                new RepositoryEntry { Name = "Shared.Services", SolutionFile = "Shared.Services.sln" },
                new RepositoryEntry { Name = "Main.WebApp", SolutionFile = "Main.WebApp.sln" }
            ]
        };

        File.WriteAllText(configPath, JsonSerializer.Serialize(template, TemplateWriteOptions));
    }

    private static void PrintReport(List<BuildResult> results, TimeSpan totalTime)
    {
        ConsoleLogger.Header("Build report");

        var succeeded = results.Count(r => r.IsSuccess);
        var failed = results.Count(r => !r.IsSuccess);

        foreach (var result in results)
        {
            if (result.IsSuccess)
            {
                ConsoleLogger.Success($"{result.RepositoryName,-30} {result.Duration.TotalSeconds,6:F1}s");
            }
            else
            {
                ConsoleLogger.Error($"{result.RepositoryName,-30} {result.Duration.TotalSeconds,6:F1}s");
                ConsoleLogger.Error($"  └─ {result.ErrorMessage}");
            }
        }

        Console.WriteLine();
        ConsoleLogger.Info($"Total: {results.Count} | Succeeded: {succeeded} | Failed: {failed}");
        ConsoleLogger.Info($"Total time: {totalTime.TotalSeconds:F1}s");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  release-builder --version <version>");
        Console.WriteLine("  release-builder -v <version>");
        Console.WriteLine("  release-builder <version>");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -v, --version <version>   Branch alvo será release/<version>");
        Console.WriteLine("  -c, --config <path>       Usa um arquivo de config alternativo");
        Console.WriteLine("      --config-path         Imprime o caminho padrão da config e sai");
        Console.WriteLine();
        Console.WriteLine("Config padrão: %APPDATA%\\release-builder\\appsettings.json");
        Console.WriteLine();
        Console.WriteLine("Exemplo:");
        Console.WriteLine("  release-builder --version 1.5.0");
    }
}
