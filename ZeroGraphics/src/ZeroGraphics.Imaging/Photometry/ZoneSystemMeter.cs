using System;

namespace ZeroGraphics.Imaging.Photometry
{
    /// <summary>
    /// Calibrated BGRA color representation for an Ansel Adams Zone.
    /// </summary>
    public readonly struct ZoneColor
    {
        public readonly byte B;
        public readonly byte G;
        public readonly byte R;
        public readonly byte A;

        public ZoneColor(byte b, byte g, byte r, byte a)
        {
            B = b;
            G = g;
            R = r;
            A = a;
        }
    }

    /// <summary>
    /// Ansel Adams 11-Zone System Exposure Evaluation and Calibration Engine.
    /// Provides calibrated 11-Zone thresholds relative to 18% Middle Gray (Zone V, 0.0 EV).
    /// </summary>
    public static class ZoneSystemMeter
    {
        public const float MiddleGrayLinear = 0.18f;

        /// <summary>
        /// Calibrated false colors for 11 Zones (B, G, R, A).
        /// </summary>
        public static readonly ZoneColor[] ZoneColors =
        {
            new ZoneColor(40, 0, 16, 225),       // Zone 0:  <= -4.5 EV (Pitch Navy / Pure Black)
            new ZoneColor(126, 35, 26, 225),     // Zone I:  -4.0 EV (Deep Indigo)
            new ZoneColor(161, 71, 13, 225),     // Zone II: -3.0 EV (Royal Blue)
            new ZoneColor(143, 131, 0, 225),     // Zone III:-2.0 EV (Teal / Textured Shadow)
            new ZoneColor(50, 125, 46, 225),     // Zone IV: -1.0 EV (Forest Green)
            new ZoneColor(158, 158, 158, 225),   // Zone V:   0.0 EV (Neutral 18% Middle Gray)
            new ZoneColor(53, 216, 253, 225),    // Zone VI: +1.0 EV (Warm Yellow)
            new ZoneColor(0, 140, 251, 225),     // Zone VII:+2.0 EV (Amber Orange)
            new ZoneColor(53, 57, 229, 225),     // Zone VIII:+3.0 EV (Coral Red)
            new ZoneColor(96, 27, 216, 225),     // Zone IX: +4.0 EV (Magenta Pink)
            new ZoneColor(255, 255, 255, 255)    // Zone X:  >= +4.5 EV (Specular Clip White)
        };

        public static readonly ZoneColor[] LuminanceZoneLut = BuildLuminanceZoneLut();

        private static ZoneColor[] BuildLuminanceZoneLut()
        {
            var lut = new ZoneColor[256];
            for (int i = 0; i < 256; i++)
            {
                int zone = GetZoneFromSrgbByte((byte)i);
                lut[i] = ZoneColors[zone];
            }
            return lut;
        }

        /// <summary>
        /// Classifies an 8-bit perceptual sRGB luminance value (0..255) into an Ansel Adams Zone index (0 to 10).
        /// </summary>
        public static int GetZoneFromSrgbByte(byte sY)
        {
            if (sY <= 5) return 0;
            if (sY <= 20) return 1;
            if (sY <= 45) return 2;
            if (sY <= 75) return 3;
            if (sY <= 105) return 4;
            if (sY <= 135) return 5;
            if (sY <= 165) return 6;
            if (sY <= 195) return 7;
            if (sY <= 225) return 8;
            if (sY <= 250) return 9;
            return 10;
        }

        /// <summary>
        /// Maps raw BGRA pixel buffer in-place to false-color Zone System colors.
        /// </summary>
        public static void MapBgraPixelsToZoneMask(Span<byte> bgraPixels)
        {
            var lut = LuminanceZoneLut;
            for (int i = 0; i < bgraPixels.Length; i += 4)
            {
                byte b = bgraPixels[i];
                byte g = bgraPixels[i + 1];
                byte r = bgraPixels[i + 2];

                int lum = (54 * r + 183 * g + 19 * b) >> 8;
                if (lum > 255) lum = 255;

                var zColor = lut[lum];
                bgraPixels[i] = zColor.B;
                bgraPixels[i + 1] = zColor.G;
                bgraPixels[i + 2] = zColor.R;
                bgraPixels[i + 3] = zColor.A;
            }
        }
    }
}
