using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Application.Matching;
using Exparter.Application.Profiles;
using Exparter.Application.Skills;
using Exparter.Application.Tests.Identity;
using Exparter.Application.Tests.Profiles;
using Exparter.Domain.Common;
using Exparter.Domain.Skills;
using FluentValidation;

namespace Exparter.Application.Tests.Skills;

internal sealed class InMemorySkills : ISkillRepository
{
    public Guid CategoryId { get; } = Guid.NewGuid();

    public List<Skill> Items { get; } = [];

    public Task<IReadOnlyList<SkillCategory>> ListCategoriesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SkillCategory>>([]);

    public Task<bool> CategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken) =>
        Task.FromResult(categoryId == CategoryId);

    public Task<PagedList<Skill>> SearchApprovedAsync(
        Guid? categoryId,
        string? slugFragment,
        PageRequest page,
        CancellationToken cancellationToken) =>
        Task.FromResult(Paged(
            Items.Where(x => x.Status == SkillStatus.Approved
                && (categoryId is null || x.CategoryId == categoryId)
                && (slugFragment is null || x.Slug.Contains(slugFragment))).OrderBy(x => x.Slug),
            page));

    public Task<Skill?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<Skill>> ListByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Skill>>(Items.Where(x => ids.Contains(x.Id)).ToList());

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(x => x.Slug == slug));

    public Task<PagedList<Skill>> ListProposedByAsync(Guid userId, PageRequest page, CancellationToken cancellationToken) =>
        Task.FromResult(Paged(Items.Where(x => x.ProposedByUserId == userId), page));

    public Task<PagedList<Skill>> ListProposalsAsync(SkillStatus? status, PageRequest page, CancellationToken cancellationToken) =>
        Task.FromResult(Paged(Items.Where(x => x.ProposedByUserId is not null && (status is null || x.Status == status)), page));

    public void Add(Skill skill) => Items.Add(skill);

    public Skill AddApproved(string name)
    {
        var skill = Skill.Propose(CategoryId, name, Guid.NewGuid(), DateTimeOffset.UnixEpoch);
        skill.Approve(DateTimeOffset.UnixEpoch);
        Items.Add(skill);
        return skill;
    }

    private static PagedList<T> Paged<T>(IEnumerable<T> source, PageRequest page)
    {
        var all = source.ToList();
        return new PagedList<T>(all.Skip(page.Skip).Take(page.PageSize).ToList(), page.Page, page.PageSize, all.Count);
    }
}

internal sealed class InMemoryUserSkills(InMemorySkills skills) : IUserSkillRepository
{
    public List<UserSkill> Items { get; } = [];

    public Task<IReadOnlyList<UserSkill>> ListByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserSkill>>(Items.Where(x => x.UserId == userId).ToList());

    public Task<PagedList<UserSkillWithSkill>> ListWithSkillsAsync(
        Guid userId,
        UserSkillType? type,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var all = Items
            .Where(x => x.UserId == userId && (type is null || x.Type == type))
            .Select(x => new UserSkillWithSkill(x, skills.Items.Single(skill => skill.Id == x.SkillId)))
            .ToList();
        return Task.FromResult(new PagedList<UserSkillWithSkill>(all, page.Page, page.PageSize, all.Count));
    }

    public Task<bool> HasOfferedSkillAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(x => x.UserId == userId && x.Type == UserSkillType.Offered));

    public Task<bool> OffersSkillAsync(Guid userId, Guid skillId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(x => x.UserId == userId && x.SkillId == skillId && x.Type == UserSkillType.Offered));

    public void Add(UserSkill userSkill) => Items.Add(userSkill);

    public void Remove(UserSkill userSkill) => Items.Remove(userSkill);
}

internal sealed class FakeSkillUsage : ISkillUsage
{
    public HashSet<Guid> SkillsInUse { get; } = [];

    public Task<bool> IsOfferedSkillInUseAsync(Guid userId, Guid skillId, CancellationToken cancellationToken) =>
        Task.FromResult(SkillsInUse.Contains(skillId));
}

