namespace ZeroAsset.Variants
{
    /// <summary>
    /// Represents an asset node in a master-variant hierarchy.
    /// </summary>
    public interface IVariantNode
    {
        /// <summary>
        /// The unique logical path identifying this asset or virtual copy (e.g. "C:/photos/IMG_01#vc1.jpg").
        /// </summary>
        string LogicalPath { get; }

        /// <summary>
        /// The canonical physical path of the master image on disk (e.g. "C:/photos/IMG_01.jpg").
        /// </summary>
        string MasterPath { get; }

        /// <summary>
        /// 1-based copy index. 0 for master.
        /// </summary>
        int VariantIndex { get; }

        /// <summary>
        /// True if this node is the original master file.
        /// </summary>
        bool IsMaster { get; }
    }

    public static class VariantNodeExtensions
    {
        /// <summary>
        /// Determines if a node is the master asset (VariantIndex == 0).
        /// </summary>
        public static bool CheckIsMaster(this IVariantNode node) => node.VariantIndex == 0;
    }
}
