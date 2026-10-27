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
    /// Interactive matrix grid control for configuring role-based permissions (Roles vs Permissions)
    /// with category grouping, live toggling, and decoupled store persistence.
    /// </summary>
    [ToolboxItem(true)]
    public class ZRoleMatrix : ControlBase
    {
        private readonly List<RoleModel> _roles = new List<RoleModel>();
        private readonly List<PermissionModel> _permissions = new List<PermissionModel>();
        private readonly Dictionary<string, HashSet<string>> _assignments = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private int _scrollY;
        private int _rowHeight = 32;
        private int _headerHeight = 44;
        private int _permColWidth = 260;
        private int _roleColWidth = 110;
        private bool _isReadOnly;

        public event EventHandler<PermissionToggleEventArgs>? PermissionToggled;

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsReadOnly
        {
            get => _isReadOnly;
            set { _isReadOnly = value; Invalidate(); }
        }

        public IReadOnlyList<IRole> CurrentRoles => _roles;
        public IReadOnlyList<IPermission> CurrentPermissions => _permissions;

        public ZRoleMatrix()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);

            Size = new Size(750, 400);
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

            Invalidate();
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
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int maxScroll = Math.Max(0, _permissions.Count * _rowHeight - (Height - _headerHeight));
            _scrollY = Math.Max(0, Math.Min(maxScroll, _scrollY - e.Delta));
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_isReadOnly || e.Y < _headerHeight) return;

            int rowIdx = (e.Y - _headerHeight + _scrollY) / _rowHeight;
            if (rowIdx < 0 || rowIdx >= _permissions.Count) return;

            var perm = _permissions[rowIdx];
            int clickX = e.X - _permColWidth;
            if (clickX < 0) return;

            int roleIdx = clickX / _roleColWidth;
            if (roleIdx < 0 || roleIdx >= _roles.Count) return;

            var role = _roles[roleIdx];
            bool current = IsGranted(role.Id, perm.Key);
            SetGranted(role.Id, perm.Key, !current);
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
            Color rowAltBack = isDark ? Color.FromArgb(24, 29, 40) : Color.FromArgb(249, 250, 251);
            Color borderColor = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(229, 231, 235);
            Color textColor = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
            Color subColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
            Color checkColor = Color.FromArgb(14, 165, 233);

            g.Clear(backColor);

            using var font = new Font(Font.FontFamily, 9f);
            using var boldFont = new Font(Font.FontFamily, 9f, FontStyle.Bold);
            using var subFont = new Font(Font.FontFamily, 7.5f);
            using var borderPen = new Pen(borderColor, 1f);
            using var textBrush = new SolidBrush(textColor);
            using var subBrush = new SolidBrush(subColor);

            // Paint rows
            int startY = _headerHeight - _scrollY;

            for (int r = 0; r < _permissions.Count; r++)
            {
                int y = startY + r * _rowHeight;
                if (y + _rowHeight < _headerHeight || y > Height) continue;

                var perm = _permissions[r];
                var rowRect = new Rectangle(0, y, Width, _rowHeight);

                // Alternating row background
                if (r % 2 == 1)
                {
                    using var rowBrush = new SolidBrush(rowAltBack);
                    g.FillRectangle(rowBrush, rowRect);
                }

                // Permission Title & Key
                g.DrawString(perm.Name, font, textBrush, 12, y + 4);
                g.DrawString($"{perm.Category} • {perm.Key}", subFont, subBrush, 12, y + 18);

                // Checkbox cells
                for (int c = 0; c < _roles.Count; c++)
                {
                    int x = _permColWidth + c * _roleColWidth;
                    bool granted = IsGranted(_roles[c].Id, perm.Key);

                    int cbSize = 18;
                    var cbRect = new Rectangle(x + (_roleColWidth - cbSize) / 2, y + (_rowHeight - cbSize) / 2, cbSize, cbSize);

                    if (granted)
                    {
                        using var checkBrush = new SolidBrush(checkColor);
                        using var checkPath = CreateRoundedRectanglePath(cbRect, 4);
                        g.FillPath(checkBrush, checkPath);

                        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                        g.DrawString("✓", boldFont, Brushes.White, cbRect, sf);
                    }
                    else
                    {
                        using var cbPen = new Pen(borderColor, 1.5f);
                        using var emptyPath = CreateRoundedRectanglePath(cbRect, 4);
                        g.DrawPath(cbPen, emptyPath);
                    }

                    g.DrawLine(borderPen, x, y, x, y + _rowHeight);
                }

                g.DrawLine(borderPen, 0, y + _rowHeight, Width, y + _rowHeight);
            }

            // Fixed Header Row
            using (var hBrush = new SolidBrush(headerBack))
            {
                g.FillRectangle(hBrush, 0, 0, Width, _headerHeight);
            }
            g.DrawLine(borderPen, 0, _headerHeight, Width, _headerHeight);

            // Header Left: "Permission / Privilege"
            g.DrawString("Permission / Function", boldFont, textBrush, 12, 14);

            // Role Headers
            for (int c = 0; c < _roles.Count; c++)
            {
                int x = _permColWidth + c * _roleColWidth;
                var role = _roles[c];

                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(role.Name, boldFont, textBrush, new Rectangle(x, 4, _roleColWidth, 20), sf);
                g.DrawString($"Level {role.SecurityLevel}", subFont, subBrush, new Rectangle(x, 24, _roleColWidth, 16), sf);

                g.DrawLine(borderPen, x, 0, x, Height);
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
