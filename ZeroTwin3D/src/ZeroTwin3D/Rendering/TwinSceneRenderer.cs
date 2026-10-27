using System;
using System.Collections.Generic;
using ZeroTwin3D.Camera;
using ZeroTwin3D.Engine;
using ZeroTwin3D.Lighting;
using ZeroTwin3D.Sync;

namespace ZeroTwin3D.Rendering
{
    /// <summary>
    /// Performance and pipeline metrics for a rendered 3D Digital Twin frame.
    /// </summary>
    public struct RenderStatistics
    {
        public int TotalNodes;
        public int RenderedMeshes;
        public int CulledMeshes;
        public int TotalTriangles;
        public int OpaqueCommands;
        public int TransparentCommands;
    }

    /// <summary>
    /// Render pipeline and scene graph collector for 3D Digital Twin scenes.
    /// Handles camera frustum culling, transparent/opaque pass ordering, and Blinn-Phong lighting shading.
    /// </summary>
    public class TwinSceneRenderer
    {
        public List<RenderCommand> OpaqueQueue { get; } = new List<RenderCommand>();
        public List<RenderCommand> TransparentQueue { get; } = new List<RenderCommand>();
        public List<RenderCommand> WireframeQueue { get; } = new List<RenderCommand>();

        /// <summary>
        /// Traverses the scene graph, applies camera frustum culling, and prepares sorted render queues.
        /// </summary>
        public RenderStatistics PrepareFrame(TwinScene scene, Camera3D? camera = null)
        {
            var cam = camera ?? scene.Camera;
            scene.Update();

            OpaqueQueue.Clear();
            TransparentQueue.Clear();
            WireframeQueue.Clear();

            int totalNodes = 0;
            int culledMeshes = 0;
            int renderedMeshes = 0;
            int totalTriangles = 0;

            TraverseNode(scene.Root, cam, ref totalNodes, ref culledMeshes, ref renderedMeshes, ref totalTriangles);

            // Sort opaque front-to-back
            OpaqueQueue.Sort((a, b) => a.DistanceToCamera.CompareTo(b.DistanceToCamera));

            // Sort transparent back-to-front
            TransparentQueue.Sort((a, b) => b.DistanceToCamera.CompareTo(a.DistanceToCamera));

            return new RenderStatistics
            {
                TotalNodes = totalNodes,
                CulledMeshes = culledMeshes,
                RenderedMeshes = renderedMeshes,
                TotalTriangles = totalTriangles,
                OpaqueCommands = OpaqueQueue.Count,
                TransparentCommands = TransparentQueue.Count
            };
        }

        private void TraverseNode(
            TwinNode node,
            Camera3D camera,
            ref int totalNodes,
            ref int culledMeshes,
            ref int renderedMeshes,
            ref int totalTriangles)
        {
            totalNodes++;

            if (!node.IsVisible)
            {
                return;
            }

            if (node.Mesh != null && node.Mesh.Vertices.Count > 0)
            {
                var bounds = node.WorldBounds;

                // View Frustum Culling
                if (!camera.IsInFrustum(bounds))
                {
                    culledMeshes++;
                }
                else
                {
                    renderedMeshes++;
                    totalTriangles += node.Mesh.Indices.Count > 0 ? node.Mesh.Indices.Count / 3 : node.Mesh.Vertices.Count / 3;

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

            foreach (var child in node.Children)
            {
                TraverseNode(child, camera, ref totalNodes, ref culledMeshes, ref renderedMeshes, ref totalTriangles);
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

            // Emissive base color
            var result = material.Emissive;

            foreach (var light in lights)
            {
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
                    attenuation = Math.Max(0f, 1.0f - (normalizedDist * normalizedDist));
                }
                else if (light.Type == LightType.Spot)
                {
                    var delta = light.Position - surfacePos;
                    float dist = delta.Length();
                    if (dist > light.Range || dist < 1e-4f) continue;

                    lightDir = delta / dist;
                    float spotCos = Vec3.Dot(Vec3.Zero - lightDir, light.Direction.Normalize());
                    float minCos = (float)Math.Cos(light.SpotAngleDegrees * Math.PI / 180.0);

                    if (spotCos < minCos) continue;

                    float normalizedDist = dist / light.Range;
                    attenuation = Math.Max(0f, 1.0f - (normalizedDist * normalizedDist)) * ((spotCos - minCos) / (1f - minCos));
                }
                else
                {
                    continue;
                }

                // Diffuse (Lambert)
                float nDotL = Math.Max(0f, Vec3.Dot(n, lightDir));
                var diffuse = material.Albedo * light.Color * (nDotL * light.Intensity * attenuation);

                // Specular (Blinn-Phong)
                var halfVector = (lightDir + viewDir).Normalize();
                float nDotH = Math.Max(0f, Vec3.Dot(n, halfVector));
                float specFactor = (float)Math.Pow(nDotH, Math.Max(1.0, material.SpecularShininess));
                var specular = light.Color * (specFactor * material.SpecularIntensity * light.Intensity * attenuation);

                result = result + diffuse + specular;
            }

            return result.Clamp();
        }
    }
}
