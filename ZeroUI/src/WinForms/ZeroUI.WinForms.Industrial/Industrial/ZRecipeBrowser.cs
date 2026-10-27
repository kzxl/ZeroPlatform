using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Industrial
{    public class Recipe { public string Name { get; set; } = string.Empty; public System.Collections.Generic.List<string> Steps { get; set; } = new System.Collections.Generic.List<string>(); public System.Collections.Generic.Dictionary<string, object> Parameters { get; set; } = new System.Collections.Generic.Dictionary<string, object>(); }
    /// <summary>
    /// ZRecipeBrowser WinForms control.
    /// </summary>
    public class ZRecipeBrowser : Control
    {        public System.Collections.Generic.List<Recipe> Recipes { get; set; } = new System.Collections.Generic.List<Recipe>();
        public Recipe SelectedRecipe { get; set; }
        public event EventHandler RecipeSelected;
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }
    }

    [Obsolete("ZeroRecipeBrowser is deprecated.")]
    [ToolboxItem(false)]
    public class ZeroRecipeBrowser : ZRecipeBrowser { }

    [Obsolete("RecipeBrowser is deprecated.")]
    [ToolboxItem(false)]
    public class RecipeBrowser : ZRecipeBrowser { }
}
