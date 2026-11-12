using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Exparter.Api.IntegrationTests.Skills;

[Collection(ApiCollection.Name)]
public class SkillApiTests(ApiFixture fixture)
{
    private readonly HttpClient _client = fixture.CreateClient();

    private static string Unique() => Guid.NewGuid().ToString("N")[..10];

    private async Task<Guid> CategoryIdAsync()
    {
        var categories = await _client.GetFromJsonAsync<JsonElement>("/api/v1/skill-categories");
        return categories[0].GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Catalog_is_seeded_and_public()
    {
        var categories = await _client.GetFromJsonAsync<JsonElement>("/api/v1/skill-categories");
        Assert.Equal(10, categories.GetArrayLength());
        Assert.Equal("Diller", categories[0].GetProperty("name").GetString());

        var all = await _client.GetFromJsonAsync<JsonElement>("/api/v1/skills?pageSize=50");
        Assert.True(all.GetProperty("totalCount").GetInt32() >= 68);
        Assert.Equal(50, all.GetProperty("items").GetArrayLength());

        var languages = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/skills?categoryId={categories[0].GetProperty("id").GetGuid()}&pageSize=50");
        Assert.Contains(languages.GetProperty("items").EnumerateArray(), skill => skill.GetProperty("name").GetString() == "İngilizce");
        Assert.DoesNotContain(languages.GetProperty("items").EnumerateArray(), skill => skill.GetProperty("name").GetString() == "Gitar");
    }

    [Theory]
    [InlineData("ingilizce", "İngilizce")]
    [InlineData("İNGİLİZCE", "İngilizce")]
    [InlineData("yapay zeka", "Yapay zekâ ve makine öğrenmesi")]
    [InlineData("orgu", "Örgü")]
    public async Task Search_ignores_case_and_accents(string query, string expectedName)
    {
        var result = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/skills?q={Uri.EscapeDataString(query)}");

        Assert.Equal(expectedName, result.GetProperty("items")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Listing_rejects_invalid_paging()
    {
        await ProblemAssert.IsAsync(await _client.GetAsync("/api/v1/skills?pageSize=51"), HttpStatusCode.BadRequest, "validation_failed");
        await ProblemAssert.IsAsync(await _client.GetAsync("/api/v1/skills?page=0"), HttpStatusCode.BadRequest, "validation_failed");
    }

    [Fact]
    public async Task Own_skills_can_be_added_listed_changed_and_removed()
    {
        var session = await TestSession.RegisterAsync(_client);

        var offeredId = await session.AddSkillAsync("gitar", "Offered", "Advanced");
        await session.AddSkillAsync("ispanyolca", "Wanted", "Intermediate");

        var all = await session.GetJsonAsync("/api/v1/me/skills");
        Assert.Equal(2, all.GetProperty("totalCount").GetInt32());
        var wanted = await session.GetJsonAsync("/api/v1/me/skills?type=Wanted");
        var only = Assert.Single(wanted.GetProperty("items").EnumerateArray());
        Assert.Equal("İspanyolca", only.GetProperty("skill").GetProperty("name").GetString());
        Assert.Equal("Intermediate", only.GetProperty("level").GetString());

        var update = await session.SendAsync(HttpMethod.Patch, $"/api/v1/me/skills/{offeredId}", new { level = "Expert" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Expert", (await update.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("level").GetString());

        var delete = await session.SendAsync(HttpMethod.Delete, $"/api/v1/me/skills/{offeredId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(1, (await session.GetJsonAsync("/api/v1/me/skills")).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Skill_rules_are_enforced()
    {
        var session = await TestSession.RegisterAsync(_client);
        var other = await TestSession.RegisterAsync(_client);
        var guitar = await session.FindSkillIdAsync("gitar");
        var userSkillId = await session.AddSkillAsync("gitar", "Offered", "Advanced");

        await ProblemAssert.IsAsync(
            await session.SendAsync(HttpMethod.Post, "/api/v1/me/skills", new { skillId = guitar, type = "Offered", level = "Beginner" }),
            HttpStatusCode.Conflict,
            "user_skill_already_exists");

        // Offering at Advanced and wanting a teacher at Advanced or below makes no sense.
        await ProblemAssert.IsAsync(
            await session.SendAsync(HttpMethod.Post, "/api/v1/me/skills", new { skillId = guitar, type = "Wanted", level = "Advanced" }),
            HttpStatusCode.Conflict,
            "wanted_level_not_higher");
        var wantedId = await session.AddSkillAsync("gitar", "Wanted", "Expert");
        await ProblemAssert.IsAsync(
            await session.SendAsync(HttpMethod.Patch, $"/api/v1/me/skills/{userSkillId}", new { level = "Expert" }),
            HttpStatusCode.Conflict,
            "wanted_level_not_higher");

        await ProblemAssert.IsAsync(
            await session.SendAsync(HttpMethod.Post, "/api/v1/me/skills", new { skillId = Guid.NewGuid(), type = "Offered", level = "Beginner" }),
            HttpStatusCode.NotFound,
            "not_found");
        await ProblemAssert.IsAsync(
            await session.SendAsync(HttpMethod.Post, "/api/v1/me/skills", new { skillId = guitar, type = "Sometimes", level = "Beginner" }),
            HttpStatusCode.BadRequest,
            "validation_failed");

        // Another user's skill cannot be changed or removed.
        await ProblemAssert.IsAsync(
            await other.SendAsync(HttpMethod.Patch, $"/api/v1/me/skills/{wantedId}", new { level = "Beginner" }),
            HttpStatusCode.NotFound,
            "not_found");
        await ProblemAssert.IsAsync(
            await other.SendAsync(HttpMethod.Delete, $"/api/v1/me/skills/{wantedId}"),
            HttpStatusCode.NotFound,
            "not_found");
    }

    [Fact]
    public async Task At_most_five_skills_of_a_type_can_be_added()
    {
        var session = await TestSession.RegisterAsync(_client);
        foreach (var name in new[] { "gitar", "piyano", "keman", "baglama", "bateri" })
        {
            await session.AddSkillAsync(name, "Offered", "Beginner");
        }

        var response = await session.SendAsync(
            HttpMethod.Post,
            "/api/v1/me/skills",
            new { skillId = await session.FindSkillIdAsync("ney"), type = "Offered", level = "Beginner" });

        await ProblemAssert.IsAsync(response, HttpStatusCode.Conflict, "skill_limit_reached");

        // The limit is per type: wanted skills are counted separately.
        await session.AddSkillAsync("ney", "Wanted", "Beginner");
    }

    [Fact]
    public async Task Interaction_requirements_are_met_step_by_step()
    {
        var session = await TestSession.RegisterAsync(_client);
        var viewer = await TestSession.RegisterAsync(_client);

        async Task AssertRequirementsAsync(bool email, bool profile, bool skill)
        {
            var status = await session.GetJsonAsync("/api/v1/me/interaction-requirements");
            Assert.Equal(email, status.GetProperty("emailVerified").GetBoolean());
            Assert.Equal(profile, status.GetProperty("profileCompleted").GetBoolean());
            Assert.Equal(skill, status.GetProperty("hasOfferedSkill").GetBoolean());
            Assert.Equal(email && profile && skill, status.GetProperty("canInteract").GetBoolean());
        }

        await AssertRequirementsAsync(false, false, false);

        // A wanted skill does not count: the user must have something to teach.
        await session.AddSkillAsync("ispanyolca", "Wanted", "Beginner");
        await AssertRequirementsAsync(false, false, false);

        await session.AddSkillAsync("gitar", "Offered", "Advanced");
        await AssertRequirementsAsync(false, false, true);

        await session.SendAsync(
            HttpMethod.Put,
            "/api/v1/me/profile",
            new { displayName = "Ayşe" });
        await AssertRequirementsAsync(false, true, true);

        // Not being able to interact yet does not hide the user's skills from others.
        var skills = await viewer.GetJsonAsync($"/api/v1/users/{session.UserId}/skills?type=Offered");
        Assert.Equal("Gitar", Assert.Single(skills.GetProperty("items").EnumerateArray()).GetProperty("skill").GetProperty("name").GetString());

        await session.VerifyEmailAsync(fixture);
        await AssertRequirementsAsync(true, true, true);
    }

    [Fact]
    public async Task A_proposed_skill_becomes_usable_once_an_admin_approves_it()
    {
        var name = $"Ukulele {Unique()}";
        var user = await TestSession.RegisterAsync(_client);
        var categoryId = await CategoryIdAsync();

        // Proposing needs a verified email.
        await ProblemAssert.IsAsync(
            await user.SendAsync(HttpMethod.Post, "/api/v1/skill-proposals", new { categoryId, name }),
            HttpStatusCode.Forbidden,
            "email_not_verified");
        await user.VerifyEmailAsync(fixture);

        var propose = await user.SendAsync(HttpMethod.Post, "/api/v1/skill-proposals", new { categoryId, name });
        Assert.Equal(HttpStatusCode.Created, propose.StatusCode);
        var proposal = await propose.Content.ReadFromJsonAsync<JsonElement>();
        var skillId = proposal.GetProperty("id").GetGuid();
        Assert.Equal("PendingApproval", proposal.GetProperty("status").GetString());

        // Pending: listed for its author, hidden from search, unusable, and a duplicate is refused.
        var own = await user.GetJsonAsync("/api/v1/me/skill-proposals");
        Assert.Contains(own.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetGuid() == skillId);
        var search = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/skills?q={Uri.EscapeDataString(name)}");
        Assert.Equal(0, search.GetProperty("totalCount").GetInt32());
        await ProblemAssert.IsAsync(
            await user.SendAsync(HttpMethod.Post, "/api/v1/me/skills", new { skillId, type = "Offered", level = "Expert" }),
            HttpStatusCode.Conflict,
            "skill_not_approved");
        await ProblemAssert.IsAsync(
            await user.SendAsync(HttpMethod.Post, "/api/v1/skill-proposals", new { categoryId, name = name.ToUpperInvariant() }),
            HttpStatusCode.Conflict,
            "skill_already_exists");

        // Only an admin can review.
        await ProblemAssert.IsAsync(
            await user.SendAsync(HttpMethod.Post, $"/api/v1/admin/skill-proposals/{skillId}/approve"),
            HttpStatusCode.Forbidden,
            "admin_required");
        await ProblemAssert.IsAsync(
            await user.SendAsync(HttpMethod.Get, "/api/v1/admin/skill-proposals"),
            HttpStatusCode.Forbidden,
            "admin_required");

        var adminAccount = await TestSession.RegisterAsync(_client);
        await fixture.MakeAdminAsync(adminAccount.UserId);
        var admin = await adminAccount.SignInAgainAsync();

        var pending = await admin.GetJsonAsync("/api/v1/admin/skill-proposals?status=PendingApproval&pageSize=50");
        var listed = pending.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == skillId);
        Assert.Equal(user.UserId, listed.GetProperty("proposedByUserId").GetGuid());

        var approve = await admin.SendAsync(HttpMethod.Post, $"/api/v1/admin/skill-proposals/{skillId}/approve");
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        await ProblemAssert.IsAsync(
            await admin.SendAsync(HttpMethod.Post, $"/api/v1/admin/skill-proposals/{skillId}/reject"),
            HttpStatusCode.Conflict,
            "invalid_state");

        search = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/skills?q={Uri.EscapeDataString(name)}");
        Assert.Equal(1, search.GetProperty("totalCount").GetInt32());
        var add = await user.SendAsync(HttpMethod.Post, "/api/v1/me/skills", new { skillId, type = "Offered", level = "Expert" });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
    }

    [Fact]
    public async Task A_rejected_proposal_stays_hidden()
    {
        var name = $"Reddedilecek {Unique()}";
        var user = await TestSession.RegisterAsync(_client);
        await user.VerifyEmailAsync(fixture);
        var propose = await user.SendAsync(HttpMethod.Post, "/api/v1/skill-proposals", new { categoryId = await CategoryIdAsync(), name });
        var skillId = (await propose.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var adminAccount = await TestSession.RegisterAsync(_client);
        await fixture.MakeAdminAsync(adminAccount.UserId);
        var admin = await adminAccount.SignInAgainAsync();

        var reject = await admin.SendAsync(HttpMethod.Post, $"/api/v1/admin/skill-proposals/{skillId}/reject");

        Assert.Equal("Rejected", (await reject.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
        var search = await _client.GetFromJsonAsync<JsonElement>($"/api/v1/skills?q={Uri.EscapeDataString(name)}");
        Assert.Equal(0, search.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Skill_endpoints_require_authentication_where_the_contract_says_so()
    {
        await ProblemAssert.IsAsync(await _client.GetAsync("/api/v1/me/skills"), HttpStatusCode.Unauthorized, "unauthorized");
        await ProblemAssert.IsAsync(await _client.GetAsync("/api/v1/me/skill-proposals"), HttpStatusCode.Unauthorized, "unauthorized");
        await ProblemAssert.IsAsync(await _client.GetAsync("/api/v1/me/interaction-requirements"), HttpStatusCode.Unauthorized, "unauthorized");
        await ProblemAssert.IsAsync(await _client.GetAsync($"/api/v1/users/{Guid.NewGuid()}/skills"), HttpStatusCode.Unauthorized, "unauthorized");
        await ProblemAssert.IsAsync(await _client.GetAsync("/api/v1/admin/skill-proposals"), HttpStatusCode.Unauthorized, "unauthorized");
        await ProblemAssert.IsAsync(
            await _client.PostAsJsonAsync("/api/v1/skill-proposals", new { categoryId = Guid.NewGuid(), name = "Ukulele" }),
            HttpStatusCode.Unauthorized,
            "unauthorized");
    }
}
