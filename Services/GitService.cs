using System.Diagnostics;

namespace release_builder.Services;

public class GitService
{
    public async Task<(bool Success, string Output)> FetchAsync(string repoPath)
    {
        return await RunGitAsync(repoPath, "fetch origin");
    }

    public async Task<(bool Success, string Output)> CheckoutBranchAsync(string repoPath, string version)
    {
        var branchName = $"release/{version}";

        var result = await RunGitAsync(repoPath, $"checkout {branchName}");

        if (!result.Success)
        {
            result = await RunGitAsync(repoPath, $"checkout -b {branchName} origin/{branchName}");
        }

        return result;
    }

    public async Task<(bool Success, string Output)> PullAsync(string repoPath, string version)
    {
        var branchName = $"release/{version}";
        return await RunGitAsync(repoPath, $"pull origin {branchName}");
    }

    public async Task<(bool Success, string Output)> GetCurrentBranchAsync(string repoPath)
    {
        return await RunGitAsync(repoPath, "branch --show-current");
    }

    public async Task<(bool Success, string Output)> CheckForDirtyWorkingTreeAsync(string repoPath)
    {
        var result = await RunGitAsync(repoPath, "status --porcelain");

        if (result.Success && !string.IsNullOrWhiteSpace(result.Output))
        {
            return (false, "Working tree has uncommitted changes. Please commit or stash before proceeding.");
        }

        return result;
    }
    public async Task<(bool Success, string Output)> StashWorkTree(string repoPath)
    {
        var result = await RunGitAsync(repoPath, "stash push -m \"stashing worktree for release build\"");

        if (result.Success && !result.Output.Contains("Saved working directory and index state"))
        {
            return (false, $"{result.Output}");
        }

        return result;
    }

    private static async Task<(bool Success, string Output)> RunGitAsync(string workingDirectory, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            var output = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;

            return (process.ExitCode == 0, output.Trim());
        }
        catch (Exception ex)
        {
            return (false, $"Failed to run 'git {arguments}': {ex.Message}");
        }
    }
}