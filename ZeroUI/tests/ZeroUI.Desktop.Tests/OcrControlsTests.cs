using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Media.Imaging;
using Xunit;
using ZeroOcr.Core.Models;
using WpfDocumentDeskew = ZeroUI.Wpf.Media.ZDocumentDeskew;
using WinFormsDocumentDeskew = ZeroUI.WinForms.Media.ZDocumentDeskew;
using WpfOcrViewer = ZeroUI.Wpf.Media.ZOcrViewer;
using WinFormsOcrViewer = ZeroUI.WinForms.Media.ZOcrViewer;

namespace ZeroUI.Desktop.Tests
{
    public class OcrControlsTests
    {
        private static BitmapSource CreateTestWpfBitmap(int width, int height)
        {
            var format = System.Windows.Media.PixelFormats.Bgra32;
            int stride = width * 4;
            byte[] pixelData = new byte[stride * height];

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

        private static OcrResult CreateSampleOcrResult()
        {
            var word1 = new OcrWord("INVOICE", new OcrRect(10, 10, 80, 25), 0.98f);
            var word2 = new OcrWord("TOTAL", new OcrRect(10, 45, 60, 25), 0.92f);
            var word3 = new OcrWord("$1,250.00", new OcrRect(75, 45, 90, 25), 0.88f);
            var word4 = new OcrWord("EXPIRY", new OcrRect(10, 80, 60, 25), 0.65f); // Low confidence

            var line1 = new OcrLine("INVOICE", new[] { word1 });
            var line2 = new OcrLine("TOTAL $1,250.00", new[] { word2, word3 });
            var line3 = new OcrLine("EXPIRY", new[] { word4 });

            return OcrResult.Create(new[] { line1, line2, line3 }, TimeSpan.FromMilliseconds(42), "en");
        }

        #region ZDocumentDeskew Parity Tests

        [Fact]
        public void Wpf_ZDocumentDeskew_Binarization_And_Angle_ProcessCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                var deskew = new WpfDocumentDeskew
                {
                    DeskewAngle = 12.5,
                    IsBinarizationEnabled = true,
                    Threshold = 140
                };

                var src = CreateTestWpfBitmap(120, 120);
                deskew.Source = src;

                var processed = deskew.GetProcessedBitmap();
                Assert.NotNull(processed);
                Assert.Equal(120, processed.PixelWidth);
                Assert.Equal(120, processed.PixelHeight);
            });
        }

        [Fact]
        public void WinForms_ZDocumentDeskew_Binarization_And_Angle_ProcessCorrectly()
        {
            using var deskew = new WinFormsDocumentDeskew
            {
                DeskewAngle = 12.5,
                IsBinarizationEnabled = true,
                Threshold = 140
            };

            using var src = CreateTestWinFormsBitmap(120, 120);
            deskew.Image = src;

            using var processed = deskew.GetProcessedBitmap();
            Assert.NotNull(processed);
            Assert.Equal(120, processed.Width);
            Assert.Equal(120, processed.Height);
        }

        #endregion

        #region ZOcrViewer WPF Tests

        [Fact]
        public void Wpf_ZOcrViewer_LoadResult_And_WordHitTest_Works()
        {
            StaTestRunner.Run(() =>
            {
                var viewer = new WpfOcrViewer();
                var bmp = CreateTestWpfBitmap(300, 200);
                var ocrResult = CreateSampleOcrResult();

                viewer.Source = bmp;
                viewer.Result = ocrResult;

                Assert.Equal(4, viewer.Result.Words.Count);
                Assert.Equal(3, viewer.Result.Lines.Count);
                Assert.True(viewer.ShowBoundingBoxes);
                Assert.True(viewer.ShowConfidenceBadges);
                Assert.Equal("INVOICE\r\nTOTAL $1,250.00\r\nEXPIRY", viewer.GetAllText().Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"));

                // Select a word
                viewer.SelectedWord = ocrResult.Words[0];
                Assert.Equal("INVOICE", viewer.GetSelectedText());

                // ROI Filtering
                var targetRoi = new OcrRect(5, 40, 180, 35); // encompasses TOTAL and $1,250.00
                var wordsInRoi = viewer.GetWordsInRoi(targetRoi);
                Assert.Equal(2, wordsInRoi.Count);
                Assert.Contains(wordsInRoi, w => w.Text == "TOTAL");
                Assert.Contains(wordsInRoi, w => w.Text == "$1,250.00");

                // Clear selection
                viewer.ClearSelection();
                Assert.Null(viewer.SelectedWord);
                Assert.Null(viewer.SelectedRoi);
            });
        }

        #endregion

        #region ZOcrViewer WinForms Tests

        [Fact]
        public void WinForms_ZOcrViewer_LoadResult_And_WordHitTest_Works()
        {
            using var viewer = new WinFormsOcrViewer();
            using var bmp = CreateTestWinFormsBitmap(300, 200);
            var ocrResult = CreateSampleOcrResult();

            viewer.Image = bmp;
            viewer.Result = ocrResult;

            Assert.Equal(4, viewer.Result.Words.Count);
            Assert.Equal(3, viewer.Result.Lines.Count);
            Assert.True(viewer.ShowBoundingBoxes);
            Assert.True(viewer.ShowConfidenceBadges);

            // Select a word
            viewer.SelectedWord = ocrResult.Words[1]; // TOTAL
            Assert.Equal("TOTAL", viewer.GetSelectedText());

            // ROI Filtering
            var targetRoi = new OcrRect(5, 5, 100, 35); // encompasses INVOICE
            var wordsInRoi = viewer.GetWordsInRoi(targetRoi);
            Assert.Single(wordsInRoi);
            Assert.Equal("INVOICE", wordsInRoi[0].Text);

            // Clear selection
            viewer.ClearSelection();
            Assert.Null(viewer.SelectedWord);
            Assert.Null(viewer.SelectedRoi);
        }

        #endregion
    }
}
