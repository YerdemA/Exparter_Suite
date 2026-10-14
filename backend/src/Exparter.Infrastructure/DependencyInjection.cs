using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Matching;
using Exparter.Application.Messaging;
using Exparter.Application.Notifications;
using Exparter.Application.Reviews;
using Exparter.Application.Sessions;
using Exparter.Application.Profiles;
using Exparter.Application.Skills;
using Exparter.Infrastructure.Email;
using Exparter.Infrastructure.Identity;
using Exparter.Infrastructure.Notifications;
using Exparter.Infrastructure.Persistence;
using Exparter.Infrastructure.Persistence.Identity;
using Exparter.Infrastructure.Persistence.Matching;
using Exparter.Infrastructure.Persistence.Messaging;
using Exparter.Infrastructure.Persistence.Notifications;
using Exparter.Infrastructure.Persistence.Reviews;
using Exparter.Infrastructure.Persistence.Sessions;
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

        services.AddScoped<IBlockedEmailRepository, BlockedEmailRepository>();
        services.AddScoped<IProfileRepository, ProfileRepository>();
        services.AddScoped<ISkillRepository, SkillRepository>();
        services.AddScoped<IUserSkillRepository, UserSkillRepository>();
        services.AddScoped<IDiscoveryReadModel, DiscoveryReadModel>();
        services.AddScoped<ISwapRequestRepository, SwapRequestRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();

        services.AddOptions<FirebaseOptions>().Bind(configuration.GetSection(FirebaseOptions.SectionName));
        if (string.IsNullOrWhiteSpace(configuration[$"{FirebaseOptions.SectionName}:{nameof(FirebaseOptions.CredentialsPath)}"]))
        {
            services.AddSingleton<IPushSender, DisabledPushSender>();
        }
        else
        {
            services.AddSingleton<IPushSender, FcmPushSender>();
        }

        return services;
    }
}
