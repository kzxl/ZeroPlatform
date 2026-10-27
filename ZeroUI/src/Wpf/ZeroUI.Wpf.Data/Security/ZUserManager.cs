using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Comprehensive user management console in WPF for creating, editing, locking,
    /// assigning RFID badges, and managing operator credentials across industrial runtime stations.
    /// </summary>
    public class ZUserManager : WpfControlBase
    {
        private IUserStore? _userStore;
        private IRoleStore? _roleStore;

        private readonly ObservableCollection<IUser> _users = new ObservableCollection<IUser>();
        private readonly List<IRole> _availableRoles = new List<IRole>();
        private readonly ListView _listView = new ListView();

        public event EventHandler? SelectedUserChanged;
        public event EventHandler? UserSaved;

        public IUser? SelectedUser => _listView.SelectedItem as IUser;

        static ZUserManager()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(ZUserManager),
                new FrameworkPropertyMetadata(typeof(ZUserManager)));
        }

        public ZUserManager()
        {
            Width = 800;
            Height = 450;
            Background = new SolidColorBrush(Color.FromRgb(20, 24, 33));

            BuildUI();
            AddVisualChild(_listView);
            AddLogicalChild(_listView);
        }

        protected override int VisualChildrenCount => 1;
        protected override Visual GetVisualChild(int index) => _listView;

        protected override Size MeasureOverride(Size constraint)
        {
            _listView.Measure(constraint);
            return _listView.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _listView.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        private void BuildUI()
        {
            _listView.Background = Brushes.Transparent;
            _listView.BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85));
            _listView.Foreground = Brushes.White;
            _listView.ItemsSource = _users;

            var gridView = new GridView();
            gridView.Columns.Add(new GridViewColumn { Header = "Username", DisplayMemberBinding = new System.Windows.Data.Binding("Username"), Width = 120 });
            gridView.Columns.Add(new GridViewColumn { Header = "Display Name", DisplayMemberBinding = new System.Windows.Data.Binding("DisplayName"), Width = 160 });
            gridView.Columns.Add(new GridViewColumn { Header = "RFID Badge", DisplayMemberBinding = new System.Windows.Data.Binding("BadgeId"), Width = 130 });
            gridView.Columns.Add(new GridViewColumn { Header = "Locked", DisplayMemberBinding = new System.Windows.Data.Binding("IsLocked"), Width = 80 });

            _listView.View = gridView;
            _listView.SelectionChanged += (s, e) => SelectedUserChanged?.Invoke(this, EventArgs.Empty);
        }

        public async Task LoadAsync(IUserStore userStore, IRoleStore? roleStore = null)
        {
            _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
            _roleStore = roleStore;

            var users = await _userStore.GetUsersAsync();
            _users.Clear();
            foreach (var u in users.OrderBy(u => u.Username))
            {
                _users.Add(u);
            }

            if (_roleStore != null)
            {
                var roles = await _roleStore.GetRolesAsync();
                _availableRoles.Clear();
                _availableRoles.AddRange(roles);
            }

            if (_users.Count > 0)
            {
                _listView.SelectedIndex = 0;
            }
        }

        public async Task<bool> AddOrUpdateUserAsync(IUser user, string? password = null, string? pin = null)
        {
            if (_userStore == null || user == null) return false;

            bool ok = await _userStore.SaveUserAsync(user);
            if (ok)
            {
                if (!string.IsNullOrEmpty(password))
                    await _userStore.SetPasswordAsync(user.Id, password);
                if (!string.IsNullOrEmpty(pin))
                    await _userStore.SetPinAsync(user.Id, pin);

                await LoadAsync(_userStore, _roleStore);
                UserSaved?.Invoke(this, EventArgs.Empty);
            }
            return ok;
        }

        public async Task<bool> DeleteSelectedUserAsync()
        {
            if (_userStore == null || SelectedUser == null) return false;

            bool ok = await _userStore.DeleteUserAsync(SelectedUser.Id);
            if (ok)
            {
                await LoadAsync(_userStore, _roleStore);
            }
            return ok;
        }

        public async Task<bool> ToggleSelectedUserLockAsync()
        {
            if (_userStore == null || SelectedUser == null) return false;

            var current = SelectedUser;
            var updated = new UserModel(
                current.Id, current.Username, current.DisplayName, current.Email,
                current.BadgeId, current.PinCodeHash, !current.IsLocked,
                current.LastLoginUtc, current.Roles, current.CustomAttributes);

            return await AddOrUpdateUserAsync(updated);
        }
    }
}
