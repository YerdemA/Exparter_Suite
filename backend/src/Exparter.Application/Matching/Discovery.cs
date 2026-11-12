using System.Text;
using System.Text.RegularExpressions;
using Exparter.Application.Common;
using Exparter.Domain.Skills;
using FluentValidation;
using Mediator;

namespace Exparter.Application.Matching;

/// <summary>Declared in ascending order of how well two users fit; ranking relies on it.</summary>
public enum Compatibility
{
    /// <summary>Neither user offers what the other wants.</summary>
    None,

    /// <summary>The listed user offers something the viewer wants.</summary>
    OneWay,

    /// <summary>Each offers something the other wants.</summary>
    Mutual,
}

/// <param name="Search">Already folded with <see cref="SearchText.Fold"/>; null to list suggestions.</param>
public sealed record DiscoveryCriteria(
    Guid ViewerId,
    Guid? SkillId,
    Guid? CategoryId,
    string? City,
    string? Search,
    int Offset,
    int Limit);

public sealed record DiscoveredSkill(Guid SkillId, string Name, SkillLevel Level);

public sealed record DiscoveredUser(
    Guid UserId,
    string Username,
    string? DisplayName,
    string? AvatarKey,
    string? City,
    Compatibility Compatibility,
    RatingSummaryDto Rating,
    IReadOnlyList<DiscoveredSkill> OfferedSkills,
    IReadOnlyList<DiscoveredSkill> WantedSkills);

/// <summary>
/// Finds and ranks other users for a viewer. Unlike everything else in the application
/// layer this reads across modules at once (accounts, profiles, skills), because ranking
/// and paging have to happen in a single query; see ADR 0011.
/// </summary>
public interface IDiscoveryReadModel
{
    /// <summary>Users in ranked order, starting at <see cref="DiscoveryCriteria.Offset"/>.</summary>
    Task<IReadOnlyList<DiscoveredUser>> FindAsync(DiscoveryCriteria criteria, CancellationToken cancellationToken);
}

public static partial class SearchText
{
    private const string From = "İIıŞşĞğÜüÖöÇçÂâÎîÛû";
    private const string To = "iiissgguuooccaaiiuu";

    /// <summary>
    /// The pair of strings a database can pass to <c>translate()</c> to fold a column the
    /// same way <see cref="Fold"/> folds the search text.
    /// </summary>
    public static (string From, string To) Translation => (From, To);

    /// <summary>
    /// Reduces text to lowercase without Turkish accents, so that "ışık", "IŞIK" and "isik"
    /// all match each other. Letter case alone is not enough: "I" and "ı" are different
    /// letters to a database that does not use Turkish rules.
    /// </summary>
    public static string Fold(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text.Trim())
        {
            var index = From.IndexOf(character);
            builder.Append(index >= 0 ? To[index] : char.ToLowerInvariant(character));
        }

        return builder.ToString();
    }
}

public sealed record DiscoveryUserSkillDto(SkillRefDto Skill, SkillLevel Level);

public sealed record DiscoveryUserDto(
    UserSummaryDto User,
    string? City,
    Compatibility Compatibility,
    RatingSummaryDto Rating,
    IReadOnlyList<DiscoveryUserSkillDto> OfferedSkills,
    IReadOnlyList<DiscoveryUserSkillDto> WantedSkills)
{
    // App versions up to 1.0.0+8 cannot read this without the two swap preferences they
    // used to show. Remove once those versions are no longer in use.
    public bool AcceptsOnline => true;

    public bool AcceptsInPerson => false;
}

public sealed record DiscoverUsersQuery(
    Guid ViewerId,
    Guid? SkillId,
    Guid? CategoryId,
    string? City,
    string? Search,
    string? Cursor,
    int? Limit) : IQuery<CursorPage<DiscoveryUserDto>>;

public sealed partial class DiscoverUsersQueryValidator : AbstractValidator<DiscoverUsersQuery>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;
    public const int SearchMaxLength = 60;

    public DiscoverUsersQueryValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, MaxLimit);
        RuleFor(x => x.Search).MaximumLength(SearchMaxLength);
        RuleFor(x => x.City)
            .Must(city => city is null || CityCode().IsMatch(city))
            .WithMessage("The city must be a plate code from 01 to 81.");
        RuleFor(x => x.Cursor)
            .Must(cursor => cursor is null || DiscoveryCursor.TryDecode(cursor, out _))
            .WithMessage("The cursor is not one returned by this endpoint.");
    }

    [GeneratedRegex("^(0[1-9]|[1-7][0-9]|8[01])$")]
    private static partial Regex CityCode();
}

/// <summary>
/// The cursor is the position in the ranked list. It is opaque to clients, so it can
/// later become a keyset without changing the contract.
/// </summary>
internal static class DiscoveryCursor
{
    private const string Prefix = "o:";

    public static string Encode(int offset) =>
        Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Prefix}{offset}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static bool TryDecode(string cursor, out int offset)
    {
        offset = 0;
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
            var text = Encoding.ASCII.GetString(Convert.FromBase64String(padded));
            return text.StartsWith(Prefix, StringComparison.Ordinal)
                && int.TryParse(text.AsSpan(Prefix.Length), out offset)
                && offset >= 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed class DiscoverUsersQueryHandler(IDiscoveryReadModel readModel, IObjectStorage storage)
    : IQueryHandler<DiscoverUsersQuery, CursorPage<DiscoveryUserDto>>
{
    private static readonly TimeSpan AvatarUrlLifetime = TimeSpan.FromHours(1);

    public async ValueTask<CursorPage<DiscoveryUserDto>> Handle(DiscoverUsersQuery query, CancellationToken cancellationToken)
    {
        var limit = query.Limit ?? DiscoverUsersQueryValidator.DefaultLimit;
        var offset = 0;
        if (query.Cursor is not null)
        {
            DiscoveryCursor.TryDecode(query.Cursor, out offset);
        }

        var search = string.IsNullOrWhiteSpace(query.Search) ? null : SearchText.Fold(query.Search);

        // One more than asked for: its presence says whether there is a next page.
        var users = await readModel.FindAsync(
            new DiscoveryCriteria(query.ViewerId, query.SkillId, query.CategoryId, query.City, search, offset, limit + 1),
            cancellationToken);

        var items = users.Take(limit).Select(ToDto).ToList();
        return new CursorPage<DiscoveryUserDto>(items, users.Count > limit ? DiscoveryCursor.Encode(offset + limit) : null);
    }

    private DiscoveryUserDto ToDto(DiscoveredUser user) =>
        new(
            new UserSummaryDto(
                user.UserId,
                user.Username,
                user.DisplayName,
                user.AvatarKey is null ? null : storage.CreateDownloadUrl(user.AvatarKey, AvatarUrlLifetime),
                IsDeleted: false),
            user.City,
            user.Compatibility,
            user.Rating,
            user.OfferedSkills.Select(ToDto).ToList(),
            user.WantedSkills.Select(ToDto).ToList());

    private static DiscoveryUserSkillDto ToDto(DiscoveredSkill skill) =>
        new(new SkillRefDto(skill.SkillId, skill.Name), skill.Level);
}
