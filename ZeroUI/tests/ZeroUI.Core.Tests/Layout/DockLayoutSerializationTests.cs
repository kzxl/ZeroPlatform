using System;
using System.IO;
using Xunit;
using ZeroUI.Core.Common;
using ZeroUI.Core.Layout;

namespace ZeroUI.Core.Tests.Layout
{
    public class DockLayoutSerializationTests
    {
        [Fact]
        public void WorkspaceLayout_SerializeAndDeserialize_PreservesFullState()
        {
            var original = new WorkspaceLayoutState
            {
                Version = "1.1",
                SavedAt = new DateTime(2026, 9, 8, 8, 30, 0, DateTimeKind.Utc),
                Containers = new DockContainerLayoutState
                {
                    LeftWidth = 310,
                    RightWidth = 290,
                    TopHeight = 150,
                    BottomHeight = 220,
                    ActiveDocumentTitle = "Main Schematic"
                }
            };

            original.DockPanels.Add(new DockPanelLayoutState
            {
                Name = "pnlSolution",
                Title = "Solution Explorer",
                DockPosition = "Left",
                IsPinned = true,
                AutoHide = false,
                Closable = true,
                Floatable = true,
                Width = 310,
                Height = 600,
                OrderIndex = 0
            });

            original.DockPanels.Add(new DockPanelLayoutState
            {
                Name = "pnlOutput",
                Title = "Diagnostic Log",
                DockPosition = "Bottom",
                IsPinned = false,
                AutoHide = true,
                Closable = true,
                Floatable = true,
                Width = 800,
                Height = 220,
                OrderIndex = 1
            });

            original.DockPanels.Add(new DockPanelLayoutState
            {
                Name = "pnlFloatTools",
                Title = "PID Loop Tuning",
                DockPosition = "Float",
                IsPinned = true,
                AutoHide = false,
                Closable = true,
                Floatable = true,
                Width = 320,
                Height = 420,
                FloatX = 250,
                FloatY = 180,
                FloatWidth = 400,
                FloatHeight = 500,
                OrderIndex = 2
            });

            original.GridColumns.Add(new ColumnLayoutState
            {
                FieldName = "TagId",
                Width = 120,
                IsVisible = true,
                IsPinned = true,
                GroupIndex = -1,
                SortOrder = SortDirection.Ascending
            });

            original.GridColumns.Add(new ColumnLayoutState
            {
                FieldName = "Value",
                Width = 90,
                IsVisible = true,
                IsPinned = false,
                GroupIndex = -1,
                SortOrder = SortDirection.None
            });

            // Act: Serialize to JSON
            string json = ZeroWorkspaceSerializer.Serialize(original);
            Assert.False(string.IsNullOrWhiteSpace(json));

            // Act: Deserialize from JSON
            var restored = ZeroWorkspaceSerializer.Deserialize(json);

            // Assert: Version & Metadata
            Assert.Equal(original.Version, restored.Version);
            Assert.Equal(original.SavedAt, restored.SavedAt);

            // Assert: Containers
            Assert.Equal(310, restored.Containers.LeftWidth);
            Assert.Equal(290, restored.Containers.RightWidth);
            Assert.Equal(150, restored.Containers.TopHeight);
            Assert.Equal(220, restored.Containers.BottomHeight);
            Assert.Equal("Main Schematic", restored.Containers.ActiveDocumentTitle);

            // Assert: DockPanels
            Assert.Equal(3, restored.DockPanels.Count);

            var p0 = restored.DockPanels[0];
            Assert.Equal("pnlSolution", p0.Name);
            Assert.Equal("Solution Explorer", p0.Title);
            Assert.Equal("Left", p0.DockPosition);
            Assert.True(p0.IsPinned);
            Assert.False(p0.AutoHide);
            Assert.Equal(310, p0.Width);

            var p1 = restored.DockPanels[1];
            Assert.Equal("pnlOutput", p1.Name);
            Assert.Equal("Diagnostic Log", p1.Title);
            Assert.Equal("Bottom", p1.DockPosition);
            Assert.False(p1.IsPinned);
            Assert.True(p1.AutoHide);

            var p2 = restored.DockPanels[2];
            Assert.Equal("pnlFloatTools", p2.Name);
            Assert.Equal("Float", p2.DockPosition);
            Assert.Equal(250, p2.FloatX);
            Assert.Equal(180, p2.FloatY);
            Assert.Equal(400, p2.FloatWidth);
            Assert.Equal(500, p2.FloatHeight);

            // Assert: GridColumns
            Assert.Equal(2, restored.GridColumns.Count);
            Assert.Equal("TagId", restored.GridColumns[0].FieldName);
            Assert.Equal(120, restored.GridColumns[0].Width);
            Assert.True(restored.GridColumns[0].IsPinned);
            Assert.Equal(SortDirection.Ascending, restored.GridColumns[0].SortOrder);
        }

        [Fact]
        public void WorkspaceLayout_DeserializeEmptyOrMalformed_ReturnsSafeDefault()
        {
            var r1 = ZeroWorkspaceSerializer.Deserialize("");
            Assert.NotNull(r1);
            Assert.NotNull(r1.Containers);
            Assert.Empty(r1.DockPanels);
            Assert.Empty(r1.GridColumns);

            var r2 = ZeroWorkspaceSerializer.Deserialize("{ \"InvalidJson\": true ");
            Assert.NotNull(r2);
            Assert.NotNull(r2.Containers);
        }

        [Fact]
        public void WorkspaceLayout_Roundtrip_JsonWithEscapeCharacters_PreservesCleanStrings()
        {
            var original = new WorkspaceLayoutState();
            original.Containers.ActiveDocumentTitle = "Line \"1\" \\ Tank / Reactor";
            original.DockPanels.Add(new DockPanelLayoutState
            {
                Name = "pnl\"Quoted\"",
                Title = "Special \"Characters\" & \\Path\\To\\File",
                DockPosition = "Left"
            });

            string json = ZeroWorkspaceSerializer.Serialize(original);
            var restored = ZeroWorkspaceSerializer.Deserialize(json);

            Assert.Equal("Line \"1\" \\ Tank / Reactor", restored.Containers.ActiveDocumentTitle);
            Assert.Single(restored.DockPanels);
            Assert.Equal("pnl\"Quoted\"", restored.DockPanels[0].Name);
            Assert.Equal("Special \"Characters\" & \\Path\\To\\File", restored.DockPanels[0].Title);
        }

        [Fact]
        public void WorkspaceLayout_LegacyJsonMissingContainers_SafelyUsesDefaults()
        {
            string legacyJson = @"
            {
              ""Version"": ""1.0"",
              ""DockPanels"": [
                {
                  ""Title"": ""Legacy Panel"",
                  ""DockPosition"": ""Right"",
                  ""Width"": 300
                }
              ]
            }";

            var restored = ZeroWorkspaceSerializer.Deserialize(legacyJson);

            Assert.NotNull(restored.Containers);
            Assert.Equal(260, restored.Containers.LeftWidth);
            Assert.Single(restored.DockPanels);
            Assert.Equal("Legacy Panel", restored.DockPanels[0].Title);
            Assert.Equal("Right", restored.DockPanels[0].DockPosition);
            Assert.Equal(300, restored.DockPanels[0].Width);
        }
    }
}
