using System;

namespace ZeroVideo.Transport
{
    /// <summary>
    /// Parsed sequence parameter set (SPS) metadata for H.264 video streams.
    /// </summary>
    public class H264SpsInfo
    {
        public byte ProfileIdc { get; set; }
        public byte ConstraintFlags { get; set; }
        public byte LevelIdc { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int ChromaFormatIdc { get; set; } = 1; // Default 4:2:0
        public bool FrameMbsOnlyFlag { get; set; }
    }

    /// <summary>
    /// Pure C# Exp-Golomb bitstream parser for H.264 AVC Sequence Parameter Set (SPS) NAL units.
    /// Extracts video resolution, profile, and level without external decoding libraries.
    /// </summary>
    public static class H264SpsParser
    {
        /// <summary>
        /// Parses an SPS NAL unit (with or without Annex B start code) into structured video metadata.
        /// </summary>
        public static H264SpsInfo? Parse(byte[] data)
        {
            if (data == null || data.Length < 4) return null;

            int offset = 0;
            // Skip Annex B start codes if present
            if (data.Length >= 4 && data[0] == 0 && data[1] == 0 && data[2] == 0 && data[3] == 1)
            {
                offset = 4;
            }
            else if (data.Length >= 3 && data[0] == 0 && data[1] == 0 && data[2] == 1)
            {
                offset = 3;
            }

            if (offset >= data.Length) return null;

            // Check NAL type
            byte nalHeader = data[offset];
            int nalType = nalHeader & 0x1F;
            if (nalType == 7) // SPS
            {
                offset++;
            }

            // Remove emulation prevention bytes (0x00 0x00 0x03 -> 0x00 0x00)
            byte[] rbsp = RemoveEmulationPreventionBytes(data, offset, data.Length - offset);
            if (rbsp.Length < 3) return null;

            try
            {
                var reader = new BitReader(rbsp);
                var sps = new H264SpsInfo();

                sps.ProfileIdc = (byte)reader.ReadBits(8);
                sps.ConstraintFlags = (byte)reader.ReadBits(8);
                sps.LevelIdc = (byte)reader.ReadBits(8);

                uint spsId = reader.ReadUe();

                int chromaFormatIdc = 1;
                if (sps.ProfileIdc == 100 || sps.ProfileIdc == 110 || sps.ProfileIdc == 122 ||
                    sps.ProfileIdc == 244 || sps.ProfileIdc == 44 || sps.ProfileIdc == 83 ||
                    sps.ProfileIdc == 86 || sps.ProfileIdc == 118 || sps.ProfileIdc == 128 ||
                    sps.ProfileIdc == 138 || sps.ProfileIdc == 139 || sps.ProfileIdc == 134 ||
                    sps.ProfileIdc == 135)
                {
                    chromaFormatIdc = (int)reader.ReadUe();
                    sps.ChromaFormatIdc = chromaFormatIdc;
                    if (chromaFormatIdc == 3)
                    {
                        reader.ReadBits(1); // separate_colour_plane_flag
                    }

                    reader.ReadUe(); // bit_depth_luma_minus8
                    reader.ReadUe(); // bit_depth_chroma_minus8
                    reader.ReadBits(1); // qpprime_y_zero_transform_bypass_flag
                    bool seqScalingMatrixPresent = reader.ReadBits(1) != 0;
                    if (seqScalingMatrixPresent)
                    {
                        int count = (chromaFormatIdc != 3) ? 8 : 12;
                        for (int i = 0; i < count; i++)
                        {
                            bool seqScalingListPresent = reader.ReadBits(1) != 0;
                            if (seqScalingListPresent)
                            {
                                int size = (i < 6) ? 16 : 64;
                                int lastScale = 8;
                                int nextScale = 8;
                                for (int j = 0; j < size; j++)
                                {
                                    if (nextScale != 0)
                                    {
                                        int deltaScale = reader.ReadSe();
                                        nextScale = (lastScale + deltaScale + 256) % 256;
                                    }
                                    lastScale = (nextScale == 0) ? lastScale : nextScale;
                                }
                            }
                        }
                    }
                }

                uint log2MaxFrameNumMinus4 = reader.ReadUe();
                uint picOrderCntType = reader.ReadUe();
                if (picOrderCntType == 0)
                {
                    reader.ReadUe(); // log2_max_pic_order_cnt_lsb_minus4
                }
                else if (picOrderCntType == 1)
                {
                    reader.ReadBits(1); // delta_pic_order_always_zero_flag
                    reader.ReadSe();   // offset_for_non_ref_pic
                    reader.ReadSe();   // offset_for_top_to_bottom_field
                    uint numRefFramesInPicOrderCntCycle = reader.ReadUe();
                    for (int i = 0; i < numRefFramesInPicOrderCntCycle; i++)
                    {
                        reader.ReadSe(); // offset_for_ref_frame[i]
                    }
                }

                reader.ReadUe(); // max_num_ref_frames
                reader.ReadBits(1); // gaps_in_frame_num_value_allowed_flag

                uint picWidthInMbsMinus1 = reader.ReadUe();
                uint picHeightInMapUnitsMinus1 = reader.ReadUe();
                bool frameMbsOnlyFlag = reader.ReadBits(1) != 0;
                sps.FrameMbsOnlyFlag = frameMbsOnlyFlag;

                if (!frameMbsOnlyFlag)
                {
                    reader.ReadBits(1); // mb_adaptive_frame_field_flag
                }

                reader.ReadBits(1); // direct_8x8_inference_flag
                bool frameCroppingFlag = reader.ReadBits(1) != 0;

                uint cropLeft = 0, cropRight = 0, cropTop = 0, cropBottom = 0;
                if (frameCroppingFlag)
                {
                    cropLeft = reader.ReadUe();
                    cropRight = reader.ReadUe();
                    cropTop = reader.ReadUe();
                    cropBottom = reader.ReadUe();
                }

                int width = (int)(picWidthInMbsMinus1 + 1) * 16;
                int height = (2 - (frameMbsOnlyFlag ? 1 : 0)) * (int)(picHeightInMapUnitsMinus1 + 1) * 16;

                if (frameCroppingFlag)
                {
                    int cropUnitX = (chromaFormatIdc == 0) ? 1 : ((chromaFormatIdc == 3) ? 1 : 2);
                    int cropUnitY = (2 - (frameMbsOnlyFlag ? 1 : 0)) * ((chromaFormatIdc == 1) ? 2 : 1);

                    width -= (int)(cropLeft + cropRight) * cropUnitX;
                    height -= (int)(cropTop + cropBottom) * cropUnitY;
                }

                sps.Width = width;
                sps.Height = height;
                return sps;
            }
            catch
            {
                return null;
            }
        }

