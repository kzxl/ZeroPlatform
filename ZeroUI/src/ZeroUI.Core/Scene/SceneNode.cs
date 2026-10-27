using System;
using System.Collections.Generic;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Scene
{
    /// <summary>
    /// Abstract base class for all visual components within an industrial scene graph.
    /// Provides hierarchical transforms, spatial bounding calculation, tag binding, and state management.
    /// Implements IScadaDrawable for full compatibility with single-HWND canvas viewports.
    /// </summary>
    public abstract class SceneNode : IScadaDrawable
    {
        private readonly List<SceneNode> _children = new List<SceneNode>();
        private SceneTransform _transform;
        private float _width = 60f;
        private float _height = 60f;
        private int _zIndex = 0;
        private bool _isVisible = true;
        private bool _isSelected = false;
        private bool _isHovered = false;
        private ScadaNodeState _state = ScadaNodeState.Stopped;
        private double _value = 0.0;
        private string _engineeringUnit = string.Empty;
        private SceneRect _cachedWorldBounds = SceneRect.Empty;
        private bool _boundsDirty = true;
        internal uint QueryStamp;

        public event EventHandler? Dirty;

        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Label { get; set; } = string.Empty;

        public SceneTransform Transform
        {
            get => _transform;
            set
            {
                if (_transform != value)
                {
                    if (_transform != null) _transform.Changed -= OnTransformChanged;
                    _transform = value ?? new SceneTransform();
                    _transform.Changed += OnTransformChanged;
                    InvalidateBounds();
                }
            }
        }

        public float X
        {
            get => _transform.X;
            set => _transform.X = value;
        }

        public float Y
        {
            get => _transform.Y;
            set => _transform.Y = value;
        }

        public float Width
        {
            get => _width;
            set
            {
                if (Math.Abs(_width - value) > 1e-5f)
                {
                    _width = Math.Max(1f, value);
                    InvalidateBounds();
                }
            }
        }

        public float Height
        {
            get => _height;
            set
            {
                if (Math.Abs(_height - value) > 1e-5f)
                {
                    _height = Math.Max(1f, value);
                    InvalidateBounds();
                }
            }
        }

        public int ZIndex
        {
            get => _zIndex;
            set
            {
                if (_zIndex != value)
                {
                    _zIndex = value;
                    NotifyDirty();
                }
            }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    NotifyDirty();
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    NotifyDirty();
                }
            }
        }

        public bool IsHovered
        {
            get => _isHovered;
            set
            {
                if (_isHovered != value)
                {
                    _isHovered = value;
                    NotifyDirty();
                }
            }
        }

        public ScadaNodeState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    NotifyDirty();
                }
            }
        }

        public double Value
        {
            get => _value;
            set
            {
                if (Math.Abs(_value - value) > 1e-5)
                {
                    _value = value;
                    OnValueChanged(value);
                    NotifyDirty();
                }
            }
        }

        public string EngineeringUnit
        {
            get => _engineeringUnit;
            set
            {
                _engineeringUnit = value ?? string.Empty;
                NotifyDirty();
            }
        }

        #region Hierarchy

        public SceneNode? Parent { get; internal set; }
        public IReadOnlyList<SceneNode> Children => _children;

        public void AddChild(SceneNode child)
        {
            if (child == null || child == this || _children.Contains(child)) return;
            child.Parent?.RemoveChild(child);
            child.Parent = this;
            _children.Add(child);
            child.InvalidateBounds();
            NotifyDirty();
        }

        public bool RemoveChild(SceneNode child)
        {
            if (child == null) return false;
            if (_children.Remove(child))
            {
                child.Parent = null;
                child.InvalidateBounds();
                NotifyDirty();
                return true;
            }
            return false;
        }

        #endregion

        #region IScadaBindable Implementation

        public string? TagPath { get; set; }
        public int TagId { get; set; } = -1;

        public string? BoundTagPath
        {
            get => TagPath;
            set => TagPath = value;
        }

        public virtual void OnTagValueChanged(IScadaTag tag)
        {
            if (tag == null) return;
            Value = tag.GetValue<double>();
        }

        public virtual void OnTagValueChanged(in ScadaValue scadaValue)
        {
            Value = scadaValue.AsDouble();
        }

        protected virtual void OnValueChanged(double newValue)
        {
        }

        #endregion

        #region Bounds & Spatial

        public SceneRect WorldBounds
        {
            get
            {
                if (_boundsDirty)
                {
                    ComputeWorldBounds();
                }
                return _cachedWorldBounds;
            }
        }

        internal void InvalidateBounds()
        {
            _boundsDirty = true;
            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].InvalidateBounds();
            }
            NotifyDirty();
        }

        private void ComputeWorldBounds()
        {
            float wx = _transform.X;
            float wy = _transform.Y;
            float wsx = _transform.ScaleX;
            float wsy = _transform.ScaleY;

            var curr = Parent;
            while (curr != null)
            {
                wx += curr.Transform.X;
                wy += curr.Transform.Y;
                wsx *= curr.Transform.ScaleX;
                wsy *= curr.Transform.ScaleY;
                curr = curr.Parent;
            }

            _cachedWorldBounds = new SceneRect(wx, wy, _width * wsx, _height * wsy);
            _boundsDirty = false;
        }

        public virtual bool HitTest(float worldX, float worldY)
        {
            if (!IsVisible) return false;
            return WorldBounds.Contains(worldX, worldY);
        }

        #endregion

        #region Lifecycle & Rendering

        protected SceneNode()
        {
            _transform = new SceneTransform();
            _transform.Changed += OnTransformChanged;
        }

        private void OnTransformChanged(object? sender, EventArgs e)
        {
            InvalidateBounds();
        }

        public virtual void UpdateAnimation(long elapsedMs)
        {
            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].UpdateAnimation(elapsedMs);
            }
        }

        /// <summary>
        /// Renders this node onto the provided graphics surface within the given render context.
        /// </summary>
        public abstract void Render(object graphicsContext, in RenderContext context);

        public void NotifyDirty()
        {
            Dirty?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}
