using Exparter.Application.Notifications;
using Exparter.Domain.Notifications;
using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Domain.Skills;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Skills;

public sealed record ListSkillCategoriesQuery : IQuery<IReadOnlyList<SkillCategoryDto>>;

public sealed class ListSkillCategoriesQueryHandler(ISkillRepository skills)
    : IQueryHandler<ListSkillCategoriesQuery, IReadOnlyList<SkillCategoryDto>>
{
    public async ValueTask<IReadOnlyList<SkillCategoryDto>> Handle(
        ListSkillCategoriesQuery query,
        CancellationToken cancellationToken) =>
        (await skills.ListCategoriesAsync(cancellationToken))
            .Select(category => new SkillCategoryDto(category.Id, category.Name, category.Slug))
            .ToList();
}

public sealed record ListSkillsQuery(Guid? CategoryId, string? Search, int? Page, int? PageSize) : IQuery<PagedList<SkillDto>>;

public sealed class ListSkillsQueryValidator : AbstractValidator<ListSkillsQuery>
{
    public ListSkillsQueryValidator()
    {
        RuleFor(x => x.Search).MaximumLength(Skill.NameMaxLength);
    }
}

public sealed class ListSkillsQueryHandler(ISkillRepository skills) : IQueryHandler<ListSkillsQuery, PagedList<SkillDto>>
{
    public async ValueTask<PagedList<SkillDto>> Handle(ListSkillsQuery query, CancellationToken cancellationToken)
    {
        // Searching by slug makes the match ignore case and accents: "ingilizce" finds "İngilizce".
        var fragment = string.IsNullOrWhiteSpace(query.Search) ? null : Slug.From(query.Search);
        var page = await skills.SearchApprovedAsync(
            query.CategoryId,
            fragment,
            PageRequest.From(query.Page, query.PageSize),
            cancellationToken);
        return page.Map(SkillDto.From);
    }
}

public sealed record ProposeSkillCommand(Guid UserId, Guid CategoryId, string Name) : ICommand<SkillDto>;

public sealed class ProposeSkillCommandValidator : AbstractValidator<ProposeSkillCommand>
{
    public ProposeSkillCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name)
            .NotEmpty()
            .Must(name => name is null || (name.Trim().Length is >= Skill.NameMinLength and <= Skill.NameMaxLength && Slug.From(name).Length > 0))
            .WithMessage($"The skill name must be {Skill.NameMinLength} to {Skill.NameMaxLength} characters and contain a letter or digit.");
    }
}

public sealed class ProposeSkillCommandHandler(
    ISkillRepository skills,
    IUserDirectory users,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ProposeSkillCommand, SkillDto>
{
    public async ValueTask<SkillDto> Handle(ProposeSkillCommand command, CancellationToken cancellationToken)
    {
        // Only the email is required here, not the full interaction requirements: a user
        // whose skill is missing from the catalog has to propose it before offering anything.
        var user = await users.FindAsync(command.UserId, cancellationToken);
        if (user is not { IsActive: true, IsEmailVerified: true })
        {
            throw SkillAppErrors.EmailNotVerified();
        }

        if (!await skills.CategoryExistsAsync(command.CategoryId, cancellationToken))
        {
            throw new ValidationException(
            [
                new FluentValidation.Results.ValidationFailure(nameof(command.CategoryId), "The category does not exist."),
            ]);
        }

        var skill = Skill.Propose(command.CategoryId, command.Name, command.UserId, timeProvider.GetUtcNow());
        if (await skills.SlugExistsAsync(skill.Slug, cancellationToken))
        {
            throw SkillAppErrors.SkillAlreadyExists();
        }

        skills.Add(skill);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SkillDto.From(skill);
    }
}

public sealed record ListOwnSkillProposalsQuery(Guid UserId, int? Page, int? PageSize) : IQuery<PagedList<SkillDto>>;

public sealed class ListOwnSkillProposalsQueryHandler(ISkillRepository skills)
    : IQueryHandler<ListOwnSkillProposalsQuery, PagedList<SkillDto>>
{
    public async ValueTask<PagedList<SkillDto>> Handle(ListOwnSkillProposalsQuery query, CancellationToken cancellationToken) =>
        (await skills.ListProposedByAsync(query.UserId, PageRequest.From(query.Page, query.PageSize), cancellationToken))
            .Map(SkillDto.From);
}

public sealed record AdminListSkillProposalsQuery(SkillStatus? Status, int? Page, int? PageSize)
    : IQuery<PagedList<AdminSkillProposalDto>>;

public sealed class AdminListSkillProposalsQueryHandler(ISkillRepository skills)
    : IQueryHandler<AdminListSkillProposalsQuery, PagedList<AdminSkillProposalDto>>
{
    public async ValueTask<PagedList<AdminSkillProposalDto>> Handle(
        AdminListSkillProposalsQuery query,
        CancellationToken cancellationToken) =>
        (await skills.ListProposalsAsync(query.Status, PageRequest.From(query.Page, query.PageSize), cancellationToken))
            .Map(AdminSkillProposalDto.From);
}

public sealed record ReviewSkillProposalCommand(Guid SkillId, bool Approve) : ICommand<SkillDto>;

public sealed class ReviewSkillProposalCommandHandler(
    ISkillRepository skills,
    IUserNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ReviewSkillProposalCommand, SkillDto>
{
    public async ValueTask<SkillDto> Handle(ReviewSkillProposalCommand command, CancellationToken cancellationToken)
    {
        var skill = await skills.GetByIdAsync(command.SkillId, cancellationToken)
            ?? throw SkillAppErrors.SkillNotFound();

        if (command.Approve)
        {
            skill.Approve(timeProvider.GetUtcNow());
        }
        else
        {
            skill.Reject(timeProvider.GetUtcNow());
        }

        if (skill.ProposedByUserId is { } proposerId)
        {
            notifier.Notify(
                proposerId,
                command.Approve ? NotificationType.SkillProposalApproved : NotificationType.SkillProposalRejected,
                new NotificationData(SkillId: skill.Id));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return SkillDto.From(skill);
    }
}
