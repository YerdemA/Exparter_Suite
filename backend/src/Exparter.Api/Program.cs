using System.Text.Json.Serialization;
using Exparter.Api.Common;
using Exparter.Api.Identity;
using Exparter.Application;
using Exparter.Application.Common;
using Exparter.Infrastructure;
using Exparter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMediator(options =>
{
    // Handlers depend on the scoped DbContext, so they cannot use the default singleton lifetime.
    options.ServiceLifetime = ServiceLifetime.Scoped;
    options.Assemblies = [typeof(Exparter.Application.DependencyInjection)];
    options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
});

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ApiProblem.EnsureCode);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// By default an unreadable body throws only in Development and is a bare 400 elsewhere.
// Throwing everywhere sends it through the exception handler, so the client always gets problem details.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddApiAuthentication();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapGroup("/api/v1").MapIdentityEndpoints();

if (app.Environment.IsDevelopment())
{
    // Production migrations are a deliberate deployment step; only local runs migrate on start.
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

await app.RunAsync();

public partial class Program;
