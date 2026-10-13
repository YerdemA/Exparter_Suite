using Exparter.Domain.Common;
using Exparter.Domain.Skills;

namespace Exparter.Domain.Tests.Skills;

public class SlugTests
{
    [Theory]
    [InlineData("Gitar", "gitar")]
    [InlineData("İngilizce", "ingilizce")]
    [InlineData("IŞIK", "isik")]
    [InlineData("ışık", "isik")]
    [InlineData("Örgü", "orgu")]
    [InlineData("Ahşap işleri", "ahsap-isleri")]
    [InlineData("Yapay zekâ ve makine öğrenmesi", "yapay-zeka-ve-makine-ogrenmesi")]
    [InlineData("UI/UX tasarımı", "ui-ux-tasarimi")]
    [InlineData("Türkçe (yabancılar için)", "turkce-yabancilar-icin")]
    [InlineData("  3D   modelleme  ", "3d-modelleme")]
    [InlineData("C++", "c")]
    [InlineData("!!!", "")]
    public void From_reduces_names_to_ascii_words(string name, string expected) =>
        Assert.Equal(expected, Slug.From(name));

    [Fact]
    public void Names_differing_only_in_case_accents_or_spacing_share_a_slug() =>
        Assert.Equal(Slug.From("Yapay Zekâ"), Slug.From("  yapay   zeka "));
}

public class SkillTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Propose_creates_a_pending_skill_with_a_slug()
    {
        var categoryId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var skill = Skill.Propose(categoryId, "  Ukulele Çalma ", userId, Now);

        Assert.Equal("Ukulele Çalma", skill.Name);
        Assert.Equal("ukulele-calma", skill.Slug);
        Assert.Equal(SkillStatus.PendingApproval, skill.Status);
        Assert.Equal(userId, skill.ProposedByUserId);
        Assert.Equal(categoryId, skill.CategoryId);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("   ")]
    [InlineData("!!!")]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901")]
    public void Propose_rejects_unusable_names(string name)
    {
        var exception = Assert.Throws<DomainException>(() => Skill.Propose(Guid.NewGuid(), name, Guid.NewGuid(), Now));

        Assert.Equal(SkillErrors.InvalidSkillNameCode, exception.Code);
    }

    [Theory]
    [InlineData(true, SkillStatus.Approved)]
    [InlineData(false, SkillStatus.Rejected)]
    public void A_pending_proposal_can_be_reviewed_once(bool approve, SkillStatus expected)
    {
        var skill = Skill.Propose(Guid.NewGuid(), "Ukulele", Guid.NewGuid(), Now);

        if (approve)
        {
            skill.Approve(Now.AddHours(1));
        }
        else
        {
            skill.Reject(Now.AddHours(1));
        }

        Assert.Equal(expected, skill.Status);
        Assert.Equal(Now.AddHours(1), skill.ReviewedAt);
        Assert.Equal(SkillErrors.InvalidStateCode, Assert.Throws<DomainException>(() => skill.Approve(Now)).Code);
        Assert.Equal(SkillErrors.InvalidStateCode, Assert.Throws<DomainException>(() => skill.Reject(Now)).Code);
    }
}

