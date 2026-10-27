using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Core.Security;
using ZeroUI.Wpf.Base;

namespace ZeroUI.Wpf.Security
{
    /// <summary>
    /// Interactive matrix grid control in WPF for configuring role-based permissions (Roles vs Permissions)
    /// with category grouping, live toggling, and decoupled store persistence.
    /// </summary>
    public class ZRoleMatrix : WpfControlBase
    {
        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(
                nameof(IsReadOnly),
                typeof(bool),
                typeof(ZRoleMatrix),
                new PropertyMetadata(false));

        public bool IsReadOnly
        {
            get => (bool)GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        private readonly List<RoleModel> _roles = new List<RoleModel>();
        private readonly List<PermissionModel> _permissions = new List<PermissionModel>();
        private readonly Dictionary<string, HashSet<string>> _assignments = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private readonly ScrollViewer _scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        private readonly Grid _matrixGrid = new Grid();

        public IReadOnlyList<IRole> CurrentRoles => _roles;
        public IReadOnlyList<IPermission> CurrentPermissions => _permissions;

        public event EventHandler<PermissionToggleEventArgs>? PermissionToggled;

        static ZRoleMatrix()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(ZRoleMatrix),
                new FrameworkPropertyMetadata(typeof(ZRoleMatrix)));
        }

        public ZRoleMatrix()
        {
            Width = 750;
            Height = 400;
            Background = new SolidColorBrush(Color.FromRgb(20, 24, 33));

            _scrollViewer.Content = _matrixGrid;
            AddVisualChild(_scrollViewer);
            AddLogicalChild(_scrollViewer);
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _scrollViewer;

        protected override Size MeasureOverride(Size constraint)
        {
            _scrollViewer.Measure(constraint);
            return _scrollViewer.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _scrollViewer.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        public async Task LoadAsync(IRoleStore store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));

            var roles = await store.GetRolesAsync();
            var perms = await store.GetAllPermissionsAsync();

            _roles.Clear();
            _permissions.Clear();
            _assignments.Clear();

            foreach (var r in roles)
            {
                var rm = new RoleModel(r.Id, r.Name, r.Description, r.SecurityLevel, r.Permissions);
                _roles.Add(rm);
                _assignments[r.Id] = new HashSet<string>(r.Permissions, StringComparer.OrdinalIgnoreCase);
            }

            foreach (var p in perms.OrderBy(p => p.Category).ThenBy(p => p.Name))
            {
                _permissions.Add(new PermissionModel(p.Key, p.Name, p.Category, p.Description));
            }

            BuildGridUI();
        }

        public async Task SaveAsync(IRoleStore store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));

            foreach (var role in _roles)
            {
                if (_assignments.TryGetValue(role.Id, out var perms))
                {
                    role.Permissions = perms.ToList();
                    await store.SaveRoleAsync(role);
                }
            }
        }

        public bool IsGranted(string roleId, string permKey)
        {
            if (_assignments.TryGetValue(roleId, out var set))
            {
                return set.Contains(permKey);
            }
            return false;
        }

        public void SetGranted(string roleId, string permKey, bool granted)
        {
            if (!_assignments.TryGetValue(roleId, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _assignments[roleId] = set;
            }

            bool changed = granted ? set.Add(permKey) : set.Remove(permKey);
            if (changed)
            {
                PermissionToggled?.Invoke(this, new PermissionToggleEventArgs(roleId, permKey, granted));
            }
        }

        private void BuildGridUI()
        {
            _matrixGrid.Children.Clear();
            _matrixGrid.RowDefinitions.Clear();
            _matrixGrid.ColumnDefinitions.Clear();

            // Col 0: Permissions info (width 260)
            _matrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });

            // Role columns
            for (int c = 0; c < _roles.Count; c++)
            {
                _matrixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            }

            // Header row (row 0)
            _matrixGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });

            var headerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 36, 49)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(12, 10, 0, 0)
            };
            headerBorder.Child = new TextBlock
            {
                Text = "Permission / Function",
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            Grid.SetRow(headerBorder, 0);
            Grid.SetColumn(headerBorder, 0);
            _matrixGrid.Children.Add(headerBorder);

            for (int c = 0; c < _roles.Count; c++)
            {
                var role = _roles[c];
                var rBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 36, 49)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(4)
                };

                var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                sp.Children.Add(new TextBlock
                {
                    Text = role.Name,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    TextAlignment = TextAlignment.Center,
                    FontSize = 11
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $"Lvl {role.SecurityLevel}",
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    TextAlignment = TextAlignment.Center,
                    FontSize = 9
                });
                rBorder.Child = sp;
                Grid.SetRow(rBorder, 0);
                Grid.SetColumn(rBorder, c + 1);
                _matrixGrid.Children.Add(rBorder);
            }

            // Data rows
            for (int r = 0; r < _permissions.Count; r++)
            {
                var perm = _permissions[r];
                int rowIdx = r + 1;
                _matrixGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });

                var rowBg = (r % 2 == 1) ? new SolidColorBrush(Color.FromRgb(24, 29, 40)) : new SolidColorBrush(Color.FromRgb(20, 24, 33));

                var titleBorder = new Border
                {
                    Background = rowBg,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Padding = new Thickness(12, 4, 0, 0)
                };

                var tsp = new StackPanel();
                tsp.Children.Add(new TextBlock { Text = perm.Name, Foreground = Brushes.White, FontSize = 11 });
                tsp.Children.Add(new TextBlock { Text = $"{perm.Category} • {perm.Key}", Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)), FontSize = 9 });
                titleBorder.Child = tsp;
                Grid.SetRow(titleBorder, rowIdx);
                Grid.SetColumn(titleBorder, 0);
                _matrixGrid.Children.Add(titleBorder);

                for (int c = 0; c < _roles.Count; c++)
                {
                    var role = _roles[c];
                    var cellBorder = new Border
                    {
                        Background = rowBg,
                        BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                        BorderThickness = new Thickness(0, 0, 1, 1)
                    };

                    var chk = new CheckBox
                    {
                        IsChecked = IsGranted(role.Id, perm.Key),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsEnabled = !IsReadOnly
                    };

                    string rId = role.Id;
                    string pKey = perm.Key;
                    chk.Click += (s, e) => SetGranted(rId, pKey, chk.IsChecked == true);

                    cellBorder.Child = chk;
                    Grid.SetRow(cellBorder, rowIdx);
                    Grid.SetColumn(cellBorder, c + 1);
                    _matrixGrid.Children.Add(cellBorder);
                }
            }
        }
    }
}
