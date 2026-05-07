namespace release_builder.Models;

public class BuildConfig
{
    public string RootPath { get; set; } = string.Empty;
    public bool StopOnError { get; set; } = true;
    public List<RepositoryEntry> Repositories { get; set; } = [];
}

public class RepositoryEntry
{
    public string Name { get; set; } = string.Empty;
    public string SolutionFile { get; set; } = string.Empty;
}