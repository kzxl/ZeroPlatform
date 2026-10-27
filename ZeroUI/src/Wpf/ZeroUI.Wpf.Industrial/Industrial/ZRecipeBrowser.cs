using System;
using System.Windows;
using System.Windows.Media;

namespace ZeroUI.Wpf.Industrial
{    public class Recipe { public string Name { get; set; } = string.Empty; public System.Collections.Generic.List<string> Steps { get; set; } = new System.Collections.Generic.List<string>(); public System.Collections.Generic.Dictionary<string, object> Parameters { get; set; } = new System.Collections.Generic.Dictionary<string, object>(); }
    /// <summary>
    /// ZRecipeBrowser WPF control.
    /// </summary>
    public class ZRecipeBrowser : FrameworkElement
    {        public static readonly DependencyProperty RecipesProperty = DependencyProperty.Register(nameof(Recipes), typeof(System.Collections.Generic.List<Recipe>), typeof(ZRecipeBrowser));
        public System.Collections.Generic.List<Recipe> Recipes { get => (System.Collections.Generic.List<Recipe>)GetValue(RecipesProperty); set => SetValue(RecipesProperty, value); }
        public static readonly DependencyProperty SelectedRecipeProperty = DependencyProperty.Register(nameof(SelectedRecipe), typeof(Recipe), typeof(ZRecipeBrowser));
        public Recipe SelectedRecipe { get => (Recipe)GetValue(SelectedRecipeProperty); set => SetValue(SelectedRecipeProperty, value); }
        public static readonly RoutedEvent RecipeSelectedEvent = EventManager.RegisterRoutedEvent(nameof(RecipeSelected), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(ZRecipeBrowser));
        public event RoutedEventHandler RecipeSelected { add { AddHandler(RecipeSelectedEvent, value); } remove { RemoveHandler(RecipeSelectedEvent, value); } }
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
        }
    }

    [Obsolete("ZeroRecipeBrowser is deprecated.")]
    public class ZeroRecipeBrowser : ZRecipeBrowser { }

    [Obsolete("RecipeBrowser is deprecated.")]
    public class RecipeBrowser : ZRecipeBrowser { }
}
