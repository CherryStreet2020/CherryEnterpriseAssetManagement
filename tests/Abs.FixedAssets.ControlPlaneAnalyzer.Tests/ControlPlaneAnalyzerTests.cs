// CHERRY025 — Control Plane Analyzer unit tests.
//
// 8 cases enumerated in docs/ADR-025-roslyn-analyzer-design.md.
// Tests run the analyzer directly against a CSharpCompilation
// (no Microsoft.CodeAnalysis.Testing harness — see csproj for why).

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Abs.FixedAssets.ControlPlane.Tests;

public class ControlPlaneAnalyzerTests
{
    // Stub types — match the FULL qualified names the analyzer matches against.
    // The analyzer matches on `current.ToDisplayString()` which returns the
    // namespace-qualified name without the global:: prefix.
    private const string Stubs = @"
namespace Microsoft.AspNetCore.Mvc.RazorPages {
    public class PageModel {}
}
namespace Microsoft.AspNetCore.Mvc {
    public class Controller {}
    public class ControllerBase {}
}
namespace Microsoft.Extensions.Hosting {
    public abstract class BackgroundService {
        protected abstract System.Threading.Tasks.Task ExecuteAsync(System.Threading.CancellationToken stoppingToken);
    }
    public interface IHostedService {
        System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken cancellationToken);
        System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken cancellationToken);
    }
}
namespace Abs.FixedAssets.Data {
    public class AppDbContext {}
}
";

    [Fact]
    public async Task Case1_PageModel_InjectsAppDbContext_Fires()
    {
        var source = Stubs + @"
namespace Test {
    public class WidgetModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel {
        private readonly Abs.FixedAssets.Data.AppDbContext _db;
        public WidgetModel(Abs.FixedAssets.Data.AppDbContext db) { _db = db; }
    }
}";
        var diagnostics = await RunAnalyzerAsync(source);
        var cherry = diagnostics.Where(d => d.Id == "CHERRY025").ToList();
        Assert.Single(cherry);
        Assert.Contains("PageModel", cherry[0].GetMessage());
        Assert.Contains("WidgetModel", cherry[0].GetMessage());
    }

    [Fact]
    public async Task Case2_Controller_InjectsAppDbContext_Fires()
    {
        var source = Stubs + @"
namespace Test {
    public class WidgetController : Microsoft.AspNetCore.Mvc.ControllerBase {
        private readonly Abs.FixedAssets.Data.AppDbContext _db;
        public WidgetController(Abs.FixedAssets.Data.AppDbContext db) { _db = db; }
    }
}";
        var diagnostics = await RunAnalyzerAsync(source);
        var cherry = diagnostics.Where(d => d.Id == "CHERRY025").ToList();
        Assert.Single(cherry);
        Assert.Contains("Controller", cherry[0].GetMessage());
        Assert.Contains("WidgetController", cherry[0].GetMessage());
    }

    [Fact]
    public async Task Case3_BackgroundService_InjectsAppDbContext_Fires()
    {
        var source = Stubs + @"
namespace Test {
    public class WidgetWorker : Microsoft.Extensions.Hosting.BackgroundService {
        private readonly Abs.FixedAssets.Data.AppDbContext _db;
        public WidgetWorker(Abs.FixedAssets.Data.AppDbContext db) { _db = db; }
        protected override System.Threading.Tasks.Task ExecuteAsync(System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.CompletedTask;
    }
}";
        var diagnostics = await RunAnalyzerAsync(source);
        var cherry = diagnostics.Where(d => d.Id == "CHERRY025").ToList();
        Assert.Single(cherry);
        Assert.Contains("BackgroundService", cherry[0].GetMessage());
        Assert.Contains("WidgetWorker", cherry[0].GetMessage());
    }

    [Fact]
    public async Task Case4_PageModel_OnlyService_NoDiagnostic()
    {
        var source = Stubs + @"
namespace Test {
    public interface IWidgetService {}
    public class WidgetModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel {
        private readonly IWidgetService _svc;
        public WidgetModel(IWidgetService svc) { _svc = svc; }
    }
}";
        var diagnostics = await RunAnalyzerAsync(source);
        Assert.Empty(diagnostics.Where(d => d.Id == "CHERRY025"));
    }

    [Fact]
    public async Task Case5_PageModel_WithExemptAttribute_NoDiagnostic()
    {
        var source = Stubs + @"
namespace Abs.FixedAssets.ControlPlane {
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class ControlPlaneExemptAttribute : System.Attribute {
        public string Reason { get; }
        public ControlPlaneExemptAttribute(string reason) { Reason = reason; }
    }
}
namespace Test {
    [Abs.FixedAssets.ControlPlane.ControlPlaneExempt(""lookup admin — no business logic"")]
    public class WidgetModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel {
        private readonly Abs.FixedAssets.Data.AppDbContext _db;
        public WidgetModel(Abs.FixedAssets.Data.AppDbContext db) { _db = db; }
    }
}";
        var diagnostics = await RunAnalyzerAsync(source);
        Assert.Empty(diagnostics.Where(d => d.Id == "CHERRY025"));
    }

    [Fact]
    public async Task Case6_PageModel_WithPragmaComment_NoDiagnostic()
    {
        var source = "// PRAGMA: control-plane-exempt — legacy admin lookup\n" + Stubs + @"
namespace Test {
    public class WidgetModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel {
        private readonly Abs.FixedAssets.Data.AppDbContext _db;
        public WidgetModel(Abs.FixedAssets.Data.AppDbContext db) { _db = db; }
    }
}";
        var diagnostics = await RunAnalyzerAsync(source);
        Assert.Empty(diagnostics.Where(d => d.Id == "CHERRY025"));
    }

    [Fact]
    public async Task Case7_PlainService_InjectsAppDbContext_NoDiagnostic()
    {
        // Plain services (not PageModel/Controller/Endpoint/BackgroundService) are
        // explicitly OK per ADR-025 — services ARE the control plane.
        var source = Stubs + @"
namespace Test {
    public class WidgetService {
        private readonly Abs.FixedAssets.Data.AppDbContext _db;
        public WidgetService(Abs.FixedAssets.Data.AppDbContext db) { _db = db; }
    }
}";
        var diagnostics = await RunAnalyzerAsync(source);
        Assert.Empty(diagnostics.Where(d => d.Id == "CHERRY025"));
    }

    [Fact]
    public async Task Case8_AllowlistEntry_NoDiagnostic()
    {
        var source = Stubs + @"
namespace Test {
    public class WidgetModel : Microsoft.AspNetCore.Mvc.RazorPages.PageModel {
        private readonly Abs.FixedAssets.Data.AppDbContext _db;
        public WidgetModel(Abs.FixedAssets.Data.AppDbContext db) { _db = db; }
    }
}";
        // Filepath of the syntax tree matches the allowlist entry exactly.
        var allowlistContent = "# header\nPages/Widgets/Edit.cshtml.cs\n";
        var diagnostics = await RunAnalyzerAsync(
            source,
            sourceFilePath: "Pages/Widgets/Edit.cshtml.cs",
            allowlistContent: allowlistContent);
        Assert.Empty(diagnostics.Where(d => d.Id == "CHERRY025"));
    }

    // === Test infrastructure ===

    /// <summary>
    /// Runs ControlPlaneAnalyzer against an in-memory CSharpCompilation
    /// constructed from a single source string. Returns the analyzer
    /// diagnostics (not compiler diagnostics).
    /// </summary>
    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(
        string source,
        string sourceFilePath = "Test.cs",
        string? allowlistContent = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, path: sourceFilePath);

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Collections.Generic.IEnumerable<>).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var additionalFiles = ImmutableArray<AdditionalText>.Empty;
        if (allowlistContent is not null)
        {
            additionalFiles = ImmutableArray.Create<AdditionalText>(
                new InMemoryAdditionalText("Analyzers/ControlPlaneAllowlist.txt", allowlistContent));
        }

        var analyzerOptions = new AnalyzerOptions(additionalFiles);
        var compilationWithAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new ControlPlaneAnalyzer()),
            analyzerOptions);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;
        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content, Encoding.UTF8);
        }
        public override string Path { get; }
        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}
