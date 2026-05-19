using System;

namespace Abs.FixedAssets.Services.Navigation.Cockpit;

// ADR-018 §D6 — PII opt-in marker for Cockpit preview JSON.
//
// CockpitPreviewSerializer ONLY emits properties decorated with this
// attribute. Properties without it are excluded, including by default any new
// fields a developer adds to a preview record. This is deliberate: every new
// field is a deliberate "I confirm this is safe to ship to the client" act.
//
// Intent matters because the preview JSON blob is rendered inside the HTML page
// (via <script id="__poDetails" type="application/json">) and is therefore
// visible to anyone with the page. Don't decorate vendor pricing rules,
// internal scoring fields, audit metadata, or any unredacted contact info.
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class CockpitPreviewVisibleAttribute : Attribute
{
    // Optional override of the JSON property name. When null, the property's
    // C# name is camelCased (e.g. RequiredDate → requiredDate). Set this when
    // you need to match the legacy JSON blob's snake or short keys exactly.
    public string? Name { get; }

    public CockpitPreviewVisibleAttribute() { }

    public CockpitPreviewVisibleAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Cockpit preview JSON name must not be empty.", nameof(name));
        }
        Name = name;
    }
}
