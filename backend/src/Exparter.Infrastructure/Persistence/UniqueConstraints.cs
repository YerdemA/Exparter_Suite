using Exparter.Application.Common;
using Exparter.Application.Reviews;
using Exparter.Application.Sessions;

namespace Exparter.Infrastructure.Persistence;

internal static class UniqueConstraints
{
    public const string UserEmail = "ix_users_email";
    public const string UserUsername = "ix_users_username";
    public const string SkillSlug = "ix_skills_slug";
    public const string UserSkillPerType = "ix_user_skills_user_id_skill_id_type";

    // The next two are over expressions (the pair of users in either order), which EF
    // cannot describe; the AddMatching migration creates them with SQL.
    public const string PendingSwapRequestPerPair = "ix_swap_requests_pending_pair";
    public const string OpenMatchPerPair = "ix_matches_open_pair";

    public const string ConversationPerPair = "ix_conversations_pair";
    public const string MessagePerClientId = "ix_messages_client_id";

    // Also over the pair in either order; created with SQL by the LimitInPersonMeetingsPerPair migration.
    public const string InPersonMeetingPerPair = "ix_swap_sessions_in_person_pair";

    public const string ReviewPerPair = "ix_reviews_reviewer_id_reviewee_id";

    public static AppException? ToAppException(string? constraintName) => constraintName switch
    {
        UserEmail => AppException.Conflict("email_already_registered", "The email address is already registered."),
        UserUsername => AppException.Conflict("username_already_taken", "The username is already taken."),
        SkillSlug => AppException.Conflict("skill_already_exists", "A skill with this name already exists or is awaiting review."),
        UserSkillPerType => AppException.Conflict("user_skill_already_exists", "The skill is already on the profile with this type."),
        PendingSwapRequestPerPair => AppException.Conflict("swap_request_already_pending", "The two users already have a pending swap request."),
        OpenMatchPerPair => AppException.Conflict("match_already_exists", "The two users already have a match that has not ended."),
        // Both mean the same request arrived twice at once; the client's retry finds what the first one stored.
        ConversationPerPair or MessagePerClientId => AppException.Conflict("concurrent_update", "The resource was changed by another request."),
        InPersonMeetingPerPair => SessionConflicts.InPersonAlreadyReported(),
        ReviewPerPair => ReviewConflicts.AlreadyExists(),
        _ => null,
    };
}
