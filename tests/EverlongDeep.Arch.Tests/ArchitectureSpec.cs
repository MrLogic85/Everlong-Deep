using System.Reflection;
using System.Xml.Linq;

namespace EverlongDeep.Arch.Tests;

/// <summary>
/// Each rule pins down a boundary declared in docs/ARCHITECTURE.md and CODE_DESIGN_PRINCIPLES.md.
/// </summary>
public sealed class ArchitectureSpec
{
    private static readonly Assembly _simulation = typeof(EverlongDeep.Simulation.AssemblyMarker).Assembly;

    [Fact]
    public void Simulation_ReferencesNoEngine()
    {
        var offenders = _simulation.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? "")
            .Where(name => name.StartsWith("Godot", StringComparison.Ordinal));

        offenders.ShouldBeEmpty("See docs/ARCHITECTURE.md and docs/CODE_DESIGN_PRINCIPLES.md §3.");
    }

    [Fact]
    public void Simulation_DependsOnlyOnTheBcl()
    {
        // The compiler drops a reference nobody uses, so the project file is checked as well as
        // the assembly: a package added before any code uses it still breaks the rule.
        var project = XDocument.Load(Path.Combine(
            RepositoryRoot.Find(), "libs", "EverlongDeep.Simulation", "EverlongDeep.Simulation.csproj"));

        var references = project.Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? element.Name.LocalName);

        references.ShouldBeEmpty("The simulation depends on the .NET BCL only; see docs/ARCHITECTURE.md → \"Dependency rules\".");
    }

    [Fact]
    public void Cli_DependsOnlyOnTheSimulationAndSpectreConsole()
    {
        var project = XDocument.Load(Path.Combine(
            RepositoryRoot.Find(), "tools", "EverlongDeep.Cli", "EverlongDeep.Cli.csproj"));

        var references = project.Descendants()
            .Where(element => element.Name.LocalName is "PackageReference" or "ProjectReference")
            .Select(element => $"{element.Name.LocalName} {element.Attribute("Include")?.Value}");

        references.ShouldBe(
            [
                @"ProjectReference ..\..\libs\EverlongDeep.Simulation\EverlongDeep.Simulation.csproj",
                "PackageReference Spectre.Console",
            ],
            ignoreOrder: true,
            "The CLI depends on the simulation, the .NET BCL and Spectre.Console only; see docs/ARCHITECTURE.md → \"Dependency rules\".");
    }

    [Fact]
    public void ProductionSourceFiles_StayWithinHardLineLimit()
    {
        const int hardLimit = 2000;
        var root = RepositoryRoot.Find();
        string[] skipped = ["bin", "obj", ".godot"];
        var offenders = new List<string>();

        foreach (var dir in new[] { "libs", "tools", Path.Combine("project", "src") }.Select(dir => Path.Combine(root, dir)).Where(Directory.Exists))
        {
            foreach (var path in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, path);
                if (relative.Split(Path.DirectorySeparatorChar).Any(skipped.Contains))
                {
                    continue;
                }

                var lines = File.ReadLines(path).Count();
                if (lines > hardLimit)
                {
                    offenders.Add($"{relative} ({lines} lines)");
                }
            }
        }

        offenders.ShouldBeEmpty(
            $"Production files must stay within {hardLimit} lines; split out functionality. " +
            "See docs/CODE_DESIGN_PRINCIPLES.md §5.");
    }
}
