using System;
using System.Collections.Generic;
using System.IO;
using ZeroGraphics.Vector.Geometry;

namespace ZeroGraphics.Vector.Text
{
    public readonly struct FontMetrics
    {
        public ushort UnitsPerEm { get; }
        public short Ascender { get; }
        public short Descender { get; }
        public short LineGap { get; }
        public int LineHeight => Ascender - Descender + LineGap;

        public FontMetrics(ushort unitsPerEm, short ascender, short descender, short lineGap)
        {
            UnitsPerEm = unitsPerEm > 0 ? unitsPerEm : (ushort)2048;
            Ascender = ascender;
            Descender = descender;
            LineGap = lineGap;
        }
    }

    public readonly struct TableEntry
    {
        public int Offset { get; }
        public int Length { get; }
        public TableEntry(int offset, int length)
        {
            Offset = offset;
            Length = length;
        }
    }

    public readonly struct GlyphHorizontalMetric
    {
        public ushort AdvanceWidth { get; }
        public short Lsb { get; }
        public GlyphHorizontalMetric(ushort advanceWidth, short lsb)
        {
            AdvanceWidth = advanceWidth;
            Lsb = lsb;
        }
    }

    internal readonly struct GlyphContourPoint
    {
        public float X { get; }
        public float Y { get; }
        public bool OnCurve { get; }
        public GlyphContourPoint(float x, float y, bool onCurve)
        {
            X = x;
            Y = y;
            OnCurve = onCurve;
        }
    }

    /// <summary>
    /// Pure C# TrueType/OpenType binary font parser and vector glyph outline extractor.
    /// Extracts font glyphs directly into sovereign Path2D vector curves without GDI or DirectWrite.
    /// </summary>
    public sealed class TrueTypeFont
    {
        private readonly byte[] _data;
        private readonly Dictionary<uint, TableEntry> _tables;
        private readonly FontMetrics _metrics;
        private readonly ushort _numGlyphs;
        private readonly short _indexToLocFormat;
        private readonly int[] _glyphOffsets;
        private readonly ushort _numberOfHMetrics;
        private readonly GlyphHorizontalMetric[] _hMetrics;
        private readonly int _cmapSubtableOffset;
        private readonly ushort _cmapFormat;

        public FontMetrics Metrics => _metrics;
        public ushort NumGlyphs => _numGlyphs;

        private TrueTypeFont(
            byte[] data,
            Dictionary<uint, TableEntry> tables,
            FontMetrics metrics,
            ushort numGlyphs,
            short indexToLocFormat,
            int[] glyphOffsets,
            ushort numberOfHMetrics,
            GlyphHorizontalMetric[] hMetrics,
            int cmapSubtableOffset,
            ushort cmapFormat)
        {
            _data = data;
            _tables = tables;
            _metrics = metrics;
            _numGlyphs = numGlyphs;
            _indexToLocFormat = indexToLocFormat;
            _glyphOffsets = glyphOffsets;
            _numberOfHMetrics = numberOfHMetrics;
            _hMetrics = hMetrics;
            _cmapSubtableOffset = cmapSubtableOffset;
            _cmapFormat = cmapFormat;
        }

        public static TrueTypeFont FromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            byte[] bytes = File.ReadAllBytes(filePath);
            return FromBytes(bytes);
        }

