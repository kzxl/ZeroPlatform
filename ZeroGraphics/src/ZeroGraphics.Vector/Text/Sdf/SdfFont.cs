using System;
using System.Collections.Generic;
using ZeroGraphics.Rhi;
using ZeroGraphics.Vector.Geometry;

namespace ZeroGraphics.Vector.Text.Sdf
{
    /// <summary>
    /// High-performance GPU Signed Distance Field (SDF) Font Container.
    /// Maps Unicode characters to pre-baked distance field glyphs packed inside a single texture atlas.
    /// </summary>
    public sealed class SdfFont
    {
        private readonly TrueTypeFont _font;
        private readonly int _glyphPixelSize;
        private readonly int _spread;
        private readonly FontAtlasPacker _packer;
        private readonly Dictionary<char, SdfGlyph> _glyphs;

        /// <summary>
        /// The originating TrueType vector font.
        /// </summary>
        public TrueTypeFont SourceFont => _font;

        /// <summary>
        /// Base pixel size used during atlas baking.
        /// </summary>
        public int GlyphPixelSize => _glyphPixelSize;

        /// <summary>
        /// Distance spread in pixels.
        /// </summary>
        public int Spread => _spread;

        /// <summary>
        /// Atlas width in pixels.
        /// </summary>
        public int AtlasWidth => _packer.Width;

        /// <summary>
        /// Atlas height in pixels.
        /// </summary>
        public int AtlasHeight => _packer.Height;

        /// <summary>
        /// Raw distance field pixel buffer of the atlas.
        /// </summary>
        public byte[] AtlasPixels => _packer.AtlasPixels;

        /// <summary>
        /// Total number of baked glyphs in this font.
        /// </summary>
        public int GlyphCount => _glyphs.Count;

        public SdfFont(
            TrueTypeFont font,
            int glyphPixelSize,
            int spread,
            FontAtlasPacker packer,
            Dictionary<char, SdfGlyph> glyphs)
        {
            _font = font ?? throw new ArgumentNullException(nameof(font));
            _glyphPixelSize = glyphPixelSize;
            _spread = spread;
            _packer = packer ?? throw new ArgumentNullException(nameof(packer));
            _glyphs = glyphs ?? throw new ArgumentNullException(nameof(glyphs));
        }

        /// <summary>
        /// Looks up the SDF glyph metadata for the specified character.
        /// </summary>
        public bool TryGetGlyph(char c, out SdfGlyph? glyph)
        {
            return _glyphs.TryGetValue(c, out glyph);
        }

        /// <summary>
        /// Gets the SDF glyph metadata for the specified character, or null if not found.
        /// </summary>
        public SdfGlyph? GetGlyph(char c)
        {
            _glyphs.TryGetValue(c, out var glyph);
            return glyph;
        }

        /// <summary>
        /// Creates an RHI Texture on the specified device containing the pre-baked SDF atlas.
        /// </summary>
        public IRhiTexture CreateRhiTexture(IRhiDevice device)
        {
            if (device == null) throw new ArgumentNullException(nameof(device));
            var desc = new RhiTextureDesc(
                _packer.Width,
                _packer.Height,
                RhiFormat.R8_UNorm,
                RhiTextureUsage.ShaderResource);

            return device.CreateTexture(desc, _packer.AtlasPixels);
        }

        /// <summary>
        /// Bakes an SdfFont with the specified character set from a TrueTypeFont.
        /// </summary>
        public static SdfFont Create(
            TrueTypeFont font,
            string? characterSet = null,
            int glyphPixelSize = 32,
            int spread = 4,
            int atlasWidth = 512,
            int atlasHeight = 512)
        {
            if (font == null) throw new ArgumentNullException(nameof(font));

            var packer = new FontAtlasPacker(atlasWidth, atlasHeight, padding: 2);
            var glyphs = new Dictionary<char, SdfGlyph>(128);

            // Default printable ASCII range: 0x20 (' ') to 0x7E ('~')
            string chars = characterSet ?? string.Empty;
            if (string.IsNullOrEmpty(chars))
            {
                var sb = new System.Text.StringBuilder(96);
                for (int c = 32; c <= 126; c++) sb.Append((char)c);
                chars = sb.ToString();
            }

            float scale = (float)glyphPixelSize / font.Metrics.UnitsPerEm;

            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (glyphs.ContainsKey(c)) continue;

                int glyphIndex = font.GetGlyphIndex(c);
                float advance = font.GetGlyphAdvance(glyphIndex) * scale;

                if (c == ' ')
                {
                    // Space character has advance width but zero geometry
                    var spaceGlyph = new SdfGlyph(c, glyphIndex, advance, 0, 0, 0, 0)
                    {
                        AtlasX = 0,
                        AtlasY = 0,
                        AtlasWidth = 0,
                        AtlasHeight = 0,
                        U0 = 0, V0 = 0, U1 = 0, V1 = 0
                    };
                    glyphs[c] = spaceGlyph;
                    continue;
                }

                var path = font.GetGlyphPath(c, glyphPixelSize, 0, 0);
                path.GetBounds(out float minX, out float minY, out float maxX, out float maxY);

                float glyphW = maxX - minX;
                float glyphH = maxY - minY;

                if (glyphW <= 0 || glyphH <= 0 || path.VerbCount == 0)
                {
                    // Empty or invisible glyph fallback
                    var emptyGlyph = new SdfGlyph(c, glyphIndex, advance, 0, 0, 0, 0);
                    glyphs[c] = emptyGlyph;
                    continue;
                }

                // Add spread padding around bounds so distance field transitions smoothly outside contour
                float paddedMinX = minX - spread;
                float paddedMinY = minY - spread;
                float paddedMaxX = maxX + spread;
                float paddedMaxY = maxY + spread;

                int bitmapW = (int)Math.Ceiling(paddedMaxX - paddedMinX);
                int bitmapH = (int)Math.Ceiling(paddedMaxY - paddedMinY);
                if (bitmapW < 1) bitmapW = 1;
                if (bitmapH < 1) bitmapH = 1;

                byte[] glyphSdf = SdfGenerator.GenerateSdf(
                    path,
                    bitmapW,
                    bitmapH,
                    paddedMinX,
                    paddedMinY,
                    paddedMaxX,
                    paddedMaxY,
                    spread);

                if (packer.TryPack(glyphSdf, bitmapW, bitmapH, out int px, out int py))
                {
                    float invAtlasW = 1.0f / atlasWidth;
                    float invAtlasH = 1.0f / atlasHeight;

                    // Screen space bearing offsets:
                    // BearingX = paddedMinX (offset from cursor to left edge)
                    // BearingY = paddedMinY (offset from baseline to top edge)
                    var glyph = new SdfGlyph(
                        c,
                        glyphIndex,
                        advance,
                        paddedMinX,
                        paddedMinY,
                        bitmapW,
                        bitmapH)
                    {
                        AtlasX = px,
                        AtlasY = py,
                        AtlasWidth = bitmapW,
                        AtlasHeight = bitmapH,
                        U0 = px * invAtlasW,
                        V0 = py * invAtlasH,
                        U1 = (px + bitmapW) * invAtlasW,
                        V1 = (py + bitmapH) * invAtlasH
                    };

                    glyphs[c] = glyph;
                }
            }

            return new SdfFont(font, glyphPixelSize, spread, packer, glyphs);
        }
    }
}
