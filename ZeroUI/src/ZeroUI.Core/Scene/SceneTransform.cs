using System;

namespace ZeroUI.Core.Scene
{
    /// <summary>
    /// Represents 2D affine transformation parameters (position, scale, rotation) for hierarchical scene nodes.
    /// Supports parent-relative coordinate propagation to computed world coordinates.
    /// </summary>
    public sealed class SceneTransform
    {
        private float _x;
        private float _y;
        private float _scaleX = 1.0f;
        private float _scaleY = 1.0f;
        private float _rotationDegrees = 0.0f;

        public event EventHandler? Changed;

        /// <summary>
        /// Local X position relative to parent node.
        /// </summary>
        public float X
        {
            get => _x;
            set
            {
                if (Math.Abs(_x - value) > 1e-5f)
                {
                    _x = value;
                    MarkDirty();
                }
            }
        }

        /// <summary>
        /// Local Y position relative to parent node.
        /// </summary>
        public float Y
        {
            get => _y;
            set
            {
                if (Math.Abs(_y - value) > 1e-5f)
                {
                    _y = value;
                    MarkDirty();
                }
            }
        }

        /// <summary>
        /// Local horizontal scale factor (default: 1.0).
        /// </summary>
        public float ScaleX
        {
            get => _scaleX;
            set
            {
                if (Math.Abs(_scaleX - value) > 1e-5f)
                {
                    _scaleX = value;
                    MarkDirty();
                }
            }
        }

        /// <summary>
        /// Local vertical scale factor (default: 1.0).
        /// </summary>
        public float ScaleY
        {
            get => _scaleY;
            set
            {
                if (Math.Abs(_scaleY - value) > 1e-5f)
                {
                    _scaleY = value;
                    MarkDirty();
                }
            }
        }

        /// <summary>
        /// Local rotation angle in degrees (clockwise).
        /// </summary>
        public float RotationDegrees
        {
            get => _rotationDegrees;
            set
            {
                if (Math.Abs(_rotationDegrees - value) > 1e-5f)
                {
                    _rotationDegrees = value;
                    MarkDirty();
                }
            }
        }

        public SceneTransform(float x = 0f, float y = 0f, float scaleX = 1f, float scaleY = 1f, float rotationDegrees = 0f)
        {
            _x = x;
            _y = y;
            _scaleX = scaleX;
            _scaleY = scaleY;
            _rotationDegrees = rotationDegrees;
        }

        public void SetPosition(float x, float y)
        {
            if (Math.Abs(_x - x) > 1e-5f || Math.Abs(_y - y) > 1e-5f)
            {
                _x = x;
                _y = y;
                MarkDirty();
            }
        }

        public void SetScale(float scaleX, float scaleY)
        {
            if (Math.Abs(_scaleX - scaleX) > 1e-5f || Math.Abs(_scaleY - scaleY) > 1e-5f)
            {
                _scaleX = scaleX;
                _scaleY = scaleY;
                MarkDirty();
            }
        }

        internal void MarkDirty()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
