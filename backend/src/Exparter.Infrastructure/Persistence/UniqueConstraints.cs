using Exparter.Application.Common;

namespace Exparter.Infrastructure.Persistence;

internal static class UniqueConstraints
{
    public const string UserEmail = "ix_users_email";
    public const string UserUsername = "ix_users_username";
    public const string SkillSlug = "ix_skills_slug";
    public const string UserSkillPerType = "ix_user_skills_user_id_skill_id_type";

    public static AppException? ToAppException(string? constraintName) => constraintName switch
    {
        UserEmail => AppException.Conflict("email_already_registered", "The email address is already registered."),
        UserUsername => AppException.Conflict("username_already_taken", "The username is already taken."),
        SkillSlug => AppException.Conflict("skill_already_exists", "A skill with this name already exists or is awaiting review."),
        UserSkillPerType => AppException.Conflict("user_skill_already_exists", "The skill is already on the profile with this type."),
        _ => null,
    };
}
