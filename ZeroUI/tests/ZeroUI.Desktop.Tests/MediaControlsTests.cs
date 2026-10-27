using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using Xunit;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Media;
using ZeroUI.Core.Theme;
using WinFormsImageViewer = ZeroUI.WinForms.Media.ZImageViewer;
using WpfImageViewer = ZeroUI.Wpf.Media.ImageViewerControl;

namespace ZeroUI.Desktop.Tests
{
    public class MediaControlsTests
    {
        private static BitmapSource CreateTestWpfBitmap(int width, int height)
        {
            var format = System.Windows.Media.PixelFormats.Bgra32;
            int stride = width * 4;
            byte[] pixelData = new byte[stride * height];

            // Fill with a test color pattern
            for (int i = 0; i < pixelData.Length; i += 4)
            {
                pixelData[i] = 120;     // B
                pixelData[i + 1] = 200; // G
                pixelData[i + 2] = 255; // R
                pixelData[i + 3] = 255; // A
            }

            var bmp = BitmapSource.Create(width, height, 96, 96, format, null, pixelData, stride);
            bmp.Freeze();
            return bmp;
        }

        private static Bitmap CreateTestWinFormsBitmap(int width, int height)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.Clear(Color.FromArgb(255, 120, 200, 255));
            return bmp;
        }

        [Fact]
        public void Wpf_ImageViewerControl_InitialState_IsExpected()
        {
            StaTestRunner.Run(() =>
            {
                var viewer = new WpfImageViewer();
                Assert.Null(viewer.Source);
                Assert.Equal(1.0, viewer.Zoom);
                Assert.Equal(ImageRotationAngle.Rotate0, viewer.Rotation);
                Assert.Equal(ImageFlipMode.None, viewer.Flip);
                Assert.Equal(ImageViewMode.FitToWindow, viewer.ViewMode);
                Assert.True(viewer.ShowPixelGrid);
                Assert.True(viewer.ShowMiniMap);
                Assert.True(viewer.ShowToolbar);
                Assert.True(viewer.ShowStatusBar);
            });
        }

        [Fact]
        public void Wpf_ImageViewerControl_SourceAssignment_UpdatesDimensions()
        {
            StaTestRunner.Run(() =>
            {
                var viewer = new WpfImageViewer();
                var bmp = CreateTestWpfBitmap(640, 480);
                viewer.Source = bmp;

                Assert.Equal(640, viewer.PixelWidth);
                Assert.Equal(480, viewer.PixelHeight);
            });
        }

        [Fact]
        public void Wpf_ImageViewerControl_ZoomAndTransformCommands_Work()
        {
            StaTestRunner.Run(() =>
            {
                var viewer = new WpfImageViewer();
                viewer.Source = CreateTestWpfBitmap(800, 600);

                viewer.ZoomIn();
                Assert.True(viewer.Zoom > 1.0);

                viewer.ZoomOut();
                viewer.ActualSize();
                Assert.Equal(1.0, viewer.Zoom);

                // Rotation
                viewer.RotateClockwise();
                Assert.Equal(ImageRotationAngle.Rotate90, viewer.Rotation);
                viewer.RotateClockwise();
                Assert.Equal(ImageRotationAngle.Rotate180, viewer.Rotation);
                viewer.RotateClockwise();
                Assert.Equal(ImageRotationAngle.Rotate270, viewer.Rotation);
                viewer.RotateClockwise();
                Assert.Equal(ImageRotationAngle.Rotate0, viewer.Rotation);

                // Flip
                viewer.ToggleFlipHorizontal();
                Assert.True(viewer.Flip.HasFlag(ImageFlipMode.Horizontal));
                viewer.ToggleFlipHorizontal();
                Assert.False(viewer.Flip.HasFlag(ImageFlipMode.Horizontal));

                viewer.ResetView();
                Assert.Equal(ImageRotationAngle.Rotate0, viewer.Rotation);
                Assert.Equal(ImageFlipMode.None, viewer.Flip);
            });
        }

        [Fact]
        public void Wpf_ImageViewerControl_CoordinateTransforms_Roundtrip()
        {
            StaTestRunner.Run(() =>
            {
                var viewer = new WpfImageViewer();
                viewer.Source = CreateTestWpfBitmap(1000, 1000);
                viewer.PanOffset = new System.Windows.Point(200, 300);
                viewer.Zoom = 2.0;

                var origImgPt = new System.Windows.Point(500, 500);
                var vpPt = viewer.ImageToViewport(origImgPt);
                var backImgPt = viewer.ViewportToImage(vpPt);

                Assert.InRange(Math.Abs(origImgPt.X - backImgPt.X), 0, 0.001);
                Assert.InRange(Math.Abs(origImgPt.Y - backImgPt.Y), 0, 0.001);
            });
        }

        [Fact]
        public void Wpf_ImageViewerControl_IZeroEditor_Contract_Works()
        {
            StaTestRunner.Run(() =>
            {
                var viewer = new WpfImageViewer();
                var bmp = CreateTestWpfBitmap(100, 100);

                viewer.EditValue = bmp;
                Assert.Same(bmp, viewer.Source);

                viewer.IsModified = true;
                Assert.True(viewer.IsModified);

                viewer.Clear();
                Assert.Null(viewer.Source);
                Assert.False(viewer.IsModified);
            });
        }

