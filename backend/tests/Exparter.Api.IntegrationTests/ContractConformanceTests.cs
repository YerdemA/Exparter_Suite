using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.RepresentationModel;

namespace Exparter.Api.IntegrationTests;

/// <summary>
/// Keeps the implementation and <c>contracts/openapi.yaml</c> from drifting apart at the
/// level of routes. It does not compare request or response schemas.
/// </summary>
[Collection(ApiCollection.Name)]
public class ContractConformanceTests(ApiFixture fixture)
{
    // Contract tags whose operations are implemented. Add a tag when its module is built;
    // from then on a contract operation without a route fails the build.
    private static readonly string[] ImplementedTags = ["Auth", "Account", "Profiles", "Skills", "Discovery", "SwapRequests", "Matches", "Conversations", "Sessions", "Reviews", "Notifications", "Moderation", "Admin"];

    private static readonly string[] HttpMethods = ["get", "put", "post", "delete", "patch"];

    [Fact]
    public void Every_api_route_is_defined_in_the_contract()
    {
        var contract = ReadContract().Select(operation => operation.Key).ToHashSet();

        var undocumented = ImplementedRoutes().Where(route => !contract.Contains(route)).ToList();

        Assert.True(undocumented.Count == 0, $"Routes missing from the contract: {string.Join(", ", undocumented)}");
    }

    [Fact]
    public void Every_contract_operation_of_an_implemented_module_has_a_route()
    {
        var routes = ImplementedRoutes().ToHashSet();

        var missing = ReadContract()
            .Where(operation => operation.Tags.Intersect(ImplementedTags).Any() && !routes.Contains(operation.Key))
            .Select(operation => operation.Key)
            .ToList();

        Assert.True(missing.Count == 0, $"Contract operations without a route: {string.Join(", ", missing)}");
    }

    private IEnumerable<string> ImplementedRoutes() =>
        fixture.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/api/", StringComparison.Ordinal))
            .SelectMany(endpoint => endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods
                .Select(method => Key(method, endpoint.RoutePattern.RawText!)));

    private static string Key(string method, string path) => $"{method.ToUpperInvariant()} {path.TrimEnd('/')}";

    private static List<(string Key, string[] Tags)> ReadContract()
    {
        var yaml = new YamlStream();
        using (var reader = new StreamReader(FindContract()))
        {
            yaml.Load(reader);
        }

        var paths = (YamlMappingNode)((YamlMappingNode)yaml.Documents[0].RootNode)["paths"];
        var operations = new List<(string, string[])>();
        foreach (var (pathNode, pathItem) in paths)
        {
            foreach (var (methodNode, operationNode) in (YamlMappingNode)pathItem)
            {
                var method = ((YamlScalarNode)methodNode).Value!;
                if (!HttpMethods.Contains(method))
                {
                    continue;
                }

                var tags = ((YamlSequenceNode)((YamlMappingNode)operationNode)["tags"])
                    .Select(tag => ((YamlScalarNode)tag).Value!)
                    .ToArray();
                operations.Add((Key(method, ((YamlScalarNode)pathNode).Value!), tags));
            }
        }

        return operations;
    }

    private static string FindContract()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "contracts", "openapi.yaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("contracts/openapi.yaml was not found above the test directory.");
    }
}
