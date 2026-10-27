using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using ZeroUI.Core.AiMl;

namespace ZeroUI.WinForms.Documents
{
    #region Markdown Block Models

    internal abstract class ChatMarkdownBlock
    {
    }

    internal sealed class ChatMarkdownTextBlock : ChatMarkdownBlock
    {
        public string Content { get; set; } = string.Empty;
    }

    internal sealed class ChatMarkdownTableBlock : ChatMarkdownBlock
    {
        public List<string> Headers { get; } = new List<string>();
        public List<DataGridViewContentAlignment> Alignments { get; } = new List<DataGridViewContentAlignment>();
        public List<List<string>> Rows { get; } = new List<List<string>>();
    }

    #endregion

    public partial class ZAiChatBox
    {
        #region Markdown Block Parser

        internal static List<ChatMarkdownBlock> ParseMarkdownBlocks(string text)
        {
            var blocks = new List<ChatMarkdownBlock>();
            if (string.IsNullOrEmpty(text))
                return blocks;

            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = normalized.Split('\n');
            var currentTextLines = new List<string>();
            int lineIdx = 0;

            while (lineIdx < lines.Length)
            {
                if (IsTableStart(lines, lineIdx, out int tableEndIdx))
                {
                    if (currentTextLines.Count > 0)
                    {
                        string t = string.Join("\n", currentTextLines).Trim('\n');
                        if (!string.IsNullOrEmpty(t))
                        {
                            blocks.Add(new ChatMarkdownTextBlock { Content = t });
                        }
                        currentTextLines.Clear();
                    }

                    var tableBlock = ParseTable(lines, lineIdx, tableEndIdx);
                    if (tableBlock != null && tableBlock.Headers.Count > 0)
                    {
                        blocks.Add(tableBlock);
                    }
                    lineIdx = tableEndIdx + 1;
                }
                else
                {
                    currentTextLines.Add(lines[lineIdx]);
                    lineIdx++;
                }
            }

            if (currentTextLines.Count > 0)
            {
                string t = string.Join("\n", currentTextLines).Trim('\n');
                if (!string.IsNullOrEmpty(t))
                {
                    blocks.Add(new ChatMarkdownTextBlock { Content = t });
                }
            }

            return blocks;
        }

        internal static bool ContainsMarkdownTable(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains("|") || !text.Contains("-"))
                return false;

            var blocks = ParseMarkdownBlocks(text);
            return blocks.Exists(b => b is ChatMarkdownTableBlock);
        }

        private static bool IsTableStart(string[] lines, int startIdx, out int endIdx)
        {
            endIdx = startIdx;
            if (startIdx + 1 >= lines.Length) return false;

            string headerLine = lines[startIdx].Trim();
            if (!headerLine.Contains("|")) return false;

            string sepLine = lines[startIdx + 1].Trim();
            if (!IsSeparatorRow(sepLine)) return false;

            var headerCells = SplitRowCells(headerLine);
            var sepCells = SplitRowCells(sepLine);
            if (headerCells.Count == 0 || sepCells.Count == 0) return false;

            int cursor = startIdx + 2;
            while (cursor < lines.Length)
            {
                string rowLine = lines[cursor].Trim();
                if (string.IsNullOrEmpty(rowLine) || !rowLine.Contains("|"))
                    break;
                cursor++;
            }
            endIdx = cursor - 1;
            return true;
        }

        private static bool IsSeparatorRow(string line)
        {
            if (string.IsNullOrEmpty(line) || !line.Contains("|") || !line.Contains("-"))
                return false;

            var cells = SplitRowCells(line);
            if (cells.Count == 0) return false;

            foreach (var cell in cells)
            {
                string c = cell.Trim();
                if (string.IsNullOrEmpty(c)) return false;
                bool hasDash = false;
                for (int i = 0; i < c.Length; i++)
                {
                    char ch = c[i];
                    if (ch == '-') hasDash = true;
                    else if (ch != ':' && ch != ' ') return false;
                }
                if (!hasDash) return false;
            }
            return true;
        }

