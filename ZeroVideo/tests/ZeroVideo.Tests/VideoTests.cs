using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using ZeroVideo.Color;
using ZeroVideo.Core;
using ZeroVideo.Playback;
using ZeroVideo.Transport;

namespace ZeroVideo.Tests
{
    public class VideoTests
    {
        [Fact]
        public void VideoFrameBuffer_AllocationsAndCloning_WorkCorrectly()
        {
            // 1. RGB24 Frame
            var rgb = new VideoFrameBuffer(640, 480, VideoPixelFormat.Rgb24);
            Assert.Equal(640, rgb.Width);
            Assert.Equal(480, rgb.Height);
            Assert.Equal(640 * 3, rgb.Stride);
            Assert.Equal(640 * 480 * 3, rgb.Data.Length);

            // 2. YUV420P Frame
            var yuv = new VideoFrameBuffer(1920, 1080, VideoPixelFormat.Yuv420p);
            Assert.Equal(1920 * 1080 * 3 / 2, yuv.Data.Length);

            // 3. Clone and timestamp
            rgb.TimestampNs = 1_000_000_000; // 1 second
            rgb.FrameIndex = 42;
            rgb.Data[0] = 255; // Red pixel

            var clone = rgb.Clone();
            Assert.Equal(rgb.TimestampNs, clone.TimestampNs);
            Assert.Equal(rgb.FrameIndex, clone.FrameIndex);
            Assert.Equal(TimeSpan.FromSeconds(1), clone.PresentationTime);
            Assert.Equal(255, clone.Data[0]);

            // Mutate clone to ensure deep copy
            clone.Data[0] = 0;
            Assert.Equal(255, rgb.Data[0]);

            // 4. Luminance
            rgb.Data[0] = 255; rgb.Data[1] = 255; rgb.Data[2] = 255; // White pixel
            Assert.True(rgb.GetLuminance(0, 0) > 0.99f);
        }

        [Fact]
        public void ColorConverter_Yuv420pAndNv12ToRgb_ConvertsAccurately()
        {
            int w = 16, h = 16;
            var yuv = new VideoFrameBuffer(w, h, VideoPixelFormat.Yuv420p);
            var rgb = new VideoFrameBuffer(w, h, VideoPixelFormat.Rgb24);

            // Fill with pure white in YUV: Y=235, U=128, V=128
            int yPlaneSize = w * h;
            for (int i = 0; i < yPlaneSize; i++) yuv.Data[i] = 235;
            for (int i = yPlaneSize; i < yuv.Data.Length; i++) yuv.Data[i] = 128;

            ColorConverter.Yuv420pToRgb(yuv, rgb);

            // Pixel (0,0) in RGB should be approx 235
            Assert.Equal(235, rgb.Data[0]);
            Assert.Equal(235, rgb.Data[1]);
            Assert.Equal(235, rgb.Data[2]);

            // NV12 Test
            var nv12 = new VideoFrameBuffer(w, h, VideoPixelFormat.Nv12);
            for (int i = 0; i < yPlaneSize; i++) nv12.Data[i] = 235;
            for (int i = yPlaneSize; i < nv12.Data.Length; i++) nv12.Data[i] = 128;

            var bgr = new VideoFrameBuffer(w, h, VideoPixelFormat.Bgr24);
            ColorConverter.Nv12ToRgb(nv12, bgr, isBgr: true);

            Assert.Equal(235, bgr.Data[0]); // Blue
            Assert.Equal(235, bgr.Data[1]); // Green
            Assert.Equal(235, bgr.Data[2]); // Red

            // RGB to Gray8
            var gray = new VideoFrameBuffer(w, h, VideoPixelFormat.Gray8);
            ColorConverter.RgbToGray8(rgb, gray);
            Assert.True(gray.Data[0] >= 234 && gray.Data[0] <= 236);
        }

