using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.Core.Security;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Security
{
    /// <summary>
    /// Comprehensive user management console for creating, editing, locking,
    /// assigning RFID badges, and managing operator credentials across industrial runtime stations.
    /// </summary>
    [ToolboxItem(true)]
    public class ZUserManager : ControlBase
    {
        private IUserStore? _userStore;
        private IRoleStore? _roleStore;

        private readonly List<IUser> _users = new List<IUser>();
        private readonly List<IRole> _availableRoles = new List<IRole>();
        private int _selectedIndex = -1;
        private int _rowHeight = 36;
        private int _headerHeight = 44;

        public event EventHandler? SelectedUserChanged;
        public event EventHandler? UserSaved;

        public IUser? SelectedUser => (_selectedIndex >= 0 && _selectedIndex < _users.Count) ? _users[_selectedIndex] : null;

        public ZUserManager()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Size = new Size(800, 450);
        }

        public async Task LoadAsync(IUserStore userStore, IRoleStore? roleStore = null)
        {
            _userStore = userStore ?? throw new ArgumentNullException(nameof(userStore));
            _roleStore = roleStore;

            var users = await _userStore.GetUsersAsync();
            _users.Clear();
            _users.AddRange(users.OrderBy(u => u.Username));

            if (_roleStore != null)
            {
                var roles = await _roleStore.GetRolesAsync();
                _availableRoles.Clear();
                _availableRoles.AddRange(roles);
            }

            _selectedIndex = _users.Count > 0 ? 0 : -1;
            Invalidate();
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

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Y < _headerHeight) return;

            int rowIdx = (e.Y - _headerHeight) / _rowHeight;
            if (rowIdx >= 0 && rowIdx < _users.Count)
            {
                if (_selectedIndex != rowIdx)
                {
                    _selectedIndex = rowIdx;
                    SelectedUserChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var skin = EffectiveSkin;
            bool isDark = skin.IsDark;

            Color backColor = isDark ? Color.FromArgb(20, 24, 33) : Color.FromArgb(255, 255, 255);
            Color headerBack = isDark ? Color.FromArgb(30, 36, 49) : Color.FromArgb(243, 244, 246);
            Color rowAltBack = isDark ? Color.FromArgb(25, 30, 42) : Color.FromArgb(249, 250, 251);
            Color selectBack = isDark ? Color.FromArgb(14, 116, 144) : Color.FromArgb(186, 230, 253);
            Color borderColor = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(229, 231, 235);
            Color textColor = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
            Color subColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);

            g.Clear(backColor);

            // Columns layout
            int colUser = 120;
            int colName = 160;
            int colRoles = 180;
            int colBadge = 130;
            int colStatus = 90;

            using var font = new Font(Font.FontFamily, 9f);
            using var boldFont = new Font(Font.FontFamily, 9f, FontStyle.Bold);
            using var subFont = new Font(Font.FontFamily, 8f);
            using var borderPen = new Pen(borderColor, 1f);
            using var textBrush = new SolidBrush(textColor);
            using var subBrush = new SolidBrush(subColor);

            // Header
            using (var hBrush = new SolidBrush(headerBack))
            {
                g.FillRectangle(hBrush, 0, 0, Width, _headerHeight);
            }
            g.DrawLine(borderPen, 0, _headerHeight, Width, _headerHeight);

            int hx = 12;
            g.DrawString("Username", boldFont, textBrush, hx, 14); hx += colUser;
            g.DrawString("Display Name", boldFont, textBrush, hx, 14); hx += colName;
            g.DrawString("Assigned Roles", boldFont, textBrush, hx, 14); hx += colRoles;
            g.DrawString("RFID Badge", boldFont, textBrush, hx, 14); hx += colBadge;
            g.DrawString("Status", boldFont, textBrush, hx, 14);

            // Rows
            for (int i = 0; i < _users.Count; i++)
            {
                var user = _users[i];
                int y = _headerHeight + i * _rowHeight;
                if (y > Height) break;

                bool isSelected = (i == _selectedIndex);
                Color rowBg = isSelected ? selectBack : (i % 2 == 1 ? rowAltBack : backColor);

                using (var rBrush = new SolidBrush(rowBg))
                {
                    g.FillRectangle(rBrush, 0, y, Width, _rowHeight);
                }

                int rx = 12;
                g.DrawString(user.Username, boldFont, textBrush, rx, y + 9); rx += colUser;
                g.DrawString(user.DisplayName, font, textBrush, rx, y + 9); rx += colName;

                string rolesText = string.Join(", ", user.Roles);
                g.DrawString(rolesText, subFont, subBrush, rx, y + 10); rx += colRoles;

                string badgeText = string.IsNullOrEmpty(user.BadgeId) ? "—" : user.BadgeId!;
                g.DrawString(badgeText, subFont, subBrush, rx, y + 10); rx += colBadge;

                Color statusColor = user.IsLocked ? Color.FromArgb(239, 68, 68) : Color.FromArgb(16, 185, 129);
                string statusText = user.IsLocked ? "Locked" : "Active";
                using (var sBrush = new SolidBrush(statusColor))
                {
                    g.DrawString(statusText, boldFont, sBrush, rx, y + 9);
                }

                g.DrawLine(borderPen, 0, y + _rowHeight, Width, y + _rowHeight);
            }
        }
    }
}
