using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Matching;
using Exparter.Domain.Skills;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Skills;

public sealed record ListOwnSkillsQuery(Guid UserId, UserSkillType? Type, int? Page, int? PageSize) : IQuery<PagedList<UserSkillDto>>;

public sealed class ListOwnSkillsQueryHandler(IUserSkillRepository userSkills)
    : IQueryHandler<ListOwnSkillsQuery, PagedList<UserSkillDto>>
{
    public async ValueTask<PagedList<UserSkillDto>> Handle(ListOwnSkillsQuery query, CancellationToken cancellationToken) =>
        (await userSkills.ListWithSkillsAsync(
            query.UserId,
            query.Type,
            PageRequest.From(query.Page, query.PageSize),
            cancellationToken)).Map(UserSkillDto.From);
}

public sealed record ListUserSkillsQuery(Guid UserId, UserSkillType? Type, int? Page, int? PageSize) : IQuery<PagedList<UserSkillDto>>;

public sealed class ListUserSkillsQueryHandler(IUserSkillRepository userSkills, IUserDirectory users)
    : IQueryHandler<ListUserSkillsQuery, PagedList<UserSkillDto>>
{
    public async ValueTask<PagedList<UserSkillDto>> Handle(ListUserSkillsQuery query, CancellationToken cancellationToken)
    {
        // Same visibility as the public profile: any active user.
        if (await users.FindAsync(query.UserId, cancellationToken) is not { IsActive: true })
        {
            throw SkillAppErrors.UserNotFound();
        }

        return (await userSkills.ListWithSkillsAsync(
            query.UserId,
            query.Type,
            PageRequest.From(query.Page, query.PageSize),
            cancellationToken)).Map(UserSkillDto.From);
    }
}

public sealed record AddOwnSkillCommand(Guid UserId, Guid SkillId, UserSkillType Type, SkillLevel Level) : ICommand<UserSkillDto>;

public sealed class AddOwnSkillCommandValidator : AbstractValidator<AddOwnSkillCommand>
{
    public AddOwnSkillCommandValidator()
    {
        RuleFor(x => x.SkillId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Level).IsInEnum();
    }
}

public sealed class AddOwnSkillCommandHandler(
    ISkillRepository skills,
    IUserSkillRepository userSkills,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<AddOwnSkillCommand, UserSkillDto>
{
    public async ValueTask<UserSkillDto> Handle(AddOwnSkillCommand command, CancellationToken cancellationToken)
    {
        var skill = await skills.GetByIdAsync(command.SkillId, cancellationToken)
            ?? throw SkillAppErrors.SkillNotFound();
        var existing = await userSkills.ListByUserAsync(command.UserId, cancellationToken);

        var userSkill = UserSkill.Add(command.UserId, skill, command.Type, command.Level, existing, timeProvider.GetUtcNow());
        userSkills.Add(userSkill);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UserSkillDto.From(userSkill, skill);
    }
}

public sealed record UpdateOwnSkillCommand(Guid UserId, Guid UserSkillId, SkillLevel Level) : ICommand<UserSkillDto>;

public sealed class UpdateOwnSkillCommandValidator : AbstractValidator<UpdateOwnSkillCommand>
{
    public UpdateOwnSkillCommandValidator()
    {
        RuleFor(x => x.Level).IsInEnum();
    }
}

public sealed class UpdateOwnSkillCommandHandler(
    ISkillRepository skills,
    IUserSkillRepository userSkills,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateOwnSkillCommand, UserSkillDto>
{
    public async ValueTask<UserSkillDto> Handle(UpdateOwnSkillCommand command, CancellationToken cancellationToken)
    {
        // Looking the skill up among the caller's own skills is what stops one user from
        // editing another user's skill by guessing its id.
        var existing = await userSkills.ListByUserAsync(command.UserId, cancellationToken);
        var userSkill = existing.FirstOrDefault(item => item.Id == command.UserSkillId)
            ?? throw SkillAppErrors.UserSkillNotFound();

        userSkill.ChangeLevel(command.Level, existing);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var skill = await skills.GetByIdAsync(userSkill.SkillId, cancellationToken)
            ?? throw SkillAppErrors.SkillNotFound();
        return UserSkillDto.From(userSkill, skill);
    }
}

public sealed record RemoveOwnSkillCommand(Guid UserId, Guid UserSkillId) : ICommand;

public sealed class RemoveOwnSkillCommandHandler(IUserSkillRepository userSkills, ISkillUsage usage, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveOwnSkillCommand>
{
    public async ValueTask<Unit> Handle(RemoveOwnSkillCommand command, CancellationToken cancellationToken)
    {
        var existing = await userSkills.ListByUserAsync(command.UserId, cancellationToken);
        var userSkill = existing.FirstOrDefault(item => item.Id == command.UserSkillId)
            ?? throw SkillAppErrors.UserSkillNotFound();

        // The other user in a pending request or an unfinished match is counting on this skill.
        if (userSkill.Type == UserSkillType.Offered
            && await usage.IsOfferedSkillInUseAsync(command.UserId, userSkill.SkillId, cancellationToken))
        {
            throw AppException.Conflict("user_skill_in_use", "The skill is used by a pending swap request or an unfinished match.");
        }

        userSkills.Remove(userSkill);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}

/// <summary>An account is being erased: the skills on its profile go with it.</summary>
public sealed class RemoveUserSkillsOnAccountErased(IUserSkillRepository userSkills) : INotificationHandler<AccountErased>
{
    public async ValueTask Handle(AccountErased notification, CancellationToken cancellationToken)
    {
        foreach (var userSkill in await userSkills.ListByUserAsync(notification.UserId, cancellationToken))
        {
            userSkills.Remove(userSkill);
        }
    }
}
