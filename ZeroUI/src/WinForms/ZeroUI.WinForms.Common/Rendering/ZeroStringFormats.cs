using System.Drawing;

namespace ZeroUI.WinForms.Rendering
{
    /// <summary>
    /// Pre-allocated, immutable singleton StringFormat instances.
    /// Eliminates unmanaged GDI+ string format handle creation and disposal churn on hot rendering loops.
    /// </summary>
    public static class ZeroStringFormats
    {
        public static readonly StringFormat Center = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        public static readonly StringFormat NearCenter = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center
        };

        public static readonly StringFormat FarCenter = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center
        };

        public static readonly StringFormat CenterNear = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Near
        };

        public static readonly StringFormat CenterFar = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Far
        };

        public static readonly StringFormat NearNear = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near
        };

        public static readonly StringFormat FarFar = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Far
        };

        public static readonly StringFormat FarNear = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Near
        };

        public static readonly StringFormat NearFar = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Far
        };

        public static readonly StringFormat EllipsisNearCenter = new StringFormat
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.LineLimit | StringFormatFlags.NoWrap
        };
    }
}
