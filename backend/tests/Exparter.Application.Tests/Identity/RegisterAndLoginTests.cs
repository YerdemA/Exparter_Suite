using Exparter.Application.Common;
using Exparter.Application.Identity;
using Exparter.Domain.Common;
using Exparter.Domain.Identity;

namespace Exparter.Application.Tests.Identity;

public class RegisterCommandHandlerTests
{
    private readonly IdentityTestContext _context = new();

    private RegisterCommandHandler Handler => new(
        _context.Users,
        _context.OneTimeTokens,
        _context.BlockedEmails,
        _context.Hasher,
        _context.Tokens,
        _context.TokenIssuer,
        _context.Emails,
        _context.Publisher,
        _context.UnitOfWork,
        _context.Clock);

    private static RegisterCommand Command(string email = "Ayse@Example.com", string username = "ayse") =>
        new(email, username, IdentityTestContext.Password, AgeConfirmed: true);

    [Fact]
    public async Task Creates_the_account_signs_in_and_sends_a_verification_email()
    {
        var result = await Handler.Handle(Command(), CancellationToken.None);

        var user = Assert.Single(_context.Users.Items);
        Assert.Equal("ayse@example.com", user.Email);
        Assert.Equal("ayse", user.Username);
        Assert.NotEqual(IdentityTestContext.Password, user.PasswordHash);
        Assert.Equal(user.Id, result.Account.Id);
        Assert.False(result.Account.EmailVerified);
        Assert.False(result.AccountRestored);
        Assert.Single(_context.RefreshTokens.Items);
        Assert.Equal(1, _context.UnitOfWork.SaveCount);
        Assert.Equal(new UserRegistered(user.Id), Assert.Single(_context.Publisher.Published));

        var email = Assert.Single(_context.Emails.Verifications);
        Assert.Equal("ayse@example.com", email.Email);
        var storedToken = Assert.Single(_context.OneTimeTokens.Items);
        Assert.Equal(_context.Tokens.HashOpaqueToken(email.Token), storedToken.TokenHash);
    }

    [Fact]
    public async Task Rejects_an_email_that_is_already_registered_regardless_of_case()
    {
        _context.AddUser(email: "ayse@example.com", username: "someone");

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await Handler.Handle(Command(email: "AYSE@example.com"), CancellationToken.None));

        Assert.Equal(AppErrorKind.Conflict, exception.Kind);
        Assert.Equal("email_already_registered", exception.Code);
    }

    [Fact]
    public async Task Rejects_a_username_that_is_already_taken()
    {
        _context.AddUser(email: "other@example.com", username: "ayse");

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await Handler.Handle(Command(username: "ayse"), CancellationToken.None));

        Assert.Equal("username_already_taken", exception.Code);
    }

    [Fact]
    public async Task Rejects_a_reserved_username()
    {
        var exception = await Assert.ThrowsAsync<DomainException>(
            async () => await Handler.Handle(Command(username: "admin"), CancellationToken.None));

        Assert.Equal(IdentityErrors.UsernameReservedCode, exception.Code);
        Assert.Empty(_context.Users.Items);
    }
}

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void Accepts_a_valid_command() =>
        Assert.True(_validator.Validate(new RegisterCommand("ayse@example.com", "ayse.yilmaz", "Passw0rd!x", true)).IsValid);

    [Theory]
    [InlineData("not-an-email", "ayse", "Passw0rd!x", true, nameof(RegisterCommand.Email))]
    [InlineData("ayse@example.com", "a", "Passw0rd!x", true, nameof(RegisterCommand.Username))]
    [InlineData("ayse@example.com", "ay..se", "Passw0rd!x", true, nameof(RegisterCommand.Username))]
    [InlineData("ayse@example.com", "Ayse", "Passw0rd!x", true, nameof(RegisterCommand.Username))]
    [InlineData("ayse@example.com", null, "Passw0rd!x", true, nameof(RegisterCommand.Username))]
    [InlineData("ayse@example.com", "ayse", "short", true, nameof(RegisterCommand.Password))]
    [InlineData("ayse@example.com", "ayse", "Passw0rd!x", false, nameof(RegisterCommand.AgeConfirmed))]
    public void Rejects_invalid_fields(string email, string? username, string password, bool ageConfirmed, string field)
    {
        var result = _validator.Validate(new RegisterCommand(email, username!, password, ageConfirmed));

        Assert.Contains(result.Errors, error => error.PropertyName == field);
    }
}

public class LoginCommandHandlerTests
{
    private readonly IdentityTestContext _context = new();

    private LoginCommandHandler Handler =>
        new(_context.Users, _context.Hasher, _context.TokenIssuer, _context.UnitOfWork);

    [Theory]
    [InlineData("ayse@example.com")]
    [InlineData("AYSE@Example.com")]
    [InlineData("ayse")]
    [InlineData("Ayse")]
    public async Task Signs_in_with_email_or_username_ignoring_case(string identifier)
    {
        var user = _context.AddUser();

        var result = await Handler.Handle(new LoginCommand(identifier, IdentityTestContext.Password), CancellationToken.None);

        Assert.Equal(user.Id, result.Account.Id);
        Assert.False(result.AccountRestored);
        Assert.Single(_context.RefreshTokens.Items);
    }

    [Theory]
    [InlineData("ayse", "wrong-password")]
    [InlineData("nobody", IdentityTestContext.Password)]
    [InlineData("nobody@example.com", IdentityTestContext.Password)]
    public async Task Gives_the_same_error_for_unknown_accounts_and_wrong_passwords(string identifier, string password)
    {
        _context.AddUser();

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await Handler.Handle(new LoginCommand(identifier, password), CancellationToken.None));

        Assert.Equal(AppErrorKind.Unauthorized, exception.Kind);
        Assert.Equal("invalid_credentials", exception.Code);
        Assert.Empty(_context.RefreshTokens.Items);
    }

    [Fact]
    public async Task Rejects_a_suspended_account()
    {
        IdentityTestContext.Suspend(_context.AddUser());

        var exception = await Assert.ThrowsAsync<AppException>(
            async () => await Handler.Handle(new LoginCommand("ayse", IdentityTestContext.Password), CancellationToken.None));

        Assert.Equal(AppErrorKind.Forbidden, exception.Kind);
        Assert.Equal("account_suspended", exception.Code);
    }

    [Fact]
    public async Task Restores_an_account_that_is_pending_deletion()
    {
        var user = _context.AddUser();
        user.RequestDeletion(_context.Clock.GetUtcNow());

        var result = await Handler.Handle(new LoginCommand("ayse", IdentityTestContext.Password), CancellationToken.None);

        Assert.True(result.AccountRestored);
        Assert.Equal(UserStatus.Active, user.Status);
    }
}
