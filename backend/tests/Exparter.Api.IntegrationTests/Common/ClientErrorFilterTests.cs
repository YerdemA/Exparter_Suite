using Exparter.Api.Common;
using Exparter.Domain.Identity;
using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace Exparter.Api.IntegrationTests.Common;

public sealed class ClientErrorFilterTests
{
    private readonly ClientErrorFilter _filter = new();

    [Fact]
    public void Client_errors_stay_out_of_the_reports()
    {
        Assert.True(_filter.Filter(IdentityErrors.EmailAlreadyVerified()));
        Assert.True(_filter.Filter(new ValidationException("invalid")));
        Assert.True(_filter.Filter(new BadHttpRequestException("unreadable")));
    }

    [Fact]
    public void Real_errors_are_reported()
    {
        Assert.False(_filter.Filter(new InvalidOperationException("broken")));
    }
}
