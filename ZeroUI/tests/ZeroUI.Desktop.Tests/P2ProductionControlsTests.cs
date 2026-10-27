using System;
using System.Collections.Generic;
using System.Drawing;
using Xunit;
using ZeroUI.Core.Editors;

namespace ZeroUI.Desktop.Tests
{
    public class P2ProductionControlsTests
    {
        #region ZMultiSelect Tests

        [Fact]
        public void ZMultiSelect_WinForms_SelectionFlowAndChipRemoval()
        {
            using var control = new ZeroUI.WinForms.Editors.ZMultiSelect();
            control.Placeholder = "Pick technologies";
            control.ItemsSource = new List<string> { "C#", "Rust", "TypeScript", "Python" };

            Assert.Equal("Pick technologies", control.Placeholder);
            Assert.Empty(control.SelectedItems);

            bool eventFired = false;
            control.SelectionChanged += (s, e) => eventFired = true;

            control.SelectedItems = new List<object> { "C#", "Rust" };
            Assert.True(eventFired);
            Assert.Equal(2, control.SelectedItems.Count);

            // Setting max selections clamps
            control.MaxSelections = 1;
            Assert.Single(control.SelectedItems);
            Assert.Equal("C#", control.SelectedItems[0]);
        }

        [Fact]
        public void ZMultiSelect_Wpf_SelectionFlowOnStaThread()
        {
            StaTestRunner.Run(() =>
            {
                var control = new ZeroUI.Wpf.Editors.ZMultiSelect();
                control.Placeholder = "Pick frameworks";
                control.ItemsSource = new List<string> { "WPF", "WinForms", "Avalonia", "MAUI" };

                bool eventFired = false;
                control.SelectionChanged += (s, e) => eventFired = true;

                control.SelectedItems = new List<object> { "WPF", "Avalonia" };
                Assert.True(eventFired);
                Assert.Equal(2, control.SelectedItems.Count);

                control.RemoveItemAt(0);
                Assert.Single(control.SelectedItems);
                Assert.Equal("Avalonia", control.SelectedItems[0]);

                control.MaxSelections = 1;
                Assert.Single(control.SelectedItems);
            });
        }

        #endregion

        #region ZSignaturePad Tests

        [Fact]
        public void ZSignaturePad_WinForms_StrokeDrawingAndImageExport()
        {
            using var pad = new ZeroUI.WinForms.Editors.ZSignaturePad();
            Assert.True(pad.IsEmpty);

            pad.StrokeColor = Color.Navy;
            pad.StrokeWidth = 4;
            pad.BackgroundColor = Color.WhiteSmoke;

            Assert.Equal(Color.Navy, pad.StrokeColor);
            Assert.Equal(4, pad.StrokeWidth);
            Assert.Equal(Color.WhiteSmoke, pad.BackgroundColor);

            // Export initial image
            using var bmp = pad.GetSignatureImage();
            Assert.NotNull(bmp);
            Assert.Equal(pad.Width, bmp.Width);
            Assert.Equal(pad.Height, bmp.Height);

            pad.Clear();
            Assert.True(pad.IsEmpty);
        }

        [Fact]
        public void ZSignaturePad_Wpf_PropertiesAndExportOnStaThread()
        {
            StaTestRunner.Run(() =>
            {
                var pad = new ZeroUI.Wpf.Editors.ZSignaturePad();
                Assert.True(pad.IsEmpty);

                pad.StrokeColor = System.Windows.Media.Colors.DarkRed;
                pad.StrokeWidth = 5;
                pad.BackgroundColor = System.Windows.Media.Colors.GhostWhite;

                Assert.Equal(System.Windows.Media.Colors.DarkRed, pad.StrokeColor);
                Assert.Equal(5, pad.StrokeWidth);
                Assert.Equal(System.Windows.Media.Colors.GhostWhite, pad.BackgroundColor);

                var img = pad.GetSignatureImage();
                Assert.NotNull(img);

                pad.Clear();
                Assert.True(pad.IsEmpty);
            });
        }

        #endregion

        #region ZTransferList Tests

        [Fact]
        public void ZTransferList_WinForms_ItemMovementActions()
        {
            using var transfer = new ZeroUI.WinForms.Containers.ZTransferList();
            transfer.AvailableItems = new List<string> { "Item A", "Item B", "Item C" };
            transfer.SelectedItems = new List<string> { "Item D" };

            Assert.Equal("Available", transfer.AvailableTitle);
            Assert.Equal("Selected", transfer.SelectedTitle);

            bool eventFired = false;
            transfer.ItemsTransferred += (s, e) => eventFired = true;

            // Transfer all to right
            transfer.TransferAllToRight();
            Assert.True(eventFired);

            var available = (List<object>)transfer.AvailableItems;
            var selected = (List<object>)transfer.SelectedItems;

            Assert.Empty(available);
            Assert.Equal(4, selected.Count);

            // Transfer all to left
            transfer.TransferAllToLeft();
            Assert.Equal(4, available.Count);
            Assert.Empty(selected);
        }

