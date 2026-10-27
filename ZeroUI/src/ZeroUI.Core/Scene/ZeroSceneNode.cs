using System;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scada.Safety;

namespace ZeroUI.Core.Scene
{
    /// <summary>
    /// Archetype classification for industrial plant mimic scene nodes.
    /// </summary>
    public enum IndustrialNodeType : byte
    {
        Generic = 0,
        Tank = 1,
        Pump = 2,
        Pipe = 3,
        Valve = 4,
        Sensor = 5,
        Motor = 6
    }

    /// <summary>
    /// Standardized industrial scene node with direct TagId telemetry binding,
    /// animated state representation, and spatial index integration.
    /// </summary>
    public class ZeroSceneNode : SceneNode
    {
        public IndustrialNodeType NodeType { get; set; } = IndustrialNodeType.Generic;
        public DeviceStatusFlags StatusFlags { get; set; } = DeviceStatusFlags.None;
        public int BoundTagId => TagId;

        public ZeroSceneNode(string id, string label = "", IndustrialNodeType nodeType = IndustrialNodeType.Generic)
        {
            Id = id ?? Guid.NewGuid().ToString("N");
            Label = label;
            NodeType = nodeType;
        }

        public void BindTag(int tagId, string tagPath = "")
        {
            TagId = tagId;
            if (!string.IsNullOrEmpty(tagPath)) TagPath = tagPath;
        }

        public void UpdateTelemetry(in ScadaValue value)
        {
            Value = value.AsDouble();
            if (value.Quality != ScadaQuality.Good)
            {
                State = ScadaNodeState.Fault;
            }
        }

        public override void Render(object graphicsContext, in RenderContext context)
        {
            // Concrete vector drawing happens in WinForms GDI / Direct2D pipeline
            // Base class provides state, bounds and spatial hit testing
        }

        #region Factory Helpers

        public static ZeroSceneNode CreateTank(string id, string label, float x, float y, float w = 80, float h = 120, int tagId = -1)
        {
            var node = new ZeroSceneNode(id, label, IndustrialNodeType.Tank)
            {
                X = x,
                Y = y,
                Width = w,
                Height = h,
                EngineeringUnit = "%"
            };
            if (tagId >= 0) node.BindTag(tagId);
            return node;
        }

        public static ZeroSceneNode CreatePump(string id, string label, float x, float y, float radius = 24, int tagId = -1)
        {
            var node = new ZeroSceneNode(id, label, IndustrialNodeType.Pump)
            {
                X = x,
                Y = y,
                Width = radius * 2,
                Height = radius * 2,
                EngineeringUnit = "RPM"
            };
            if (tagId >= 0) node.BindTag(tagId);
            return node;
        }

        public static ZeroSceneNode CreateSensor(string id, string label, float x, float y, string unit = "°C", int tagId = -1)
        {
            var node = new ZeroSceneNode(id, label, IndustrialNodeType.Sensor)
            {
                X = x,
                Y = y,
                Width = 70,
                Height = 36,
                EngineeringUnit = unit
            };
            if (tagId >= 0) node.BindTag(tagId);
            return node;
        }

        public static ZeroSceneNode CreateValve(string id, string label, float x, float y, int tagId = -1)
        {
            var node = new ZeroSceneNode(id, label, IndustrialNodeType.Valve)
            {
                X = x,
                Y = y,
                Width = 36,
                Height = 36
            };
            if (tagId >= 0) node.BindTag(tagId);
            return node;
        }

        #endregion
    }
}
