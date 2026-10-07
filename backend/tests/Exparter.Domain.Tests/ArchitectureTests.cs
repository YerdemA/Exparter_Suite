using Exparter.Domain.Common;

namespace Exparter.Domain.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_depends_only_on_the_base_class_library()
    {
        var references = typeof(DomainException).Assembly.GetReferencedAssemblies()
            .Select(assembly => assembly.Name!)
            .Where(name => !name.StartsWith("System.", StringComparison.Ordinal) && name != "netstandard")
            .ToList();

        Assert.Empty(references);
    }
}