        public static TrueTypeFont FromBytes(byte[] fontData)
        {
            if (fontData == null || fontData.Length < 12)
                throw new ArgumentException("Invalid font file data.", nameof(fontData));

            var reader = new TrueTypeReader(fontData);

            // Read Offset Table
            uint sfntVersion = reader.ReadUInt32();
            ushort numTables = reader.ReadUInt16();
            reader.Skip(6); // searchRange, entrySelector, rangeShift

            var tables = new Dictionary<uint, TableEntry>(numTables);
            for (int i = 0; i < numTables; i++)
            {
                uint tag = reader.ReadTag();
                uint checkSum = reader.ReadUInt32();
                int offset = reader.ReadInt32();
                int length = reader.ReadInt32();
                tables[tag] = new TableEntry(offset, length);
            }

            // Parse 'head'
            uint headTag = TrueTypeReader.StringToTag("head");
            if (!tables.TryGetValue(headTag, out var headInfo))
                throw new InvalidDataException("Missing 'head' table in font.");

            reader.Position = headInfo.Offset + 18;
            ushort unitsPerEm = reader.ReadUInt16();
            reader.Position = headInfo.Offset + 50;
            short indexToLocFormat = reader.ReadInt16();

            // Parse 'maxp'
            uint maxpTag = TrueTypeReader.StringToTag("maxp");
            if (!tables.TryGetValue(maxpTag, out var maxpInfo))
                throw new InvalidDataException("Missing 'maxp' table in font.");

            reader.Position = maxpInfo.Offset + 4;
            ushort numGlyphs = reader.ReadUInt16();

            // Parse 'hhea'
            uint hheaTag = TrueTypeReader.StringToTag("hhea");
            short ascender = 0, descender = 0, lineGap = 0;
            ushort numberOfHMetrics = 0;
            if (tables.TryGetValue(hheaTag, out var hheaInfo))
            {
                reader.Position = hheaInfo.Offset + 4;
                ascender = reader.ReadInt16();
                descender = reader.ReadInt16();
                lineGap = reader.ReadInt16();
                reader.Position = hheaInfo.Offset + 34;
                numberOfHMetrics = reader.ReadUInt16();
            }

            var metrics = new FontMetrics(unitsPerEm, ascender, descender, lineGap);

            // Parse 'hmtx'
            var hMetrics = new GlyphHorizontalMetric[numberOfHMetrics];
            uint hmtxTag = TrueTypeReader.StringToTag("hmtx");
            if (tables.TryGetValue(hmtxTag, out var hmtxInfo))
            {
                reader.Position = hmtxInfo.Offset;
                for (int i = 0; i < numberOfHMetrics; i++)
                {
                    ushort adv = reader.ReadUInt16();
                    short lsb = reader.ReadInt16();
                    hMetrics[i] = new GlyphHorizontalMetric(adv, lsb);
                }
            }

            // Parse 'loca'
            uint locaTag = TrueTypeReader.StringToTag("loca");
            uint glyfTag = TrueTypeReader.StringToTag("glyf");
            if (!tables.TryGetValue(locaTag, out var locaInfo) || !tables.TryGetValue(glyfTag, out var glyfInfo))
                throw new InvalidDataException("Missing 'loca' or 'glyf' table in font.");

            reader.Position = locaInfo.Offset;
            var glyphOffsets = new int[numGlyphs + 1];

            if (indexToLocFormat == 0)
            {
                for (int i = 0; i <= numGlyphs; i++)
                {
                    glyphOffsets[i] = glyfInfo.Offset + (reader.ReadUInt16() * 2);
                }
            }
            else
            {
                for (int i = 0; i <= numGlyphs; i++)
                {
                    glyphOffsets[i] = glyfInfo.Offset + reader.ReadInt32();
                }
            }

            // Parse 'cmap'
            uint cmapTag = TrueTypeReader.StringToTag("cmap");
            if (!tables.TryGetValue(cmapTag, out var cmapInfo))
                throw new InvalidDataException("Missing 'cmap' table in font.");

            reader.Position = cmapInfo.Offset;
            reader.ReadUInt16(); // version
            ushort numSubtables = reader.ReadUInt16();

            int selectedSubtableOffset = -1;
            ushort selectedFormat = 0;

            for (int i = 0; i < numSubtables; i++)
            {
                ushort platformID = reader.ReadUInt16();
                ushort encodingID = reader.ReadUInt16();
                int subtableOffset = cmapInfo.Offset + reader.ReadInt32();

                int savePos = reader.Position;
                reader.Position = subtableOffset;
                ushort format = reader.ReadUInt16();
                reader.Position = savePos;

                if (format == 4 && (platformID == 0 || (platformID == 3 && encodingID == 1)))
                {
                    selectedSubtableOffset = subtableOffset;
                    selectedFormat = format;
                    break;
                }
            }

            return new TrueTypeFont(
                fontData, tables, metrics, numGlyphs, indexToLocFormat,
                glyphOffsets, numberOfHMetrics, hMetrics, selectedSubtableOffset, selectedFormat);
        }

