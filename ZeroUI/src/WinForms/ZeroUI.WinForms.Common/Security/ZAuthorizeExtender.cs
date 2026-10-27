using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using ZeroUI.Core.Security;

namespace ZeroUI.WinForms.Security
{
    public enum UnauthorizedBehavior
    {
        Disable = 0,
        Hide = 1
    }

    /// <summary>
    /// Extender provider that dynamically enables, disables, or hides WinForms controls
    /// based on the active operator's assigned roles or permissions.
    /// </summary>
    [ToolboxItem(true)]
    [ProvideProperty("RequiredRole", typeof(Control))]
    [ProvideProperty("RequiredPermission", typeof(Control))]
    [ProvideProperty("UnauthorizedBehavior", typeof(Control))]
    public class ZAuthorizeExtender : Component, IExtenderProvider
    {
        private readonly Dictionary<Control, string> _requiredRoles = new Dictionary<Control, string>();
        private readonly Dictionary<Control, string> _requiredPermissions = new Dictionary<Control, string>();
        private readonly Dictionary<Control, UnauthorizedBehavior> _behaviors = new Dictionary<Control, UnauthorizedBehavior>();
        private ISessionManager? _session;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ISessionManager Session
        {
            get => _session ?? SessionContext.Current;
            set
            {
                if (!ReferenceEquals(_session, value))
                {
                    UnsubscribeSession();
                    _session = value;
                    SubscribeSession();
                    EvaluateAll();
                }
            }
        }

        public bool CanExtend(object extendee) => extendee is Control;

        [Category("ZeroUI Security")]
        [DefaultValue("")]
        [Description("The role identifier required to access this control (e.g. 'Administrator', 'Engineer').")]
        public string GetRequiredRole(Control control)
        {
            _requiredRoles.TryGetValue(control, out var role);
            return role ?? string.Empty;
        }

        public void SetRequiredRole(Control control, string role)
        {
            if (string.IsNullOrWhiteSpace(role)) _requiredRoles.Remove(control);
            else _requiredRoles[control] = role;
            EvaluateAccess(control);
        }

        [Category("ZeroUI Security")]
        [DefaultValue("")]
        [Description("The permission key required to access this control (e.g. 'Parameters.Write').")]
        public string GetRequiredPermission(Control control)
        {
            _requiredPermissions.TryGetValue(control, out var perm);
            return perm ?? string.Empty;
        }

        public void SetRequiredPermission(Control control, string permission)
        {
            if (string.IsNullOrWhiteSpace(permission)) _requiredPermissions.Remove(control);
            else _requiredPermissions[control] = permission;
            EvaluateAccess(control);
        }

        [Category("ZeroUI Security")]
        [DefaultValue(UnauthorizedBehavior.Disable)]
        [Description("Action taken when current user lacks required authorization (Disable or Hide).")]
        public UnauthorizedBehavior GetUnauthorizedBehavior(Control control)
        {
            _behaviors.TryGetValue(control, out var b);
            return b;
        }

        public void SetUnauthorizedBehavior(Control control, UnauthorizedBehavior behavior)
        {
            _behaviors[control] = behavior;
            EvaluateAccess(control);
        }

        public ZAuthorizeExtender()
        {
            SubscribeSession();
        }

        public ZAuthorizeExtender(IContainer container) : this()
        {
            container?.Add(this);
        }

        private void SubscribeSession()
        {
            var s = Session;
            if (s != null)
            {
                s.UserChanged += OnUserChanged;
                s.SessionLocked += OnSessionLockedOrUnlocked;
                s.SessionUnlocked += OnSessionLockedOrUnlocked;
            }
        }

        private void UnsubscribeSession()
        {
            var s = Session;
            if (s != null)
            {
                s.UserChanged -= OnUserChanged;
                s.SessionLocked -= OnSessionLockedOrUnlocked;
                s.SessionUnlocked -= OnSessionLockedOrUnlocked;
            }
        }

        private void OnUserChanged(object? sender, UserChangedEventArgs e)
        {
            EvaluateAll();
        }

        private void OnSessionLockedOrUnlocked(object? sender, EventArgs e)
        {
            EvaluateAll();
        }

        public void EvaluateAll()
        {
            var keys = new HashSet<Control>(_requiredRoles.Keys);
            keys.UnionWith(_requiredPermissions.Keys);

            foreach (var control in keys)
            {
                EvaluateAccess(control);
            }
        }

        public void EvaluateAccess(Control control)
        {
            if (control == null || control.IsDisposed) return;

            var session = Session;
            bool authorized = true;

            // Check role requirement
            if (_requiredRoles.TryGetValue(control, out var role) && !string.IsNullOrWhiteSpace(role))
            {
                if (!session.HasRole(role)) authorized = false;
            }

            // Check permission requirement
            if (authorized && _requiredPermissions.TryGetValue(control, out var perm) && !string.IsNullOrWhiteSpace(perm))
            {
                if (!session.HasPermission(perm)) authorized = false;
            }

            _behaviors.TryGetValue(control, out var behavior);

            if (behavior == UnauthorizedBehavior.Hide)
            {
                control.Visible = authorized;
            }
            else
            {
                control.Enabled = authorized;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnsubscribeSession();
                _requiredRoles.Clear();
                _requiredPermissions.Clear();
                _behaviors.Clear();
            }
            base.Dispose(disposing);
        }
    }
}
