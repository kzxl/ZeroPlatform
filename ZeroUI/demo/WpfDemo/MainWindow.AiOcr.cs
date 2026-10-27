using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ZeroOcr.Core.Models;
using ZeroUI.Core.AiMl;

namespace ZeroUI.Samples.WpfDemo
{
    public partial class MainWindow
    {
        private DispatcherTimer? _aiStreamingTimer;
        private string? _activeStreamingMessageId;
        private readonly Queue<string> _pendingStreamingTokens = new();

        private void SetupAiOcrDemo()
        {
            // 1. Setup AI Chat Box & Prompt Suggestions
            WpfChatControl.PromptSuggestions.Clear();
            WpfChatControl.PromptSuggestions.Add("⚡ Analyze SCADA Telemetry Anomalies");
            WpfChatControl.PromptSuggestions.Add("📑 Extract Invoice OCR Summary");
            WpfChatControl.PromptSuggestions.Add("🔄 Query ISA-88 Batch State");
            WpfChatControl.PromptSuggestions.Add("🚀 Benchmark ZeroGrid 10M Virtualization");

            // Seed initial greeting
            var welcome = WpfChatControl.AppendAssistantMessage(
                "Welcome to **ZeroUI AI Assistant**! 🤖\n\n" +
                "I am your edge-deployed reasoning copilot. You can ask me to inspect SCADA alarms, analyze document OCR extractions, or optimize real-time telemetry pipelines.\n\n" +
                "Try typing a prompt or clicking one of the suggested actions below!");
            welcome.IsStreaming = false;

            WpfChatControl.SendMessageRequested += OnWpfChatSendMessageRequested;
            WpfChatControl.StopGenerationRequested += (s, e) => StopAiStreaming();
            WpfChatControl.ClearChatRequested += (s, e) => StopAiStreaming();

            // 2. Setup OCR Vision Viewer
            var docBmp = CreateSampleDocumentBitmap(skewed: false);
            var ocrResult = CreateEnterpriseSampleOcrResult();
            WpfOcrViewerControl.Source = docBmp;
            WpfOcrViewerControl.Result = ocrResult;

            // Word selection observation
            var dpd = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                ZeroUI.Wpf.Media.ZOcrViewer.SelectedWordProperty, typeof(ZeroUI.Wpf.Media.ZOcrViewer));
            dpd?.AddValueChanged(WpfOcrViewerControl, (s, e) =>
            {
                if (WpfOcrViewerControl.SelectedWord is { } word)
                {
                    TxtWpfOcrToken.Text = $"\"{word.Text}\"";
                    TxtWpfOcrTokenConfidence.Text = $"{(int)(word.Confidence * 100)}%";
                    TxtWpfOcrTokenBounds.Text = $"[{word.BoundingBox.X}, {word.BoundingBox.Y}, {word.BoundingBox.Width}x{word.BoundingBox.Height}]";
                }
                else
                {
                    TxtWpfOcrToken.Text = "(Click a token or drag ROI)";
                    TxtWpfOcrTokenConfidence.Text = "--";
                    TxtWpfOcrTokenBounds.Text = "--";
                }
            });

            // 3. Setup Document Deskew
            var skewedBmp = CreateSampleDocumentBitmap(skewed: true, skewAngle: -8.5);
            WpfDeskewControl.Source = skewedBmp;
            WpfDeskewControl.DeskewAngle = -8.5;
        }

        #region AI Chat Simulator

