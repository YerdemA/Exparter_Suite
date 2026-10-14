using System.Reflection;
using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Domain.Common;

namespace Exparter.Application.Tests;

public class ArchitectureTests
{
    /// <summary>
    /// The only types of a module that other modules may use: its published contract.
    /// Adding a type here is a deliberate decision to widen a module's public surface.
    /// </summary>
    private static readonly HashSet<Type> ModuleContracts =
    [
        typeof(IUserDirectory),
        typeof(UserInfo),
        typeof(UserRegistered),
        typeof(AccountDeletionRequested),
        typeof(AccountErased),
        typeof(Exparter.Domain.Skills.SkillLevel),
        typeof(Exparter.Application.Profiles.IProfileDirectory),
        typeof(Exparter.Application.Profiles.ProfileCard),
        typeof(Exparter.Application.Skills.IUserSkillDirectory),
        typeof(Exparter.Application.Matching.ISkillUsage),
        typeof(Exparter.Application.Matching.IMatchDirectory),
        typeof(Exparter.Application.Matching.MatchCreated),
        typeof(Exparter.Application.Matching.MatchEnded),
        typeof(Exparter.Application.Matching.MatchInfo),
        typeof(Exparter.Application.Matching.MatchSummary),
        typeof(Exparter.Application.Sessions.ISessionDirectory),
        typeof(Exparter.Application.Reviews.IReviewDirectory),
        typeof(Exparter.Application.Notifications.IUserNotifier),
        typeof(Exparter.Application.Notifications.PushNotifier),
        typeof(Exparter.Application.Moderation.IBlockDirectory),
        typeof(Exparter.Application.Moderation.BlockRelation),
        typeof(Exparter.Application.Moderation.BlockDirectoryExtensions),
        typeof(Exparter.Application.Moderation.UserBlocked),
        typeof(Exparter.Domain.Notifications.NotificationType),
        typeof(Exparter.Domain.Notifications.NotificationData),
        typeof(Exparter.Application.Messaging.IConversationDirectory),
        typeof(Exparter.Application.Messaging.MessageInfo),
        typeof(Exparter.Application.Reviews.ReviewInfo),
    ];

    private const BindingFlags AllMembers =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    [Theory]
    [InlineData("Exparter.Infrastructure")]
    [InlineData("Exparter.Api")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.AspNetCore")]
    public void Application_does_not_depend_on_outer_layers_or_their_frameworks(string forbiddenPrefix)
    {
        var references = typeof(AppException).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name!);

        Assert.DoesNotContain(references, name => name.StartsWith(forbiddenPrefix, StringComparison.Ordinal));
    }

    /// <summary>
    /// Checks what types declare: base types, fields, properties and method signatures,
    /// including the fields compilers generate for lambdas and async methods. A call to
    /// another module's static method that leaves no such trace is not detected.
    /// </summary>
    [Fact]
    public void Modules_reference_each_other_only_through_published_contracts()
    {
        var violations = new[] { typeof(DomainException).Assembly, typeof(AppException).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Select(type => (Type: type, Module: ModuleOf(type)))
            .Where(item => item.Module is not null)
            .SelectMany(item => ReferencedTypes(item.Type)
                .Where(referenced => ModuleOf(referenced) is { } other
                    && other != item.Module
                    && !ModuleContracts.Contains(referenced))
                .Select(referenced => $"{item.Type.FullName} -> {referenced.FullName}"))
            .Distinct()
            .ToList();

        Assert.True(violations.Count == 0, "Cross-module references:\n" + string.Join("\n", violations));
    }

    /// <returns>"Identity" for Exparter.Domain.Identity.User; null for shared and foreign types.</returns>
    private static string? ModuleOf(Type type)
    {
        var parts = (type.Namespace ?? string.Empty).Split('.');
        var isModuleNamespace = parts.Length >= 3
            && parts[0] == "Exparter"
            && parts[1] is "Domain" or "Application"
            && parts[2] != "Common";
        return isModuleNamespace ? parts[2] : null;
    }

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        var direct = new List<Type?> { type.BaseType };
        direct.AddRange(type.GetInterfaces());
        direct.AddRange(type.GetFields(AllMembers).Select(field => field.FieldType));
        direct.AddRange(type.GetProperties(AllMembers).Select(property => property.PropertyType));
        foreach (var method in type.GetMethods(AllMembers).Cast<MethodBase>().Concat(type.GetConstructors(AllMembers)))
        {
            direct.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));
            if (method is MethodInfo info)
            {
                direct.Add(info.ReturnType);
            }
        }

        return direct.Where(referenced => referenced is not null).SelectMany(referenced => Flatten(referenced!));
    }

    /// <summary>Unwraps arrays and generic arguments: Task&lt;List&lt;User&gt;&gt; references User.</summary>
    private static IEnumerable<Type> Flatten(Type type)
    {
        if (type.HasElementType)
        {
            return Flatten(type.GetElementType()!);
        }

        var self = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        return type.IsGenericType ? type.GetGenericArguments().SelectMany(Flatten).Prepend(self) : [self];
    }
}
