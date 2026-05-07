using release_builder.Model;
using release_builder.Models;
using release_builder.Services;
using System.Diagnostics;
using System.Text.Json;

namespace release_builder;

public class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static async Task<int> Main(string[] args)
    {
        var version = ParseVersion(args);

        if (version is null)
        {
            PrintUsage();
            return 1;
        }

        ConsoleLogger.Header($"Release Builder — version {version}");

        var config = LoadConfig();

        if (config is null)
        {
            ConsoleLogger.Error("Failed to load appsettings.json");
            return 1;
        }

        if (!Directory.Exists(config.RootPath))
        {
            ConsoleLogger.Error($"Root path does not exist: {config.RootPath}");
            return 1;
        }

        ConsoleLogger.Info($"Root path: {config.RootPath}");
        ConsoleLogger.Info($"Repositories: {config.Repositories.Count}");
        ConsoleLogger.Info($"Target branch: release/{version}");
        ConsoleLogger.Info($"Stop on error: {config.StopOnError}");

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
                gitService, buildService, repo, repoPath, solutionPath, version);

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

    private static string? ParseVersion(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--version" or "-v")
            {
                return args[i + 1];
            }
        }

        if (args.Length == 1 && !args[0].StartsWith('-'))
        {
            return args[0];
        }

        return null;
    }

    private static BuildConfig LoadConfig()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (!File.Exists(configPath))
        {
            configPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        }

        if (!File.Exists(configPath))
        {
            ConsoleLogger.Error($"Config file not found at: {configPath}");
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
        Console.WriteLine("  ReleaseBuilder --version <version>");
        Console.WriteLine("  ReleaseBuilder -v <version>");
        Console.WriteLine("  ReleaseBuilder <version>");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("ReleaseBuilder --version 1.5.0");
    }
}