public class SkillHandlerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InMemorySkills _skills = new();
    private readonly InMemoryUserSkills _userSkills;
    private readonly FakeUserDirectory _users = new();
    private readonly FakeSkillUsage _usage = new();
    private readonly CountingUnitOfWork _unitOfWork = new();
    private readonly TestClock _clock = new();

    public SkillHandlerTests()
    {
        _userSkills = new InMemoryUserSkills(_skills);
        _users.Users[_userId] = new UserInfo(_userId, "ayse", IsActive: true, IsEmailVerified: true);
    }

    private AddOwnSkillCommandHandler AddHandler => new(_skills, _userSkills, _unitOfWork, _clock);

    private Task<UserSkillDto> AddAsync(Skill skill, UserSkillType type = UserSkillType.Offered, SkillLevel level = SkillLevel.Intermediate) =>
        AddHandler.Handle(new AddOwnSkillCommand(_userId, skill.Id, type, level), CancellationToken.None).AsTask();

    [Fact]
    public async Task Search_matches_names_ignoring_case_and_accents_and_hides_unapproved_skills()
    {
        _skills.AddApproved("İngilizce");
        _skills.AddApproved("Gitar");
        _skills.Add(Skill.Propose(_skills.CategoryId, "İngiliz edebiyatı", _userId, _clock.GetUtcNow()));

        var page = await new ListSkillsQueryHandler(_skills)
            .Handle(new ListSkillsQuery(null, "INGILIZ", null, null), CancellationToken.None);

        Assert.Equal("İngilizce", Assert.Single(page.Items).Name);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(20, page.PageSize);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 51)]
    public async Task Listing_rejects_out_of_range_paging(int page, int pageSize) =>
        await Assert.ThrowsAsync<ValidationException>(
            async () => await new ListSkillsQueryHandler(_skills)
                .Handle(new ListSkillsQuery(null, null, page, pageSize), CancellationToken.None));

    [Fact]
    public async Task Add_puts_the_skill_on_the_profile()
    {
        var skill = _skills.AddApproved("Gitar");

        var dto = await AddAsync(skill, UserSkillType.Offered, SkillLevel.Advanced);

        Assert.Equal("Gitar", dto.Skill.Name);
        Assert.Equal(UserSkillType.Offered, dto.Type);
        Assert.Equal(SkillLevel.Advanced, dto.Level);
        Assert.Single(_userSkills.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Add_fails_for_an_unknown_skill()
    {
        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await AddHandler.Handle(
                new AddOwnSkillCommand(_userId, Guid.NewGuid(), UserSkillType.Offered, SkillLevel.Beginner),
                CancellationToken.None));

        Assert.Equal(AppErrorKind.NotFound, exception.Kind);
    }

    [Fact]
    public async Task Add_enforces_the_limit_per_type()
    {
        for (var i = 0; i < UserSkillRules.MaxPerType; i++)
        {
            await AddAsync(_skills.AddApproved($"Yetenek {i}"));
        }

        var exception = await Assert.ThrowsAsync<DomainException>(async () => await AddAsync(_skills.AddApproved("Fazladan")));

        Assert.Equal(SkillErrors.SkillLimitReachedCode, exception.Code);
        Assert.Equal(UserSkillRules.MaxPerType, _userSkills.Items.Count);
    }

    [Fact]
    public async Task Update_changes_the_level_of_the_callers_own_skill_only()
    {
        var added = await AddAsync(_skills.AddApproved("Gitar"), level: SkillLevel.Beginner);
        var handler = new UpdateOwnSkillCommandHandler(_skills, _userSkills, _unitOfWork);

        var dto = await handler.Handle(new UpdateOwnSkillCommand(_userId, added.Id, SkillLevel.Expert), CancellationToken.None);
        Assert.Equal(SkillLevel.Expert, dto.Level);

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await handler.Handle(
                new UpdateOwnSkillCommand(Guid.NewGuid(), added.Id, SkillLevel.Beginner),
                CancellationToken.None));
        Assert.Equal(AppErrorKind.NotFound, exception.Kind);
        Assert.Equal(SkillLevel.Expert, _userSkills.Items.Single().Level);
    }

    [Fact]
    public async Task Remove_deletes_the_callers_own_skill_only()
    {
        var added = await AddAsync(_skills.AddApproved("Gitar"));
        var handler = new RemoveOwnSkillCommandHandler(_userSkills, _usage, _unitOfWork);

        await Assert.ThrowsAsync<AppException>(
            async () => await handler.Handle(new RemoveOwnSkillCommand(Guid.NewGuid(), added.Id), CancellationToken.None));
        Assert.Single(_userSkills.Items);

        await handler.Handle(new RemoveOwnSkillCommand(_userId, added.Id), CancellationToken.None);
        Assert.Empty(_userSkills.Items);
    }

    [Fact]
    public async Task An_offered_skill_in_use_cannot_be_removed_but_a_wanted_one_can()
    {
        var skill = _skills.AddApproved("Gitar");
        var offered = await AddAsync(skill, level: SkillLevel.Beginner);
        var wanted = await AddAsync(skill, UserSkillType.Wanted, SkillLevel.Advanced);
        _usage.SkillsInUse.Add(skill.Id);
        var handler = new RemoveOwnSkillCommandHandler(_userSkills, _usage, _unitOfWork);

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await handler.Handle(new RemoveOwnSkillCommand(_userId, offered.Id), CancellationToken.None));
        Assert.Equal("user_skill_in_use", exception.Code);

        await handler.Handle(new RemoveOwnSkillCommand(_userId, wanted.Id), CancellationToken.None);
        Assert.Equal(offered.Id, Assert.Single(_userSkills.Items).Id);
    }

    [Fact]
    public async Task Own_skills_can_be_filtered_by_type()
    {
        await AddAsync(_skills.AddApproved("Gitar"), UserSkillType.Offered);
        await AddAsync(_skills.AddApproved("İspanyolca"), UserSkillType.Wanted);

        var wanted = await new ListOwnSkillsQueryHandler(_userSkills)
            .Handle(new ListOwnSkillsQuery(_userId, UserSkillType.Wanted, null, null), CancellationToken.None);

        Assert.Equal("İspanyolca", Assert.Single(wanted.Items).Skill.Name);
    }

    [Fact]
    public async Task Another_users_skills_are_listed_only_while_that_account_is_active()
    {
        await AddAsync(_skills.AddApproved("Gitar"));
        var handler = new ListUserSkillsQueryHandler(_userSkills, _users);

        // An unverified email does not hide the user; only an inactive account does.
        _users.Users[_userId] = new UserInfo(_userId, "ayse", IsActive: true, IsEmailVerified: false);
        var page = await handler.Handle(new ListUserSkillsQuery(_userId, null, null, null), CancellationToken.None);
        Assert.Single(page.Items);

        _users.Users[_userId] = new UserInfo(_userId, "ayse", IsActive: false, IsEmailVerified: true);
        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await handler.Handle(new ListUserSkillsQuery(_userId, null, null, null), CancellationToken.None));
        Assert.Equal(AppErrorKind.NotFound, exception.Kind);
    }

    [Fact]
    public async Task Propose_creates_a_pending_skill_that_search_does_not_show()
    {
        var dto = await new ProposeSkillCommandHandler(_skills, _users, _unitOfWork, _clock)
            .Handle(new ProposeSkillCommand(_userId, _skills.CategoryId, "Ukulele"), CancellationToken.None);

        Assert.Equal(SkillStatus.PendingApproval, dto.Status);
        var search = await new ListSkillsQueryHandler(_skills).Handle(new ListSkillsQuery(null, "ukulele", null, null), CancellationToken.None);
        Assert.Empty(search.Items);
        var own = await new ListOwnSkillProposalsQueryHandler(_skills)
            .Handle(new ListOwnSkillProposalsQuery(_userId, null, null), CancellationToken.None);
        Assert.Equal("Ukulele", Assert.Single(own.Items).Name);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Propose_requires_an_active_account_with_a_verified_email(bool isActive, bool isEmailVerified)
    {
        _users.Users[_userId] = new UserInfo(_userId, "ayse", isActive, isEmailVerified);

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await new ProposeSkillCommandHandler(_skills, _users, _unitOfWork, _clock)
                .Handle(new ProposeSkillCommand(_userId, _skills.CategoryId, "Ukulele"), CancellationToken.None));

        Assert.Equal(AppErrorKind.Forbidden, exception.Kind);
        Assert.Equal("email_not_verified", exception.Code);
    }

    [Fact]
    public async Task Propose_rejects_a_name_that_matches_an_existing_skill()
    {
        _skills.AddApproved("Yapay zekâ");

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await new ProposeSkillCommandHandler(_skills, _users, _unitOfWork, _clock)
                .Handle(new ProposeSkillCommand(_userId, _skills.CategoryId, "YAPAY ZEKA"), CancellationToken.None));

        Assert.Equal("skill_already_exists", exception.Code);
    }

    [Fact]
    public async Task Propose_rejects_an_unknown_category() =>
        await Assert.ThrowsAsync<ValidationException>(
            async () => await new ProposeSkillCommandHandler(_skills, _users, _unitOfWork, _clock)
                .Handle(new ProposeSkillCommand(_userId, Guid.NewGuid(), "Ukulele"), CancellationToken.None));

    [Theory]
    [InlineData("A")]
    [InlineData("!!!")]
    [InlineData("")]
    public void Propose_validator_rejects_unusable_names(string name) =>
        Assert.False(new ProposeSkillCommandValidator().Validate(new ProposeSkillCommand(_userId, Guid.NewGuid(), name)).IsValid);

    [Fact]
    public async Task Approving_a_proposal_makes_it_searchable_and_usable()
    {
        var proposal = Skill.Propose(_skills.CategoryId, "Ukulele", _userId, _clock.GetUtcNow());
        _skills.Add(proposal);
        await Assert.ThrowsAsync<DomainException>(async () => await AddAsync(proposal));

        var pending = await new AdminListSkillProposalsQueryHandler(_skills)
            .Handle(new AdminListSkillProposalsQuery(SkillStatus.PendingApproval, null, null), CancellationToken.None);
        Assert.Equal(_userId, Assert.Single(pending.Items).ProposedByUserId);

        var notifier = new Exparter.Application.Tests.Matching.RecordingNotifier();
        var dto = await new ReviewSkillProposalCommandHandler(_skills, notifier, _unitOfWork, _clock)
            .Handle(new ReviewSkillProposalCommand(proposal.Id, Approve: true), CancellationToken.None);

        Assert.Equal(SkillStatus.Approved, dto.Status);
        Assert.Equal(
            [(_userId, Exparter.Domain.Notifications.NotificationType.SkillProposalApproved)],
            notifier.Take());
        await AddAsync(proposal);
        Assert.Single(_userSkills.Items);
    }

    [Fact]
    public async Task Reviewing_fails_for_an_unknown_or_already_reviewed_proposal()
    {
        var handler = new ReviewSkillProposalCommandHandler(_skills, new Exparter.Application.Tests.Matching.RecordingNotifier(), _unitOfWork, _clock);
        var proposal = Skill.Propose(_skills.CategoryId, "Ukulele", _userId, _clock.GetUtcNow());
        _skills.Add(proposal);

        await Assert.ThrowsAsync<AppException>(
            async () => await handler.Handle(new ReviewSkillProposalCommand(Guid.NewGuid(), Approve: true), CancellationToken.None));

        await handler.Handle(new ReviewSkillProposalCommand(proposal.Id, Approve: false), CancellationToken.None);
        var exception = await Assert.ThrowsAsync<DomainException>(
            async () => await handler.Handle(new ReviewSkillProposalCommand(proposal.Id, Approve: true), CancellationToken.None));
        Assert.Equal(SkillErrors.InvalidStateCode, exception.Code);
    }
}

