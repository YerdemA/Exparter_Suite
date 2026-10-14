using Exparter.Application.Common;
using Exparter.Domain.Identity;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Identity;

public sealed record GetAccountQuery(Guid UserId) : IQuery<AccountDto>;

public sealed class GetAccountQueryHandler(IUserRepository users) : IQueryHandler<GetAccountQuery, AccountDto>
{
    public async ValueTask<AccountDto> Handle(GetAccountQuery query, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(query.UserId, cancellationToken)
            ?? throw IdentityAppErrors.AccountNotFound();
        return AccountDto.From(user);
    }
}

public sealed record RequestAccountDeletionCommand(Guid UserId, string Password) : ICommand<AccountDeletionScheduled>;

public sealed class RequestAccountDeletionCommandValidator : AbstractValidator<RequestAccountDeletionCommand>
{
    public RequestAccountDeletionCommandValidator()
    {
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class RequestAccountDeletionCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IPublisher publisher,
    IRealtimeConnections connections,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<RequestAccountDeletionCommand, AccountDeletionScheduled>
{
    public async ValueTask<AccountDeletionScheduled> Handle(
        RequestAccountDeletionCommand command,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken)
            ?? throw IdentityAppErrors.AccountNotFound();
        if (!passwordHasher.Verify(user.PasswordHash, command.Password))
        {
            throw IdentityAppErrors.InvalidPassword();
        }

        var now = timeProvider.GetUtcNow();
        user.RequestDeletion(now);
        foreach (var refreshToken in await refreshTokens.ListActiveByUserAsync(user.Id, now, cancellationToken))
        {
            refreshToken.Revoke(now);
        }

        // Published before saving: what other modules close for the leaving user is
        // committed together with the request, or not at all.
        await publisher.Publish(new AccountDeletionRequested(user.Id), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        connections.CloseAll(user.Id);
        return new AccountDeletionScheduled(user.PermanentDeletionAt!.Value);
    }
}

/// <summary>
/// An administrator stops an account: it is signed out everywhere, cannot sign in and
/// nobody finds it. What it shares with others stays, for when the suspension is lifted.
/// </summary>
public sealed record SuspendUserCommand(Guid AdminId, Guid UserId) : ICommand;

public sealed class SuspendUserCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IRealtimeConnections connections,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SuspendUserCommand>
{
    public async ValueTask<Unit> Handle(SuspendUserCommand command, CancellationToken cancellationToken)
    {
        if (command.UserId == command.AdminId)
        {
            throw IdentityErrors.InvalidState("Administrators cannot suspend themselves.");
        }

        var user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null || user.Status == UserStatus.Deleted)
        {
            throw IdentityAppErrors.AccountNotFound();
        }

        var now = timeProvider.GetUtcNow();
        user.Suspend();
        foreach (var refreshToken in await refreshTokens.ListActiveByUserAsync(user.Id, now, cancellationToken))
        {
            refreshToken.Revoke(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        connections.CloseAll(user.Id);
        return Unit.Value;
    }
}

public sealed record LiftSuspensionCommand(Guid UserId) : ICommand;

public sealed class LiftSuspensionCommandHandler(IUserRepository users, IUnitOfWork unitOfWork)
    : ICommandHandler<LiftSuspensionCommand>
{
    public async ValueTask<Unit> Handle(LiftSuspensionCommand command, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null || user.Status == UserStatus.Deleted)
        {
            throw IdentityAppErrors.AccountNotFound();
        }

        user.LiftSuspension();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