        [Fact]
        public void ZTransferList_Wpf_ItemMovementActionsOnStaThread()
        {
            StaTestRunner.Run(() =>
            {
                var transfer = new ZeroUI.Wpf.Containers.ZTransferList();
                transfer.AvailableItems = new List<string> { "Alpha", "Beta", "Gamma" };
                transfer.SelectedItems = new List<string> { "Delta" };

                bool eventFired = false;
                transfer.ItemsTransferred += (s, e) => eventFired = true;

                transfer.TransferAllRight();
                Assert.True(eventFired);

                var available = (System.Collections.ObjectModel.ObservableCollection<object>)transfer.AvailableItems;
                var selected = (System.Collections.ObjectModel.ObservableCollection<object>)transfer.SelectedItems;

                Assert.Empty(available);
                Assert.Equal(4, selected.Count);

                transfer.TransferAllLeft();
                Assert.Equal(4, available.Count);
                Assert.Empty(selected);
            });
        }

        #endregion

        #region ZEmptyState Tests

        [Fact]
        public void ZEmptyState_WinForms_GlyphAndActionTextProperties()
        {
            using var emptyState = new ZeroUI.WinForms.Feedback.ZEmptyState();
            emptyState.Glyph = "🔍";
            emptyState.Title = "No Results";
            emptyState.Description = "Try searching for a different keyword.";
            emptyState.ActionText = "Clear Search";

            Assert.Equal("🔍", emptyState.Glyph);
            Assert.Equal("No Results", emptyState.Title);
            Assert.Equal("Try searching for a different keyword.", emptyState.Description);
            Assert.Equal("Clear Search", emptyState.ActionText);
            Assert.True(emptyState.ShowAction);

            emptyState.ActionText = "";
            Assert.False(emptyState.ShowAction);
        }

        [Fact]
        public void ZEmptyState_Wpf_GlyphAndActionTextOnStaThread()
        {
            StaTestRunner.Run(() =>
            {
                var emptyState = new ZeroUI.Wpf.Feedback.ZEmptyState();
                emptyState.Glyph = "⚡";
                emptyState.Title = "No Signals";
                emptyState.Description = "Connect an active sensor channel.";
                emptyState.ActionText = "Reconnect";

                Assert.Equal("⚡", emptyState.Glyph);
                Assert.Equal("No Signals", emptyState.Title);
                Assert.True(emptyState.ShowAction);

                emptyState.ActionText = "   ";
                Assert.False(emptyState.ShowAction);
            });
        }

        #endregion

        #region ZAvatar Tests

        [Fact]
        public void ZAvatar_WinForms_InitialsPresetsAndStatus()
        {
            using var avatar = new ZeroUI.WinForms.Feedback.ZAvatar();
            avatar.Initials = "jd";
            Assert.Equal("JD", avatar.Initials);

            avatar.Size = "Small";
            Assert.Equal(28, avatar.Width);
            Assert.Equal(28, avatar.Height);

            avatar.Size = "Large";
            Assert.Equal(56, avatar.Width);
            Assert.Equal(56, avatar.Height);

            avatar.Size = "64";
            Assert.Equal(64, avatar.Width);
            Assert.Equal(64, avatar.Height);

            avatar.ShowOnlineIndicator = true;
            avatar.Status = AvatarStatus.Busy;
            Assert.True(avatar.ShowOnlineIndicator);
            Assert.Equal(AvatarStatus.Busy, avatar.Status);
        }

        [Fact]
        public void ZAvatar_Wpf_InitialsPresetsAndStatusOnStaThread()
        {
            StaTestRunner.Run(() =>
            {
                var avatar = new ZeroUI.Wpf.Feedback.ZAvatar();
                avatar.Initials = "pv";
                Assert.Equal("PV", avatar.Initials);

                avatar.Size = "XLarge";
                Assert.Equal(72, avatar.Width);
                Assert.Equal(72, avatar.Height);

                avatar.Size = "Medium";
                Assert.Equal(40, avatar.Width);
                Assert.Equal(40, avatar.Height);

                avatar.ShowOnlineIndicator = true;
                avatar.Status = AvatarStatus.Online;
                Assert.True(avatar.ShowOnlineIndicator);
                Assert.Equal(AvatarStatus.Online, avatar.Status);
            });
        }

        #endregion
    }
}
