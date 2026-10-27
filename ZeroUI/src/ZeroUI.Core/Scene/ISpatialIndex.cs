using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Scene
{
    /// <summary>
    /// Contract for 2D spatial acceleration indexing structures (Grid, QuadTree, R-Tree).
    /// Accelerates viewport frustum culling and mouse pointer hit-testing for large-scale scene graphs.
    /// </summary>
    public interface ISpatialIndex
    {
        /// <summary>
        /// Inserts a node into the spatial partition index.
        /// </summary>
        void Insert(SceneNode node);

        /// <summary>
        /// Removes a node from the spatial partition index.
        /// </summary>
        void Remove(SceneNode node);

        /// <summary>
        /// Updates the spatial boundaries of a node when its transform or dimensions change.
        /// </summary>
        void Update(SceneNode node);

        /// <summary>
        /// Removes all elements from the index.
        /// </summary>
        void Clear();

        /// <summary>
        /// Populates the results list with all nodes intersecting the specified viewport rectangle (frustum culling).
        /// </summary>
        void Query(in SceneRect viewport, List<SceneNode> results);

        /// <summary>
        /// Finds the topmost visible node at the specified world coordinate point.
        /// </summary>
        SceneNode? HitTest(float worldX, float worldY);
    }
}