        private static List<string> SplitRowCells(string line)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("|")) trimmed = trimmed.Substring(1);
            if (trimmed.EndsWith("|")) trimmed = trimmed.Substring(0, trimmed.Length - 1);

            var parts = trimmed.Split('|');
            var cells = new List<string>(parts.Length);
            foreach (var p in parts)
            {
                cells.Add(p.Trim());
            }
            return cells;
        }

        private static ChatMarkdownTableBlock? ParseTable(string[] lines, int startIdx, int endIdx)
        {
            string headerLine = lines[startIdx].Trim();
            string sepLine = lines[startIdx + 1].Trim();

            var rawHeaders = SplitRowCells(headerLine);
            var sepCells = SplitRowCells(sepLine);
            if (rawHeaders.Count == 0) return null;

            var table = new ChatMarkdownTableBlock();
            for (int i = 0; i < rawHeaders.Count; i++)
            {
                table.Headers.Add(CleanCellMarkdown(rawHeaders[i]));

                var align = DataGridViewContentAlignment.MiddleLeft;
                if (i < sepCells.Count)
                {
                    string s = sepCells[i].Trim();
                    bool left = s.StartsWith(":");
                    bool right = s.EndsWith(":");
                    if (left && right) align = DataGridViewContentAlignment.MiddleCenter;
                    else if (right) align = DataGridViewContentAlignment.MiddleRight;
                }
                table.Alignments.Add(align);
            }

            for (int r = startIdx + 2; r <= endIdx; r++)
            {
                string rowLine = lines[r].Trim();
                if (string.IsNullOrEmpty(rowLine)) continue;
                var rawCells = SplitRowCells(rowLine);
                var row = new List<string>(table.Headers.Count);
                for (int c = 0; c < table.Headers.Count; c++)
                {
                    string val = c < rawCells.Count ? CleanCellMarkdown(rawCells[c]) : string.Empty;
                    row.Add(val);
                }
                table.Rows.Add(row);
            }

            return table;
        }

        private static string CleanCellMarkdown(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            string val = raw.Trim();
            if (val.StartsWith("**") && val.EndsWith("**") && val.Length >= 4)
                val = val.Substring(2, val.Length - 4).Trim();
            else if (val.StartsWith("*") && val.EndsWith("*") && val.Length >= 2)
                val = val.Substring(1, val.Length - 2).Trim();
            else if (val.StartsWith("`") && val.EndsWith("`") && val.Length >= 2)
                val = val.Substring(1, val.Length - 2).Trim();
            return val;
        }

        #endregion
    }

    #region ZChatTableControl Component

    internal sealed class ZChatTableGrid : DataGridView
    {
        public ZChatTableGrid()
        {
            DoubleBuffered = true;
        }
    }

    internal sealed class ZChatTableControl : Panel
    {
        private readonly ChatMarkdownTableBlock _tableBlock;
        private readonly ZChatTableGrid _grid;
        private readonly Panel _topBar;
        private readonly Button _copyButton;
        private readonly Label _titleLabel;

        public ZChatTableControl(ChatMarkdownTableBlock tableBlock, int targetWidth, bool isDark)
        {
            _tableBlock = tableBlock;

            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);

            // Palette colors aligned with ZeroTheme
            Color bg = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(255, 255, 255);
            Color headerBg = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(241, 245, 249);
            Color headerFore = isDark ? Color.FromArgb(147, 197, 253) : Color.FromArgb(30, 41, 59);
            Color textFore = isDark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(15, 23, 42);
            Color rowBg = isDark ? Color.FromArgb(17, 24, 39) : Color.FromArgb(255, 255, 255);
            Color altRowBg = isDark ? Color.FromArgb(24, 32, 47) : Color.FromArgb(248, 250, 252);
            Color gridLine = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);
            Color borderColor = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225);
            Color selectBg = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(224, 231, 255);
            Color selectFore = isDark ? Color.White : Color.FromArgb(30, 27, 75);

            BackColor = bg;
            Padding = new Padding(1);

            // 1. Top bar
            _topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 26,
                BackColor = headerBg,
                Padding = new Padding(6, 2, 6, 2)
            };

            _titleLabel = new Label
            {
                Text = $"📊 Bảng dữ liệu ({tableBlock.Rows.Count} dòng)",
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                ForeColor = headerFore,
                AutoSize = true,
                Location = new Point(6, 5)
            };
            _topBar.Controls.Add(_titleLabel);

            _copyButton = new Button
            {
                Text = "📋 Sao chép",
                Font = new Font("Segoe UI", 7.75f),
                ForeColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(71, 85, 105),
                BackColor = isDark ? Color.FromArgb(40, 53, 73) : Color.FromArgb(226, 232, 240),
                FlatStyle = FlatStyle.Flat,
                Size = new Size(82, 20),
                Cursor = Cursors.Hand,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _copyButton.FlatAppearance.BorderSize = 0;
            _copyButton.Location = new Point(Math.Max(120, targetWidth - 92), 3);
            _copyButton.Click += OnCopyButtonClicked;
            _topBar.Controls.Add(_copyButton);

            Controls.Add(_topBar);

            // 2. DataGridView
            _grid = new ZChatTableGrid
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AllowUserToOrderColumns = false,
                RowHeadersVisible = false,
                MultiSelect = true,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithAutoHeaderText,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false,
                ScrollBars = ScrollBars.Both,
                BackgroundColor = bg,
                GridColor = gridLine,
                Font = new Font("Segoe UI", 8.75f)
            };

            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _grid.ColumnHeadersHeight = 28;
            _grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = headerBg,
                ForeColor = headerFore,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                WrapMode = DataGridViewTriState.False
            };

            _grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = rowBg,
                ForeColor = textFore,
                SelectionBackColor = selectBg,
                SelectionForeColor = selectFore,
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                WrapMode = DataGridViewTriState.False
            };

            _grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = altRowBg,
                ForeColor = textFore,
                SelectionBackColor = selectBg,
                SelectionForeColor = selectFore
            };

            // Add Columns
            for (int i = 0; i < tableBlock.Headers.Count; i++)
            {
                var align = tableBlock.Alignments[i];
                var col = new DataGridViewTextBoxColumn
                {
                    HeaderText = tableBlock.Headers[i],
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                };
                col.HeaderCell.Style.Alignment = align;
                col.DefaultCellStyle.Alignment = align;
                _grid.Columns.Add(col);
            }

            // Add Rows
            foreach (var row in tableBlock.Rows)
            {
                _grid.Rows.Add(row.ToArray());
            }

            Controls.Add(_grid);
            _grid.BringToFront();

            // Sizing calculation
            _grid.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
            int totalColsWidth = 0;
            foreach (DataGridViewColumn col in _grid.Columns)
            {
                totalColsWidth += col.Width;
            }

            if (totalColsWidth < targetWidth - 20 && _grid.Columns.Count > 0)
            {
                _grid.Columns[_grid.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            }

            int headerHeight = _grid.ColumnHeadersHeight;
            int rowHeight = 26;
            int totalRowsH = tableBlock.Rows.Count * rowHeight;
            bool hasHScroll = totalColsWidth > (targetWidth - 4);
            int scrollBarH = hasHScroll ? SystemInformation.HorizontalScrollBarHeight : 0;

            int desiredGridH = headerHeight + totalRowsH + scrollBarH + 4;
            int maxGridH = 300;
            int minGridH = headerHeight + rowHeight + scrollBarH + 2;
            int finalGridH = Math.Max(minGridH, Math.Min(maxGridH, desiredGridH));

            int totalH = _topBar.Height + finalGridH + 2;
            Size = new Size(targetWidth, totalH);

            // Border painting
            Paint += (s, e) =>
            {
                using var pen = new Pen(borderColor, 1f);
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            };
        }

        private void OnCopyButtonClicked(object? sender, EventArgs e)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Join("\t", _tableBlock.Headers));
                foreach (var row in _tableBlock.Rows)
                {
                    sb.AppendLine(string.Join("\t", row));
                }
                Clipboard.SetText(sb.ToString());

                _copyButton.Text = "✓ Đã chép!";
                var timer = new System.Windows.Forms.Timer { Interval = 1500 };
                timer.Tick += (s, ev) =>
                {
                    _copyButton.Text = "📋 Sao chép";
                    timer.Stop();
                    timer.Dispose();
                };
                timer.Start();
            }
            catch
            {
                // Clipboard fallback
            }
        }
    }

    #endregion
}
