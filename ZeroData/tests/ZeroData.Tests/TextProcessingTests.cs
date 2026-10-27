using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Xunit;
using ZeroData.Core.Text;

namespace ZeroData.Tests;

public class TextProcessingTests
{
    [Fact]
    public void VariableInterpolator_ReplacesTokensAndFallbacksCorrectly()
    {
        var vars = new Dictionary<string, string>
        {
            ["baseUrl"] = "https://api.zeroplatform.io",
            ["apiVersion"] = "v2"
        };

        var template = "{{baseUrl}}/{{apiVersion}}/users/{{userId:guest}}?token={{missingToken}}";
        var result = VariableInterpolator.Interpolate(template, vars);

        Assert.Equal("https://api.zeroplatform.io/v2/users/guest?token={{missingToken}}", result);
    }

    [Fact]
    public void DotPathQuery_TraversesObjectAndArrays()
    {
        var json = """
        {
            "status": "success",
            "data": {
                "organization": "ZeroUniverse",
                "members": [
                    { "id": 101, "name": "Alice" },
                    { "id": 102, "name": "Bob" }
                ]
            }
        }
        """;

        using var doc = JsonDocument.Parse(json);

        var org = doc.SelectPath("data.organization")?.GetString();
        Assert.Equal("ZeroUniverse", org);

        var bobName = doc.SelectPath("data.members[1].name")?.GetString();
        Assert.Equal("Bob", bobName);

        var bobId = doc.SelectPath("data.members[1].id")?.GetInt32();
        Assert.Equal(102, bobId);

        var nonexistent = doc.SelectPath("data.members[5].name");
        Assert.Null(nonexistent);
    }

    [Fact]
    public void CurlCommandParser_ExtractsHeadersAndPayload()
    {
        var curl = "curl -X POST https://api.zero.io/v1/telemetry " +
                   "-H 'Content-Type: application/json' " +
                   "-H 'Authorization: Bearer mySecretToken' " +
                   "--data-raw '{\"metric\":\"cpu\",\"value\":42}' " +
                   "--insecure";

        var req = CurlCommandParser.Parse(curl);

        Assert.Equal("POST", req.Method);
        Assert.Equal("https://api.zero.io/v1/telemetry", req.Url);
        Assert.True(req.AllowInsecureSsl);
        Assert.Equal("application/json", req.Headers["Content-Type"]);
        Assert.Equal("Bearer mySecretToken", req.Headers["Authorization"]);
        Assert.Equal("{\"metric\":\"cpu\",\"value\":42}", req.Body);
    }

    [Fact]
    public void NupkgMemoryInspector_ExtractsAssemblyAndNuspecDirectlyFromMemory()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Add nuspec
            var nuspec = zip.CreateEntry("ZeroDemo.nuspec");
            using (var writer = new StreamWriter(nuspec.Open()))
            {
                writer.Write("""
                <?xml version="1.0"?>
                <package>
                  <metadata>
                    <id>ZeroDemo.Core</id>
                    <version>2.0.0</version>
                    <authors>ZeroTeam</authors>
                    <description>Synthetic package</description>
                  </metadata>
                </package>
                """);
            }

            // Add net8.0 dll
            var dll = zip.CreateEntry("lib/net8.0/ZeroDemo.Core.dll");
            using (var writer = new StreamWriter(dll.Open()))
            {
                writer.Write("SYNTHETIC_DLL_CONTENT");
            }
        }

        ms.Position = 0;
        using var pkg = NupkgMemoryInspector.ExtractFromStream(ms, "net8.0");

        Assert.Equal("ZeroDemo.Core", pkg.Metadata.PackageId);
        Assert.Equal("2.0.0", pkg.Metadata.Version);
        Assert.Equal("net8.0", pkg.Metadata.SelectedFramework);
        Assert.NotNull(pkg.AssemblyStream);

        using var reader = new StreamReader(pkg.AssemblyStream, Encoding.UTF8);
        var content = reader.ReadToEnd();
        Assert.Equal("SYNTHETIC_DLL_CONTENT", content);
    }
}