        /// <summary>
        /// Maps a character code to its glyph index using the font's cmap table.
        /// </summary>
        public int GetGlyphIndex(char character)
        {
            if (_cmapSubtableOffset < 0 || _cmapFormat != 4) return 0;

            var reader = new TrueTypeReader(_data) { Position = _cmapSubtableOffset };
            reader.ReadUInt16(); // format
            reader.ReadUInt16(); // length
            reader.ReadUInt16(); // language
            ushort segCountX2 = reader.ReadUInt16();
            int segCount = segCountX2 / 2;
            reader.Skip(6); // searchRange, entrySelector, rangeShift

            var endCodes = new ushort[segCount];
            for (int i = 0; i < segCount; i++) endCodes[i] = reader.ReadUInt16();
            reader.ReadUInt16(); // reservedPad

            var startCodes = new ushort[segCount];
            for (int i = 0; i < segCount; i++) startCodes[i] = reader.ReadUInt16();

            var idDeltas = new short[segCount];
            for (int i = 0; i < segCount; i++) idDeltas[i] = reader.ReadInt16();

            int idRangeOffsetBase = reader.Position;
            var idRangeOffsets = new ushort[segCount];
            for (int i = 0; i < segCount; i++) idRangeOffsets[i] = reader.ReadUInt16();

            ushort code = (ushort)character;
            for (int i = 0; i < segCount; i++)
            {
                if (endCodes[i] >= code)
                {
                    if (startCodes[i] <= code)
                    {
                        if (idRangeOffsets[i] == 0)
                        {
                            return (ushort)(code + idDeltas[i]);
                        }
                        else
                        {
                            int rangeOffsetLocation = idRangeOffsetBase + (i * 2);
                            int glyphIndexAddress = rangeOffsetLocation + idRangeOffsets[i] + 2 * (code - startCodes[i]);
                            reader.Position = glyphIndexAddress;
                            ushort glyphIndex = reader.ReadUInt16();
                            if (glyphIndex != 0)
                            {
                                return (ushort)(glyphIndex + idDeltas[i]);
                            }
                            return 0;
                        }
                    }
                    break;
                }
            }

            return 0;
        }

        /// <summary>
        /// Retrieves the horizontal advance width of the specified glyph in font design units.
        /// </summary>
        public float GetGlyphAdvance(int glyphIndex)
        {
            if (_hMetrics == null || _hMetrics.Length == 0) return _metrics.UnitsPerEm * 0.5f;

            if (glyphIndex >= 0 && glyphIndex < _numberOfHMetrics)
            {
                return _hMetrics[glyphIndex].AdvanceWidth;
            }
            if (_numberOfHMetrics > 0)
            {
                return _hMetrics[_numberOfHMetrics - 1].AdvanceWidth;
            }
            return _metrics.UnitsPerEm * 0.5f;
        }

        /// <summary>
        /// Extracts the vector outline for a character directly into a Path2D scaled to fontSize.
        /// </summary>
        public Path2D GetGlyphPath(char character, float fontSize, float x = 0.0f, float y = 0.0f)
        {
            int glyphIndex = GetGlyphIndex(character);
            var path = new Path2D();
            if (glyphIndex < 0 || glyphIndex >= _numGlyphs) return path;

            float scale = fontSize / _metrics.UnitsPerEm;
            RenderGlyphOutline(glyphIndex, scale, x, y, path, depth: 0);
            return path;
        }

