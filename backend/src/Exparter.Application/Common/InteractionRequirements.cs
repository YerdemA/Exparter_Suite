using Exparter.Application.Identity;
using Exparter.Application.Profiles;
using Exparter.Application.Skills;

namespace Exparter.Application.Common;

/// <param name="CanInteract">
/// Everything below is met and the account is active. Until then the user may browse, but
/// cannot interact with other users and is not shown to them.
/// </param>
public sealed record InteractionStatus(bool EmailVerified, bool ProfileCompleted, bool HasOfferedSkill, bool CanInteract)
{
    public static InteractionStatus None { get; } = new(false, false, false, false);
}

/// <summary>
/// The one place that decides whether a user may interact with others. It belongs to no
/// module: it combines what Identity, Profiles and Skills each know about the user.
/// </summary>
public interface IInteractionRequirements
{
    /// <returns><see cref="InteractionStatus.None"/> for an unknown user.</returns>
    Task<InteractionStatus> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Throws the 403 that interaction endpoints answer with.</summary>
    Task EnsureCanInteractAsync(Guid userId, CancellationToken cancellationToken);
}

internal sealed class InteractionRequirements(
    IUserDirectory users,
    IProfileDirectory profiles,
    IUserSkillDirectory skills) : IInteractionRequirements
{
    public async Task<InteractionStatus> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindAsync(userId, cancellationToken);
        if (user is null)
        {
            return InteractionStatus.None;
        }

        var profileCompleted = await profiles.IsCompleteAsync(userId, cancellationToken);
        var hasOfferedSkill = await skills.HasOfferedSkillAsync(userId, cancellationToken);
        return new InteractionStatus(
            user.IsEmailVerified,
            profileCompleted,
            hasOfferedSkill,
            user.IsActive && user.IsEmailVerified && profileCompleted && hasOfferedSkill);
    }

    public async Task EnsureCanInteractAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!(await GetAsync(userId, cancellationToken)).CanInteract)
        {
            throw AppException.Forbidden(
                "interaction_requirements_not_met",
                "A verified email, a completed profile and at least one offered skill are required.");
        }
    }
}