        [Fact]
        public void RtpPacket_ParseAndSerialize_RoundTripsAccurately()
        {
            var packet = new RtpPacket
            {
                Version = 2,
                PayloadType = 96,
                SequenceNumber = 10542,
                Timestamp = 3840294,
                Ssrc = 0xDEADBEEF,
                Marker = true,
                Payload = new byte[] { 0x67, 0x42, 0x00, 0x1F, 0x95, 0xA8 }
            };

            byte[] rawBytes = packet.ToByteArray();
            Assert.NotNull(rawBytes);
            Assert.Equal(12 + packet.Payload.Length, rawBytes.Length);

            bool success = RtpPacket.TryParse(rawBytes, out var parsed);
            Assert.True(success);
            Assert.NotNull(parsed);

            Assert.Equal(packet.Version, parsed.Version);
            Assert.Equal(packet.PayloadType, parsed.PayloadType);
            Assert.Equal(packet.SequenceNumber, parsed.SequenceNumber);
            Assert.Equal(packet.Timestamp, parsed.Timestamp);
            Assert.Equal(packet.Ssrc, parsed.Ssrc);
            Assert.True(parsed.Marker);
            Assert.Equal(packet.Payload, parsed.Payload);
        }

        [Fact]
        public void H264NaluParser_ExtractsAnnexBNalus()
        {
            // Build Annex B stream: 4-byte start code + SPS (0x67), 3-byte start code + IDR (0x65)
            var stream = new List<byte>();

            // SPS
            stream.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x67, 0x42, 0x00, 0x1E });
            // IDR
            stream.AddRange(new byte[] { 0x00, 0x00, 0x01, 0x65, 0x88, 0x84, 0x00, 0xFF });

            var nalus = H264NaluParser.ExtractNalus(stream.ToArray());

            Assert.Equal(2, nalus.Count);
            Assert.Equal(NaluType.Sps, nalus[0].Type);
            Assert.True(nalus[0].IsParameterSet);
            Assert.Equal(3, nalus[0].Payload.Length); // 0x42, 0x00, 0x1E

