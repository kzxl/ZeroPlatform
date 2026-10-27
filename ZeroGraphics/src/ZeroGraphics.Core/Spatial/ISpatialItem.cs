namespace ZeroGraphics.Core.Spatial
{
    /// <summary>
    /// Represents an entity that has 2D spatial bounds (such as a warehouse rack, pallet, AGV, machine, or sensor node).
    /// </summary>
    public interface ISpatialItem
    {
        BoundingBox2D Bounds { get; }
    }
}
