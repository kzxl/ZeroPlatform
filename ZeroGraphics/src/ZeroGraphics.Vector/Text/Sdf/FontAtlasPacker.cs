using System;

namespace ZeroGraphics.Vector.Text.Sdf
{
    /// <summary>
    /// 2D Shelf/Row bin packer for font texture atlases.
    /// Combines multiple glyph distance fields into a single compact power-of-two texture.
    /// </summary>
    public sealed class FontAtlasPacker
    {
        private readonly int _width;
        private readonly int _height;
        private readonly int _padding;
        private readonly byte[] _atlasPixels;

        private int _currentX;
        private int _currentY;
        private int _currentRowHeight;

        /// <summary>
        /// Width of the texture atlas in pixels.
        /// </summary>
        public int Width => _width;

        /// <summary>
        /// Height of the texture atlas in pixels.
        /// </summary>
        public int Height => _height;

        /// <summary>
        /// Raw single-channel (R8_UNorm) distance field pixel buffer of the atlas.
        /// </summary>
        public byte[] AtlasPixels => _atlasPixels;

        public FontAtlasPacker(int width = 512, int height = 512, int padding = 2)
        {
            _width = width;
            _height = height;
            _padding = padding;
            _atlasPixels = new byte[width * height];
            _currentX = padding;
            _currentY = padding;
            _currentRowHeight = 0;
        }

        /// <summary>
        /// Attempts to allocate and copy a glyph distance field bitmap into the atlas.
        /// </summary>
        public bool TryPack(byte[] glyphPixels, int glyphWidth, int glyphHeight, out int packedX, out int packedY)
        {
            packedX = 0;
            packedY = 0;
            if (glyphWidth <= 0 || glyphHeight <= 0) return true;

            // Check if fits horizontally on current shelf
            if (_currentX + glyphWidth + _padding > _width)
            {
                // Advance to next shelf
                _currentX = _padding;
                _currentY += _currentRowHeight + _padding;
                _currentRowHeight = 0;
            }

            // Check if fits in texture vertically
            if (_currentY + glyphHeight + _padding > _height)
            {
                return false; // Atlas is full
            }

            packedX = _currentX;
            packedY = _currentY;

            // Copy glyph pixels into atlas buffer
            if (glyphPixels != null && glyphPixels.Length >= glyphWidth * glyphHeight)
            {
                for (int row = 0; row < glyphHeight; row++)
                {
                    int srcOffset = row * glyphWidth;
                    int dstOffset = (packedY + row) * _width + packedX;
                    Buffer.BlockCopy(glyphPixels, srcOffset, _atlasPixels, dstOffset, glyphWidth);
                }
            }

            // Update packer state
            _currentX += glyphWidth + _padding;
            if (glyphHeight > _currentRowHeight)
            {
                _currentRowHeight = glyphHeight;
            }

            return true;
        }
    }
}