            Assert.Equal(NaluType.IdrSlice, nalus[1].Type);
            Assert.True(nalus[1].IsKeyFrame);
            Assert.Equal(4, nalus[1].Payload.Length);
        }

        [Fact]
        public void H264FuAReassembler_ReassemblesFragments()
        {
            var reassembler = new H264FuAReassembler();

            // Original NALU to transmit: IDR Slice (0x25 = 001 00101)
            // Payload: 10 bytes of data
            byte[] originalPayload = new byte[10];
            for (int i = 0; i < originalPayload.Length; i++) originalPayload[i] = (byte)(i + 1);

            // Packet 1: Start fragment (S=1, E=0, Type=5)
            // Indicator: F=0, NRI=1 (001 11100 = 0x3C -> FuA)
            byte fuIndicator = (byte)((1 << 5) | 28);
            byte fuHeaderStart = (byte)(0x80 | 5);
            byte[] frag1 = new byte[] { fuIndicator, fuHeaderStart, 1, 2, 3, 4, 5 };

            // Packet 2: End fragment (S=0, E=1, Type=5)
            byte fuHeaderEnd = (byte)(0x40 | 5);
            byte[] frag2 = new byte[] { fuIndicator, fuHeaderEnd, 6, 7, 8, 9, 10 };

            var nalu1 = reassembler.ProcessRtpPayload(frag1);
            Assert.Null(nalu1); // Awaiting end fragment
            Assert.True(reassembler.IsAssembling);

            var nalu2 = reassembler.ProcessRtpPayload(frag2);
            Assert.NotNull(nalu2); // Reassembly complete
            Assert.False(reassembler.IsAssembling);

            Assert.Equal(NaluType.IdrSlice, nalu2.Type);
            Assert.Equal(originalPayload, nalu2.Payload);
        }

        [Fact]
        public void RtspClient_FormatsRequestsAndParsesResponses()
        {
            // 1. Options Request
            string opt = RtspClient.CreateOptionsRequest("rtsp://192.168.1.100:554/live", 1);
            Assert.Contains("OPTIONS rtsp://192.168.1.100:554/live RTSP/1.0", opt);
            Assert.Contains("CSeq: 1", opt);

            // 2. Parse Response
            string resp = "RTSP/1.0 200 OK\r\n" +
                          "CSeq: 1\r\n" +
                          "Session: A8492041\r\n" +
                          "Content-Type: application/sdp\r\n\r\n" +
                          "v=0\r\nm=video 0 RTP/AVP 96\r\na=control:trackID=1\r\n";

            bool ok = RtspClient.TryParseResponse(resp, out int status, out var headers, out string body);
            Assert.True(ok);
            Assert.Equal(200, status);
            Assert.Equal("A8492041", headers["Session"]);
            Assert.Contains("m=video", body);

            // 3. Extract Video Track
            string trackUri = RtspClient.ExtractVideoTrack(body, "rtsp://192.168.1.100:554/live");
            Assert.Equal("rtsp://192.168.1.100:554/live/trackID=1", trackUri);
        }

        [Fact]
        public void VideoClock_And_VideoPlayer_StateAndStepping()
        {
            var player = new VideoPlayer();
            Assert.Equal(PlaybackState.Stopped, player.State);

            // Enqueue 3 frames
            var f1 = new VideoFrameBuffer(100, 100, VideoPixelFormat.Gray8) { PresentationTime = TimeSpan.FromMilliseconds(0) };
            var f2 = new VideoFrameBuffer(100, 100, VideoPixelFormat.Gray8) { PresentationTime = TimeSpan.FromMilliseconds(40) };
            var f3 = new VideoFrameBuffer(100, 100, VideoPixelFormat.Gray8) { PresentationTime = TimeSpan.FromMilliseconds(80) };

            player.EnqueueFrame(f1);
            player.EnqueueFrame(f2);
            player.EnqueueFrame(f3);
            Assert.Equal(3, player.QueueCount);

            // Step frame by frame
            bool step1 = player.StepNextFrame();
            Assert.True(step1);
            Assert.Same(f1, player.CurrentFrame);
            Assert.Equal(2, player.QueueCount);

            // Play & Pause
            player.Play();
            Assert.Equal(PlaybackState.Playing, player.State);
            Assert.True(player.Clock.IsRunning);

            player.Pause();
            Assert.Equal(PlaybackState.Paused, player.State);
            Assert.False(player.Clock.IsRunning);

            player.Seek(TimeSpan.FromSeconds(5));
            Assert.Equal(TimeSpan.FromSeconds(5), player.Clock.Position);

            player.Stop();
            Assert.Equal(PlaybackState.Stopped, player.State);
            Assert.Equal(0, player.QueueCount);
        }

        [Fact]
        public void SnapshotExporter_BmpGeneration_CreatesValidBmpHeaderAndPayload()
        {
            // 1. Test 24-bit BGR export
            var bgrFrame = new VideoFrameBuffer(64, 48, VideoPixelFormat.Bgr24);
            // Fill with a distinct test color
            bgrFrame.Data[0] = 10;  // B
            bgrFrame.Data[1] = 20;  // G
            bgrFrame.Data[2] = 30;  // R

            byte[] bmpBytes = SnapshotExporter.ExportToBmpBytes(bgrFrame);
            Assert.NotNull(bmpBytes);
            Assert.True(bmpBytes.Length > 54);

            // Verify BITMAPFILEHEADER
            Assert.Equal((byte)'B', bmpBytes[0]);
            Assert.Equal((byte)'M', bmpBytes[1]);
            uint reportedFileSize = BitConverter.ToUInt32(bmpBytes, 2);
            Assert.Equal((uint)bmpBytes.Length, reportedFileSize);
            uint dataOffset = BitConverter.ToUInt32(bmpBytes, 10);
            Assert.Equal(54u, dataOffset); // 14 file header + 40 info header

            // Verify BITMAPINFOHEADER
            uint headerSize = BitConverter.ToUInt32(bmpBytes, 14);
            Assert.Equal(40u, headerSize);
            int width = BitConverter.ToInt32(bmpBytes, 18);
            Assert.Equal(64, width);
            int height = BitConverter.ToInt32(bmpBytes, 22);
            Assert.Equal(48, height);
            ushort planes = BitConverter.ToUInt16(bmpBytes, 26);
            Assert.Equal((ushort)1, planes);
            ushort bitCount = BitConverter.ToUInt16(bmpBytes, 28);
            Assert.Equal((ushort)24, bitCount);

            // 2. Test 8-bit Gray8 export with palette
            var grayFrame = new VideoFrameBuffer(32, 24, VideoPixelFormat.Gray8);
            grayFrame.Data[0] = 128;

            byte[] grayBmp = SnapshotExporter.ExportToBmpBytes(grayFrame);
            Assert.Equal((byte)'B', grayBmp[0]);
            Assert.Equal((byte)'M', grayBmp[1]);
            uint grayOffset = BitConverter.ToUInt32(grayBmp, 10);
            Assert.Equal(1078u, grayOffset); // 14 + 40 + (256 * 4) = 1078
            ushort grayBits = BitConverter.ToUInt16(grayBmp, 28);
            Assert.Equal((ushort)8, grayBits);
        }

        [Fact]
        public void MjpegClient_BoundaryParsingAndMarkerExtraction_WorksCorrectly()
        {
            // 1. Boundary Header parsing
            string header = "multipart/x-mixed-replace; boundary=--myboundary";
            string? boundary = MjpegClient.ParseBoundary(header);
            Assert.Equal("--myboundary", boundary);

            string quotedHeader = "multipart/x-mixed-replace; boundary=\"frame_chunk\"";
            Assert.Equal("frame_chunk", MjpegClient.ParseBoundary(quotedHeader));

            // 2. Marker search & multi-frame extraction
            // Construct simulated stream with 2 JPEG frames
            var testStream = new System.IO.MemoryStream();
            byte[] frame1 = new byte[] { 0xFF, 0xD8, 0x11, 0x22, 0x33, 0xFF, 0xD9 };
            byte[] separator = Encoding.ASCII.GetBytes("\r\n--boundary\r\nContent-Type: image/jpeg\r\n\r\n");
            byte[] frame2 = new byte[] { 0xFF, 0xD8, 0x44, 0x55, 0x66, 0x77, 0x88, 0xFF, 0xD9 };

            testStream.Write(separator, 0, separator.Length);
            testStream.Write(frame1, 0, frame1.Length);
            testStream.Write(separator, 0, separator.Length);
            testStream.Write(frame2, 0, frame2.Length);

            byte[] streamBytes = testStream.ToArray();

            // Extract all frames synchronously
            var extracted = MjpegClient.ExtractAllFrames(streamBytes);
            Assert.Equal(2, extracted.Count);
            Assert.Equal(frame1.Length, extracted[0].Length);
            Assert.Equal(frame2.Length, extracted[1].Length);
            Assert.Equal(0xFF, extracted[0][0]);
            Assert.Equal(0xD8, extracted[0][1]);
            Assert.Equal(0xFF, extracted[0][extracted[0].Length - 2]);
            Assert.Equal(0xD9, extracted[0][extracted[0].Length - 1]);

            // 3. Streaming client event dispatch
            using (var client = new MjpegClient())
            {
                var receivedFrames = new List<byte[]>();
                client.FrameReceived += (s, e) =>
                {
                    lock (receivedFrames)
                    {
                        receivedFrames.Add(e.JpegData);
                    }
                };

                testStream.Position = 0;
                client.Start(testStream);

                // Allow background task to process stream
                System.Threading.Thread.Sleep(200);
                client.Stop();

                Assert.Equal(2, receivedFrames.Count);
                Assert.Equal(2, client.TotalFramesReceived);
            }
        }

        [Fact]
        public void VideoFramePool_RentAndRecycle_WorksCorrectly()
        {
            var pool = VideoFramePool.Shared;
            VideoFrameBuffer frame;

            using (frame = pool.Rent(640, 480, VideoPixelFormat.Rgb24, timestampNs: 500_000, frameIndex: 1))
            {
                Assert.False(frame.IsDisposed);
                Assert.Equal(640, frame.Width);
                Assert.Equal(480, frame.Height);
                Assert.Equal(640 * 3, frame.Stride);
                Assert.Equal(500_000, frame.TimestampNs);
                Assert.Equal(1, frame.FrameIndex);

                // Span access
                var span = frame.AsSpan();
                Assert.True(span.Length >= 640 * 480 * 3);
                span[0] = 123;
                Assert.Equal(123, frame.AsSpan()[0]);

                var rowSpan = frame.GetRowSpan(10);
                Assert.Equal(640 * 3, rowSpan.Length);
                rowSpan[0] = 200;
                Assert.Equal(200, frame.AsSpan()[10 * frame.Stride]);
            }

            Assert.True(frame.IsDisposed);

            // Rent again: should succeed and return a valid clean buffer
            using (var frame2 = pool.Rent(320, 240, VideoPixelFormat.Gray8))
            {
                Assert.False(frame2.IsDisposed);
                Assert.Equal(320, frame2.Width);
                Assert.Equal(240, frame2.Height);
            }
        }

        [Fact]
        public void VideoPlayer_BoundedBackpressure_DropsOldestAndNewest()
        {
            var player = new VideoPlayer();
            player.MaxQueueCapacity = 2;

            // 1. DropOldest Strategy
            player.DropStrategy = FrameDropStrategy.DropOldest;
            var pool = VideoFramePool.Shared;

            var f1 = pool.Rent(10, 10, VideoPixelFormat.Gray8, 0, 1);
            var f2 = pool.Rent(10, 10, VideoPixelFormat.Gray8, 0, 2);
            var f3 = pool.Rent(10, 10, VideoPixelFormat.Gray8, 0, 3);

            VideoFrameBuffer? droppedFrame = null;
            player.FrameDropped += (s, e) => droppedFrame = e;

            Assert.True(player.EnqueueFrame(f1));
            Assert.True(player.EnqueueFrame(f2));
            Assert.False(player.EnqueueFrame(f3)); // Capacity reached: f1 dropped

            Assert.Equal(2, player.QueueCount);
            Assert.Equal(1, player.DroppedFramesCount);
            Assert.Same(f1, droppedFrame);
            Assert.True(f1.IsDisposed); // Dropped pooled frame is automatically disposed

            // Queue should now contain f2 and f3
            player.StepNextFrame();
            Assert.Same(f2, player.CurrentFrame);
            player.StepNextFrame();
            Assert.Same(f3, player.CurrentFrame);

            // Clean up
            f2.Dispose();
            f3.Dispose();

            // 2. DropNewest Strategy
            player.Stop();
            player.MaxQueueCapacity = 2;
            player.DropStrategy = FrameDropStrategy.DropNewest;

            var fA = pool.Rent(10, 10, VideoPixelFormat.Gray8, 0, 10);
            var fB = pool.Rent(10, 10, VideoPixelFormat.Gray8, 0, 20);
            var fC = pool.Rent(10, 10, VideoPixelFormat.Gray8, 0, 30);

            Assert.True(player.EnqueueFrame(fA));
            Assert.True(player.EnqueueFrame(fB));
            Assert.False(player.EnqueueFrame(fC)); // Capacity reached: fC rejected

            Assert.Equal(2, player.QueueCount);
            Assert.Equal(2, player.DroppedFramesCount);
            Assert.True(fC.IsDisposed);

            player.Stop();
            Assert.True(fA.IsDisposed);
            Assert.True(fB.IsDisposed);
        }

        [Fact]
        public void H264FuAReassembler_SequenceGapDetection_AbortsCorruptedNALU()
        {
            var reassembler = new H264FuAReassembler();

            byte fuIndicator = (byte)((1 << 5) | 28); // RefIdc=1, Type=FuA
            byte fuStart = (byte)(0x80 | 5);          // Start, Type=IDR
            byte fuMid = (byte)(5);                   // Middle
            byte fuEnd = (byte)(0x40 | 5);            // End

            byte[] chunk1 = new byte[] { fuIndicator, fuStart, 1, 2, 3 };
            byte[] chunk2 = new byte[] { fuIndicator, fuMid, 4, 5, 6 };
            byte[] chunk3 = new byte[] { fuIndicator, fuEnd, 7, 8, 9 };

            // Scenario 1: Missing packet between seq 100 and seq 102
            var nalu1 = reassembler.ProcessRtpPayload(chunk1, 100);
            Assert.Null(nalu1);
            Assert.True(reassembler.IsAssembling);

            // Jump sequence from 100 -> 102 (Packet 101 was lost!)
            var nalu2 = reassembler.ProcessRtpPayload(chunk2, 102);
            Assert.Null(nalu2);
            Assert.False(reassembler.IsAssembling); // Aborted!

            // Subsequent end packet should not return anything
            var nalu3 = reassembler.ProcessRtpPayload(chunk3, 103);
            Assert.Null(nalu3);

            // Scenario 2: Valid continuous sequence
            reassembler.Reset();
            reassembler.ProcessRtpPayload(chunk1, 200);
            reassembler.ProcessRtpPayload(chunk2, 201);
            var completed = reassembler.ProcessRtpPayload(chunk3, 202);

            Assert.NotNull(completed);
            Assert.Equal(NaluType.IdrSlice, completed.Type);
            Assert.Equal(9, completed.Payload.Length);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }, completed.Payload);
        }

        [Fact]
        public void H264SpsParser_ExtractsResolutionAndProfile()
        {
            // 640x480 Baseline SPS test bitstream:
            // Profile: 66 (0x42), Level: 30 (0x1E), Width: 640, Height: 480
            byte[] spsPayload = new byte[] { 0x67, 0x42, 0x00, 0x1E, 0xF4, 0x05, 0x01, 0xED };

            var info = H264SpsParser.Parse(spsPayload);
            Assert.NotNull(info);
            Assert.Equal(66, info.ProfileIdc);
            Assert.Equal(30, info.LevelIdc);
            Assert.Equal(640, info.Width);
            Assert.Equal(480, info.Height);
            Assert.True(info.FrameMbsOnlyFlag);

            // Test with 4-byte start code prefix
            byte[] annexB = new byte[] { 0x00, 0x00, 0x00, 0x01, 0x67, 0x42, 0x00, 0x1E, 0xF4, 0x05, 0x01, 0xED };
            var infoAnnexB = H264SpsParser.Parse(annexB);
            Assert.NotNull(infoAnnexB);
            Assert.Equal(640, infoAnnexB.Width);
            Assert.Equal(480, infoAnnexB.Height);
        }

        [Fact]
        public void ColorConverter_ChromaPairOptimization_OddAndEvenWidths()
        {
            // Test odd dimensions (15x15) to verify both paired and trailing odd pixel logic
            int w = 15, h = 15;
            var yuv = new VideoFrameBuffer(w, h, VideoPixelFormat.Yuv420p);
            var rgb = new VideoFrameBuffer(w, h, VideoPixelFormat.Rgb24);

            int yPlaneSize = yuv.Stride * h;
            for (int i = 0; i < yPlaneSize; i++) yuv.Data[i] = 200;
            for (int i = yPlaneSize; i < yuv.Data.Length; i++) yuv.Data[i] = 128;

            ColorConverter.Yuv420pToRgb(yuv, rgb);

            // Every pixel (including trailing odd pixel at x=14) should be 200
            for (int x = 0; x < w; x++)
            {
                int offset = x * 3;
                Assert.Equal(200, rgb.Data[offset]);
                Assert.Equal(200, rgb.Data[offset + 1]);
                Assert.Equal(200, rgb.Data[offset + 2]);
            }
        }

        [Fact]
        public void ColorConverter_SpanBoundsCheck_ThrowsOnUndersizedBuffers()
        {
            int w = 64, h = 64;
            var normalDst = new VideoFrameBuffer(w, h, VideoPixelFormat.Rgb24);

            // Undersized source data
            var undersizedSrc = new VideoFrameBuffer(w, h, VideoPixelFormat.Yuv420p, new byte[100]);
            Assert.Throws<ArgumentException>(() => ColorConverter.Yuv420pToRgb(undersizedSrc, normalDst));

            var normalSrcYuv = new VideoFrameBuffer(w, h, VideoPixelFormat.Yuv420p);
            var undersizedDst = new VideoFrameBuffer(w, h, VideoPixelFormat.Rgb24, new byte[100]);
            Assert.Throws<ArgumentException>(() => ColorConverter.Yuv420pToRgb(normalSrcYuv, undersizedDst));

            var undersizedNv12 = new VideoFrameBuffer(w, h, VideoPixelFormat.Nv12, new byte[100]);
            Assert.Throws<ArgumentException>(() => ColorConverter.Nv12ToRgb(undersizedNv12, normalDst));

            var normalNv12 = new VideoFrameBuffer(w, h, VideoPixelFormat.Nv12);
            Assert.Throws<ArgumentException>(() => ColorConverter.Nv12ToRgb(normalNv12, undersizedDst));

            var undersizedGray = new VideoFrameBuffer(w, h, VideoPixelFormat.Gray8, new byte[10]);
            Assert.Throws<ArgumentException>(() => ColorConverter.RgbToGray8(normalDst, undersizedGray));
        }

        [Fact]
        public void VideoFramePool_OffHeapAndOversizeFallback_Succeeds()
        {
            // Create off-heap pool with 1MB slab size
            using var pool = new VideoFramePool(FramePoolBackend.OffHeapSlab, slabSize: 1024 * 1024);
            Assert.Equal(FramePoolBackend.OffHeapSlab, pool.Backend);
            Assert.False(pool.IsDisposed);
            Assert.Equal(0, pool.ActiveBlocks);

            // 1. Regular off-heap frame fitting within 1MB slab (e.g. 320x240 RGB24 = 230,400 bytes)
            using (var frame = pool.Rent(320, 240, VideoPixelFormat.Rgb24))
            {
                Assert.IsType<NativeVideoFrameBuffer>(frame);
                Assert.Equal(1, pool.ActiveBlocks);
                Assert.True(pool.HasActiveLeases);
                var span = frame.AsSpan();
                span[0] = 42;
                Assert.Equal(42, span[0]);
            }
            Assert.Equal(0, pool.ActiveBlocks);

            // 2. Oversize frame exceeding 1MB slab capacity (e.g. 1920x1080 RGB24 = 6,220,800 bytes)
            // Must fallback cleanly to unpooled native allocation without throwing ArgumentOutOfRangeException
            using (var oversizeFrame = pool.Rent(1920, 1080, VideoPixelFormat.Rgb24))
            {
                Assert.IsType<NativeVideoFrameBuffer>(oversizeFrame);
                var span = oversizeFrame.AsSpan();
                Assert.True(span.Length >= 1920 * 1080 * 3);
                span[0] = 99;
                Assert.Equal(99, span[0]);
            }
        }

        [Fact]
        public void VideoFramePool_DisposalAndSafeShutdown_WorksCorrectly()
        {
            var pool = new VideoFramePool(FramePoolBackend.OffHeapSlab, slabSize: 512 * 1024);
            Assert.False(pool.IsDisposed);

            // Safe shutdown with no leases
            bool shutdownOk = pool.WaitForShutdown(TimeSpan.FromMilliseconds(50));
            Assert.True(shutdownOk);
            Assert.True(pool.IsDisposed);

            // Double dispose must be idempotent
            pool.Dispose();
            Assert.True(pool.IsDisposed);

            // Rent after dispose must throw ObjectDisposedException
            Assert.Throws<ObjectDisposedException>(() => pool.Rent(100, 100, VideoPixelFormat.Gray8));
        }
    }
}