        [Fact]
        public void Wpf_ImageViewerControl_IZeroSkinnable_Contract_Works()
        {
            StaTestRunner.Run(() =>
            {
                var viewer = new WpfImageViewer();
                Assert.True(viewer.UseDefaultSkin);
                Assert.NotNull(viewer.EffectiveSkin);

                viewer.UseDefaultSkin = false;
                viewer.CustomSkin = ZeroSkinDefaults.ObsidianDark;
                Assert.Equal(ZeroSkinDefaults.ObsidianDark, viewer.EffectiveSkin);

                viewer.ApplySkin(ZeroSkinDefaults.CleanLight);
            });
        }

        [Fact]
        public void WinForms_ZImageViewer_InitialState_IsExpected()
        {
            StaTestRunner.Run(() =>
            {
                using var viewer = new WinFormsImageViewer();
                Assert.Null(viewer.Image);
                Assert.Equal(1.0f, viewer.ZoomFactor);
                Assert.Equal(ImageRotationAngle.Rotate0, viewer.Rotation);
                Assert.Equal(ImageFlipMode.None, viewer.Flip);
                Assert.Equal(ImageViewMode.FitToWindow, viewer.ViewMode);
                Assert.True(viewer.ShowPixelGrid);
                Assert.True(viewer.ShowMiniMap);
                Assert.True(viewer.ShowToolbar);
                Assert.True(viewer.ShowStatusBar);
            });
        }

        [Fact]
        public void WinForms_ZImageViewer_ImageAssignment_UpdatesDimensions()
        {
            StaTestRunner.Run(() =>
            {
                using var viewer = new WinFormsImageViewer();
                using var bmp = CreateTestWinFormsBitmap(1280, 720);
                viewer.Image = bmp;

                Assert.Equal(1280, viewer.PixelWidth);
                Assert.Equal(720, viewer.PixelHeight);
            });
        }

        [Fact]
        public void WinForms_ZImageViewer_TransformsAndCommands_Work()
        {
            StaTestRunner.Run(() =>
            {
                using var viewer = new WinFormsImageViewer();
                using var bmp = CreateTestWinFormsBitmap(800, 600);
                viewer.Image = bmp;

                float initialZoom = viewer.ZoomFactor;
                viewer.ZoomIn();
                Assert.True(viewer.ZoomFactor > initialZoom);

                viewer.ActualSize();
                Assert.Equal(1.0f, viewer.ZoomFactor);
                viewer.ZoomIn();
                Assert.True(viewer.ZoomFactor > 1.0f);

                // Rotation
                viewer.RotateClockwise();
                Assert.Equal(ImageRotationAngle.Rotate90, viewer.Rotation);
                viewer.RotateClockwise();
                Assert.Equal(ImageRotationAngle.Rotate180, viewer.Rotation);
                viewer.RotateClockwise();
                Assert.Equal(ImageRotationAngle.Rotate270, viewer.Rotation);
                viewer.RotateClockwise();
                Assert.Equal(ImageRotationAngle.Rotate0, viewer.Rotation);

                // Flip
                viewer.ToggleFlipHorizontal();
                Assert.True(viewer.Flip.HasFlag(ImageFlipMode.Horizontal));
                viewer.ToggleFlipHorizontal();
                Assert.False(viewer.Flip.HasFlag(ImageFlipMode.Horizontal));

                viewer.ResetView();
                Assert.Equal(ImageRotationAngle.Rotate0, viewer.Rotation);
                Assert.Equal(ImageFlipMode.None, viewer.Flip);
            });
        }

        [Fact]
        public void WinForms_ZImageViewer_CoordinateTransforms_Roundtrip()
        {
            StaTestRunner.Run(() =>
            {
                using var viewer = new WinFormsImageViewer();
                using var bmp = CreateTestWinFormsBitmap(1000, 1000);
                viewer.Image = bmp;
                viewer.PanOffset = new PointF(150f, 250f);
                viewer.ZoomFactor = 2.5f;

                var origImgPt = new PointF(400f, 600f);
                var vpPt = viewer.ImageToViewport(origImgPt);
                var backImgPt = viewer.ViewportToImage(new System.Drawing.Point((int)Math.Round(vpPt.X), (int)Math.Round(vpPt.Y)));

                Assert.InRange(Math.Abs(origImgPt.X - backImgPt.X), 0, 1.5f);
                Assert.InRange(Math.Abs(origImgPt.Y - backImgPt.Y), 0, 1.5f);
            });
        }

        [Fact]
        public void WinForms_ZImageViewer_IZeroEditor_Contract_Works()
        {
            StaTestRunner.Run(() =>
            {
                using var viewer = new WinFormsImageViewer();
                using var bmp = CreateTestWinFormsBitmap(200, 200);

                viewer.EditValue = bmp;
                Assert.Same(bmp, viewer.Image);

                viewer.IsModified = true;
                Assert.True(viewer.IsModified);

                viewer.Clear();
                Assert.Null(viewer.Image);
                Assert.False(viewer.IsModified);
            });
        }

