using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Containers
{
    /// <summary>
    /// Represents a file attached to the FileAttachmentBox.
    /// </summary>
    public class FileAttachmentItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string FileName { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public long FileSizeBytes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public object? Tag { get; set; }

        public string Extension => Path.GetExtension(FileName).ToLowerInvariant();

        public string FormattedSize
        {
            get
            {
                if (FileSizeBytes < 1024) return $"{FileSizeBytes} B";
                if (FileSizeBytes < 1024 * 1024) return $"{FileSizeBytes / 1024.0:F1} KB";
                return $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB";
            }
        }

        public FileAttachmentItem() { }

        public FileAttachmentItem(string filePath)
        {
            if (File.Exists(filePath))
            {
                var fi = new FileInfo(filePath);
                FileName = fi.Name;
                FilePath = fi.FullName;
                FileSizeBytes = fi.Length;
                CreatedAt = fi.CreationTime;
            }
            else
            {
                FileName = Path.GetFileName(filePath);
                FilePath = filePath;
            }
        }
    }

    /// <summary>
    /// Event arguments for attachment operations.
    /// </summary>
    public class FileAttachmentEventArgs : EventArgs
    {
        public FileAttachmentItem Item { get; }

        public FileAttachmentEventArgs(FileAttachmentItem item)
        {
            Item = item;
        }
    }

    /// <summary>
    /// Enterprise Drag & Drop file attachment box for ERP forms, QC tickets, invoices, and inspection records.
    /// Supports Explorer drag-and-drop, clipboard screenshot pasting, extension filtering, size constraints,
    /// and built-in file actions (Open, Download, Delete).
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Containers")]
    [DefaultEvent(nameof(FilesChanged))]
    [Description("Drag & drop file attachment manager with clipboard screenshot pasting and file actions.")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroCard.bmp")]
    public class ZFileAttachmentBox : ControlBase
    {
        private readonly List<FileAttachmentItem> _files = new List<FileAttachmentItem>();

        private readonly Panel _topBar;
        private readonly Label _lblTitle;
        private readonly ZButton _btnAdd;
        private readonly ZButton _btnPaste;
        private readonly ZButton _btnClearAll;
        private readonly FlowLayoutPanel _flowList;

        private bool _readOnly = false;
        private int _maxFileSizeMb = 50;
        private string _allowedExtensions = "*.*";
        private bool _isDragOver = false;

        public event EventHandler<FileAttachmentEventArgs>? FileAdded;
        public event EventHandler<FileAttachmentEventArgs>? FileRemoved;
        public event EventHandler<FileAttachmentEventArgs>? FileOpenRequested;
        public event EventHandler? FilesChanged;

        public ZFileAttachmentBox()
        {
            Size = new Size(480, 220);
            Font = new Font("Segoe UI", 9f);
            AllowDrop = true;

            // Top Header Bar
            _topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 38,
                BackColor = Color.Transparent,
                Padding = new Padding(12, 6, 12, 4)
            };

            _lblTitle = new Label
            {
                Text = "Attachments (0)",
                Font = new Font("Segoe UI", 9.25f, FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Dock = DockStyle.Left,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _btnClearAll = new ZButton
            {
                Text = "Clear All",
                ButtonStyle = ZeroButtonStyle.Ghost,
                Width = 75,
                Height = 28,
                Dock = DockStyle.Right
            };
            _btnClearAll.Click += (s, e) => ClearFiles();

            _btnPaste = new ZButton
            {
                Text = "📋 Paste",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Width = 80,
                Height = 28,
                Dock = DockStyle.Right
            };
            _btnPaste.Click += (s, e) => PasteFromClipboard();

            _btnAdd = new ZButton
            {
                Text = "+ Add Files...",
                ButtonStyle = ZeroButtonStyle.Primary,
                Width = 100,
                Height = 28,
                Dock = DockStyle.Right
            };
            _btnAdd.Click += (s, e) => PromptAddFiles();

            _topBar.Controls.Add(_lblTitle);
            _topBar.Controls.Add(_btnAdd);
            _topBar.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 6 });
            _topBar.Controls.Add(_btnPaste);
            _topBar.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 6 });
            _topBar.Controls.Add(_btnClearAll);

            // Flow Panel for File Chips / Cards
            _flowList = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(12),
                BackColor = Color.Transparent,
                AllowDrop = true
            };
            _flowList.DragEnter += OnFlowDragEnter;
            _flowList.DragOver += OnFlowDragOver;
            _flowList.DragLeave += OnFlowDragLeave;
            _flowList.DragDrop += OnFlowDragDrop;

            Controls.Add(_flowList);
            Controls.Add(_topBar);

            DragEnter += OnFlowDragEnter;
            DragOver += OnFlowDragOver;
            DragLeave += OnFlowDragLeave;
            DragDrop += OnFlowDragDrop;

            
        }

        #region Properties

        [Category("ZeroUI - Behavior")]
        [Description("Disables adding or removing files.")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _readOnly;
            set
            {
                _readOnly = value;
                _btnAdd.Visible = !value;
                _btnPaste.Visible = !value;
                _btnClearAll.Visible = !value;
                RebuildFileCards();
                Invalidate();
            }
        }

        [Category("ZeroUI - Constraints")]
        [Description("Maximum allowed file size in megabytes.")]
        [DefaultValue(50)]
        public int MaxFileSizeMb
        {
            get => _maxFileSizeMb;
            set => _maxFileSizeMb = Math.Max(1, value);
        }

        [Category("ZeroUI - Constraints")]
        [Description("Semicolon-separated list of allowed extensions (e.g. .pdf;.xlsx;.png). Use *.* for all.")]
        [DefaultValue("*.*")]
        public string AllowedExtensions
        {
            get => _allowedExtensions;
            set => _allowedExtensions = value;
        }

        [Browsable(false)]
        public IReadOnlyList<FileAttachmentItem> Files => _files;

        #endregion

        #region File Management Actions

        public void AddFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

            var fi = new FileInfo(filePath);
            if (!ValidateFile(fi, out string error))
            {
                MessageBox.Show(error, "File Attachment Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var item = new FileAttachmentItem(filePath);
            _files.Add(item);
            RebuildFileCards();
            FileAdded?.Invoke(this, new FileAttachmentEventArgs(item));
            FilesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddFiles(IEnumerable<string> filePaths)
        {
            if (filePaths == null) return;
            bool anyAdded = false;

            foreach (var path in filePaths)
            {
                if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    if (ValidateFile(fi, out _))
                    {
                        var item = new FileAttachmentItem(path);
                        _files.Add(item);
                        FileAdded?.Invoke(this, new FileAttachmentEventArgs(item));
                        anyAdded = true;
                    }
                }
            }

            if (anyAdded)
            {
                RebuildFileCards();
                FilesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RemoveFile(FileAttachmentItem item)
        {
            if (_files.Remove(item))
            {
                RebuildFileCards();
                FileRemoved?.Invoke(this, new FileAttachmentEventArgs(item));
                FilesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ClearFiles()
        {
            if (_files.Count == 0) return;
            _files.Clear();
            RebuildFileCards();
            FilesChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PromptAddFiles()
        {
            if (_readOnly) return;

            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = "Select files to attach";
                ofd.Multiselect = true;
                ofd.Filter = string.Equals(_allowedExtensions, "*.*", StringComparison.OrdinalIgnoreCase)
                    ? "All Supported Files|*.*"
                    : $"Allowed Files ({_allowedExtensions})|{string.Join(";", _allowedExtensions.Split(';'))}|All Files (*.*)|*.*";

                if (ofd.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    AddFiles(ofd.FileNames);
                }
            }
        }

        public void PasteFromClipboard()
        {
            if (_readOnly) return;

            if (Clipboard.ContainsImage())
            {
                try
                {
                    using (var img = Clipboard.GetImage())
                    {
                        if (img != null)
                        {
                            string tempDir = Path.Combine(Path.GetTempPath(), "ZeroUI_Attachments");
                            Directory.CreateDirectory(tempDir);
                            string fileName = $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                            string targetPath = Path.Combine(tempDir, fileName);
                            img.Save(targetPath, System.Drawing.Imaging.ImageFormat.Png);

                            AddFile(targetPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to paste image from clipboard: {ex.Message}", "Clipboard Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else if (Clipboard.ContainsFileDropList())
            {
                var dropList = Clipboard.GetFileDropList();
                var paths = new List<string>();
                foreach (string? p in dropList)
                {
                    if (!string.IsNullOrEmpty(p)) paths.Add(p);
                }
                AddFiles(paths);
            }
        }

        private bool ValidateFile(FileInfo fi, out string error)
        {
            long maxBytes = _maxFileSizeMb * 1024L * 1024L;
            if (fi.Length > maxBytes)
            {
                error = $"File '{fi.Name}' exceeds the maximum allowed size of {_maxFileSizeMb} MB.";
                return false;
            }

            if (!string.Equals(_allowedExtensions, "*.*", StringComparison.OrdinalIgnoreCase))
            {
                string ext = fi.Extension.ToLowerInvariant();
                var allowed = _allowedExtensions.ToLowerInvariant().Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
                bool matched = false;
                foreach (var a in allowed)
                {
                    string trim = a.Trim();
                    if (trim == "*.*" || trim == ext || trim == ("*" + ext))
                    {
                        matched = true;
                        break;
                    }
                }
                if (!matched)
                {
                    error = $"File type '{ext}' is not permitted. Allowed: {_allowedExtensions}";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        #endregion

        #region Drag and Drop

        private void OnFlowDragEnter(object? sender, DragEventArgs e)
        {
            if (_readOnly) return;
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                _isDragOver = true;
                Invalidate();
            }
        }

        private void OnFlowDragOver(object? sender, DragEventArgs e)
        {
            if (_readOnly) return;
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void OnFlowDragLeave(object? sender, EventArgs e)
        {
            _isDragOver = false;
            Invalidate();
        }

        private void OnFlowDragDrop(object? sender, DragEventArgs e)
        {
            _isDragOver = false;
            Invalidate();

            if (_readOnly) return;
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[]? dropped = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (dropped != null && dropped.Length > 0)
                {
                    AddFiles(dropped);
                }
            }
        }

        #endregion

        #region Card Builder

        private void RebuildFileCards()
        {
            _lblTitle.Text = $"Attachments ({_files.Count})";
            _flowList.SuspendLayout();
            _flowList.Controls.Clear();

            if (_files.Count == 0)
            {
                var emptyLabel = new Label
                {
                    Text = _readOnly
                        ? "No files attached."
                        : "Drag & drop files here, or click '+ Add Files...' (Screenshots can be pasted directly via Ctrl+V)",
                    ForeColor = ZeroTheme.Colors.TextSecondary,
                    Font = new Font("Segoe UI", 9f, FontStyle.Italic),
                    AutoSize = true,
                    Padding = new Padding(8, 24, 8, 8)
                };
                _flowList.Controls.Add(emptyLabel);
            }
            else
            {
                foreach (var f in _files)
                {
                    _flowList.Controls.Add(CreateFileCard(f));
                }
            }

            _flowList.ResumeLayout();
        }

        private Control CreateFileCard(FileAttachmentItem item)
        {
            var card = new Panel
            {
                Width = 210,
                Height = 58,
                Margin = new Padding(0, 0, 10, 10),
                BackColor = ZeroTheme.Colors.BgCard,
                Padding = new Padding(6)
            };

            Color fileAccent = GetFileColor(item.Extension);

            card.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var b = new Rectangle(0, 0, card.Width - 1, card.Height - 1);
                using var borderPen = new Pen(ZeroTheme.Colors.BorderDefault, 1f);
                using var path = ZeroUIConfig.CreateRoundedRectangle(b, 5);
                g.DrawPath(borderPen, path);

                // Left color stripe
                using var stripeBrush = new SolidBrush(fileAccent);
                g.FillRectangle(stripeBrush, 2, 8, 3, card.Height - 16);
            };

            // Left icon
            var lblIcon = new Label
            {
                Text = GetFileGlyph(item.Extension),
                Font = new Font("Segoe UI Symbol", 12f),
                ForeColor = fileAccent,
                Width = 28,
                Height = 32,
                Location = new Point(8, 12),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Name
            var lblName = new Label
            {
                Text = item.FileName,
                Font = new Font("Segoe UI", 8.75f, FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Location = new Point(40, 8),
                Width = 120,
                Height = 18,
                AutoEllipsis = true
            };

            // Size + Date
            var lblMeta = new Label
            {
                Text = $"{item.FormattedSize} • {item.CreatedAt:MM/dd}",
                Font = new Font("Segoe UI", 7.5f),
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Location = new Point(40, 28),
                Width = 120,
                Height = 16
            };

            // Action: Open / View
            var btnOpen = new Button
            {
                Text = "👁",
                Size = new Size(20, 20),
                Location = new Point(164, 6),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnOpen.FlatAppearance.BorderSize = 0;
            btnOpen.Click += (s, e) => OpenFile(item);

            // Action: Delete (if not read-only)
            if (!_readOnly)
            {
                var btnDelete = new Button
                {
                    Text = "✕",
                    Size = new Size(20, 20),
                    Location = new Point(186, 6),
                    FlatStyle = FlatStyle.Flat,
                    ForeColor = ZeroTheme.Colors.Danger,
                    Cursor = Cursors.Hand
                };
                btnDelete.FlatAppearance.BorderSize = 0;
                btnDelete.Click += (s, e) => RemoveFile(item);
                card.Controls.Add(btnDelete);
            }

            card.Controls.Add(lblIcon);
            card.Controls.Add(lblName);
            card.Controls.Add(lblMeta);
            card.Controls.Add(btnOpen);

            return card;
        }

        private void OpenFile(FileAttachmentItem item)
        {
            FileOpenRequested?.Invoke(this, new FileAttachmentEventArgs(item));

            if (!string.IsNullOrEmpty(item.FilePath) && File.Exists(item.FilePath))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(item.FilePath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Unable to open file: {ex.Message}", "Error Opening File", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static string GetFileGlyph(string ext)
        {
            return ext switch
            {
                ".pdf" => "📄",
                ".xls" or ".xlsx" or ".csv" => "📊",
                ".doc" or ".docx" => "📝",
                ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" => "🖼",
                ".zip" or ".rar" or ".7z" => "🗜",
                ".dwg" or ".dxf" => "📐",
                _ => "📎"
            };
        }

        private static Color GetFileColor(string ext)
        {
            return ext switch
            {
                ".pdf" => Color.FromArgb(239, 68, 68),       // Red
                ".xls" or ".xlsx" or ".csv" => Color.FromArgb(16, 185, 129), // Emerald
                ".doc" or ".docx" => Color.FromArgb(37, 99, 235), // Blue
                ".png" or ".jpg" or ".jpeg" => Color.FromArgb(168, 85, 247), // Purple
                ".zip" or ".rar" => Color.FromArgb(245, 158, 11),  // Amber
                ".dwg" or ".dxf" => Color.FromArgb(14, 165, 233),  // Sky
                _ => Color.FromArgb(100, 116, 139)                 // Slate
            };
        }

        #endregion

        #region Paint Drag Overlay

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = ZeroUIConfig.GetEffectiveRadius(6);

            // Base Border
            using (var borderPen = new Pen(_isDragOver ? ZeroTheme.Colors.PrimaryAccent : ZeroTheme.Colors.BorderDefault, _isDragOver ? 2f : 1f))
            {
                if (_isDragOver)
                {
                    borderPen.DashStyle = DashStyle.Dash;
                }
                using var path = ZeroUIConfig.CreateRoundedRectangle(bounds, radius);
                g.DrawPath(borderPen, path);
            }

            // Drag overlay badge
            if (_isDragOver)
            {
                using var overlayBrush = new SolidBrush(Color.FromArgb(32, ZeroTheme.Colors.PrimaryAccent));
                using var path = ZeroUIConfig.CreateRoundedRectangle(bounds, radius);
                g.FillPath(overlayBrush, path);
            }
        }

        #endregion
    
    protected override void OnThemeChanged(ZeroSkin skin)
    {
        base.OnThemeChanged(skin);
        _lblTitle.ForeColor = ZeroTheme.Colors.TextPrimary;
                RebuildFileCards();
                Invalidate();
    }

}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZFileAttachmentBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("FileAttachmentBox is deprecated and will be removed in 5 release cycles. Please migrate to ZFileAttachmentBox instead.")]
    [ToolboxItem(false)]
    public class FileAttachmentBox : ZFileAttachmentBox
    {
    }

    #endregion
}
