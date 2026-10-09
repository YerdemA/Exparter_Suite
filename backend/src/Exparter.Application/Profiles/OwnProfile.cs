using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Domain.Profiles;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Profiles;

internal static class ProfileAppErrors
{
    public static AppException ProfileNotFound() => AppException.NotFound("The profile does not exist.");

    public static AppException AvatarNotUploaded() =>
        AppException.BadRequest("avatar_not_uploaded", "No uploaded file was found for this object key.");

    public static AppException AvatarInvalid() =>
        AppException.BadRequest("avatar_invalid", "The uploaded file is not an allowed image or is too large.");
}

/// <summary>Gives every new account its blank profile, in the registration's own transaction.</summary>
public sealed class CreateProfileOnUserRegistered(IProfileRepository profiles, TimeProvider timeProvider)
    : INotificationHandler<UserRegistered>
{
    public ValueTask Handle(UserRegistered notification, CancellationToken cancellationToken)
    {
        profiles.Add(Profile.CreateEmpty(notification.UserId, timeProvider.GetUtcNow()));
        return ValueTask.CompletedTask;
    }
}

public sealed record GetOwnProfileQuery(Guid UserId) : IQuery<ProfileDto>;

public sealed class GetOwnProfileQueryHandler(IProfileRepository profiles, IObjectStorage storage)
    : IQueryHandler<GetOwnProfileQuery, ProfileDto>
{
    public async ValueTask<ProfileDto> Handle(GetOwnProfileQuery query, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByUserIdAsync(query.UserId, cancellationToken)
            ?? throw ProfileAppErrors.ProfileNotFound();
        return profile.ToDto(storage);
    }
}

public sealed record UpdateOwnProfileCommand(
    Guid UserId,
    string DisplayName,
    string? Bio,
    string? City,
    bool AcceptsOnline,
    bool AcceptsInPerson) : ICommand<ProfileDto>;

public sealed class UpdateOwnProfileCommandValidator : AbstractValidator<UpdateOwnProfileCommand>
{
    public UpdateOwnProfileCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .Must(name => name is null || name.Trim().Length is >= Profile.DisplayNameMinLength and <= Profile.DisplayNameMaxLength)
            .WithMessage($"The display name must be {Profile.DisplayNameMinLength} to {Profile.DisplayNameMaxLength} characters.");
        RuleFor(x => x.Bio).MaximumLength(Profile.BioMaxLength);
        RuleFor(x => x.City)
            .Must(city => city is null || Cities.IsValidCode(city))
            .WithMessage("The city must be a plate code from 01 to 81.");
        RuleFor(x => x.City)
            .NotNull()
            .When(x => x.AcceptsInPerson)
            .WithMessage("A city is required when in-person swaps are accepted.");
        RuleFor(x => x.AcceptsOnline)
            .Must((command, acceptsOnline) => acceptsOnline || command.AcceptsInPerson)
            .WithMessage("At least one of online and in-person must be accepted.");
    }
}

public sealed class UpdateOwnProfileCommandHandler(
    IProfileRepository profiles,
    IObjectStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UpdateOwnProfileCommand, ProfileDto>
{
    public async ValueTask<ProfileDto> Handle(UpdateOwnProfileCommand command, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByUserIdAsync(command.UserId, cancellationToken)
            ?? throw ProfileAppErrors.ProfileNotFound();

        profile.Update(
            command.DisplayName,
            command.Bio,
            command.City,
            command.AcceptsOnline,
            command.AcceptsInPerson,
            timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return profile.ToDto(storage);
    }
}

public sealed record GetUserProfileQuery(Guid UserId) : IQuery<PublicProfileDto>;

public sealed class GetUserProfileQueryHandler(
    IProfileRepository profiles,
    IUserDirectory users,
    IObjectStorage storage) : IQueryHandler<GetUserProfileQuery, PublicProfileDto>
{
    public async ValueTask<PublicProfileDto> Handle(GetUserProfileQuery query, CancellationToken cancellationToken)
    {
        var user = await users.FindAsync(query.UserId, cancellationToken);
        var profile = user is null ? null : await profiles.GetByUserIdAsync(query.UserId, cancellationToken);

        // A user who cannot interact yet (unverified email, incomplete profile, suspended
        // or leaving) is invisible to others: the same answer as for an unknown id.
        if (user is null || profile is null || !user.CanInteract || !profile.IsComplete)
        {
            throw ProfileAppErrors.ProfileNotFound();
        }

        return new PublicProfileDto(
            profile.UserId,
            user.Username,
            profile.DisplayName!,
            profile.Bio,
            Avatars.UrlOf(profile, storage),
            profile.City,
            profile.AcceptsOnline,
            profile.AcceptsInPerson,
            RatingSummaryDto.None);
    }
}
