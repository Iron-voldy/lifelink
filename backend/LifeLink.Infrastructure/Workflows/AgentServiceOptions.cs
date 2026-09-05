namespace LifeLink.Infrastructure.Workflows;
public sealed class AgentServiceOptions
{
    public const string SectionName = "AgentService";
    public string BaseUrl { get; init; } = "http://localhost:8000";
    public string InternalApiKey { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 20;
}
