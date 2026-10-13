using System.Globalization;
using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Matching;
using Exparter.Application.Profiles;
using Exparter.Application.Skills;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Exparter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Messages are for developers and logs; clients localize by error code. Without this,
        // FluentValidation would follow the server's culture and answer in mixed languages.
        ValidatorOptions.Global.LanguageManager.Culture = CultureInfo.GetCultureInfo("en");
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddScoped<TokenIssuer>();
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IProfileDirectory, ProfileDirectory>();
        services.AddScoped<IUserSkillDirectory, UserSkillDirectory>();
        services.AddScoped<IInteractionRequirements, InteractionRequirements>();
        services.AddScoped<IUserSummaries, UserSummaries>();
        services.AddScoped<ISkillUsage, SkillUsage>();
        services.AddScoped<MatchingDtoFactory>();
        return services;
    }
}
