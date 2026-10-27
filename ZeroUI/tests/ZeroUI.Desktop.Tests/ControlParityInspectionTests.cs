using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using Xunit;
using Xunit.Abstractions;

namespace ZeroUI.Desktop.Tests
{
    public class ControlParityInspectionTests
    {
        private readonly ITestOutputHelper _output;

        public ControlParityInspectionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void AuditCommonControlsParity_GeneratesComprehensiveReport()
        {
            var winformsAssemblies = new[]
            {
                typeof(ZeroUI.WinForms.Base.ControlBase).Assembly,
                typeof(ZeroUI.WinForms.Editors.TextEdit).Assembly,
                typeof(ZeroUI.WinForms.DataGrid.GridControl).Assembly,
                typeof(ZeroUI.WinForms.Charts.ChartControl).Assembly,
                typeof(ZeroUI.WinForms.Industrial.LinearGauge).Assembly,
                typeof(ZeroUI.WinForms.Reporting.PdfViewerControl).Assembly
            };
            var wpfAssemblies = new[]
            {
                typeof(ZeroUI.Wpf.Base.WpfControlBase).Assembly,
                typeof(ZeroUI.Wpf.Editors.TextEdit).Assembly,
                typeof(ZeroUI.Wpf.DataGrid.GridControl).Assembly,
                typeof(ZeroUI.Wpf.Charts.ChartControl).Assembly,
                typeof(ZeroUI.Wpf.Industrial.LinearGauge).Assembly,
                typeof(ZeroUI.Wpf.Reporting.PdfViewerControl).Assembly
            };

            var baseWfPropNames = typeof(Control).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name).ToHashSet();
            var baseWpfPropNames = typeof(FrameworkElement).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name).ToHashSet();

            // Known framework-specific exclusions
            baseWfPropNames.UnionWith(new[] { "DpiScale", "AutoScaleDimensions", "CurrentAutoScaleDimensions", "CustomSkin", "EffectiveSkin", "UseDefaultSkin" });
            baseWpfPropNames.UnionWith(new[] { "ClipToBounds" });

            var wfControls = winformsAssemblies
                .SelectMany(a => a.GetExportedTypes())
                .Where(t => !t.IsAbstract && typeof(Control).IsAssignableFrom(t))
                .GroupBy(t => t.Name)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.GetCustomAttribute<ObsoleteAttribute>() != null ? 1 : 0).First());

            var wpfControls = wpfAssemblies
                .SelectMany(a => a.GetExportedTypes())
                .Where(t => !t.IsAbstract && typeof(FrameworkElement).IsAssignableFrom(t))
                .GroupBy(t => t.Name)
                .ToDictionary(g => g.Key, g => g.OrderBy(t => t.GetCustomAttribute<ObsoleteAttribute>() != null ? 1 : 0).First());

            var commonNames = wfControls.Keys.Intersect(wpfControls.Keys).OrderBy(n => n).ToList();
            var onlyWf = wfControls.Keys.Except(wpfControls.Keys).OrderBy(n => n).ToList();
            var onlyWpf = wpfControls.Keys.Except(wfControls.Keys).OrderBy(n => n).ToList();

            _output.WriteLine($"Discovered {commonNames.Count} common control pairs between WinForms and WPF.");
            _output.WriteLine($"Controls ONLY in WinForms ({onlyWf.Count}): {string.Join(", ", onlyWf)}");
            _output.WriteLine($"Controls ONLY in WPF ({onlyWpf.Count}): {string.Join(", ", onlyWpf)}\n");

            var reportBuilder = new StringBuilder();
            reportBuilder.AppendLine("# Cross-Platform Control Parity Audit Report");
            reportBuilder.AppendLine($"Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            reportBuilder.AppendLine($"Total Common Controls Analyzed: {commonNames.Count}\n");
            reportBuilder.AppendLine("| Control Name | WinForms Specific Properties | WPF Specific Properties | Parity Status |");
            reportBuilder.AppendLine("| :--- | :--- | :--- | :--- |");

            int fullySynchronizedCount = 0;
            int driftedCount = 0;

            foreach (var name in commonNames)
            {
                var wfType = wfControls[name];
                var wpfType = wpfControls[name];

                var wfProps = wfType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<ObsoleteAttribute>() == null)
                    .Select(p => p.Name)
                    .Where(p => !baseWfPropNames.Contains(p) && !p.EndsWith("Property"))
                    .ToHashSet();

                var wpfProps = wpfType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<ObsoleteAttribute>() == null)
                    .Select(p => p.Name)
                    .Where(p => !baseWpfPropNames.Contains(p) && !p.EndsWith("Property"))
                    .ToHashSet();

                var missingInWpf = wfProps.Except(wpfProps).OrderBy(p => p).ToList();
                var missingInWf = wpfProps.Except(wfProps).OrderBy(p => p).ToList();

                bool isParity = missingInWpf.Count == 0 && missingInWf.Count == 0;
                if (isParity)
                {
                    fullySynchronizedCount++;
                    reportBuilder.AppendLine($"| **{name}** | *(None)* | *(None)* | 🟢 100% Synced |");
                }
                else
                {
                    driftedCount++;
                    string wfSpecific = missingInWpf.Count > 0 ? string.Join(", ", missingInWpf.Take(5)) + (missingInWpf.Count > 5 ? $" (+{missingInWpf.Count - 5})" : "") : "*(None)*";
                    string wpfSpecific = missingInWf.Count > 0 ? string.Join(", ", missingInWf.Take(5)) + (missingInWf.Count > 5 ? $" (+{missingInWf.Count - 5})" : "") : "*(None)*";
                    reportBuilder.AppendLine($"| **{name}** | {wfSpecific} | {wpfSpecific} | ⚠️ Drifted |");
                }
            }

            reportBuilder.AppendLine("\n## Summary Metrics");
            reportBuilder.AppendLine($"- **Fully Synchronized (100% Parity):** {fullySynchronizedCount} controls ({(fullySynchronizedCount * 100.0 / commonNames.Count):F1}%)");
            reportBuilder.AppendLine($"- **Feature Drifted (Needs Harmonization):** {driftedCount} controls ({(driftedCount * 100.0 / commonNames.Count):F1}%)");

            string reportContent = reportBuilder.ToString();
            _output.WriteLine(reportContent);

            // Persist report into artifacts directory for review
            string artifactDir = @"C:\Users\phong.vo\.gemini\antigravity-ide\brain\52bcabd2-e06d-4d2d-ac0c-f63384ed54fc";
            if (Directory.Exists(artifactDir))
            {
                File.WriteAllText(Path.Combine(artifactDir, "control_parity_audit_report.md"), reportContent);
            }

            Assert.True(commonNames.Count > 0, "Expected to find common controls between WinForms and WPF.");
        }
    }
}
