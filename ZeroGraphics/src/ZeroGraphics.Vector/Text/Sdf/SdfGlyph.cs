using System;

namespace ZeroGraphics.Vector.Text.Sdf
{
    /// <summary>
    /// Holds metrics, physical dimensions, and atlas texture coordinates for a single SDF glyph.
    /// </summary>
    public sealed class SdfGlyph
    {
        /// <summary>
        /// The character represented by this glyph.
        /// </summary>
        public char Character { get; }

        /// <summary>
        /// Glyph index in the originating TrueType font.
        /// </summary>
        public int GlyphIndex { get; }

        /// <summary>
        /// Horizontal advance width in design pixels.
        /// </summary>
        public float AdvanceWidth { get; }

        /// <summary>
        /// Horizontal bearing offset from the cursor position to the left bounding box edge.
        /// </summary>
        public float BearingX { get; }

        /// <summary>
        /// Vertical bearing offset from the baseline to the top bounding box edge.
        /// </summary>
        public float BearingY { get; }

        /// <summary>
        /// Rendered pixel width of the glyph quad.
        /// </summary>
        public float Width { get; }

        /// <summary>
        /// Rendered pixel height of the glyph quad.
        /// </summary>
        public float Height { get; }

        /// <summary>
        /// X coordinate of glyph bitmap in atlas pixels.
        /// </summary>
        public int AtlasX { get; set; }

        /// <summary>
        /// Y coordinate of glyph bitmap in atlas pixels.
        /// </summary>
        public int AtlasY { get; set; }

        /// <summary>
        /// Width of glyph bitmap in atlas pixels.
        /// </summary>
        public int AtlasWidth { get; set; }

        /// <summary>
        /// Height of glyph bitmap in atlas pixels.
        /// </summary>
        public int AtlasHeight { get; set; }

        /// <summary>
        /// Left normalized texture coordinate [0..1].
        /// </summary>
        public float U0 { get; set; }

        /// <summary>
        /// Top normalized texture coordinate [0..1].
        /// </summary>
        public float V0 { get; set; }

        /// <summary>
        /// Right normalized texture coordinate [0..1].
        /// </summary>
        public float U1 { get; set; }

        /// <summary>
        /// Bottom normalized texture coordinate [0..1].
        /// </summary>
        public float V1 { get; set; }

        public SdfGlyph(
            char character,
            int glyphIndex,
            float advanceWidth,
            float bearingX,
            float bearingY,
            float width,
            float height)
        {
            Character = character;
            GlyphIndex = glyphIndex;
            AdvanceWidth = advanceWidth;
            BearingX = bearingX;
            BearingY = bearingY;
            Width = width;
            Height = height;
        }
    }
}
