using System.Text.Json.Serialization;
using Exparter.Api.Common;
using Exparter.Api.Identity;
using Exparter.Api.Matching;
using Exparter.Api.Messaging;
using Exparter.Api.Moderation;
using Exparter.Api.Notifications;
using Exparter.Application.Notifications;
using Exparter.Api.Reviews;
using Exparter.Api.Sessions;
using Exparter.Api.Profiles;
using Exparter.Api.Skills;
using Exparter.Application;
using Exparter.Application.Common;
using Exparter.Infrastructure;
using Exparter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Sentry.Extensibility;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()

        // Errors become Sentry events, with the lines logged before them as breadcrumbs.
        // Without a DSN Sentry is off and this sink does nothing (ADR 0013).
        .WriteTo.Sentry(sentry =>
        {
            sentry.InitializeSdk = false;
            sentry.MinimumEventLevel = LogEventLevel.Error;
            sentry.MinimumBreadcrumbLevel = LogEventLevel.Information;
        });

    // Readable lines while developing; one JSON object per line elsewhere, with the trace
    // id that problem details carry, so that a reported error leads to its log lines.
    if (context.HostingEnvironment.IsDevelopment())
    {
        configuration.WriteTo.Console();
    }
    else
    {
        configuration.WriteTo.Console(new RenderedCompactJsonFormatter());
    }
});

builder.WebHost.UseSentry(sentry =>
{
    // The DSN comes from configuration (Sentry:Dsn); without it nothing is sent.
    sentry.SendDefaultPii = false;
    sentry.MaxRequestBodySize = RequestSize.None;
    sentry.TracesSampleRate = 0;
    sentry.AddExceptionFilter(new ClientErrorFilter());
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMediator(options =>
{
    // Handlers depend on the scoped DbContext, so they cannot use the default singleton lifetime.
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.Assemblies = [typeof(Exparter.Application.DependencyInjection)];
    options.PipelineBehaviors = [typeof(ValidationBehavior<,>), typeof(NotificationDeliveryBehavior<,>)];
});

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ApiProblem.EnsureCode);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// By default an unreadable body throws only in Development and is a bare 400 elsewhere.
// Throwing everywhere sends it through the exception handler, so the client always gets problem details.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddApiAuthentication();
builder.Services.AddApiRateLimits(builder.Configuration);
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = Hardening.MaxRequestBodyBytes);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHostedService<MatchingDeadlineWorker>();
builder.Services.AddSignalR(hub => hub.MaximumReceiveMessageSize = Hardening.MaxHubMessageBytes)
    .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, SubjectUserIdProvider>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddSingleton<ConnectionRegistry>();
builder.Services.AddSingleton<IUserPresence>(services => services.GetRequiredService<ConnectionRegistry>());
builder.Services.AddSingleton<IRealtimeConnections>(services => services.GetRequiredService<ConnectionRegistry>());
builder.Services.AddSingleton<PushQueue>();
builder.Services.AddSingleton<IPushQueue>(services => services.GetRequiredService<PushQueue>());
builder.Services.AddHostedService<PushWorker>();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseSecurityHeaders();
if (!app.Environment.IsDevelopment())
{
    // Only sent over HTTPS, which the deployment terminates in front of the API.
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseBodySizeLimit();
app.UseAuthentication();
app.UseErrorReportUser();
app.UseAuthorization();

// After authentication: signed-in users are limited per account, others per address.
app.UseRateLimiter();

app.MapHealthChecks("/health").AllowAnonymous();
var api = app.MapGroup("/api/v1");
api.MapIdentityEndpoints();
api.MapProfileEndpoints();
api.MapSkillEndpoints();
api.MapDiscoveryEndpoints();
api.MapSwapEndpoints();
api.MapConversationEndpoints();
api.MapSessionEndpoints();
api.MapReviewEndpoints();
api.MapNotificationEndpoints();
api.MapModerationEndpoints();
app.MapHub<RealtimeHub>(RealtimeHub.Path);

// The deployment applies migrations as a step of its own, before the new version starts:
// `dotnet Exparter.Api.dll --migrate` migrates the database and exits (docs/deployment.md).
if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    return;
}

if (app.Environment.IsDevelopment())
{
    // Production migrations are a deliberate deployment step; only local runs migrate on start.
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

await app.RunAsync();

public partial class Program;
