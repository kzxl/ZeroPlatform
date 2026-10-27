using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace ZeroData.Core.Text;

/// <summary>
/// Parsed metadata extracted from a NuGet .nupkg package.
/// </summary>
public class NupkgPackageMetadata
{
    public string PackageId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public List<string> TargetFrameworks { get; } = new();
    public string? SelectedFramework { get; set; }
}

/// <summary>
/// Container holding memory streams of DLL and XML doc extracted from a .nupkg archive.
/// </summary>
public class NupkgInMemoryPackage : IDisposable
{
    public NupkgPackageMetadata Metadata { get; set; } = new();
    public Stream AssemblyStream { get; set; } = Stream.Null;
    public Stream? XmlDocumentationStream { get; set; }

    public void Dispose()
    {
        AssemblyStream?.Dispose();
        XmlDocumentationStream?.Dispose();
    }
}

/// <summary>
/// In-memory stream inspector for NuGet packages (.nupkg) that avoids temporary disk writes.
/// </summary>
public static class NupkgMemoryInspector
{
    public static NupkgInMemoryPackage ExtractFromStream(Stream nupkgStream, string? preferredTfm = null)
    {
        if (nupkgStream == null) throw new ArgumentNullException(nameof(nupkgStream));

        using var archive = new ZipArchive(nupkgStream, ZipArchiveMode.Read, leaveOpen: true);
        var meta = new NupkgPackageMetadata();

        // 1. Locate and parse .nuspec entry
        var nuspecEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        if (nuspecEntry != null)
        {
            using var nuspecStream = nuspecEntry.Open();
            var doc = XDocument.Load(nuspecStream);
            XNamespace ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
            var metadataElem = doc.Root?.Element(ns + "metadata") ?? doc.Root?.Element("metadata");

            if (metadataElem != null)
            {
                meta.PackageId = metadataElem.Element(ns + "id")?.Value ?? metadataElem.Element("id")?.Value ?? string.Empty;
                meta.Version = metadataElem.Element(ns + "version")?.Value ?? metadataElem.Element("version")?.Value ?? string.Empty;
                meta.Description = metadataElem.Element(ns + "description")?.Value ?? metadataElem.Element("description")?.Value ?? string.Empty;
                meta.Authors = metadataElem.Element(ns + "authors")?.Value ?? metadataElem.Element("authors")?.Value ?? string.Empty;
            }
        }

        // 2. Discover available target frameworks in lib/
        var libPrefix = "lib/";
        var tfms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.StartsWith(libPrefix, StringComparison.OrdinalIgnoreCase) && !entry.FullName.EndsWith("/"))
            {
                var rel = entry.FullName.Substring(libPrefix.Length);
                int slash = rel.IndexOf('/');
                if (slash > 0)
                {
                    tfms.Add(rel.Substring(0, slash));
                }
            }
        }

        meta.TargetFrameworks.AddRange(tfms);

        string selectedTfm = preferredTfm ?? meta.TargetFrameworks.FirstOrDefault() ?? string.Empty;
        if (!meta.TargetFrameworks.Contains(selectedTfm) && meta.TargetFrameworks.Count > 0)
        {
            selectedTfm = meta.TargetFrameworks[0];
        }
        meta.SelectedFramework = selectedTfm;

        // 3. Extract Assembly & XML Doc streams into MemoryStreams
        string prefix = string.IsNullOrEmpty(selectedTfm) ? "lib/" : $"lib/{selectedTfm}/";
        var tfmEntries = archive.Entries.Where(e => e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();

        if (tfmEntries.Count == 0)
        {
            tfmEntries = archive.Entries.Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                                                    e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        MemoryStream? assemblyStream = null;
        MemoryStream? xmlDocStream = null;

        foreach (var entry in tfmEntries)
        {
            if (entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                if (assemblyStream == null)
                {
                    assemblyStream = new MemoryStream();
                    using var s = entry.Open();
                    s.CopyTo(assemblyStream);
                    assemblyStream.Position = 0;
                }
            }
            else if (entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                xmlDocStream = new MemoryStream();
                using var s = entry.Open();
                s.CopyTo(xmlDocStream);
                xmlDocStream.Position = 0;
            }
        }

        if (assemblyStream == null)
        {
            throw new InvalidOperationException($"No compiled assembly (.dll) found in package stream for framework '{selectedTfm}'.");
        }

        return new NupkgInMemoryPackage
        {
            Metadata = meta,
            AssemblyStream = assemblyStream,
            XmlDocumentationStream = xmlDocStream
        };
    }
}