public class UserSkillTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    private static Skill ApprovedSkill(string name)
    {
        var skill = Skill.Propose(Guid.NewGuid(), name, Guid.NewGuid(), Now);
        skill.Approve(Now);
        return skill;
    }

    private static UserSkill Add(Skill skill, UserSkillType type, SkillLevel level, List<UserSkill> existing)
    {
        var userSkill = UserSkill.Add(UserId, skill, type, level, existing, Now);
        existing.Add(userSkill);
        return userSkill;
    }

    [Fact]
    public void Add_puts_an_approved_skill_on_the_profile()
    {
        var skill = ApprovedSkill("Gitar");

        var userSkill = Add(skill, UserSkillType.Offered, SkillLevel.Advanced, []);

        Assert.Equal(UserId, userSkill.UserId);
        Assert.Equal(skill.Id, userSkill.SkillId);
        Assert.Equal(UserSkillType.Offered, userSkill.Type);
        Assert.Equal(SkillLevel.Advanced, userSkill.Level);
    }

    [Fact]
    public void Add_rejects_a_skill_that_is_not_approved()
    {
        var pending = Skill.Propose(Guid.NewGuid(), "Ukulele", Guid.NewGuid(), Now);

        var exception = Assert.Throws<DomainException>(
            () => UserSkill.Add(UserId, pending, UserSkillType.Offered, SkillLevel.Beginner, [], Now));

        Assert.Equal(SkillErrors.SkillNotApprovedCode, exception.Code);
    }

    [Fact]
    public void Add_rejects_the_same_skill_twice_with_the_same_type()
    {
        var skill = ApprovedSkill("Gitar");
        var existing = new List<UserSkill>();
        Add(skill, UserSkillType.Offered, SkillLevel.Beginner, existing);

        var exception = Assert.Throws<DomainException>(
            () => Add(skill, UserSkillType.Offered, SkillLevel.Expert, existing));

        Assert.Equal(SkillErrors.UserSkillAlreadyExistsCode, exception.Code);
    }

    [Fact]
    public void Add_allows_five_skills_of_each_type_and_no_more()
    {
        var existing = new List<UserSkill>();
        foreach (var type in new[] { UserSkillType.Offered, UserSkillType.Wanted })
        {
            for (var i = 0; i < UserSkillRules.MaxPerType; i++)
            {
                Add(ApprovedSkill($"Yetenek {type} {i}"), type, SkillLevel.Beginner, existing);
            }
        }

        Assert.Equal(10, existing.Count);
        foreach (var type in new[] { UserSkillType.Offered, UserSkillType.Wanted })
        {
            var exception = Assert.Throws<DomainException>(
                () => Add(ApprovedSkill($"Fazladan {type}"), type, SkillLevel.Beginner, existing));
            Assert.Equal(SkillErrors.SkillLimitReachedCode, exception.Code);
        }
    }

    [Theory]
    [InlineData(SkillLevel.Beginner, SkillLevel.Intermediate, true)]
    [InlineData(SkillLevel.Advanced, SkillLevel.Expert, true)]
    [InlineData(SkillLevel.Intermediate, SkillLevel.Intermediate, false)]
    [InlineData(SkillLevel.Advanced, SkillLevel.Beginner, false)]
    [InlineData(SkillLevel.Expert, SkillLevel.Expert, false)]
    public void A_skill_can_be_offered_and_wanted_only_if_the_wanted_level_is_higher(
        SkillLevel offered,
        SkillLevel wanted,
        bool allowed)
    {
        var skill = ApprovedSkill("Gitar");

        // The rule holds whichever of the two is added first.
        foreach (var offeredFirst in new[] { true, false })
        {
            var existing = new List<UserSkill>();
            var first = offeredFirst ? (UserSkillType.Offered, offered) : (UserSkillType.Wanted, wanted);
            var second = offeredFirst ? (UserSkillType.Wanted, wanted) : (UserSkillType.Offered, offered);
            Add(skill, first.Item1, first.Item2, existing);

            if (allowed)
            {
                Add(skill, second.Item1, second.Item2, existing);
                Assert.Equal(2, existing.Count);
            }
            else
            {
                var exception = Assert.Throws<DomainException>(() => Add(skill, second.Item1, second.Item2, existing));
                Assert.Equal(SkillErrors.WantedLevelNotHigherCode, exception.Code);
            }
        }
    }

    [Fact]
    public void ChangeLevel_keeps_offered_and_wanted_levels_consistent()
    {
        var skill = ApprovedSkill("Gitar");
        var existing = new List<UserSkill>();
        var offered = Add(skill, UserSkillType.Offered, SkillLevel.Beginner, existing);
        var wanted = Add(skill, UserSkillType.Wanted, SkillLevel.Advanced, existing);

        offered.ChangeLevel(SkillLevel.Intermediate, existing);
        Assert.Equal(SkillLevel.Intermediate, offered.Level);

        Assert.Equal(
            SkillErrors.WantedLevelNotHigherCode,
            Assert.Throws<DomainException>(() => offered.ChangeLevel(SkillLevel.Advanced, existing)).Code);
        Assert.Equal(
            SkillErrors.WantedLevelNotHigherCode,
            Assert.Throws<DomainException>(() => wanted.ChangeLevel(SkillLevel.Intermediate, existing)).Code);
        Assert.Equal(SkillLevel.Intermediate, offered.Level);
        Assert.Equal(SkillLevel.Advanced, wanted.Level);
    }

    [Fact]
    public void ChangeLevel_is_free_for_a_skill_with_only_one_type()
    {
        var existing = new List<UserSkill>();
        var offered = Add(ApprovedSkill("Gitar"), UserSkillType.Offered, SkillLevel.Beginner, existing);

        offered.ChangeLevel(SkillLevel.Expert, existing);

        Assert.Equal(SkillLevel.Expert, offered.Level);
    }
}