public class InteractionRequirementsTests
{
    private sealed class StubProfiles(bool complete) : IProfileDirectory
    {
        public Task<bool> IsCompleteAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(complete);

        public Task<IReadOnlyList<ProfileCard>> GetCardsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProfileCard>>([]);
    }

    private sealed class StubSkills(bool hasOffered) : IUserSkillDirectory
    {
        public Task<bool> HasOfferedSkillAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(hasOffered);

        public Task<bool> OffersSkillAsync(Guid userId, Guid skillId, CancellationToken cancellationToken) => Task.FromResult(hasOffered);

        public Task<IReadOnlyDictionary<Guid, string>> GetSkillNamesAsync(IReadOnlyCollection<Guid> skillIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
    }

    private static readonly Guid UserId = Guid.NewGuid();

    private static InteractionRequirements Create(bool active, bool verified, bool profile, bool skill)
    {
        var users = new FakeUserDirectory();
        users.Users[UserId] = new UserInfo(UserId, "ayse", active, verified);
        return new InteractionRequirements(users, new StubProfiles(profile), new StubSkills(skill));
    }

    [Theory]
    [InlineData(true, true, true, true, true)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(false, true, true, true, false)]
    public async Task A_user_can_interact_only_when_every_requirement_is_met(
        bool active,
        bool verified,
        bool profile,
        bool skill,
        bool expected)
    {
        var requirements = Create(active, verified, profile, skill);

        var status = await requirements.GetAsync(UserId, CancellationToken.None);

        Assert.Equal(new InteractionStatus(verified, profile, skill, expected), status);
        if (expected)
        {
            await requirements.EnsureCanInteractAsync(UserId, CancellationToken.None);
        }
        else
        {
            var exception = await Assert.ThrowsAsync<AppException>(
                async () => await requirements.EnsureCanInteractAsync(UserId, CancellationToken.None));
            Assert.Equal(AppErrorKind.Forbidden, exception.Kind);
            Assert.Equal("interaction_requirements_not_met", exception.Code);
        }
    }

    [Fact]
    public async Task An_unknown_user_meets_nothing() =>
        Assert.Equal(
            InteractionStatus.None,
            await Create(true, true, true, true).GetAsync(Guid.NewGuid(), CancellationToken.None));
}
