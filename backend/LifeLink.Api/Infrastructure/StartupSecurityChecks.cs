using System.Text;
using LifeLink.Infrastructure.Authentication;

namespace LifeLink.Api.Infrastructure;

/// <summary>Fails fast at startup when secrets are missing, weak, or still set to development placeholders outside dev/test.</summary>
public static class StartupSecurityChecks
{
    // Fragments of every placeholder/default secret shipped in this repository (appsettings, compose files, .env examples).
    // A value containing one of them is publicly known, so anyone could forge tokens or call the agent service with it.
    private static readonly string[] KnownPlaceholderFragments = ["development-only", "replace-with", "local-only", "local_only", "change-me", "changeme", "change-before", "change-in-production"];

    public static void Validate(IConfiguration configuration, IHostEnvironment environment, JwtOptions jwt)
    {
        var strict = !(environment.IsDevelopment() || environment.IsEnvironment("Testing"));
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32) problems.Add("Jwt:SigningKey must be at least 32 characters.");
        else if (strict && IsPlaceholder(jwt.SigningKey)) problems.Add("Jwt:SigningKey is still a development placeholder; set a random secret.");
        var agentKey = configuration["AgentService:InternalApiKey"];
        if (strict && (string.IsNullOrWhiteSpace(agentKey) || agentKey.Trim().Length < 16 || IsPlaceholder(agentKey))) problems.Add("AgentService:InternalApiKey is missing, shorter than 16 characters, or still a placeholder.");
        var databasePassword = DatabasePassword(configuration.GetConnectionString("LifeLink"));
        if (strict && databasePassword is not null && IsPlaceholder(databasePassword)) problems.Add("ConnectionStrings:LifeLink uses a placeholder database password.");
        if (problems.Count > 0) throw new InvalidOperationException("Insecure configuration: " + string.Join(" ", problems));
    }

    private static bool IsPlaceholder(string value) => KnownPlaceholderFragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string? DatabasePassword(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator > 0 && part[..separator].Trim().Equals("Password", StringComparison.OrdinalIgnoreCase)) return part[(separator + 1)..];
        }
        return null;
    }
}
