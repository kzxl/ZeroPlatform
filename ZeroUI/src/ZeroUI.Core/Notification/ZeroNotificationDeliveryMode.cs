namespace ZeroUI.Core.Notification
{
    /// <summary>
    /// Dictates whether notifications are presented via in-app floating overlays,
    /// native Windows OS Action Center banners, or an automated hybrid approach.
    /// </summary>
    public enum ZeroNotificationDeliveryMode
    {
        /// <summary>
        /// Automatically selects delivery target: in-app toast when the application window
        /// is active and in foreground; Windows native notification when minimized or in background.
        /// </summary>
        Auto,

        /// <summary>
        /// Displays notifications strictly within the application window hierarchy (in-app toast).
        /// </summary>
        InAppOnly,

        /// <summary>
        /// Routes notifications directly to native Windows desktop notifications (Action Center / System Tray).
        /// </summary>
        SystemOnly,

        /// <summary>
        /// Dispatches simultaneously to both in-app toast and native Windows notifications.
        /// Ideal for high-severity industrial alarms.
        /// </summary>
        Dual
    }
}
