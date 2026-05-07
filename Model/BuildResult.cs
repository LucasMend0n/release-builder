namespace release_builder.Model
{
    public class BuildResult
    {
        public string RepositoryName { get; set; } = string.Empty;
        public bool GitSuccess { get; set; }
        public bool BuildSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public TimeSpan Duration { get; set; }

        public bool IsSuccess => GitSuccess && BuildSuccess;
    }
}
