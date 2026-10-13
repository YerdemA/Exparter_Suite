using Exparter.Application.Common;
using Exparter.Application.Reviews;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Profiles;

public sealed record CreateAvatarUploadUrlCommand(Guid UserId, string ContentType, long ContentLength)
    : ICommand<AvatarUploadUrlDto>;

public sealed class CreateAvatarUploadUrlCommandValidator : AbstractValidator<CreateAvatarUploadUrlCommand>
{
    public CreateAvatarUploadUrlCommandValidator()
    {
        RuleFor(x => x.ContentType)
            .Must(Avatars.IsAllowedContentType)
            .WithMessage("The content type must be image/jpeg, image/png or image/webp.");
        RuleFor(x => x.ContentLength).InclusiveBetween(1, Avatars.MaxBytes);
    }
}

public sealed class CreateAvatarUploadUrlCommandHandler(IObjectStorage storage)
    : ICommandHandler<CreateAvatarUploadUrlCommand, AvatarUploadUrlDto>
{
    public ValueTask<AvatarUploadUrlDto> Handle(CreateAvatarUploadUrlCommand command, CancellationToken cancellationToken)
    {
        var key = Avatars.NewKey(command.UserId, command.ContentType);
        var upload = storage.CreateUploadUrl(key, command.ContentType, Avatars.UploadUrlLifetime);
        return ValueTask.FromResult(new AvatarUploadUrlDto(upload.Url, key, upload.ExpiresAt));
    }
}

public sealed record SetAvatarCommand(Guid UserId, string ObjectKey) : ICommand<ProfileDto>;

public sealed class SetAvatarCommandValidator : AbstractValidator<SetAvatarCommand>
{
    public SetAvatarCommandValidator()
    {
        RuleFor(x => x.ObjectKey).NotEmpty().MaximumLength(512);
    }
}

public sealed class SetAvatarCommandHandler(
    IProfileRepository profiles,
    IObjectStorage storage,
    IReviewDirectory reviews,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SetAvatarCommand, ProfileDto>
{
    public async ValueTask<ProfileDto> Handle(SetAvatarCommand command, CancellationToken cancellationToken)
    {
        // The key is client input: without this check a user could claim another user's file.
        if (!command.ObjectKey.StartsWith(Avatars.PrefixOf(command.UserId), StringComparison.Ordinal))
        {
            throw ProfileAppErrors.AvatarNotUploaded();
        }

        var profile = await profiles.GetByUserIdAsync(command.UserId, cancellationToken)
            ?? throw ProfileAppErrors.ProfileNotFound();
        var uploaded = await storage.GetInfoAsync(command.ObjectKey, cancellationToken)
            ?? throw ProfileAppErrors.AvatarNotUploaded();

        // A presigned URL cannot limit what is uploaded to it, so the limits promised when
        // the URL was issued are enforced here, on what actually arrived.
        if (uploaded.ContentLength > Avatars.MaxBytes || !Avatars.IsAllowedContentType(uploaded.ContentType))
        {
            await storage.DeleteAsync(command.ObjectKey, cancellationToken);
            throw ProfileAppErrors.AvatarInvalid();
        }

        var previous = profile.SetAvatar(command.ObjectKey, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (previous is not null && previous != command.ObjectKey)
        {
            await storage.DeleteAsync(previous, cancellationToken);
        }

        return profile.ToDto(storage, await reviews.GetRatingAsync(profile.UserId, cancellationToken));
    }
}

public sealed record DeleteAvatarCommand(Guid UserId) : ICommand;

public sealed class DeleteAvatarCommandHandler(
    IProfileRepository profiles,
    IObjectStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<DeleteAvatarCommand>
{
    public async ValueTask<Unit> Handle(DeleteAvatarCommand command, CancellationToken cancellationToken)
    {
        var profile = await profiles.GetByUserIdAsync(command.UserId, cancellationToken)
            ?? throw ProfileAppErrors.ProfileNotFound();

        var previous = profile.RemoveAvatar(timeProvider.GetUtcNow());
        if (previous is not null)
        {
            // Save first: a dangling file is harmless, a profile pointing at a deleted file is not.
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await storage.DeleteAsync(previous, cancellationToken);
        }

        return Unit.Value;
    }
}
