using System;
using System.Windows;
using ZeroUI.Core.Security;

namespace ZeroUI.Wpf.Security
{
    public enum UnauthorizedBehavior
    {
        Disable = 0,
        Hide = 1
    }

    /// <summary>
    /// Attached properties providing declarative authorization for WPF elements
    /// based on the active operator's assigned roles or permissions.
    /// </summary>
    public static class ZAuthorize
    {
        public static readonly DependencyProperty RequiredRoleProperty =
            DependencyProperty.RegisterAttached(
                "RequiredRole",
                typeof(string),
                typeof(ZAuthorize),
                new PropertyMetadata(string.Empty, OnSecurityPropertyChanged));

        public static readonly DependencyProperty RequiredPermissionProperty =
            DependencyProperty.RegisterAttached(
                "RequiredPermission",
                typeof(string),
                typeof(ZAuthorize),
                new PropertyMetadata(string.Empty, OnSecurityPropertyChanged));

        public static readonly DependencyProperty BehaviorProperty =
            DependencyProperty.RegisterAttached(
                "Behavior",
                typeof(UnauthorizedBehavior),
                typeof(ZAuthorize),
                new PropertyMetadata(UnauthorizedBehavior.Disable, OnSecurityPropertyChanged));

        public static string GetRequiredRole(DependencyObject obj) => (string)obj.GetValue(RequiredRoleProperty);
        public static void SetRequiredRole(DependencyObject obj, string value) => obj.SetValue(RequiredRoleProperty, value);

        public static string GetRequiredPermission(DependencyObject obj) => (string)obj.GetValue(RequiredPermissionProperty);
        public static void SetRequiredPermission(DependencyObject obj, string value) => obj.SetValue(RequiredPermissionProperty, value);

        public static UnauthorizedBehavior GetBehavior(DependencyObject obj) => (UnauthorizedBehavior)obj.GetValue(BehaviorProperty);
        public static void SetBehavior(DependencyObject obj, UnauthorizedBehavior value) => obj.SetValue(BehaviorProperty, value);

        public static ISessionManager? Session { get; set; }

        private static void OnSecurityPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element)
            {
                EvaluateElement(element);
            }
        }

        public static void EvaluateElement(UIElement element)
        {
            if (element == null) return;

            string role = GetRequiredRole(element);
            string permission = GetRequiredPermission(element);
            var behavior = GetBehavior(element);

            var session = Session ?? SessionContext.Current;
            bool authorized = true;

            if (!string.IsNullOrWhiteSpace(role) && !session.HasRole(role))
            {
                authorized = false;
            }

            if (authorized && !string.IsNullOrWhiteSpace(permission) && !session.HasPermission(permission))
            {
                authorized = false;
            }

            if (behavior == UnauthorizedBehavior.Hide)
            {
                element.Visibility = authorized ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                element.IsEnabled = authorized;
            }
        }
    }
}
