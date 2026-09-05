using System.Net.Http.Json;
using System.Text.Json;
using LifeLink.Application.Workflows;
using Microsoft.Extensions.Options;

namespace LifeLink.Infrastructure.Workflows;

public sealed class AgentWorkflowClient(HttpClient httpClient, IOptions<AgentServiceOptions> options) : IAgentWorkflowClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private readonly AgentServiceOptions _options = options.Value;
    public async Task<AgentRunResponse> RunAsync(AgentRunRequest request, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/workflows/run") { Content = JsonContent.Create(request, options: Json) };
        message.Headers.Add("X-Internal-API-Key", _options.InternalApiKey);
        if (!string.IsNullOrWhiteSpace(request.CorrelationId)) message.Headers.Add("X-Correlation-ID", request.CorrelationId);
        using var response = await httpClient.SendAsync(message, ct); response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AgentRunResponse>(Json, ct) ?? throw new InvalidOperationException("Agent service returned an empty response.");
    }
}
