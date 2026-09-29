using System.Reflection;

namespace DocumentIntelligence.UnitTests.Architecture;

public sealed class LayerDependencyTests
{
    private const string Prefix = "DocumentIntelligence.";

    [Theory]
    [InlineData("DocumentIntelligence.Domain", new string[0])]
    [InlineData("DocumentIntelligence.Contracts", new string[0])]
    [InlineData("DocumentIntelligence.Application", new[] { "DocumentIntelligence.Domain", "DocumentIntelligence.Contracts" })]
    public void Layer_depends_only_on_allowed_project_assemblies(string assemblyName, string[] allowed)
    {
        var projectReferences = Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith(Prefix, StringComparison.Ordinal));

        projectReferences.ShouldBeSubsetOf(allowed);
    }
}
