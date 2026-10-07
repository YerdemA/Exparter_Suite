using Exparter.Application.Common;

namespace Exparter.Application.Tests;

public class ArchitectureTests
{
    [Theory]
    [InlineData("Exparter.Infrastructure")]
    [InlineData("Exparter.Api")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.AspNetCore")]
    public void Application_does_not_depend_on_outer_layers_or_their_frameworks(string forbiddenPrefix)
    {
        var references = typeof(AppException).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name!);

        Assert.DoesNotContain(references, name => name.StartsWith(forbiddenPrefix, StringComparison.Ordinal));
    }
}
