using LifeLink.Api.Infrastructure;
using LifeLink.Application;
using LifeLink.Infrastructure;
using LifeLink.Infrastructure.Authentication;
using LifeLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = ValidationProblemFactory.Create);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the access token only, without the 'Bearer ' prefix."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var permits = builder.Configuration.GetValue("RateLimiting:AuthPermitsPerMinute", 10);
    var refreshPermits = builder.Configuration.GetValue("RateLimiting:RefreshPermitsPerMinute", 60);
    // Per-client-IP window on credential endpoints slows brute-force and credential-stuffing attempts.
    // RemoteIpAddress is the real client only after UseForwardedHeaders (below); otherwise every user behind
    // the reverse proxy would share one bucket and lock each other out of sign-in.
    RateLimitPartition<string> PerClient(HttpContext http, int limit) => builder.Environment.IsEnvironment("Testing")
        ? RateLimitPartition.GetNoLimiter("testing")
        : RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });
    options.AddPolicy("auth", http => PerClient(http, permits));
    // Refresh tokens are 512-bit random values, so guessing is not a concern; this limit only stops floods.
    // It is separate so page reloads and token renewals never consume the sign-in budget.
    options.AddPolicy("refresh", http => PerClient(http, refreshPermits));
    options.OnRejected = async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)) context.HttpContext.Response.Headers.RetryAfter = ((int)retry.TotalSeconds).ToString();
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new { status = 429, title = "too_many_requests", detail = "Too many attempts. Please wait a minute and try again.", correlationId = context.HttpContext.TraceIdentifier }, ct);
    };
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? throw new InvalidOperationException("Jwt configuration is required.");
StartupSecurityChecks.Validate(builder.Configuration, builder.Environment, jwt);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("BloodBankAdmin"));
    options.AddPolicy("HospitalOnly", policy => policy.RequireRole("HospitalRequester"));
    options.AddPolicy("DonorOnly", policy => policy.RequireRole("Donor"));
});
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"];
builder.Services.AddCors(options => options.AddPolicy("Clients", policy => policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
if (builder.Configuration.GetValue<bool>("Security:TrustProxyClientIp"))
{
    // Only enable where the API is reachable solely through our own nginx, which overwrites X-Real-IP with the
    // connecting address (deploy/vps). A client-supplied value can then never reach the API unmodified.
    var proxy = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor, ForwardedForHeaderName = "X-Real-IP", ForwardLimit = 1 };
    proxy.KnownNetworks.Clear();
    proxy.KnownProxies.Clear();
    app.UseForwardedHeaders(proxy);
}
if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<LifeLinkDbContext>().Database.MigrateAsync();
}
if (builder.Configuration.GetValue<bool>("Security:RequireHttps"))
{
    // Behind a TLS-terminating proxy (Render, Vercel): trust its forwarded headers, then enforce HTTPS.
    // When the client IP already comes from the trusted X-Real-IP (above), only take the scheme here: a second
    // X-Forwarded-For pass would replace that IP with an intermediate proxy address and merge everyone's rate-limit bucket.
    var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = builder.Configuration.GetValue<bool>("Security:TrustProxyClientIp") ? ForwardedHeaders.XForwardedProto : ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
    // PaaS load balancers are not loopback, so the default trust list would ignore their headers (redirect loop, wrong client IP).
    // Only enable Security:RequireHttps where the API is reachable solely through the platform proxy.
    forwarded.KnownNetworks.Clear();
    forwarded.KnownProxies.Clear();
    app.UseForwardedHeaders(forwarded);
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseMiddleware<CorrelationIdMiddleware>();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
if (!app.Environment.IsEnvironment("Testing")) app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseCors("Clients");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program;