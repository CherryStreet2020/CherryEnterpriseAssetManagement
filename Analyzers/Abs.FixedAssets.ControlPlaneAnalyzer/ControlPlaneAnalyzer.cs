// ADR-025 D4 upgrade — Roslyn DiagnosticAnalyzer emitting CHERRY025.
//
// Detects classes that inject Abs.FixedAssets.Data.AppDbContext directly
// when they are in one of four "write-path-relevant" categories:
//
//   1. PageModel (Razor Pages)
//   2. Controller / ControllerBase (MVC + Web API)
//   3. BackgroundService (or any class implementing IHostedService)
//   4. Endpoint class under Abs.FixedAssets.Endpoints namespace (Minimal API)
//
// Exemption mechanisms (any one suppresses the diagnostic):
//
//   - [ControlPlaneExempt("reason")] attribute on the class
//   - `// PRAGMA: control-plane-exempt` comment anywhere in the file
//     (backward-compat with the bash gate)
//   - File path listed in Analyzers/ControlPlaneAllowlist.txt
//     (grandfathered legacy files)
//
// See:
//   - docs/ADR-025-service-layer-standard.md
//   - docs/ADR-025-roslyn-analyzer-design.md

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Abs.FixedAssets.ControlPlane;

/// <summary>
/// CHERRY025 — Service Layer Standard CI gate (ADR-025 D4).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ControlPlaneAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "CHERRY025";

    private const string Title = "Class injects AppDbContext directly";

    private const string MessageFormat =
        "{0} '{1}' injects AppDbContext directly. Per ADR-025 (Service Layer Standard), " +
        "mutations must flow through a domain service (IFooService). Either refactor to " +
        "inject the relevant service, or mark the class with [ControlPlaneExempt(\"reason\")] " +
        "if this is a read-only admin/lookup surface. See docs/ADR-025-service-layer-standard.md.";

    private const string Category = "ControlPlane";

    private const string Description =
        "Operational mutations must flow through a domain service. See ADR-025.";

    private const string HelpLink =
        "https://github.com/CherryStreet2020/CherryEnterpriseAssetManagement/blob/main/docs/ADR-025-service-layer-standard.md";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: Title,
        messageFormat: MessageFormat,
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description,
        helpLinkUri: HelpLink);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    // Fully-qualified type names that gate a class as "write-path-relevant" via base type.
    private const string PageModelType = "Microsoft.AspNetCore.Mvc.RazorPages.PageModel";
    private const string ControllerType = "Microsoft.AspNetCore.Mvc.Controller";
    private const string ControllerBaseType = "Microsoft.AspNetCore.Mvc.ControllerBase";
    private const string BackgroundServiceType = "Microsoft.Extensions.Hosting.BackgroundService";
    private const string HostedServiceInterfaceType = "Microsoft.Extensions.Hosting.IHostedService";

    // Class kinds (the {0} arg in the diagnostic message).
    private const string KindPage = "PageModel";
    private const string KindController = "Controller";
    private const string KindBackgroundService = "BackgroundService";
    private const string KindHostedService = "HostedService";
    private const string KindEndpoint = "Endpoint";

    // Fully-qualified AppDbContext type name.
    private const string AppDbContextType = "Abs.FixedAssets.Data.AppDbContext";

    // The exemption attribute (matched by simple name OR fully-qualified name).
    private const string ExemptAttributeFullName =
        "Abs.FixedAssets.ControlPlane.ControlPlaneExemptAttribute";
    private const string ExemptAttributeSimpleName = "ControlPlaneExemptAttribute";

    // Bash-era pragma comment. Backward-compat with scripts/check-control-plane.sh.
    private const string PragmaText = "PRAGMA: control-plane-exempt";

    // Namespace prefix for Minimal-API endpoint classes (no marker base type exists).
    private const string EndpointNamespacePrefix = "Abs.FixedAssets.Endpoints";

    // Allowlist file we look for in AdditionalFiles.
    private const string AllowlistFileName = "ControlPlaneAllowlist.txt";

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext startContext)
    {
        var allowlist = LoadAllowlist(startContext.Options.AdditionalFiles, startContext.CancellationToken);

        startContext.RegisterSymbolAction(
            symbolContext => AnalyzeNamedType(symbolContext, allowlist),
            SymbolKind.NamedType);
    }

    private static ImmutableHashSet<string> LoadAllowlist(
        ImmutableArray<AdditionalText> additionalFiles,
        CancellationToken ct)
    {
        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in additionalFiles)
        {
            if (!IsAllowlistFile(file.Path)) continue;

            var text = file.GetText(ct);
            if (text is null) continue;

            foreach (var line in text.Lines)
            {
                var raw = line.ToString().Trim();
                if (raw.Length == 0 || raw.StartsWith("#", StringComparison.Ordinal)) continue;
                builder.Add(NormalizePath(raw));
            }
        }

        return builder.ToImmutable();
    }

    private static bool IsAllowlistFile(string path)
    {
        var name = Path.GetFileName(path);
        return string.Equals(name, AllowlistFileName, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private static void AnalyzeNamedType(
        SymbolAnalysisContext context,
        ImmutableHashSet<string> allowlist)
    {
        if (context.Symbol is not INamedTypeSymbol type) return;
        if (type.TypeKind != TypeKind.Class) return;
        if (type.IsAbstract && !ImplementsHostedServiceDirectly(type)) return;

        // Gate 1: Is this a write-path-relevant class kind?
        var classKind = ClassifyGate(type);
        if (classKind is null) return;

        // Gate 2: Exemption attribute?
        if (HasExemptAttribute(type)) return;

        // Gate 3: Does any constructor inject AppDbContext?
        var ctorInjectionLocation = FindAppDbContextInjection(type);
        if (ctorInjectionLocation is null) return;

        // Gate 4: PRAGMA comment in the file?
        if (HasPragmaExempt(type, context.CancellationToken)) return;

        // Gate 5: Allowlist entry for this file?
        if (IsAllowlisted(type, allowlist)) return;

        var diagnostic = Diagnostic.Create(
            descriptor: Rule,
            location: ctorInjectionLocation,
            messageArgs: new object[] { classKind, type.Name });

        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>
    /// Returns the display kind (PageModel / Controller / BackgroundService /
    /// HostedService / Endpoint) if the type is write-path-relevant, or null
    /// if it should not be gated.
    /// </summary>
    private static string? ClassifyGate(INamedTypeSymbol type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            var fullName = current.ToDisplayString();
            if (fullName == PageModelType) return KindPage;
            if (fullName == ControllerType || fullName == ControllerBaseType) return KindController;
            if (fullName == BackgroundServiceType) return KindBackgroundService;
        }

        if (ImplementsHostedServiceDirectly(type))
        {
            return KindHostedService;
        }

        var ns = type.ContainingNamespace?.ToDisplayString();
        if (ns is not null &&
            (ns == EndpointNamespacePrefix ||
             ns.StartsWith(EndpointNamespacePrefix + ".", StringComparison.Ordinal)))
        {
            return KindEndpoint;
        }

        return null;
    }

    private static bool ImplementsHostedServiceDirectly(INamedTypeSymbol type)
    {
        // Skip BackgroundService — that's handled by the base-type check.
        foreach (var iface in type.Interfaces)
        {
            if (iface.ToDisplayString() == HostedServiceInterfaceType)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasExemptAttribute(INamedTypeSymbol type)
    {
        foreach (var attr in type.GetAttributes())
        {
            var cls = attr.AttributeClass;
            if (cls is null) continue;
            if (cls.Name == ExemptAttributeSimpleName) return true;
            if (cls.ToDisplayString() == ExemptAttributeFullName) return true;
        }
        return false;
    }

    private static Location? FindAppDbContextInjection(INamedTypeSymbol type)
    {
        foreach (var ctor in type.InstanceConstructors)
        {
            if (ctor.IsImplicitlyDeclared) continue;
            foreach (var param in ctor.Parameters)
            {
                var paramTypeName = param.Type.ToDisplayString();
                if (paramTypeName == AppDbContextType)
                {
                    return param.Locations.FirstOrDefault() ?? ctor.Locations.FirstOrDefault();
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Checks whether any source file declaring this type contains the
    /// `// PRAGMA: control-plane-exempt` opt-out marker anywhere in its text.
    /// </summary>
    private static bool HasPragmaExempt(INamedTypeSymbol type, CancellationToken ct)
    {
        foreach (var syntaxRef in type.DeclaringSyntaxReferences)
        {
            var sourceText = syntaxRef.SyntaxTree.GetText(ct);
            // Quick substring scan — pragma is rare so this is cheap on miss.
            if (sourceText.ToString().IndexOf(PragmaText, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks whether any source file declaring this type matches an entry
    /// in Analyzers/ControlPlaneAllowlist.txt. Matches are case-insensitive
    /// and handle both direct equality and repo-relative suffix matching
    /// (covers CI absolute paths vs. local relative paths).
    /// </summary>
    private static bool IsAllowlisted(
        INamedTypeSymbol type,
        ImmutableHashSet<string> allowlist)
    {
        if (allowlist.Count == 0) return false;

        foreach (var syntaxRef in type.DeclaringSyntaxReferences)
        {
            var path = syntaxRef.SyntaxTree.FilePath;
            if (string.IsNullOrEmpty(path)) continue;

            var normalized = NormalizePath(path);

            if (allowlist.Contains(normalized)) return true;

            foreach (var entry in allowlist)
            {
                if (normalized.EndsWith("/" + entry, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