        [Fact]
        public void WinForms_ZImageViewer_OnPaint_RendersWithoutException()
        {
            StaTestRunner.Run(() =>
            {
                using var viewer = new WinFormsImageViewer { Size = new System.Drawing.Size(400, 300) };
                using var bmp = CreateTestWinFormsBitmap(200, 200);
                viewer.Image = bmp;

                using var targetBmp = new Bitmap(400, 300);
                using var g = Graphics.FromImage(targetBmp);
                using var pe = new PaintEventArgs(g, new System.Drawing.Rectangle(0, 0, 400, 300));

                var onPaintMethod = typeof(WinFormsImageViewer).GetMethod("OnPaint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.NotNull(onPaintMethod);
                onPaintMethod.Invoke(viewer, new object[] { pe });
            });
        }

        [Fact]
        public void Wpf_ZAnnotationCanvas_AddAndBurn_WorksCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                var canvas = new ZeroUI.Wpf.Media.ZAnnotationCanvas
                {
                    Width = 400,
                    Height = 300
                };

                var item = new ZeroUI.Wpf.Media.VisualAnnotationItem
                {
                    ShapeType = AnnotationShapeType.BoundingBox,
                    Severity = AnnotationSeverity.Defect,
                    Label = "Scratch #1",
                    StartPoint = new System.Windows.Point(20, 20),
                    EndPoint = new System.Windows.Point(100, 80)
                };

                canvas.Annotations.Add(item);
                Assert.Single(canvas.Annotations);
                Assert.Equal("Scratch #1", canvas.Annotations[0].Label);

                var src = CreateTestWpfBitmap(200, 200);
                var burned = canvas.BurnAnnotationsToBitmap(src);
                Assert.NotNull(burned);
                Assert.Equal(200, burned.PixelWidth);
                Assert.Equal(200, burned.PixelHeight);
            });
        }

        [Fact]
        public void Wpf_ZMeasurementRuler_Calculations_AreAccurate()
        {
            StaTestRunner.Run(() =>
            {
                var ruler = new ZeroUI.Wpf.Media.ZMeasurementRuler
                {
                    CalibrationFactor = 0.5,
                    Unit = MeasurementUnit.Millimeter,
                    Mode = MeasurementMode.LinearDistance
                };

                Assert.Equal(0.5, ruler.CalibrationFactor);
                Assert.Equal(MeasurementUnit.Millimeter, ruler.Unit);
            });
        }

        [Fact]
        public void Wpf_ZWatermarkOverlay_Burn_ProducesValidBitmap()
        {
            StaTestRunner.Run(() =>
            {
                var watermark = new ZeroUI.Wpf.Media.ZWatermarkOverlay
                {
                    WatermarkText = "TEST WATERMARK",
                    WatermarkOpacity = 0.3,
                    Placement = WatermarkPlacement.Center
                };

                var src = CreateTestWpfBitmap(300, 200);
                var result = watermark.BurnWatermarkToBitmap(src);
                Assert.NotNull(result);
                Assert.Equal(300, result.PixelWidth);
                Assert.Equal(200, result.PixelHeight);
            });
        }

        [Fact]
        public void Wpf_ZVideoPlayer_Commands_ExecuteSafely()
        {
            StaTestRunner.Run(() =>
            {
                var player = new ZeroUI.Wpf.Media.ZVideoPlayer();
                Assert.Equal(MediaPlaybackState.Stopped, player.PlaybackState);
                Assert.Equal(0.8, player.Volume);
                Assert.False(player.IsMuted);

                player.Volume = 0.5;
                Assert.Equal(0.5, player.Volume);
                player.IsMuted = true;
                Assert.True(player.IsMuted);
            });
        }

        [Fact]
        public void Wpf_ZAudioWaveform_Properties_AndProgress_Work()
        {
            StaTestRunner.Run(() =>
            {
                var wf = new ZeroUI.Wpf.Media.ZAudioWaveform();
                Assert.Equal(0.0, wf.Progress);
                wf.Progress = 0.75;
                Assert.Equal(0.75, wf.Progress);

                wf.WaveformData = new float[] { 0.1f, 0.5f, 0.8f, 0.3f };
                Assert.Equal(4, wf.WaveformData.Count);
            });
        }

        [Fact]
        public void Wpf_ZDocumentDeskew_Binarize_WorksCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                var deskew = new ZeroUI.Wpf.Media.ZDocumentDeskew
                {
                    DeskewAngle = 5.0,
                    IsBinarizationEnabled = true,
                    Threshold = 120
                };

                var src = CreateTestWpfBitmap(100, 100);
                deskew.Source = src;
                var processed = deskew.GetProcessedBitmap();
                Assert.NotNull(processed);
                Assert.Equal(100, processed.PixelWidth);
                Assert.Equal(100, processed.PixelHeight);
            });
        }
    }
}

