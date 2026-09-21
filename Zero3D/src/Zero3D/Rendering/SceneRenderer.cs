using System;
using System.Collections.Generic;
using Zero3D.Camera;
using Zero3D.Math;
using Zero3D.Mesh;
using Zero3D.Materials;
using Zero3D.Lighting;
using Zero3D.Scene;

namespace Zero3D.Rendering
{
    /// <summary>
    /// Performance and pipeline metrics for a prepared 3D frame.
    /// </summary>
    public struct RenderStatistics
    {
        public int TotalNodes;
        public int RenderedMeshes;
        public int CulledMeshes;
        public int TotalTriangles;
        public int OpaqueCommands;
        public int TransparentCommands;
        public int WireframeCommands;
    }

    /// <summary>
    /// Render pipeline collector for 3D scenes. Performs camera frustum culling,
    /// state sorting (opaque front-to-back, transparent back-to-front), and lighting evaluation.
    /// </summary>
    public class SceneRenderer
    {
        public List<RenderCommand> OpaqueQueue { get; } = new List<RenderCommand>();
        public List<RenderCommand> TransparentQueue { get; } = new List<RenderCommand>();
        public List<RenderCommand> WireframeQueue { get; } = new List<RenderCommand>();

        /// <summary>
        /// Prepares render commands from a Scene3D.
        /// </summary>
        public RenderStatistics PrepareFrame(Scene3D scene, Camera3D? camera = null)
        {
            var cam = camera ?? scene.Camera;
            scene.Update();
            return PrepareFrame(scene.Root, cam);
        }

        /// <summary>
        /// Prepares render commands directly from any root SceneNode hierarchy.
        /// </summary>
        public RenderStatistics PrepareFrame(SceneNode root, Camera3D camera)
        {
            OpaqueQueue.Clear();
            TransparentQueue.Clear();
            WireframeQueue.Clear();

            int totalNodes = 0;
            int culledMeshes = 0;
            int renderedMeshes = 0;
            int totalTriangles = 0;

            TraverseNode(root, camera, ref totalNodes, ref culledMeshes, ref renderedMeshes, ref totalTriangles);

            // Sort opaque front-to-back (minimizes overdraw)
            OpaqueQueue.Sort((a, b) => a.DistanceToCamera.CompareTo(b.DistanceToCamera));

            // Sort transparent back-to-front (correct alpha blending)
            TransparentQueue.Sort((a, b) => b.DistanceToCamera.CompareTo(a.DistanceToCamera));

            return new RenderStatistics
            {
                TotalNodes = totalNodes,
                CulledMeshes = culledMeshes,
                RenderedMeshes = renderedMeshes,
                TotalTriangles = totalTriangles,
                OpaqueCommands = OpaqueQueue.Count,
                TransparentCommands = TransparentQueue.Count,
                WireframeCommands = WireframeQueue.Count
            };
        }

        private void TraverseNode(
            SceneNode node,
            Camera3D camera,
            ref int totalNodes,
            ref int culledMeshes,
            ref int renderedMeshes,
            ref int totalTriangles)
        {
            totalNodes++;

            if (!node.IsVisible) return;

            if (node.Mesh != null && node.Mesh.Vertices.Count > 0)
            {
                var bounds = node.WorldBounds;

                // Frustum Culling
                if (!camera.IsInFrustum(bounds))
                {
                    culledMeshes++;
                }
                else
                {
                    renderedMeshes++;
                    totalTriangles += node.Mesh.TriangleCount;

                    float dist = Vec3.Distance(camera.Position, bounds.Center);
                    var mat = node.Material ?? Material3D.Default;

                    var cmd = new RenderCommand
                    {
                        NodeName = node.Name,
                        Mesh = node.Mesh,
                        WorldTransform = node.WorldTransform,
                        Material = mat,
                        WorldBounds = bounds,
                        DistanceToCamera = dist,
                        CastShadow = node.CastShadow
                    };

                    if (mat.Wireframe)
                    {
                        cmd.Pass = RenderPass.Wireframe;
                        WireframeQueue.Add(cmd);
                    }
                    else if (mat.Alpha < 0.999f)
                    {
                        cmd.Pass = RenderPass.Transparent;
                        TransparentQueue.Add(cmd);
                    }
                    else
                    {
                        cmd.Pass = RenderPass.Opaque;
                        OpaqueQueue.Add(cmd);
                    }
                }
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                TraverseNode(node.Children[i], camera, ref totalNodes, ref culledMeshes, ref renderedMeshes, ref totalTriangles);
            }
        }

        /// <summary>
        /// Evaluates Blinn-Phong lighting shading for a surface point in world coordinates.
        /// </summary>
        public static ColorRgb ComputeBlinnPhongShading(
            Vec3 surfacePos,
            Vec3 normal,
            Vec3 viewPos,
            Material3D material,
            IReadOnlyList<Light3D> lights)
        {
            var n = normal.Normalize();
            var viewDir = (viewPos - surfacePos).Normalize();

            // Base emissive
            var result = material.Emissive;

            for (int i = 0; i < lights.Count; i++)
            {
                var light = lights[i];
                if (!light.IsEnabled) continue;

                if (light.Type == LightType.Ambient)
                {
                    result = result + (material.Albedo * light.Color * light.Intensity);
                    continue;
                }

                Vec3 lightDir;
                float attenuation = 1.0f;

                if (light.Type == LightType.Directional)
                {
                    lightDir = (Vec3.Zero - light.Direction).Normalize();
                }
                else if (light.Type == LightType.Point)
                {
                    var delta = light.Position - surfacePos;
                    float dist = delta.Length();
                    if (dist > light.Range || dist < 1e-4f) continue;

                    lightDir = delta / dist;
                    float normalizedDist = dist / light.Range;
                    attenuation = System.Math.Max(0f, 1.0f - (normalizedDist * normalizedDist));
                }
                else if (light.Type == LightType.Spot)
                {
                    var delta = light.Position - surfacePos;
                    float dist = delta.Length();
                    if (dist > light.Range || dist < 1e-4f) continue;

                    lightDir = delta / dist;
                    float spotCos = Vec3.Dot(Vec3.Zero - lightDir, light.Direction.Normalize());
                    float minCos = (float)System.Math.Cos(light.SpotAngleDegrees * System.Math.PI / 180.0);

                    if (spotCos < minCos) continue;

                    float normalizedDist = dist / light.Range;
                    attenuation = System.Math.Max(0f, 1.0f - (normalizedDist * normalizedDist)) * ((spotCos - minCos) / (1f - minCos));
                }
                else
                {
                    continue;
                }

                // Diffuse (Lambert)
                float nDotL = System.Math.Max(0f, Vec3.Dot(n, lightDir));
                var diffuse = material.Albedo * light.Color * (nDotL * light.Intensity * attenuation);

                // Specular (Blinn-Phong)
                var halfVector = (lightDir + viewDir).Normalize();
                float nDotH = System.Math.Max(0f, Vec3.Dot(n, halfVector));
                float specFactor = (float)System.Math.Pow(nDotH, System.Math.Max(1.0, material.SpecularShininess));
                var specular = light.Color * (specFactor * material.SpecularIntensity * light.Intensity * attenuation);

                result = result + diffuse + specular;
            }

            return result.Clamp();
        }
    }
}
