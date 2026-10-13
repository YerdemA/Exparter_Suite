namespace Exparter.Application.Profiles;

/// <summary>The part of a profile shown wherever a user is mentioned.</summary>
public sealed record ProfileCard(Guid UserId, string? DisplayName, string? AvatarKey);

/// <summary>What other modules may know about a user's profile.</summary>
public interface IProfileDirectory
{
    /// <returns>False for an unknown user.</returns>
    Task<bool> IsCompleteAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The profiles that exist among the given users, in no particular order.</summary>
    Task<IReadOnlyList<ProfileCard>> GetCardsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}

internal sealed class ProfileDirectory(IProfileRepository profiles) : IProfileDirectory
{
    public async Task<bool> IsCompleteAsync(Guid userId, CancellationToken cancellationToken) =>
        (await profiles.GetByUserIdAsync(userId, cancellationToken))?.IsComplete ?? false;

    public async Task<IReadOnlyList<ProfileCard>> GetCardsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
        (await profiles.ListByUserIdsAsync(userIds, cancellationToken))
            .Select(profile => new ProfileCard(profile.UserId, profile.DisplayName, profile.AvatarKey))
            .ToList();
}
