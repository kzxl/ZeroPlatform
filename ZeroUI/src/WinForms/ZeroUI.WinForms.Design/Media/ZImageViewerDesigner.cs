using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Windows.Forms;
using ZeroUI.Core.Media;
using ZeroUI.WinForms.Media;

namespace ZeroUI.WinForms.Design.Media
{
    /// <summary>
    /// Smart Tag Action List for ZImageViewer.
    /// </summary>
    public class ZImageViewerActionList : ZeroActionList<ZImageViewer>
    {
        public ZImageViewerActionList(IComponent component) : base(component)
        {
        }

        public ImageViewMode ViewMode
        {
            get => GetProperty<ImageViewMode>(nameof(ZImageViewer.ViewMode));
            set => SetProperty(nameof(ZImageViewer.ViewMode), value);
        }

        public bool ShowToolbar
        {
            get => GetProperty<bool>(nameof(ZImageViewer.ShowToolbar));
            set => SetProperty(nameof(ZImageViewer.ShowToolbar), value);
        }

        public bool ShowStatusBar
        {
            get => GetProperty<bool>(nameof(ZImageViewer.ShowStatusBar));
            set => SetProperty(nameof(ZImageViewer.ShowStatusBar), value);
        }

        public bool ShowMiniMap
        {
            get => GetProperty<bool>(nameof(ZImageViewer.ShowMiniMap));
            set => SetProperty(nameof(ZImageViewer.ShowMiniMap), value);
        }

        public bool ShowPixelGrid
        {
            get => GetProperty<bool>(nameof(ZImageViewer.ShowPixelGrid));
            set => SetProperty(nameof(ZImageViewer.ShowPixelGrid), value);
        }

        public DockStyle Dock
        {
            get => Control.Dock;
            set => SetProperty(nameof(Control.Dock), value);
        }

        public void LoadSampleImage()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Select Image for ZImageViewer",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files (*.*)|*.*"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var img = Image.FromFile(ofd.FileName);
                    SetProperty(nameof(ZImageViewer.Image), img);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load image: {ex.Message}", "ZeroUI Designer", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public void ClearImage()
        {
            SetProperty(nameof(ZImageViewer.Image), null);
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection();

            items.Add(new DesignerActionHeaderItem("Image Source"));
            items.Add(new DesignerActionMethodItem(this, nameof(LoadSampleImage), "Select Image File...", "Image Source", "Loads image into viewer", true));
            items.Add(new DesignerActionMethodItem(this, nameof(ClearImage), "Clear Image", "Image Source", "Clears assigned image"));

            items.Add(new DesignerActionHeaderItem("Viewport & Scaling"));
            items.Add(new DesignerActionPropertyItem(nameof(ViewMode), "View Mode", "Viewport & Scaling", "Fit to window or 1:1 scale"));
            items.Add(new DesignerActionPropertyItem(nameof(Dock), "Dock in Container", "Viewport & Scaling", "Fill parent container"));

            items.Add(new DesignerActionHeaderItem("Forensic HUD & Overlays"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowToolbar), "Show Action Toolbar", "Forensic HUD & Overlays", "Top pan/zoom toolbar"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowStatusBar), "Show Telemetry Status Bar", "Forensic HUD & Overlays", "Bottom coordinates/RGB status bar"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowMiniMap), "Show MiniMap Navigator", "Forensic HUD & Overlays", "Interactive corner thumbnail navigator"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowPixelGrid), "Show Pixel Grid (>800%)", "Forensic HUD & Overlays", "Sub-pixel forensic alignment grid"));

            return items;
        }
    }

    /// <summary>
    /// Component Designer for ZImageViewer.
    /// </summary>
    public class ZImageViewerDesigner : ZeroControlDesigner<ZImageViewer>
    {
        protected override DesignerActionList CreateActionList() => new ZImageViewerActionList(Component);
    }
}
