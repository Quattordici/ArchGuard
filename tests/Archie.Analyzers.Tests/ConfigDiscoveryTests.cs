using System.Reflection;
using Archie.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Archie.Analyzers.Tests;

public class ConfigDiscoveryTests
{
    private const string Source = "namespace App.Domain { public class Order { } }";

    [Test]
    public async Task Reports_ARCH909_when_there_is_no_archie_json()
    {
        var test = NewTest();
        test.ExpectedDiagnostics.Add(new DiagnosticResult(Diagnostics.NoConfig));

        await test.RunAsync();
    }

    [Test]
    public async Task Is_silent_when_archie_json_is_an_additional_file()
    {
        var test = NewTest();
        test.TestState.AdditionalFiles.Add(("/solution/archie.json", """{ "rootNamespace": "App", "layers": ["Domain"] }"""));

        await test.RunAsync();
    }

    [Test]
    [Arguments("IsTestProject", "true")]
    [Arguments("ArchieEnabled", "false")]
    public async Task Is_silent_for_excluded_projects(string property, string value)
    {
        var test = NewTest();
        test.TestState.AnalyzerConfigFiles.Add(("/.globalconfig", $"is_global = true\nbuild_property.{property} = {value}\n"));

        await test.RunAsync();
    }

    [Test]
    public void Runs_on_the_requested_Roslyn_version()
    {
        // Guards the roslyn-floor CI job: if -p:TestAgainstFloors stopped working, the tests would silently run on the latest Roslyn.
        var requested = typeof(ConfigDiscoveryTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "RoslynTestVersion").Value!;

        typeof(Compilation).Assembly.GetName().Version!.ToString(2).ShouldBe(new Version(requested).ToString(2));
    }

    private static CSharpAnalyzerTest<ArchitectureAnalyzer, DefaultVerifier> NewTest() => new()
    {
        TestCode = Source,
        ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
    };
}