        private static byte[] RemoveEmulationPreventionBytes(byte[] src, int offset, int length)
        {
            var dst = new byte[length];
            int dstIdx = 0;
            int i = offset;
            int end = offset + length;

            while (i < end)
            {
                if (i + 2 < end && src[i] == 0 && src[i + 1] == 0 && src[i + 2] == 3)
                {
                    dst[dstIdx++] = 0;
                    dst[dstIdx++] = 0;
                    i += 3; // Skip the 0x03 byte
                }
                else
                {
                    dst[dstIdx++] = src[i++];
                }
            }

            if (dstIdx == length) return dst;
            var trimmed = new byte[dstIdx];
            Buffer.BlockCopy(dst, 0, trimmed, 0, dstIdx);
            return trimmed;
        }

        private class BitReader
        {
            private readonly byte[] _data;
            private int _byteOffset;
            private int _bitOffset;

            public BitReader(byte[] data)
            {
                _data = data;
                _byteOffset = 0;
                _bitOffset = 0;
            }

            public uint ReadBits(int count)
            {
                uint result = 0;
                for (int i = 0; i < count; i++)
                {
                    if (_byteOffset >= _data.Length) return result;
                    int bit = (_data[_byteOffset] >> (7 - _bitOffset)) & 1;
                    result = (result << 1) | (uint)bit;
                    _bitOffset++;
                    if (_bitOffset == 8)
                    {
                        _bitOffset = 0;
                        _byteOffset++;
                    }
                }
                return result;
            }

            public uint ReadUe()
            {
                int leadingZeroBits = 0;
                while (_byteOffset < _data.Length && ReadBits(1) == 0)
                {
                    leadingZeroBits++;
                    if (leadingZeroBits > 31) return 0;
                }

                if (leadingZeroBits == 0) return 0;
                uint suffix = ReadBits(leadingZeroBits);
                return (uint)((1 << leadingZeroBits) - 1 + suffix);
            }

            public int ReadSe()
            {
                uint ue = ReadUe();
                if ((ue & 1) == 0)
                {
                    return -(int)(ue >> 1);
                }
                else
                {
                    return (int)((ue + 1) >> 1);
                }
            }
        }
    }
}