        private void OnWpfChatSendMessageRequested(object? sender, string prompt)
        {
            StopAiStreaming();

            var assistantMessage = WpfChatControl.AppendAssistantMessage();
            _activeStreamingMessageId = assistantMessage.Id;

            WpfChatControl.IsGenerating = true;
            WpfChatControl.StreamingState = ChatStreamingState.Streaming;

            string fullResponse = GenerateMockAiResponse(prompt);
            string[] words = fullResponse.Split(new[] { ' ' }, StringSplitOptions.None);
            _pendingStreamingTokens.Clear();
            for (int i = 0; i < words.Length; i++)
            {
                string token = words[i] + (i < words.Length - 1 ? " " : "");
                _pendingStreamingTokens.Enqueue(token);
            }

            int intervalMs = ChkWpfAiFastStream?.IsChecked == true ? 15 : 30;
            _aiStreamingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(intervalMs)
            };
            _aiStreamingTimer.Tick += (s, e) =>
            {
                if (_pendingStreamingTokens.Count > 0 && _activeStreamingMessageId != null)
                {
                    string chunk = _pendingStreamingTokens.Dequeue();
                    WpfChatControl.StreamToken(_activeStreamingMessageId, chunk);
                }
                else
                {
                    StopAiStreaming();
                }
            };
            _aiStreamingTimer.Start();
        }

        private void StopAiStreaming()
        {
            if (_aiStreamingTimer != null)
            {
                _aiStreamingTimer.Stop();
                _aiStreamingTimer = null;
            }

            if (_activeStreamingMessageId != null)
            {
                WpfChatControl.CompleteStreaming(_activeStreamingMessageId);
                _activeStreamingMessageId = null;
            }

            WpfChatControl.IsGenerating = false;
            WpfChatControl.StreamingState = ChatStreamingState.Completed;
            _pendingStreamingTokens.Clear();
        }

        private static string GenerateMockAiResponse(string prompt)
        {
            string p = prompt.ToLowerInvariant();
            if (p.Contains("scada") || p.Contains("telemetry") || p.Contains("anomal"))
            {
                return "⚡ **SCADA Telemetry Analysis Complete**:\n\n" +
                       "- **Reactor Temp (R-101)**: 84.6°C (Target: 85°C, stability 99.4%)\n" +
                       "- **Feed Pump P-201**: Flow rate 142 L/min, pressure nominal at 4.2 bar.\n" +
                       "- **Vibration Sensors**: FFT spectrum confirms zero harmonic resonance.\n\n" +
                       "✅ **Diagnosis**: All 16 automation nodes are operating within ISA-18.2 Class A envelopes. Zero warnings logged.";
            }

            if (p.Contains("ocr") || p.Contains("invoice") || p.Contains("document"))
            {
                return "📑 **ZeroOCR Extraction Summary**:\n\n" +
                       "- **Document Type**: Commercial Tax Invoice\n" +
                       "- **Invoice No**: `INV-2026-09-881` (Confidence: 98.5%)\n" +
                       "- **Customer**: ACME Industrial Automation Corp\n" +
                       "- **Line Items Detected**:\n" +
                       "  1. ZeroGrid High-Performance Engine 10M — $4,200.00\n" +
                       "  2. SCADA Edge Telemetry Gateway — $1,850.00\n" +
                       "  3. ZeroOCR Vision & Deskew Processor — $1,500.00\n" +
                       "- **Total Payable**: **$8,305.00 USD** (Subtotal: $7,550.00, VAT 10%: $755.00)\n\n" +
                       "✨ All 14 token bounding boxes aligned with zero skew.";
            }

            if (p.Contains("batch") || p.Contains("isa-88") || p.Contains("state"))
            {
                return "🔄 **ISA-88 Batch Execution Status**:\n\n" +
                       "- **Current Unit Procedure**: `UP-CRYSTALLIZE-04`\n" +
                       "- **Current Operation**: `OP-PRECIPITATION-B`\n" +
                       "- **Current Phase**: `PHASE-AGITATE-AND-COOL` (Step 3/6, Active 00:14:22)\n" +
                       "- **Interlocks**: All 8 safety permissives verified.\n\n" +
                       "Next transition scheduled in 2 min 40 sec upon reaching temperature setpoint 22.0°C.";
            }

            if (p.Contains("zerogrid") || p.Contains("grid") || p.Contains("10m") || p.Contains("benchmark"))
            {
                return "🚀 **ZeroGrid 10M Virtualization Benchmark Metrics**:\n\n" +
                       "- **Total Dataset Size**: 10,000,000 rows x 15 columns in memory.\n" +
                       "- **Visible Viewport Window**: 28 items dynamically rendered.\n" +
                       "- **Allocation Rate**: **0 bytes/frame** during continuous 100K row scrolls.\n" +
                       "- **Render Latency**: **0.42 ms** (capped at 60+ FPS smooth lock).\n\n" +
                       "ZeroUI uses custom direct visual layout bypassing standard WPF items controls overhead.";
            }

            return $"Analyzed input: \"{prompt}\".\n\n" +
                   "ZeroUI Edge AI Engine completed localized inference in **18 ms**.\n" +
                   "Data pipelines and interactive controls are synchronized and ready.";
        }

        private void BtnWpfAiScada_Click(object sender, RoutedEventArgs e)
        {
            WpfChatControl.AppendUserMessage("⚡ Analyze SCADA Telemetry Anomalies");
            OnWpfChatSendMessageRequested(WpfChatControl, "Analyze SCADA Telemetry Anomalies");
        }

        private void BtnWpfAiOcr_Click(object sender, RoutedEventArgs e)
        {
            WpfChatControl.AppendUserMessage("📑 Extract Invoice OCR Summary");
            OnWpfChatSendMessageRequested(WpfChatControl, "Extract Invoice OCR Summary");
        }

        private void BtnWpfAiBatch_Click(object sender, RoutedEventArgs e)
        {
            WpfChatControl.AppendUserMessage("🔄 Query ISA-88 Batch State");
            OnWpfChatSendMessageRequested(WpfChatControl, "Query ISA-88 Batch State");
        }

        private void BtnWpfAiClear_Click(object sender, RoutedEventArgs e)
        {
            WpfChatControl.ClearMessages();
            StopAiStreaming();
        }

        #endregion

        #region OCR Viewer & Document Deskew Handlers

        private void ChkWpfOcrOption_Changed(object sender, RoutedEventArgs e)
        {
            if (WpfOcrViewerControl == null) return;
            WpfOcrViewerControl.ShowBoundingBoxes = ChkWpfOcrBoxes?.IsChecked == true;
            WpfOcrViewerControl.ShowConfidenceBadges = ChkWpfOcrBadges?.IsChecked == true;
            WpfOcrViewerControl.ShowTextOverlay = ChkWpfOcrText?.IsChecked == true;
        }

        private void SliderWpfOcrConfidence_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (WpfOcrViewerControl == null) return;
            WpfOcrViewerControl.MinConfidenceThreshold = (float)e.NewValue;
            if (TxtWpfOcrConfidenceVal != null)
            {
                TxtWpfOcrConfidenceVal.Text = $"{(int)(e.NewValue * 100)}%";
            }
        }

        private void BtnWpfOcrZoomIn_Click(object sender, RoutedEventArgs e)
        {
            WpfOcrViewerControl.Zoom = Math.Min(5.0, WpfOcrViewerControl.Zoom + 0.25);
        }

        private void BtnWpfOcrZoomOut_Click(object sender, RoutedEventArgs e)
        {
            WpfOcrViewerControl.Zoom = Math.Max(0.25, WpfOcrViewerControl.Zoom - 0.25);
        }

        private void BtnWpfOcrZoomFit_Click(object sender, RoutedEventArgs e)
        {
            WpfOcrViewerControl.ZoomToFit();
        }

        private void BtnWpfOcrClearSel_Click(object sender, RoutedEventArgs e)
        {
            WpfOcrViewerControl.ClearSelection();
        }

        private void BtnWpfOcrCopy_Click(object sender, RoutedEventArgs e)
        {
            string? text = WpfOcrViewerControl.GetSelectedText();
            if (string.IsNullOrEmpty(text))
            {
                text = WpfOcrViewerControl.GetAllText();
            }

            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
                MessageBox.Show($"Copied {text.Length} characters to clipboard:\n\n{text}", "ZeroOCR Text Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void SliderWpfDeskewAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (WpfDeskewControl == null) return;
            WpfDeskewControl.DeskewAngle = e.NewValue;
            if (TxtWpfDeskewAngle != null)
            {
                TxtWpfDeskewAngle.Text = $"{e.NewValue:F1}°";
            }
        }

        private void ChkWpfDeskewBinarize_Changed(object sender, RoutedEventArgs e)
        {
            if (WpfDeskewControl == null) return;
            WpfDeskewControl.IsBinarizationEnabled = ChkWpfDeskewBinarize?.IsChecked == true;
        }

        private void SliderWpfDeskewThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (WpfDeskewControl == null) return;
            byte val = (byte)Math.Clamp((int)e.NewValue, 0, 255);
            WpfDeskewControl.Threshold = val;
            if (TxtWpfDeskewThreshold != null)
            {
                TxtWpfDeskewThreshold.Text = val.ToString();
            }
        }

        private void BtnWpfDeskewAuto_Click(object sender, RoutedEventArgs e)
        {
            // Simulate automated deskew algorithm detecting angle and setting correction
            SliderWpfDeskewAngle.Value = 0.0;
        }

        private void BtnWpfDeskewSendToOcr_Click(object sender, RoutedEventArgs e)
        {
            var processed = WpfDeskewControl.GetProcessedBitmap();
            if (processed != null)
            {
                WpfOcrViewerControl.Source = processed;
                MessageBox.Show("Processed straightened document transferred to ZeroOCR Viewer!", "ZeroOCR Deskew Pipeline", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Mock Document & OCR Data Generation

        private static void DrawDocText(DrawingContext dc, string text, double x, double y, Typeface typeface, double size, Brush brush)
        {
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                size,
                brush,
                1.0);
            dc.DrawText(ft, new Point(x, y));
        }

        private static BitmapSource CreateSampleDocumentBitmap(bool skewed, double skewAngle = 0.0)
        {
            int width = 720;
            int height = 460;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                if (skewed && Math.Abs(skewAngle) > 0.01)
                {
                    dc.PushTransform(new RotateTransform(skewAngle, width / 2.0, height / 2.0));
                }

                // Background
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(248, 250, 252)), new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1.5), new Rect(10, 10, width - 20, height - 20));

                // Header bar
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 41, 59)), null, new Rect(10, 10, width - 20, 50));

                var typefaceHeader = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                var typefaceBody = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                var typefaceBold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

                DrawDocText(dc, "ZERO PLATFORM ENTERPRISE - TAX INVOICE", 30, 24, typefaceHeader, 16, Brushes.White);
                DrawDocText(dc, "INVOICE NO: INV-2026-09-881", 30, 78, typefaceBold, 13, new SolidColorBrush(Color.FromRgb(30, 41, 59)));
                DrawDocText(dc, "DATE: 2026-09-30", 420, 78, typefaceBold, 13, new SolidColorBrush(Color.FromRgb(30, 41, 59)));
                DrawDocText(dc, "CUSTOMER: ACME INDUSTRIAL AUTOMATION CORP", 30, 108, typefaceBody, 12, new SolidColorBrush(Color.FromRgb(71, 85, 105)));

                // Table Header
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(241, 245, 249)), null, new Rect(30, 138, width - 60, 28));
                DrawDocText(dc, "ITEM DESCRIPTION", 40, 144, typefaceBold, 11, new SolidColorBrush(Color.FromRgb(71, 85, 105)));
                DrawDocText(dc, "QTY", 380, 144, typefaceBold, 11, new SolidColorBrush(Color.FromRgb(71, 85, 105)));
                DrawDocText(dc, "UNIT PRICE", 460, 144, typefaceBold, 11, new SolidColorBrush(Color.FromRgb(71, 85, 105)));
                DrawDocText(dc, "AMOUNT", 580, 144, typefaceBold, 11, new SolidColorBrush(Color.FromRgb(71, 85, 105)));

                // Items
                DrawDocText(dc, "ZeroGrid High-Performance Engine 10M", 40, 178, typefaceBody, 12, Brushes.Black);
                DrawDocText(dc, "1", 390, 178, typefaceBody, 12, Brushes.Black);
                DrawDocText(dc, "$4,200.00", 465, 178, typefaceBody, 12, Brushes.Black);
                DrawDocText(dc, "$4,200.00", 585, 178, typefaceBold, 12, Brushes.Black);

                DrawDocText(dc, "SCADA Edge Telemetry Gateway OPC", 40, 212, typefaceBody, 12, Brushes.Black);
                DrawDocText(dc, "2", 390, 212, typefaceBody, 12, Brushes.Black);
                DrawDocText(dc, "$925.00", 470, 212, typefaceBody, 12, Brushes.Black);
                DrawDocText(dc, "$1,850.00", 585, 212, typefaceBold, 12, Brushes.Black);

                DrawDocText(dc, "ZeroOCR Vision & Deskew Processor", 40, 246, typefaceBody, 12, Brushes.Black);
                DrawDocText(dc, "1", 390, 246, typefaceBody, 12, Brushes.Black);
                DrawDocText(dc, "$1,500.00", 465, 246, typefaceBody, 12, Brushes.Black);
                DrawDocText(dc, "$1,500.00", 585, 246, typefaceBold, 12, Brushes.Black);

                // Divider
                dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1), new Point(30, 285), new Point(width - 30, 285));

                // Totals
                DrawDocText(dc, "SUBTOTAL:", 460, 300, typefaceBold, 12, new SolidColorBrush(Color.FromRgb(71, 85, 105)));
                DrawDocText(dc, "$7,550.00", 585, 300, typefaceBold, 12, Brushes.Black);

                DrawDocText(dc, "VAT TAX (10%):", 460, 328, typefaceBold, 12, new SolidColorBrush(Color.FromRgb(71, 85, 105)));
                DrawDocText(dc, "$755.00", 585, 328, typefaceBold, 12, Brushes.Black);

                DrawDocText(dc, "TOTAL PAYABLE:", 430, 362, typefaceHeader, 13, new SolidColorBrush(Color.FromRgb(16, 185, 129)));
                DrawDocText(dc, "$8,305.00 USD", 565, 362, typefaceHeader, 13, new SolidColorBrush(Color.FromRgb(16, 185, 129)));

                DrawDocText(dc, "STATUS: VERIFIED & CLEARED", 30, 410, typefaceBold, 11, new SolidColorBrush(Color.FromRgb(100, 116, 139)));

                if (skewed && Math.Abs(skewAngle) > 0.01)
                {
                    dc.Pop();
                }
            }

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        private static OcrResult CreateEnterpriseSampleOcrResult()
        {
            var lines = new List<OcrLine>();

            // Line 1: Header
            var wTitle1 = new OcrWord("ZERO", new OcrRect(30, 24, 48, 22), 0.99f);
            var wTitle2 = new OcrWord("PLATFORM", new OcrRect(84, 24, 96, 22), 0.98f);
            var wTitle3 = new OcrWord("INVOICE", new OcrRect(300, 24, 75, 22), 0.97f);
            lines.Add(new OcrLine("ZERO PLATFORM INVOICE", new[] { wTitle1, wTitle2, wTitle3 }));

            // Line 2: Invoice No
            var wInv1 = new OcrWord("INVOICE", new OcrRect(30, 78, 65, 18), 0.99f);
            var wInv2 = new OcrWord("NO:", new OcrRect(98, 78, 28, 18), 0.98f);
            var wInv3 = new OcrWord("INV-2026-09-881", new OcrRect(130, 78, 125, 18), 0.96f);
            lines.Add(new OcrLine("INVOICE NO: INV-2026-09-881", new[] { wInv1, wInv2, wInv3 }));

            // Line 3: Date
            var wDate1 = new OcrWord("DATE:", new OcrRect(420, 78, 45, 18), 0.97f);
            var wDate2 = new OcrWord("2026-09-30", new OcrRect(470, 78, 85, 18), 0.98f);
            lines.Add(new OcrLine("DATE: 2026-09-30", new[] { wDate1, wDate2 }));

            // Line 4: Item 1
            var wItm1 = new OcrWord("ZeroGrid", new OcrRect(40, 178, 65, 18), 0.95f);
            var wItm2 = new OcrWord("High-Performance", new OcrRect(110, 178, 120, 18), 0.92f);
            var wItm3 = new OcrWord("$4,200.00", new OcrRect(585, 178, 75, 18), 0.98f);
            lines.Add(new OcrLine("ZeroGrid High-Performance $4,200.00", new[] { wItm1, wItm2, wItm3 }));

            // Line 5: Item 2
            var wScada1 = new OcrWord("SCADA", new OcrRect(40, 212, 55, 18), 0.94f);
            var wScada2 = new OcrWord("Gateway", new OcrRect(145, 212, 65, 18), 0.88f);
            var wScada3 = new OcrWord("$1,850.00", new OcrRect(585, 212, 75, 18), 0.96f);
            lines.Add(new OcrLine("SCADA Gateway $1,850.00", new[] { wScada1, wScada2, wScada3 }));

            // Line 6: Item 3
            var wOcr1 = new OcrWord("ZeroOCR", new OcrRect(40, 246, 68, 18), 0.93f);
            var wOcr2 = new OcrWord("Processor", new OcrRect(165, 246, 75, 18), 0.89f);
            var wOcr3 = new OcrWord("$1,500.00", new OcrRect(585, 246, 75, 18), 0.97f);
            lines.Add(new OcrLine("ZeroOCR Processor $1,500.00", new[] { wOcr1, wOcr2, wOcr3 }));

            // Line 7: Totals
            var wTot1 = new OcrWord("TOTAL", new OcrRect(430, 362, 55, 20), 0.99f);
            var wTot2 = new OcrWord("PAYABLE:", new OcrRect(490, 362, 70, 20), 0.97f);
            var wTot3 = new OcrWord("$8,305.00", new OcrRect(565, 362, 85, 20), 0.99f);
            var wTot4 = new OcrWord("USD", new OcrRect(655, 362, 38, 20), 0.72f); // Test lower confidence badge!
            lines.Add(new OcrLine("TOTAL PAYABLE: $8,305.00 USD", new[] { wTot1, wTot2, wTot3, wTot4 }));

            return OcrResult.Create(lines, TimeSpan.FromMilliseconds(38), "en");
        }

        #endregion
    }
}
