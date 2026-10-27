using System;
using System.Windows;

namespace ZeroUI.Wpf.Overlays
{
    /// <summary>
    /// Represents an individual guided walkthrough step within a <see cref="ZTour"/>.
    /// Supports target element anchoring, spotlight masking, custom placement, and action triggers.
    /// </summary>
    public class ZTourStep : DependencyObject
    {
        public static readonly DependencyProperty TargetProperty =
            DependencyProperty.Register(nameof(Target), typeof(UIElement), typeof(ZTourStep), new PropertyMetadata(null));

        public static readonly DependencyProperty TargetNameProperty =
            DependencyProperty.Register(nameof(TargetName), typeof(string), typeof(ZTourStep), new PropertyMetadata(null));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ZTourStep), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(ZTourStep), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty PlacementProperty =
            DependencyProperty.Register(nameof(Placement), typeof(ZTourPlacement), typeof(ZTourStep), new PropertyMetadata(ZTourPlacement.Auto));

        public static readonly DependencyProperty TargetPaddingProperty =
            DependencyProperty.Register(nameof(TargetPadding), typeof(Thickness), typeof(ZTourStep), new PropertyMetadata(new Thickness(6)));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(double), typeof(ZTourStep), new PropertyMetadata(8.0));

        public static readonly DependencyProperty MaskProperty =
            DependencyProperty.Register(nameof(Mask), typeof(bool), typeof(ZTourStep), new PropertyMetadata(true));

        public static readonly DependencyProperty CustomContentProperty =
            DependencyProperty.Register(nameof(CustomContent), typeof(object), typeof(ZTourStep), new PropertyMetadata(null));

        public static readonly DependencyProperty NextButtonTextProperty =
            DependencyProperty.Register(nameof(NextButtonText), typeof(string), typeof(ZTourStep), new PropertyMetadata(null));

        public static readonly DependencyProperty PrevButtonTextProperty =
            DependencyProperty.Register(nameof(PrevButtonText), typeof(string), typeof(ZTourStep), new PropertyMetadata(null));

        public static readonly DependencyProperty AllowInteractionProperty =
            DependencyProperty.Register(nameof(AllowInteraction), typeof(bool), typeof(ZTourStep), new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets the target UI element to highlight with a spotlight cutout.
        /// </summary>
        public UIElement? Target
        {
            get => (UIElement?)GetValue(TargetProperty);
            set => SetValue(TargetProperty, value);
        }

        /// <summary>
        /// Gets or sets the name of the target element to resolve from the owner visual tree.
        /// </summary>
        public string? TargetName
        {
            get => (string?)GetValue(TargetNameProperty);
            set => SetValue(TargetNameProperty, value);
        }

        /// <summary>
        /// Gets or sets the title of the walkthrough step.
        /// </summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>
        /// Gets or sets the explanatory description text.
        /// </summary>
        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        /// <summary>
        /// Gets or sets the preferred placement of the card relative to the target.
        /// </summary>
        public ZTourPlacement Placement
        {
            get => (ZTourPlacement)GetValue(PlacementProperty);
            set => SetValue(PlacementProperty, value);
        }

        /// <summary>
        /// Gets or sets the padding around the target element bounding box for the spotlight cutout.
        /// </summary>
        public Thickness TargetPadding
        {
            get => (Thickness)GetValue(TargetPaddingProperty);
            set => SetValue(TargetPaddingProperty, value);
        }

        /// <summary>
        /// Gets or sets the rounded corner radius for the spotlight cutout hole.
        /// </summary>
        public double CornerRadius
        {
            get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether a dark backdrop mask is rendered.
        /// </summary>
        public bool Mask
        {
            get => (bool)GetValue(MaskProperty);
            set => SetValue(MaskProperty, value);
        }

        /// <summary>
        /// Gets or sets custom WPF content to render inside the tour step card.
        /// </summary>
        public object? CustomContent
        {
            get => GetValue(CustomContentProperty);
            set => SetValue(CustomContentProperty, value);
        }

        /// <summary>
        /// Gets or sets custom text for the Next button. Defaults to "Tiếp theo" or "Hoàn tất".
        /// </summary>
        public string? NextButtonText
        {
            get => (string?)GetValue(NextButtonTextProperty);
            set => SetValue(NextButtonTextProperty, value);
        }

        /// <summary>
        /// Gets or sets custom text for the Previous button. Defaults to "Quay lại".
        /// </summary>
        public string? PrevButtonText
        {
            get => (string?)GetValue(PrevButtonTextProperty);
            set => SetValue(PrevButtonTextProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether mouse clicks on the spotlighted element are allowed.
        /// </summary>
        public bool AllowInteraction
        {
            get => (bool)GetValue(AllowInteractionProperty);
            set => SetValue(AllowInteractionProperty, value);
        }

        /// <summary>
        /// Action invoked when this step becomes active.
        /// </summary>
        public Action<ZTourStep>? OnEnter { get; set; }

        /// <summary>
        /// Action invoked when navigating away from this step.
        /// </summary>
        public Action<ZTourStep>? OnLeave { get; set; }

        public ZTourStep() { }

        public ZTourStep(UIElement target, string title, string description, ZTourPlacement placement = ZTourPlacement.Auto)
        {
            Target = target;
            Title = title;
            Description = description;
            Placement = placement;
        }

        public ZTourStep(string targetName, string title, string description, ZTourPlacement placement = ZTourPlacement.Auto)
        {
            TargetName = targetName;
            Title = title;
            Description = description;
            Placement = placement;
        }
    }
}
