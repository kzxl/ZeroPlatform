using System;

namespace ZeroUI.Core.Editors
{
    public enum NotificationSeverity 
    { 
        Info, 
        Success, 
        Warning, 
        Error 
    }

    /// <summary>
    /// Represents a single notification message.
    /// </summary>
    public class ZNotification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsRead { get; set; }
        public string? ActionText { get; set; }
        public object? Tag { get; set; }
    }

    /// <summary>
    /// Event arguments for when a notification is clicked.
    /// </summary>
    public class NotificationClickedEventArgs : EventArgs
    {
        public ZNotification Notification { get; }

        public NotificationClickedEventArgs(ZNotification notification) 
        { 
            Notification = notification; 
        }
    }

    /// <summary>
    /// Event arguments for when a notification action is clicked.
    /// </summary>
    public class NotificationActionEventArgs : EventArgs
    {
        public ZNotification Notification { get; }
        public string ActionText { get; }

        public NotificationActionEventArgs(ZNotification notification, string actionText)
        {
            Notification = notification;
            ActionText = actionText;
        }
    }
}
