using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.Core.AiMl;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Documents
{
    public partial class ZAiChatBox
    {
        #region Win32 Redraw Interop

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int wMsg, bool wParam, int lParam);
        private const int WM_SETREDRAW = 0x000B;

        #endregion

        #region High-Performance Token Streaming

        /// <summary>
        /// Appends a new streaming token directly into the target message bubble with zero-flicker double buffering.
        /// Bypasses full conversation rebuilds to maintain fluid 60 FPS typing performance.
        /// </summary>
        public void StreamToken(string messageId, string token)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() => StreamToken(messageId, token)));
                }
                return;
            }

            if (string.IsNullOrEmpty(token)) return;

            var msg = _messages.FirstOrDefault(m => m.Id == messageId);
            if (msg == null) return;

            msg.Content += token;
            msg.IsStreaming = true;

            // Direct in-place bubble update via ViewHolder cache
            if (_bubbleCache.TryGetValue(messageId, out var holder) && holder.RichTextBox != null && !holder.BubblePanel.IsDisposed)
            {
                var rtb = holder.RichTextBox;
                var bubble = holder.BubblePanel;

                // Lock redraw to prevent Windows Forms RichTextBox character-level flashing
                SendMessage(rtb.Handle, WM_SETREDRAW, false, 0);
                try
                {
                    string safeToken = token.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", Environment.NewLine);
                    rtb.AppendText(safeToken);

                    int newTextHeight = CalculateRichTextBoxHeight(rtb, rtb.Width);
                    if (newTextHeight != rtb.Height)
                    {
                        int oldBubbleHeight = bubble.Height;
                        rtb.Height = newTextHeight;

                        int newBubbleHeight = 28 + newTextHeight + (holder.FooterPanel != null ? 44 : 10);
                        bubble.Height = newBubbleHeight;

                        if (holder.FooterPanel != null)
                        {
                            holder.FooterPanel.Location = new Point(0, 28 + newTextHeight + 6);
                        }

                        int delta = newBubbleHeight - oldBubbleHeight;
                        if (delta != 0)
                        {
                            bool after = false;
                            foreach (Control c in _messagesContainer.Controls)
                            {
                                if (after)
                                {
                                    c.Top += delta;
                                }
                                else if (c == bubble)
                                {
                                    after = true;
                                }
                            }
                            bubble.Invalidate();
                        }
                    }
                }
                finally
                {
                    SendMessage(rtb.Handle, WM_SETREDRAW, true, 0);
                    rtb.Invalidate();
                }

                ScrollToBottom();
            }
            else
            {
                // Fallback if bubble was not yet initialized in cache
                RebuildAllMessages();
            }
        }

        /// <summary>
        /// Finalizes token streaming by rendering comprehensive markdown syntax and showing action buttons.
        /// </summary>
        public void CompleteStreaming(string messageId)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() => CompleteStreaming(messageId)));
                }
                return;
            }

            var msg = _messages.FirstOrDefault(m => m.Id == messageId);
            if (msg != null)
            {
                msg.IsStreaming = false;

                // Rebuild bubble with DataGridView if tables are present in the streamed content
                if (ContainsMarkdownTable(msg.Content ?? ""))
                {
                    RebuildSingleBubble(messageId);
                }
                else if (_bubbleCache.TryGetValue(messageId, out var holder) && holder.RichTextBox != null && !holder.BubblePanel.IsDisposed)
                {
                    var rtb = holder.RichTextBox;
                    var bubble = holder.BubblePanel;
                    bool isDark = ZeroTheme.IsDark;
                    Color textFore = isDark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(15, 23, 42);
                    Color highlight = isDark ? Color.FromArgb(56, 189, 248) : Color.FromArgb(2, 132, 199);

                    SendMessage(rtb.Handle, WM_SETREDRAW, false, 0);
                    try
                    {
                        // Single-pass markdown rendering once generation finishes
                        SetMarkdownText(rtb, msg.Content ?? "", textFore, highlight);

                        int newTextHeight = CalculateRichTextBoxHeight(rtb, rtb.Width);
                        int oldBubbleHeight = bubble.Height;
                        rtb.Height = newTextHeight;

                        int newBubbleHeight = 28 + newTextHeight + (holder.FooterPanel != null ? 44 : 10);
                        bubble.Height = newBubbleHeight;

                        if (holder.FooterPanel != null)
                        {
                            holder.FooterPanel.Location = new Point(0, 28 + newTextHeight + 6);
                            holder.FooterPanel.Visible = true;
                        }

                        int delta = newBubbleHeight - oldBubbleHeight;
                        if (delta != 0)
                        {
                            bool after = false;
                            foreach (Control c in _messagesContainer.Controls)
                            {
                                if (after) c.Top += delta;
                                else if (c == bubble) after = true;
                            }
                            bubble.Invalidate();
                        }
                    }
                    finally
                    {
                        SendMessage(rtb.Handle, WM_SETREDRAW, true, 0);
                        rtb.Invalidate();
                    }

                    ScrollToBottom();
                }
                else
                {
                    RebuildAllMessages();
                }
            }

            if (StreamingState == ChatStreamingState.Thinking || StreamingState == ChatStreamingState.Streaming)
            {
                StreamingState = ChatStreamingState.Idle;
            }
        }

        #endregion

        #region Thinking Indicator

        public void ShowThinkingIndicator(string text = "Đang phân tích...")
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() => ShowThinkingIndicator(text)));
                }
                return;
            }

            StreamingState = ChatStreamingState.Thinking;

            if (_thinkingBubble != null)
            {
                var lbl = _thinkingBubble.Controls.OfType<Label>().FirstOrDefault(l => (string?)l.Tag == "statusText");
                if (lbl != null) lbl.Text = text;
                ScrollToBottom();
                return;
            }

            int yOffset = 10;
            foreach (Control c in _messagesContainer.Controls)
            {
                if (c.Bottom + 12 > yOffset) yOffset = c.Bottom + 12;
            }

            int safeWidth = Math.Min(320, _messagesContainer.ClientSize.Width - 24);
            bool isDark = ZeroTheme.IsDark;
            var pnl = new Panel
            {
                BackColor = isDark ? Color.FromArgb(17, 24, 39) : Color.FromArgb(255, 255, 255),
                Size = new Size(safeWidth, 58),
                Location = new Point(10, yOffset),
                Padding = new Padding(10, 6, 10, 6)
            };

            pnl.Paint += (s, e) =>
            {
                var penColor = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);
                using var p = new Pen(penColor, 1f);
                e.Graphics.DrawRectangle(p, 0, 0, pnl.Width - 1, pnl.Height - 1);
            };

            var header = new Label
            {
                Text = string.IsNullOrWhiteSpace(_modelName) ? _assistantName : _modelName,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = isDark ? Color.FromArgb(52, 211, 153) : Color.FromArgb(5, 150, 105),
                AutoSize = true,
                Location = new Point(10, 6)
            };
            pnl.Controls.Add(header);

            var dots = new PulsingDotsControl
            {
                Location = new Point(10, 26),
                Size = new Size(60, 24)
            };
            pnl.Controls.Add(dots);

            var lblText = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic),
                ForeColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139),
                AutoSize = true,
                Location = new Point(72, 28),
                Tag = "statusText"
            };
            pnl.Controls.Add(lblText);

            _thinkingBubble = pnl;
            _messagesContainer.Controls.Add(pnl);
            ScrollToBottom();
        }

        public void HideThinkingIndicator()
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(HideThinkingIndicator));
                }
                return;
            }

            if (_thinkingBubble != null)
            {
                _messagesContainer.Controls.Remove(_thinkingBubble);
                _thinkingBubble.Dispose();
                _thinkingBubble = null;
            }

            if (StreamingState == ChatStreamingState.Thinking)
            {
                StreamingState = ChatStreamingState.Idle;
            }
        }

        public async Task<ChatMessage> AppendAssistantActionMessageAnimatedAsync(
            string fullContent,
            bool canUndo = true,
            int delayMs = 16)
        {
            HideThinkingIndicator();

            var msg = ChatMessage.Assistant(fullContent, _modelName);
            msg.CanUndo = canUndo;

            _suppressRebuild = true;
            _messages.Add(msg);
            _suppressRebuild = false;

            int containerW = Math.Max(200, _messagesContainer.ClientSize.Width - 24);
            int yOffset = 10;
            foreach (Control c in _messagesContainer.Controls)
            {
                if (c.Bottom + 12 > yOffset) yOffset = c.Bottom + 12;
            }

            var tempMsg = ChatMessage.Assistant("", _modelName);
            tempMsg.CanUndo = canUndo;
            var bubble = CreateBubbleControl(tempMsg, containerW, out var rtb, out var footerPanel);
            bubble.Location = new Point(10, yOffset);

            if (footerPanel != null)
            {
                footerPanel.Visible = false;
            }

            _messagesContainer.Controls.Add(bubble);
            ScrollToBottom();

            if (rtb == null)
            {
                return msg;
            }

            _isGenerating = true;
            StreamingState = ChatStreamingState.Streaming;
            _sendButton.Visible = false;
            _stopButton.Visible = true;
            _cancelStreaming = false;

            // Tokenize by word groups
            var tokens = Regex.Matches(fullContent, @"(\S+\s*|\s+)").Cast<Match>().Select(m => m.Value).ToList();
            var sb = new System.Text.StringBuilder();

            bool isDarkToken = ZeroTheme.IsDark;
            Color textFore = isDarkToken ? Color.FromArgb(226, 232, 240) : Color.FromArgb(15, 23, 42);
            Color highlight = isDarkToken ? Color.FromArgb(56, 189, 248) : Color.FromArgb(2, 132, 199);

            for (int i = 0; i < tokens.Count; i += 2)
            {
                if (_cancelStreaming)
                {
                    sb.Clear();
                    sb.Append(fullContent);
                    SetMarkdownText(rtb, sb.ToString(), textFore, highlight);
                    break;
                }

                sb.Append(tokens[i]);
                if (i + 1 < tokens.Count) sb.Append(tokens[i + 1]);

                SetMarkdownText(rtb, sb.ToString(), textFore, highlight);
                int h = CalculateRichTextBoxHeight(rtb, rtb.Width);
                rtb.Height = h;
                bubble.Height = h + (canUndo ? 76 : 64);
                ScrollToBottom();
                await Task.Delay(delayMs);
            }

            if (ContainsMarkdownTable(fullContent))
            {
                RebuildSingleBubble(msg.Id);
            }
            else
            {
                // Final render pass
                SetMarkdownText(rtb, fullContent, textFore, highlight);
                int finalH = CalculateRichTextBoxHeight(rtb, rtb.Width);
                rtb.Height = finalH;
                bubble.Height = finalH + (canUndo ? 76 : 64);

                if (footerPanel != null)
                {
                    footerPanel.Location = new Point(0, 28 + finalH + 6);
                    footerPanel.Visible = true;
                }
            }

            _isGenerating = false;
            StreamingState = ChatStreamingState.Completed;
            _stopButton.Visible = false;
            _sendButton.Visible = true;
            ScrollToBottom();

            return msg;
        }

        #endregion
    }

    #region Pulsing Dots Animation Control

    internal sealed class PulsingDotsControl : Control
    {
        private readonly System.Windows.Forms.Timer _timer;
        private int _tick;

        public PulsingDotsControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(64, 24);

            _timer = new System.Windows.Forms.Timer { Interval = 40 };
            _timer.Tick += (s, e) =>
            {
                _tick++;
                Invalidate();
            };
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            for (int i = 0; i < 3; i++)
            {
                float phase = _tick * 0.18f + i * 0.9f;
                float yOffset = (float)(Math.Sin(phase) * 3.5);
                int alpha = (int)(140 + 115 * Math.Sin(phase));
                alpha = Math.Max(40, Math.Min(255, alpha));

                using var brush = new SolidBrush(Color.FromArgb(alpha, 52, 211, 153));
                e.Graphics.FillEllipse(brush, 6 + i * 16, 8 + yOffset, 8, 8);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #endregion
}
