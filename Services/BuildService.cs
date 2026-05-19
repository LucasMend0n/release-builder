using System.Diagnostics;

namespace release_builder.Services;

public class BuildService
{
    public async Task<(bool Success, string Output)> RebuildAsync(string solutionPath)
    {
        if (!File.Exists(solutionPath))
        {
            return (false, $"Solution file not found: {solutionPath}");
        }

        try
        {

            var restoreResult = await RunDotnetAsync($"restore \"{solutionPath}\"");

            if (!restoreResult.Success)
            {
                return (false, $"Restore failed: {restoreResult.Output}");
            }

            var cleanResult = await RunDotnetAsync($"clean \"{solutionPath}\" -v quiet");

            if (!cleanResult.Success)
            {
                return (false, $"Clean failed: {cleanResult.Output}");
            }

            var buildResult = await RunDotnetAsync($"build \"{solutionPath}\" --no-incremental --no-restore");

            return buildResult;
        }
        catch (Exception ex)
        {
            return (false, $"Build process failed: {ex.Message}");
        }
    }

    private static async Task<(bool Success, string Output)> RunDotnetAsync(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
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
            return (false, $"Failed to run 'dotnet {arguments}': {ex.Message}");
        }
    }
}