        /// <summary>
        /// Generates a composite Path2D containing all letter glyphs laid out horizontally.
        /// </summary>
        public Path2D GetTextPath(string text, float fontSize, float x = 0.0f, float y = 0.0f)
        {
            var compoundPath = new Path2D();
            if (string.IsNullOrEmpty(text)) return compoundPath;

            float scale = fontSize / _metrics.UnitsPerEm;
            float cursorX = x;
            float cursorY = y;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n')
                {
                    cursorX = x;
                    cursorY += _metrics.LineHeight * scale;
                    continue;
                }

                int glyphIdx = GetGlyphIndex(c);
                RenderGlyphOutline(glyphIdx, scale, cursorX, cursorY, compoundPath, depth: 0);
                cursorX += GetGlyphAdvance(glyphIdx) * scale;
            }

            return compoundPath;
        }

        private void RenderGlyphOutline(int glyphIndex, float scale, float originX, float originY, Path2D path, int depth)
        {
            if (depth > 8) return;

            int offset = _glyphOffsets[glyphIndex];
            int nextOffset = _glyphOffsets[glyphIndex + 1];
            if (offset == nextOffset) return; // Empty glyph (e.g. space)

            var reader = new TrueTypeReader(_data) { Position = offset };
            short numberOfContours = reader.ReadInt16();
            reader.Skip(8); // xMin, yMin, xMax, yMax

            if (numberOfContours > 0)
            {
                RenderSimpleGlyph(reader, numberOfContours, scale, originX, originY, path);
            }
            else if (numberOfContours < 0)
            {
                RenderCompositeGlyph(reader, scale, originX, originY, path, depth);
            }
        }

        private void RenderSimpleGlyph(
            TrueTypeReader reader, short numberOfContours, float scale,
            float originX, float originY, Path2D path)
        {
            var endPtsOfContours = new ushort[numberOfContours];
            for (int i = 0; i < numberOfContours; i++)
            {
                endPtsOfContours[i] = reader.ReadUInt16();
            }

            ushort instructionLength = reader.ReadUInt16();
            reader.Skip(instructionLength);

            int pointCount = endPtsOfContours[numberOfContours - 1] + 1;
            if (pointCount <= 0) return;

            // Read Flags
            var flags = new byte[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                byte flag = reader.ReadByte();
                flags[i] = flag;

                if ((flag & 0x08) != 0) // REPEAT_FLAG
                {
                    byte repeatCount = reader.ReadByte();
                    for (int r = 0; r < repeatCount; r++)
                    {
                        i++;
                        if (i < pointCount) flags[i] = flag;
                    }
                }
            }

            // Read X Coordinates
            var xCoords = new short[pointCount];
            short currentX = 0;
            for (int i = 0; i < pointCount; i++)
            {
                byte f = flags[i];
                if ((f & 0x02) != 0) // X_SHORT_VECTOR
                {
                    byte b = reader.ReadByte();
                    currentX += (short)((f & 0x10) != 0 ? b : -b);
                }
                else if ((f & 0x10) == 0) // NEXT_INT16
                {
                    currentX += reader.ReadInt16();
                }
                xCoords[i] = currentX;
            }

            // Read Y Coordinates
            var yCoords = new short[pointCount];
            short currentY = 0;
            for (int i = 0; i < pointCount; i++)
            {
                byte f = flags[i];
                if ((f & 0x04) != 0) // Y_SHORT_VECTOR
                {
                    byte b = reader.ReadByte();
                    currentY += (short)((f & 0x20) != 0 ? b : -b);
                }
                else if ((f & 0x20) == 0) // NEXT_INT16
                {
                    currentY += reader.ReadInt16();
                }
                yCoords[i] = currentY;
            }

            // Convert to Path2D
            int startIdx = 0;
            for (int c = 0; c < numberOfContours; c++)
            {
                int endIdx = endPtsOfContours[c];
                int count = endIdx - startIdx + 1;
                if (count < 2)
                {
                    startIdx = endIdx + 1;
                    continue;
                }

                var pts = new GlyphContourPoint[count];
                for (int i = 0; i < count; i++)
                {
                    int src = startIdx + i;
                    // Flip Y for screen coordinates: yScreen = originY - (Y * scale)
                    float px = originX + xCoords[src] * scale;
                    float py = originY - yCoords[src] * scale;
                    bool onCurve = (flags[src] & 0x01) != 0;
                    pts[i] = new GlyphContourPoint(px, py, onCurve);
                }

                EmitContourToPath(pts, path);
                startIdx = endIdx + 1;
            }
        }

        private static void EmitContourToPath(GlyphContourPoint[] pts, Path2D path)
        {
            int n = pts.Length;
            if (n == 0) return;

            int i;
            if (!pts[0].OnCurve)
            {
                if (pts[n - 1].OnCurve)
                {
                    path.MoveTo(pts[n - 1].X, pts[n - 1].Y);
                    i = 0;
                }
                else
                {
                    // Both pts[0] and pts[n-1] are off-curve:
                    // Virtual on-curve start point is their midpoint.
                    float mx = (pts[n - 1].X + pts[0].X) * 0.5f;
                    float my = (pts[n - 1].Y + pts[0].Y) * 0.5f;
                    path.MoveTo(mx, my);
                    i = 0; // First quadratic control point is pts[0]
                }
            }
            else
            {
                path.MoveTo(pts[0].X, pts[0].Y);
                i = 1;
            }

            int steps = 0;
            while (steps < n)
            {
                var curr = pts[i];
                if (curr.OnCurve)
                {
                    path.LineTo(curr.X, curr.Y);
                    i = (i + 1) % n;
                    steps++;
                }
                else
                {
                    // Quadratic control point
                    var ctrl = curr;
                    int nextIdx = (i + 1) % n;
                    var next = pts[nextIdx];

                    if (next.OnCurve)
                    {
                        path.QuadTo(ctrl.X, ctrl.Y, next.X, next.Y);
                        i = (nextIdx + 1) % n;
                        steps += 2;
                    }
                    else
                    {
                        // Virtual on-curve midpoint between two consecutive off-curve points
                        float midX = (ctrl.X + next.X) * 0.5f;
                        float midY = (ctrl.Y + next.Y) * 0.5f;
                        path.QuadTo(ctrl.X, ctrl.Y, midX, midY);
                        i = nextIdx;
                        steps++;
                    }
                }
            }

            path.Close();
        }

        private void RenderCompositeGlyph(
            TrueTypeReader reader, float scale,
            float originX, float originY, Path2D path, int depth)
        {
            ushort flags;
            do
            {
                flags = reader.ReadUInt16();
                ushort componentGlyphIndex = reader.ReadUInt16();

                float compX = 0f, compY = 0f;
                if ((flags & 0x01) != 0) // ARG_1_AND_2_ARE_WORDS
                {
                    compX = reader.ReadInt16();
                    compY = reader.ReadInt16();
                }
                else
                {
                    compX = reader.ReadSByte();
                    compY = reader.ReadSByte();
                }

                if ((flags & 0x02) == 0) // Not XY values (point matching)
                {
                    compX = compY = 0f;
                }

                float compScale = scale;
                if ((flags & 0x08) != 0) // WE_HAVE_A_SCALE
                {
                    compScale *= reader.ReadF2Dot14();
                }
                else if ((flags & 0x40) != 0) // WE_HAVE_AN_X_AND_Y_SCALE
                {
                    float sx = reader.ReadF2Dot14();
                    float sy = reader.ReadF2Dot14();
                    compScale *= (Math.Abs(sx) + Math.Abs(sy)) * 0.5f;
                }
                else if ((flags & 0x80) != 0) // WE_HAVE_A_TWO_BY_TWO
                {
                    float a = reader.ReadF2Dot14();
                    float b = reader.ReadF2Dot14();
                    float c = reader.ReadF2Dot14();
                    float d = reader.ReadF2Dot14();
                    compScale *= (float)Math.Sqrt(Math.Max(a * d - b * c, 0.0f));
                }

                float subOriginX = originX + compX * scale;
                float subOriginY = originY - compY * scale;

                RenderGlyphOutline(componentGlyphIndex, compScale, subOriginX, subOriginY, path, depth + 1);

            } while ((flags & 0x20) != 0); // MORE_COMPONENTS
        }
    }
}
