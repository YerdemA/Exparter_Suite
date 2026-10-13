namespace Exparter.Application.Profiles;

/// <summary>What other modules may know about a user's profile.</summary>
public interface IProfileDirectory
{
    /// <returns>False for an unknown user.</returns>
    Task<bool> IsCompleteAsync(Guid userId, CancellationToken cancellationToken);
}

internal sealed class ProfileDirectory(IProfileRepository profiles) : IProfileDirectory
{
    public async Task<bool> IsCompleteAsync(Guid userId, CancellationToken cancellationToken) =>
        (await profiles.GetByUserIdAsync(userId, cancellationToken))?.IsComplete ?? false;
}
