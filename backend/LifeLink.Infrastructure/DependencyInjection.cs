using LifeLink.Infrastructure.Persistence;
using LifeLink.Application.Auth;
using LifeLink.Infrastructure.Authentication;
using LifeLink.Application.Donors;
using LifeLink.Infrastructure.Donors;
using LifeLink.Application.Requests;
using LifeLink.Infrastructure.Requests;
using LifeLink.Application.Inventory;
using LifeLink.Infrastructure.Inventory;
using LifeLink.Application.Camps;
using LifeLink.Application.Notifications;
using LifeLink.Infrastructure.Camps;
using LifeLink.Infrastructure.Notifications;
using LifeLink.Application.Workflows;
using LifeLink.Infrastructure.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace LifeLink.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("LifeLink")
            ?? throw new InvalidOperationException("ConnectionStrings:LifeLink is required.");
        services.AddDbContext<LifeLinkDbContext>(options => options.UseNpgsql(connectionString));
        services.AddOptions<BootstrapOptions>().Bind(configuration.GetSection("Bootstrap"));
        services.AddHostedService<AdminBootstrapper>();
        services.AddOptions<DemoDataOptions>().Bind(configuration.GetSection("DemoData"));
        services.AddHostedService<DemoDataSeeder>();
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName)).Validate(x => x.SigningKey.Length >= 32, "JWT signing key must be at least 32 characters.").ValidateOnStart();
        services.AddScoped<IAuthService, AuthService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IDonorService, DonorService>();
        services.AddScoped<IRequestService, RequestService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddOptions<FirebaseOptions>().Bind(configuration.GetSection(FirebaseOptions.SectionName));
        services.AddHttpClient<FirebaseNotificationProvider>(client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<LoggingNotificationProvider>();
        services.AddScoped<INotificationProvider>(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FirebaseOptions>>().Value;
            if (!options.Enabled) return provider.GetRequiredService<LoggingNotificationProvider>();
            if (string.IsNullOrWhiteSpace(options.ProjectId) || string.IsNullOrWhiteSpace(options.ServiceAccountJson) && string.IsNullOrWhiteSpace(options.ServiceAccountJsonBase64)) throw new InvalidOperationException("Firebase project ID and service account JSON are required when Firebase is enabled.");
            return provider.GetRequiredService<FirebaseNotificationProvider>();
        });
        services.AddScoped<INotificationService, NotificationService>();
        services.AddOptions<NotificationOutboxOptions>().Bind(configuration.GetSection("NotificationOutbox"));
        services.AddHostedService<NotificationOutboxWorker>();
        services.AddOptions<LifeLink.Infrastructure.Inventory.ReservationExpiryOptions>().Bind(configuration.GetSection("ReservationExpiry"));
        services.AddHostedService<LifeLink.Infrastructure.Inventory.ReservationExpiryWorker>();
        services.AddScoped<ICampService, CampService>();
        services.AddOptions<AgentServiceOptions>().Bind(configuration.GetSection(AgentServiceOptions.SectionName)).Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(x.InternalApiKey) && x.TimeoutSeconds is >= 1 and <= 120, "Agent service configuration is invalid.").ValidateOnStart();
        services.AddHttpClient<IAgentWorkflowClient, AgentWorkflowClient>((provider, client) => { var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentServiceOptions>>().Value; client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"); client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds); });
        services.AddScoped<WorkflowService>();
        services.AddScoped<IWorkflowService>(provider => provider.GetRequiredService<WorkflowService>());
        services.AddScoped<IRequestWorkflowTrigger>(provider => provider.GetRequiredService<WorkflowService>());
        return services;
    }
}
