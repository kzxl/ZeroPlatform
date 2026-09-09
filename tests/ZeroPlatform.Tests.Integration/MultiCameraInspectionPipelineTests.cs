using System;
using System.Collections.Generic;
using System.Drawing;
using Xunit;
using ZeroData.Core;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Contours;
using ZeroGraphics.Vision.Edge;
using ZeroGraphics.Vision.Matching;
using ZeroGraphics.Vision.Stitching;
using ZeroUI.Core.Data;

namespace ZeroPlatform.Tests.Integration
{
    public class MultiCameraInspectionPipelineTests
    {
        [Fact]
        public unsafe void MultiCamera_FullInspectionPipeline_StitchingToMetrologyToUI_EndToEnd()
        {
            // =========================================================================
            // 1. Multi-Camera Image Acquisition Simulation
            // =========================================================================
            // Camera 1 (Left field of view): 120x80 pixels
            // Camera 2 (Right field of view): 120x80 pixels with a 30-pixel horizontal overlap
            int camW = 120;
            int camH = 80;

            using (var cam1 = ImageBuffer.CreateGray8(camW, camH))
            using (var cam2 = ImageBuffer.CreateGray8(camW, camH))
            {
                cam1.Clear(30); // Dark background
                cam2.Clear(30);

                // Feature on global canvas: a rectangular test part from X=20 to X=170, Y=15 to Y=65
                // Part is bright (200) with a dark central hole (30) from X=80 to X=110, Y=30 to Y=50
                // In Camera 1 (X: 0..119):
                // Part occupies [20..119, 15..65]
                // Hole occupies [80..110, 30..50]
                for (int y = 15; y <= 65; y++)
                {
                    byte* row1 = cam1.GetRowPointer(y);
                    for (int x = 20; x < camW; x++)
                    {
                        if (x >= 80 && x <= 110 && y >= 30 && y <= 50)
                            row1[x] = 30; // Hole
                        else
                            row1[x] = 200; // Part body
                    }
                }

                // In Camera 2 (shifted right by +90 pixels, so Camera 2 X' = X - 90):
                // Part occupies [0..80, 15..65] (global 90..170)
                // Hole occupies [0..20, 30..50] (global 80..110 -> Camera 2 X' = -10..20, visible in 0..20)
                for (int y = 15; y <= 65; y++)
                {
                    byte* row2 = cam2.GetRowPointer(y);
                    for (int x = 0; x <= 80; x++)
                    {
                        if (x >= 0 && x <= 20 && y >= 30 && y <= 50)
                            row2[x] = 30; // Hole overlap
                        else
                            row2[x] = 200; // Part body
                    }
                }

                // =========================================================================
                // 2. Multi-Camera Homography Estimation & Panoramic Stitching
                // =========================================================================
                // 4 corresponding fiducial points between Camera 2 and Camera 1 (pure translation +90px)
                var cam2Pts = new PointF[]
                {
                    new PointF(0, 0),
                    new PointF(50, 0),
                    new PointF(50, 50),
                    new PointF(0, 50)
                };

                var cam1Pts = new PointF[]
                {
                    new PointF(90, 0),
                    new PointF(140, 0),
                    new PointF(140, 50),
                    new PointF(90, 50)
                };

                var hCam2ToCam1 = Homography2D.Estimate(cam2Pts, cam1Pts);
                Assert.NotNull(hCam2ToCam1);

                // Stitch Camera 1 and Camera 2 into a seamless composite panoramic image
                using (var stitched = ImageStitcher.StitchPair(cam1, cam2, hCam2ToCam1))
                {
                    // Total composite canvas width should be 120 + 90 = 210 pixels
                    Assert.Equal(210, stitched.Width);
                    Assert.Equal(camH, stitched.Height);

                    Rectangle bbox = Rectangle.Empty;

                    // =========================================================================
                    // 3. Canny Edge Detection & Topological Contour Extraction
                    // =========================================================================
                    using (var edgeMap = CannyEdgeDetector.Detect(stitched, lowThreshold: 30, highThreshold: 80, applyGaussian: false))
                    {
                        Assert.Equal(stitched.Width, edgeMap.Width);
                        Assert.Equal(stitched.Height, edgeMap.Height);

                        // Extract closed polygonal contours via Suzuki-Abe
                        var contours = ContourTracer.FindContours(edgeMap, minPoints: 10);
                        Assert.NotEmpty(contours);

                        // Find the largest outer boundary contour (the test part)
                        Contour? mainPartContour = null;
                        double maxArea = 0.0;

                        foreach (var c in contours)
                        {
                            double area = ContourFeatures.ComputeArea(c.Points);
                            if (area > maxArea)
                            {
                                maxArea = area;
                                mainPartContour = c;
                            }
                        }

                        Assert.NotNull(mainPartContour);
                        Assert.False(mainPartContour!.IsHole);

                        // Part dimensions: (170 - 20) x (65 - 15) = 150 x 50 = 7500 pixels area
                        // Minus hole (110 - 80) x (50 - 30) = 30 x 20 = 600 -> expected area ~ 6900
                        Assert.InRange(maxArea, 6000.0, 8000.0);

                        // Check bounding box
                        bbox = ContourFeatures.ComputeBoundingBox(mainPartContour.Points);
                        Assert.InRange(bbox.Width, 145, 155);
                        Assert.InRange(bbox.Height, 45, 55);

                        // Simplify contour via Ramer-Douglas-Peucker (RDP)
                        var poly = ContourFeatures.ApproximatePolygon(mainPartContour.Points, epsilon: 2.0);
                        Assert.True(poly.Count >= 4 && poly.Count <= 12, "RDP must simplify contour to bounding vertices.");
                    }

                    // =========================================================================
                    // 4. Zernike Moments Sub-Pixel Metrology
                    // =========================================================================
                    // Measure the left boundary edge of the part near X=20, Y=40 with sub-pixel precision
                    bool edgeFound = ZernikeEdgeDetector.RefineEdge(
                        stitched, 20, 40, out var subPixelEdge, minContrast: 30.0, maxDistance: 2.0);

                    Assert.True(edgeFound);
                    Assert.InRange(subPixelEdge.X, 19.0, 21.0); // Sub-pixel precision within 1.0px
                    Assert.True(subPixelEdge.Contrast >= 50.0);

                    // =========================================================================
                    // 5. High-Precision Pattern Localization (NCC Template Matching)
                    // =========================================================================
                    // Create a 20x20 template representing a distinctive corner feature
                    using (var template = ImageBuffer.CreateGray8(20, 20))
                    {
                        template.Clear(30);
                        for (int y = 5; y < 15; y++)
                        {
                            byte* row = template.GetRowPointer(y);
                            for (int x = 5; x < 15; x++) row[x] = 200;
                        }

                        // Match against stitched panorama
                        var match = NccTemplateMatcher.Match(stitched, template, minScore: 0.5);
                        Assert.True(match.IsFound);
                        Assert.True(match.Score >= 0.5);
                    }

                    // =========================================================================
                    // 6. Quality Control Report & Virtualized UI Grid Binding
                    // =========================================================================
                    // Package measurement statistics into a ZeroData DataFrame
                    string[] featureNames = new[] { "Part Width", "Part Height", "Left Edge X", "Hole Clearance" };
                    double[] nominalValues = new[] { 150.0, 50.0, 20.0, 20.0 };
                    double[] measuredValues = new[] { (double)bbox.Width, (double)bbox.Height, subPixelEdge.X, 20.0 };
                    string[] statuses = new[] { "PASS", "PASS", "PASS", "PASS" };

                    var df = new DataFrame(
                        new DataColumn<string>("Feature", featureNames),
                        new DataColumn<double>("Nominal", nominalValues),
                        new DataColumn<double>("Measured", measuredValues),
                        new DataColumn<string>("Status", statuses)
                    );

                    Assert.Equal(4, df.RowCount);
                    Assert.Equal(4, df.ColumnCount);

                    // Bind to ZeroUI Virtual Provider
                    var virtualProvider = new ZeroDataVirtualProvider(df);
                    Assert.Equal(4, virtualProvider.TotalRowCount);
                    Assert.Equal(4, virtualProvider.TotalColumnCount);

                    var cell = new CellValueBuffer();
                    virtualProvider.GetCellValue(0, 0, ref cell);
                    Assert.Equal("Part Width", cell.Text.ToString());

                    virtualProvider.GetCellValue(2, 2, ref cell); // Left Edge sub-pixel measured
                    double parsedEdge = double.Parse(cell.Text.ToString());
                    Assert.InRange(parsedEdge, 19.5, 20.5);

                    virtualProvider.GetCellValue(0, 3, ref cell);
                    Assert.Equal("PASS", cell.Text.ToString());
                }
            }
        }
    }
}
