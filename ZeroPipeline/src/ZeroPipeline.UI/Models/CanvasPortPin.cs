using System;
using System.Drawing;
using ZeroPipeline.Core.Ports;

namespace ZeroPipeline.UI.Models
{
    /// <summary>
    /// Visual pin representing an input or output port on a CanvasNode.
    /// Handles pin hit-testing, position calculation, and type-based coloring.
    /// </summary>
    public sealed class CanvasPortPin
    {
        public string Name { get; }
        public Type DataType { get; }
        public PortDirection Direction { get; }
        public CanvasNode OwnerNode { get; }
        public PointF LocalOffset { get; set; }
        public Color PinColor { get; }

        public const float PinRadius = 6.0f;
        public const float PinHitRadius = 10.0f;

        public CanvasPortPin(
            CanvasNode ownerNode,
            string name,
            Type dataType,
            PortDirection direction,
            PointF localOffset)
        {
            OwnerNode = ownerNode ?? throw new ArgumentNullException(nameof(ownerNode));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DataType = dataType ?? typeof(object);
            Direction = direction;
            LocalOffset = localOffset;
            PinColor = ResolvePinColor(DataType);
        }

        public PointF GetWorldCenter() =>
            new PointF(OwnerNode.X + LocalOffset.X, OwnerNode.Y + LocalOffset.Y);

        public RectangleF GetWorldHitBox()
        {
            var center = GetWorldCenter();
            return new RectangleF(
                center.X - PinHitRadius,
                center.Y - PinHitRadius,
                PinHitRadius * 2f,
                PinHitRadius * 2f);
        }

        public static Color ResolvePinColor(Type type)
        {
            string typeName = type.Name;

            if (type == typeof(byte[]) ||
                typeName.IndexOf("Image", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("Bitmap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("Frame", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.FromArgb(0, 229, 255); // Cyan (Vision / Image)
            }

            if (type == typeof(int) || type == typeof(long) || type == typeof(double) || type == typeof(float))
            {
                return Color.FromArgb(0, 255, 136); // Emerald Green (Numeric)
            }

            if (typeName.IndexOf("Inspection", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("Judge", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.FromArgb(179, 136, 255); // Lavender / Purple (Quality / Inspection)
            }

            if (typeName.IndexOf("Tensor", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.FromArgb(255, 215, 0); // Gold / Yellow (AI Tensor)
            }

            if (type == typeof(string) || typeName.IndexOf("Barcode", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Color.FromArgb(255, 152, 0); // Amber (Barcode / String)
            }

            if (type == typeof(bool))
            {
                return Color.FromArgb(244, 67, 54); // Red / Coral (Boolean)
            }

            return Color.FromArgb(200, 210, 225); // Default Silver / Gray
        }

        public override string ToString() =>
            $"{Direction}Pin '{Name}' ({DataType.Name}) on '{OwnerNode.Name}'";
    }
}
