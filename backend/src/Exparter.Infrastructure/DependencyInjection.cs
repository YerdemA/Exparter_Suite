using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Profiles;
using Exparter.Application.Skills;
using Exparter.Infrastructure.Email;
using Exparter.Infrastructure.Identity;
using Exparter.Infrastructure.Persistence;
using Exparter.Infrastructure.Persistence.Identity;
using Exparter.Infrastructure.Persistence.Profiles;
using Exparter.Infrastructure.Persistence.Skills;
using Exparter.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Exparter.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Database";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddDbContext<AppDbContext>((serviceProvider, options) => options
            .UseNpgsql(serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName))
            .UseSnakeCaseNamingConvention());
        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IOneTimeTokenRepository, OneTimeTokenRepository>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();

        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<ISkillRepository, SkillRepository>();
        services.AddScoped<IUserSkillRepository, UserSkillRepository>();

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();

        return services;
    }
}
