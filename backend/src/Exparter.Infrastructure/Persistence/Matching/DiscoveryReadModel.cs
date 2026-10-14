using Exparter.Application.Common;
using Exparter.Application.Matching;
using Exparter.Domain.Skills;
using Microsoft.EntityFrameworkCore;

namespace Exparter.Infrastructure.Persistence.Matching;

/// <summary>
/// The one query that joins tables of several modules (see ADR 0011). It only reads.
/// </summary>
internal sealed class DiscoveryReadModel(AppDbContext dbContext) : IDiscoveryReadModel
{
    // Column names follow the snake_case convention of the DbContext.
    private sealed class Row
    {
        public Guid UserId { get; init; }
        public string Username { get; init; } = null!;
        public string? DisplayName { get; init; }
        public string? AvatarKey { get; init; }
        public string? City { get; init; }
        public bool AcceptsOnline { get; init; }
        public bool AcceptsInPerson { get; init; }
        public int Compatibility { get; init; }
        public int ReviewCount { get; init; }
        public double? AverageRating { get; init; }
    }

    public async Task<IReadOnlyList<DiscoveredUser>> FindAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken)
    {
        var viewer = criteria.ViewerId;
        var searching = criteria.Search is not null;
        var pattern = criteria.Search is null ? null : $"%{EscapeLike(criteria.Search)}%";
        var mode = criteria.Mode?.ToString();
        var (from, to) = SearchText.Translation;

        // The "can interact" conditions repeat IInteractionRequirements in SQL, because the
        // list has to be filtered, ranked and paged by the database in one go.
        var rows = await dbContext.Database.SqlQuery<Row>(
            $"""
             SELECT u.id AS user_id, u.username, p.display_name, p.avatar_key, p.city,
                    p.accepts_online, p.accepts_in_person,
                    CASE WHEN fit.teaches_viewer AND fit.learns_from_viewer THEN 2
                         WHEN fit.teaches_viewer THEN 1
                         ELSE 0 END AS compatibility,
                    rating.review_count, rating.average_rating
             FROM identity.users u
             JOIN profiles.profiles p ON p.user_id = u.id
             CROSS JOIN LATERAL (
                 SELECT
                     EXISTS (
                         SELECT 1
                         FROM skills.user_skills theirs
                         JOIN skills.user_skills mine ON mine.skill_id = theirs.skill_id
                         WHERE theirs.user_id = u.id AND theirs.type = 'Offered'
                           AND mine.user_id = {viewer} AND mine.type = 'Wanted'
                           AND theirs.level >= mine.level) AS teaches_viewer,
                     EXISTS (
                         SELECT 1
                         FROM skills.user_skills theirs
                         JOIN skills.user_skills mine ON mine.skill_id = theirs.skill_id
                         WHERE theirs.user_id = u.id AND theirs.type = 'Wanted'
                           AND mine.user_id = {viewer} AND mine.type = 'Offered'
                           AND mine.level >= theirs.level) AS learns_from_viewer,
                     EXISTS (
                         SELECT 1
                         FROM profiles.profiles me
                         WHERE me.user_id = {viewer}
                           AND ((me.accepts_online AND p.accepts_online)
                             OR (me.accepts_in_person AND p.accepts_in_person AND me.city = p.city))) AS shares_preference
             ) fit
             CROSS JOIN LATERAL (
                 SELECT count(*)::int AS review_count, avg(r.rating)::float8 AS average_rating
                 FROM reviews.reviews r
                 WHERE r.reviewee_id = u.id AND r.deleted_at IS NULL
             ) rating
             WHERE u.id <> {viewer}
               AND u.status = 'Active'
               AND NOT EXISTS (
                   SELECT 1 FROM moderation.blocks b
                   WHERE (b.blocker_id = {viewer} AND b.blocked_id = u.id)
                      OR (b.blocker_id = u.id AND b.blocked_id = {viewer}))
               AND ({searching}
                 OR (u.email_verified_at IS NOT NULL
                     AND p.display_name IS NOT NULL
                     AND (p.accepts_online OR p.accepts_in_person)
                     AND EXISTS (SELECT 1 FROM skills.user_skills s WHERE s.user_id = u.id AND s.type = 'Offered')))
               AND ({criteria.SkillId}::uuid IS NULL
                 OR EXISTS (
                     SELECT 1 FROM skills.user_skills s
                     WHERE s.user_id = u.id AND s.type = 'Offered' AND s.skill_id = {criteria.SkillId}::uuid))
               AND ({criteria.CategoryId}::uuid IS NULL
                 OR EXISTS (
                     SELECT 1 FROM skills.user_skills s
                     JOIN skills.skills k ON k.id = s.skill_id
                     WHERE s.user_id = u.id AND s.type = 'Offered' AND k.category_id = {criteria.CategoryId}::uuid))
               AND ({criteria.City}::text IS NULL OR p.city = {criteria.City}::text)
               AND ({mode}::text IS NULL
                 OR ({mode}::text = 'Online' AND p.accepts_online)
                 OR ({mode}::text = 'InPerson' AND p.accepts_in_person))
               AND ({pattern}::text IS NULL
                 OR u.username LIKE {pattern}::text ESCAPE '\'
                 OR lower(translate(p.display_name, {from}, {to})) LIKE {pattern}::text ESCAPE '\')
             ORDER BY compatibility DESC, fit.shares_preference DESC, rating.average_rating DESC NULLS LAST, p.updated_at DESC, u.id
             LIMIT {criteria.Limit} OFFSET {criteria.Offset}
             """).ToListAsync(cancellationToken);

        var userIds = rows.Select(row => row.UserId).ToList();
        var skills = await dbContext.UserSkills.AsNoTracking()
            .Where(userSkill => userIds.Contains(userSkill.UserId))
            .Join(dbContext.Skills.AsNoTracking(), userSkill => userSkill.SkillId, skill => skill.Id, (userSkill, skill) => new
            {
                userSkill.UserId,
                userSkill.Type,
                userSkill.Level,
                userSkill.CreatedAt,
                SkillId = skill.Id,
                skill.Name,
            })
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        IReadOnlyList<DiscoveredSkill> SkillsOf(Guid userId, UserSkillType type) =>
            skills.Where(item => item.UserId == userId && item.Type == type)
                .Select(item => new DiscoveredSkill(item.SkillId, item.Name, item.Level))
                .ToList();

        return rows.Select(row => new DiscoveredUser(
                row.UserId,
                row.Username,
                row.DisplayName,
                row.AvatarKey,
                row.City,
                row.AcceptsOnline,
                row.AcceptsInPerson,
                (Compatibility)row.Compatibility,
                new RatingSummaryDto(row.ReviewCount, row.AverageRating),
                SkillsOf(row.UserId, UserSkillType.Offered),
                SkillsOf(row.UserId, UserSkillType.Wanted)))
            .ToList();
    }

    private static string EscapeLike(string text) =>
        text.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
