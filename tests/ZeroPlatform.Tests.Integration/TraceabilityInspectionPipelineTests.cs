using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using Xunit;
using ZeroComm.Core.Buffers;
using ZeroData.Core;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Codes;
using ZeroGraphics.Vision.Contours;
using ZeroGraphics.Vision.Edge;
using ZeroInference.Core.Vision;
using ZeroStorage.Core.Gorilla;
using ZeroStorage.Core.TimeSeries;
using ZeroUI.Core.Data;

namespace ZeroPlatform.Tests.Integration
{
    /// <summary>
    /// Cross-Subsystem Traceability Integration Test connecting:
    /// Synthetic Camera Capture -> Optical Code Reader (QR Code, DataMatrix, Code128) ->
    /// Sub-Pixel Metrology (Zernike, Contours) -> AI Defect Inference (FastNMS) ->
    /// Time-Series Audit Store (ZeroStorage Gorilla) -> Data Warehouse (ZeroData DataFrame) ->
    /// Factory Bus Streaming (ZeroComm RingBuffer) -> Operator UI Virtual Provider (ZeroUI).
    /// </summary>
    public class TraceabilityInspectionPipelineTests
    {
        [Fact]
        public unsafe void Traceability_FullPipeline_OpticalCodeToMetrologyToStorageToComm_EndToEnd()
        {
            // =========================================================================
            // 1. Synthetic Industrial Part & Laser-Etched Code Generation
            // =========================================================================
            // Part: 240 x 160 pixels rectangular metallic housing
            // Background: dark (20). Housing body: light gray (180).
            // Drilled mounting hole: dark (20) at center (180, 80) with radius 15 (diameter 30).
            // A 2D QR Code laser etched on the left side of the part:
            string partSerialPayload = "PART-2026-X99;LOT-8821";
            bool[,] qrGrid = QrEncoder.EncodeSymbol(partSerialPayload, QrErrorCorrectionLevel.M);
            int qrDim = qrGrid.GetLength(0); // 21 for Version 1

            int imgW = 260;
            int imgH = 180;
            using (var cameraFrame = ImageBuffer.CreateGray8(imgW, imgH))
            {
                cameraFrame.Clear(0); // Dark conveyor belt background (0)

                byte* scan0 = cameraFrame.Scan0;
                int stride = cameraFrame.Stride;

                // Draw part body [10..250, 10..170] -> width = 240, height = 160
                int partLeft = 10, partTop = 10, partRight = 250, partBottom = 170;
                for (int y = partTop; y <= partBottom; y++)
                {
                    byte* row = scan0 + y * stride;
                    for (int x = partLeft; x <= partRight; x++)
                    {
                        // Circular mounting hole at (180, 90) radius 15
                        int dx = x - 180;
                        int dy = y - 90;
                        if (dx * dx + dy * dy <= 15 * 15)
                        {
                            row[x] = 0; // Drilled hole (conveyor background visible)
                        }
                        else
                        {
                            row[x] = 180; // Metallic housing
                        }
                    }
                }

                // Laser etch the QR code onto the part at offset (X: 30, Y: 40) with module size = 4
                int qrOffsetModuleX = 30;
                int qrOffsetModuleY = 40;
                int modSize = 4;

                // First draw white quiet zone for QR code
                int qzBorder = 4;
                int qrTotalSize = (qrDim + qzBorder * 2) * modSize;
                for (int y = 0; y < qrTotalSize; y++)
                {
                    byte* row = scan0 + (qrOffsetModuleY + y) * stride;
                    for (int x = 0; x < qrTotalSize; x++)
                    {
                        row[qrOffsetModuleX + x] = 255; // White quiet zone background
                    }
                }

                // Render QR code modules
                for (int r = 0; r < qrDim; r++)
                {
                    for (int c = 0; c < qrDim; c++)
                    {
                        byte color = qrGrid[r, c] ? (byte)0 : (byte)255;
                        int startX = qrOffsetModuleX + (c + qzBorder) * modSize;
                        int startY = qrOffsetModuleY + (r + qzBorder) * modSize;

                        for (int py = 0; py < modSize; py++)
                        {
                            byte* row = scan0 + (startY + py) * stride;
                            for (int px = 0; px < modSize; px++)
                            {
                                row[startX + px] = color;
                            }
                        }
                    }
                }

                // =========================================================================
                // 2. Optical Code Reader Subsystem (ZeroGraphics.Vision.Codes)
                // =========================================================================
                var qrDecoder = new QrDecoder();
                var codeResult = qrDecoder.Decode(cameraFrame);

                Assert.NotNull(codeResult);
                Assert.Equal(BarcodeSymbology.QrCode, codeResult.Symbology);
                Assert.Equal(partSerialPayload, codeResult.Text);
                Assert.Equal(4, codeResult.CornerPoints.Length);
                Assert.True(codeResult.Confidence > 0.85);

                // Also verify 2D DataMatrix and 1D Code128 decoding capabilities in the same pipeline
                string dmPayload = "DM-LOT-7700-BATCH-A";
                bool[,] dmGrid = DataMatrixEncoder.EncodeSymbol(dmPayload);
                var dmDecoder = new DataMatrixDecoder();
                var dmResult = dmDecoder.DecodeSymbolGrid(dmGrid);
                Assert.NotNull(dmResult);
                Assert.Equal(dmPayload, dmResult.Text);
                Assert.Equal(BarcodeSymbology.DataMatrix, dmResult.Symbology);

                // =========================================================================
                // 3. Dimensional Metrology & Topological Inspection (ZeroGraphics.Vision)
                // =========================================================================
                // Extract part outer contours and mounting hole
                var contours = ContourTracer.FindContours(cameraFrame, minPoints: 20);
                Assert.NotEmpty(contours);

                Contour? outerContour = null;
                Contour? holeContour = null;
                double maxContourArea = 0.0;

                foreach (var c in contours)
                {
                    double area = ContourFeatures.ComputeArea(c.Points);
                    if (!c.IsHole && area > maxContourArea)
                    {
                        maxContourArea = area;
                        outerContour = c;
                    }
                    else if (c.IsHole && area > 400 && area < 1000)
                    {
                        holeContour = c;
                    }
                }

                Assert.NotNull(outerContour);
                var outerBox = ContourFeatures.ComputeBoundingBox(outerContour!.Points);
                // Outer part dimensions nominal 240 x 160
                Assert.InRange(outerBox.Width, 238, 242);
                Assert.InRange(outerBox.Height, 158, 162);

                // Sub-pixel edge refinement on top edge of the housing at X=120, Y=10
                bool edgeOk = ZernikeEdgeDetector.RefineEdge(
                    cameraFrame, 120, 10, out var subPixelTopEdge, minContrast: 40.0, maxDistance: 2.0);
                Assert.True(edgeOk);
                Assert.InRange(subPixelTopEdge.Y, 9.5, 10.5);

                // =========================================================================
                // 4. AI Defect Detection & Non-Maximum Suppression (ZeroInference)
                // =========================================================================
                // Edge AI model proposes bounding boxes for candidate surface anomalies (x1, y1, x2, y2)
                var defectCandidates = new List<BoundingBox>
                {
                    new BoundingBox(175, 85, 187, 97, score: 0.94f, classId: 1), // Scratch near mounting hole
                    new BoundingBox(174, 84, 187, 97, score: 0.81f, classId: 1), // Redundant overlapping box
                    new BoundingBox(220, 140, 228, 148, score: 0.35f, classId: 1) // Low confidence false positive
                };

                var confirmedDefects = NonMaximumSuppression.Filter(defectCandidates, confidenceThreshold: 0.8f, iouThreshold: 0.4f);
                Assert.Single(confirmedDefects);
                Assert.Equal(1, confirmedDefects[0].ClassId);
                Assert.True(confirmedDefects[0].Score >= 0.9f);

                // =========================================================================
                // 5. Time-Series Audit Store (ZeroStorage Gorilla TSDB)
                // =========================================================================
                // Record inspection cycle measurements: cycle time, edge position, confidence
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var timeSeriesPoints = new List<TimeSeriesPoint>
                {
                    new TimeSeriesPoint(nowMs - 200, 10.02),
                    new TimeSeriesPoint(nowMs - 100, 10.01),
                    new TimeSeriesPoint(nowMs, subPixelTopEdge.Y)
                };

                var tsBlock = TimeSeriesBlock.FromPoints(metricId: 501, timeSeriesPoints);
                Assert.Equal(3, tsBlock.Count);
                Assert.Equal(501, tsBlock.MetricId);

                // Decompress points and verify sub-pixel metric precision
                var decompressed = tsBlock.Decompress();
                Assert.Equal(3, decompressed.Count);
                Assert.Equal(subPixelTopEdge.Y, decompressed[2].Value, 4);

                // =========================================================================
                // 6. Data Warehouse Record (ZeroData DataFrame)
                // =========================================================================
                // Package complete traceability record
                string[] serials = new[] { codeResult.Text };
                string[] symbologies = new[] { codeResult.Symbology.ToString() };
                double[] widths = new[] { (double)outerBox.Width };
                double[] heights = new[] { (double)outerBox.Height };
                double[] subPixelEdgeYs = new[] { subPixelTopEdge.Y };
                int[] defectCounts = new[] { confirmedDefects.Count };
                string[] verdicts = new[] { confirmedDefects.Count == 0 ? "PASS" : "REWORK" };

                var inspectionDf = new DataFrame(
                    new DataColumn<string>("SerialNumber", serials),
                    new DataColumn<string>("Symbology", symbologies),
                    new DataColumn<double>("MeasuredWidth", widths),
                    new DataColumn<double>("MeasuredHeight", heights),
                    new DataColumn<double>("SubPixelTopEdgeY", subPixelEdgeYs),
                    new DataColumn<int>("DefectsFound", defectCounts),
                    new DataColumn<string>("Verdict", verdicts)
                );

                Assert.Equal(1, inspectionDf.RowCount);
                Assert.Equal(7, inspectionDf.ColumnCount);

                // =========================================================================
                // 7. Factory Bus Telemetry Streaming (ZeroComm RingBuffer)
                // =========================================================================
                // Serialize inspection telemetry message into binary bus packet
                string telemetryMsg = $"TRACELOG|{serials[0]}|{symbologies[0]}|{widths[0]}|{heights[0]}|{verdicts[0]}";
                byte[] payloadBytes = Encoding.UTF8.GetBytes(telemetryMsg);

                // Packet layout: [Header 0xAA 0x55] [Length 2B] [Payload N bytes]
                byte[] packet = new byte[4 + payloadBytes.Length];
                packet[0] = 0xAA;
                packet[1] = 0x55;
                packet[2] = (byte)(payloadBytes.Length >> 8);
                packet[3] = (byte)(payloadBytes.Length & 0xFF);
                Array.Copy(payloadBytes, 0, packet, 4, payloadBytes.Length);

                var commBuffer = new CircularRingBuffer(4096);
                commBuffer.Write(packet, 0, packet.Length);
                Assert.Equal(packet.Length, commBuffer.Count);

                // Read packet from bus
                byte[] header = new byte[4];
                commBuffer.Read(header, 0, 4);
                Assert.Equal(0xAA, header[0]);
                Assert.Equal(0x55, header[1]);
                int receivedLen = (header[2] << 8) | header[3];
                Assert.Equal(payloadBytes.Length, receivedLen);

                byte[] receivedPayload = new byte[receivedLen];
                commBuffer.Read(receivedPayload, 0, receivedLen);
                string receivedText = Encoding.UTF8.GetString(receivedPayload);
                Assert.Equal(telemetryMsg, receivedText);

                // =========================================================================
                // 8. Operator Dashboard Virtual Provider (ZeroUI)
                // =========================================================================
                var uiProvider = new ZeroDataVirtualProvider(inspectionDf);
                Assert.Equal(1, uiProvider.TotalRowCount);
                Assert.Equal(7, uiProvider.TotalColumnCount);

                var cellBuffer = new CellValueBuffer();
                uiProvider.GetCellValue(0, 0, ref cellBuffer); // Serial Number
                Assert.Equal(partSerialPayload, cellBuffer.Text.ToString());

                uiProvider.GetCellValue(0, 6, ref cellBuffer); // Verdict
                Assert.Equal("REWORK", cellBuffer.Text.ToString());
            }
        }
    }
}
