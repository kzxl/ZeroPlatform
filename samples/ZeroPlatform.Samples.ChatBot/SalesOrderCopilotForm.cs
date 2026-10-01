using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.Core.AiMl;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.Documents;
using ZeroUI.WinForms.Theme;

namespace ZeroPlatform.Samples.ChatBot
{
    public sealed class SalesOrderCopilotForm : Form
    {
        private readonly HybridEntityMatcher _matcher = new();
        private readonly OrderDocumentExtractor _extractor;
        private OrderDraft _currentOrder = new();
        private readonly Stack<OrderDraftSnapshot> _undoStack = new();

        // Conversational Subject & Dialogue State Tracking (Anaphora & Coreference Resolution)
        private MasterCustomer? _lastActiveCustomer;
        private MasterProduct? _lastActiveProduct;
        private int? _lastActiveLineIndex;

        private Label _lblContextCustomer = null!;
        private Label _lblContextProduct = null!;

        // Left Form Controls
        private TextBox _txtOrderCode = null!;
        private ComboBox _cboCustomer = null!;
        private DateTimePicker _dtpOrderDate = null!;
        private DateTimePicker _dtpPromiseDate = null!;
        private ComboBox _cboDefaultWarehouse = null!;
        private TextBox _txtNotes = null!;
        private DataGridView _gridItems = null!;
        private Label _lblTotalAmount = null!;
        private Label _lblStatusBadge = null!;

        // Right AI Copilot
        private ZAiChatBox _chatBox = null!;

        public SalesOrderCopilotForm()
        {
            _extractor = new OrderDocumentExtractor(_matcher);

            Text = "ZeroPlatform - Autonomous Sales Order Copilot (Conversational ERP Form)";
            Size = new Size(1440, 900);
            MinimumSize = new Size(1180, 750);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(15, 23, 42); // Slate 900
            ForeColor = Color.FromArgb(241, 245, 249);
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);

            InitializeComponents();
            BindCustomerCatalog();
        }

        private void InitializeComponents()
        {
            // 1. Top Global Banner
            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(30, 41, 59),
                Padding = new Padding(20, 10, 20, 10)
            };

            var lblTitle = new Label
            {
                Text = "🛒 ZEROPLATFORM ENTERPRISE SALES ORDER COPILOT",
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(248, 250, 252),
                AutoSize = true,
                Location = new Point(16, 8)
            };
            pnlTop.Controls.Add(lblTitle);

            var lblSub = new Label
            {
                Text = "Bán hàng thông minh: Thêm đơn hàng bằng một câu nói hoặc tải lên ảnh chụp / tệp PDF (Tự học mã hàng, Anaphora, Undo 2 chiều)",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Location = new Point(18, 32)
            };
            pnlTop.Controls.Add(lblSub);

            Controls.Add(pnlTop);

            // 2. Main Dual-Pane Splitter
            var splitMain = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel2,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(30, 41, 59)
            };
            splitMain.Panel1.BackColor = Color.FromArgb(15, 23, 42);
            splitMain.Panel2.BackColor = Color.FromArgb(15, 23, 42);

            BuildOrderFormPanel(splitMain.Panel1);
            BuildChatDrawerPanel(splitMain.Panel2);

            Controls.Add(splitMain);

            // Configure boundaries and initial responsive distance once form is sized
            void UpdateSplitter()
            {
                if (WindowState != FormWindowState.Minimized && !splitMain.IsDisposed && splitMain.Width > 600)
                {
                    try
                    {
                        int minP1 = Math.Min(400, splitMain.Width / 2);
                        int minP2 = Math.Min(360, splitMain.Width / 3);
                        splitMain.Panel1MinSize = minP1;
                        splitMain.Panel2MinSize = minP2;

                        int chatW = 460;
                        int targetDist = Math.Max(minP1, splitMain.Width - chatW);
                        targetDist = Math.Min(targetDist, splitMain.Width - minP2);
                        if (targetDist > minP1 && targetDist < splitMain.Width - minP2)
                        {
                            splitMain.SplitterDistance = targetDist;
                        }
                    }
                    catch
                    {
                        // Ignore boundary transient exceptions during resizing
                    }
                }
            }

            Load += (s, e) => UpdateSplitter();
            Resize += (s, e) => UpdateSplitter();
        }

        private void BuildOrderFormPanel(Panel parent)
        {
            var pnlContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16),
                BackColor = Color.FromArgb(15, 23, 42)
            };

            // Order Header Card
            var pnlHeaderCard = new Panel
            {
                Dock = DockStyle.Top,
                Height = 160,
                BackColor = Color.FromArgb(24, 34, 53),
                Padding = new Padding(16)
            };
            pnlHeaderCard.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(51, 65, 85));
                e.Graphics.DrawRectangle(p, 0, 0, pnlHeaderCard.Width - 1, pnlHeaderCard.Height - 1);
            };

            var lblFormTitle = new Label
            {
                Text = "THÔNG TIN CHỨNG TỪ BÁN HÀNG",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(56, 189, 248),
                AutoSize = true,
                Location = new Point(14, 10)
            };
            pnlHeaderCard.Controls.Add(lblFormTitle);

            _lblStatusBadge = new Label
            {
                Text = "● DRAFT (ĐƠN TẠM)",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 211, 153),
                BackColor = Color.FromArgb(20, 52, 211, 153),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(6, 2, 6, 2),
                AutoSize = true,
                Location = new Point(pnlHeaderCard.Width - 170, 10),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            pnlHeaderCard.Controls.Add(_lblStatusBadge);

            // Row 1: Số đơn & Khách hàng
            var lblCode = new Label { Text = "Số đơn hàng:", Location = new Point(16, 42), AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184) };
            _txtOrderCode = new TextBox { Text = _currentOrder.OrderCode, ReadOnly = true, Location = new Point(110, 39), Width = 160, BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            pnlHeaderCard.Controls.Add(lblCode);
            pnlHeaderCard.Controls.Add(_txtOrderCode);

            var lblCus = new Label { Text = "Khách hàng:", Location = new Point(290, 42), AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184) };
            _cboCustomer = new ComboBox { Location = new Point(380, 39), Width = 320, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White };
            _cboCustomer.SelectedIndexChanged += (s, e) =>
            {
                if (_cboCustomer.SelectedItem is MasterCustomer c)
                {
                    _currentOrder.CustomerCode = c.Code;
                    _currentOrder.CustomerName = c.Name;
                    _currentOrder.DefaultWarehouse = c.DefaultWarehouse;
                    SetActiveCustomerSubject(c);
                }
            };
            pnlHeaderCard.Controls.Add(lblCus);
            pnlHeaderCard.Controls.Add(_cboCustomer);

            // Row 2: Ngày đặt, Ngày hẹn giao & Kho
            var lblDate = new Label { Text = "Ngày đặt:", Location = new Point(16, 78), AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184) };
            _dtpOrderDate = new DateTimePicker { Location = new Point(110, 75), Width = 160, Format = DateTimePickerFormat.Short };
            pnlHeaderCard.Controls.Add(lblDate);
            pnlHeaderCard.Controls.Add(_dtpOrderDate);

            var lblPromise = new Label { Text = "Ngày giao:", Location = new Point(290, 78), AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184) };
            _dtpPromiseDate = new DateTimePicker { Location = new Point(380, 75), Width = 140, Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddDays(3) };
            pnlHeaderCard.Controls.Add(lblPromise);
            pnlHeaderCard.Controls.Add(_dtpPromiseDate);

            var lblWh = new Label { Text = "Kho mặc định:", Location = new Point(540, 78), AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184) };
            _cboDefaultWarehouse = new ComboBox { Location = new Point(640, 75), Width = 110, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White };
            _cboDefaultWarehouse.Items.AddRange(new object[] { "Kho 01", "Kho 02", "Kho Tổng" });
            _cboDefaultWarehouse.SelectedIndex = 0;
            pnlHeaderCard.Controls.Add(lblWh);
            pnlHeaderCard.Controls.Add(_cboDefaultWarehouse);

            // Row 3: Ghi chú
            var lblNote = new Label { Text = "Diễn giải:", Location = new Point(16, 114), AutoSize = true, ForeColor = Color.FromArgb(148, 163, 184) };
            _txtNotes = new TextBox { Location = new Point(110, 111), Width = 640, BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            pnlHeaderCard.Controls.Add(lblNote);
            pnlHeaderCard.Controls.Add(_txtNotes);

            // Center DataGridView (ZeroGrid Style)
            _gridItems = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(15, 23, 42),
                GridColor = Color.FromArgb(51, 65, 85),
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                EnableHeadersVisualStyles = false
            };

            _gridItems.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
            _gridItems.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(241, 245, 249);
            _gridItems.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _gridItems.ColumnHeadersHeight = 32;

            _gridItems.DefaultCellStyle.BackColor = Color.FromArgb(24, 34, 53);
            _gridItems.DefaultCellStyle.ForeColor = Color.FromArgb(226, 232, 240);
            _gridItems.DefaultCellStyle.SelectionBackColor = Color.FromArgb(79, 70, 229);
            _gridItems.DefaultCellStyle.SelectionForeColor = Color.White;
            _gridItems.RowTemplate.Height = 28;

            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColIndex", HeaderText = "#", Width = 40, ReadOnly = true });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColSku", HeaderText = "Mã hàng", Width = 110 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColName", HeaderText = "Tên hàng hóa / Quy cách", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 200 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColWarehouse", HeaderText = "Kho xuất", Width = 90 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColQty", HeaderText = "Số lượng", Width = 80 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColUnit", HeaderText = "ĐVT", Width = 60 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColPrice", HeaderText = "Đơn giá", Width = 100 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColTotal", HeaderText = "Thành tiền", Width = 110, ReadOnly = true });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColConf", HeaderText = "Độ tin cậy AI", Width = 110, ReadOnly = true });

            _gridItems.SelectionChanged += (s, e) =>
            {
                if (_gridItems.SelectedRows.Count > 0)
                {
                    int rowIdx = _gridItems.SelectedRows[0].Index;
                    if (rowIdx >= 0 && rowIdx < _currentOrder.Items.Count)
                    {
                        var item = _currentOrder.Items[rowIdx];
                        var prod = _matcher.Products.FirstOrDefault(p => p.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase))
                                   ?? new MasterProduct(item.Sku, item.ItemName, item.UnitPrice, item.Unit, item.Warehouse);
                        SetActiveProductSubject(prod, rowIdx + 1);
                    }
                }
            };

            // Bottom Summary & Actions Panel
            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.FromArgb(24, 34, 53),
                Padding = new Padding(16, 12, 16, 12)
            };

            _lblTotalAmount = new Label
            {
                Text = "TỔNG TIỀN: 0 đ",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 211, 153),
                Dock = DockStyle.Right,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleRight
            };
            pnlBottom.Controls.Add(_lblTotalAmount);

            var btnSave = new Button
            {
                Text = "✅ Lưu & Ghi sổ",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(16, 185, 129),
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Left,
                Width = 140,
                Cursor = Cursors.Hand
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) =>
            {
                _lblStatusBadge.Text = "● APPROVED (ĐÃ LƯU & GHI SỔ)";
                _lblStatusBadge.ForeColor = Color.FromArgb(16, 185, 129);
                MessageBox.Show(this, $"Đơn hàng {_currentOrder.OrderCode} cho {_currentOrder.CustomerName} đã được lưu thành công vào CSDL với tổng tiền {_lblTotalAmount.Text}!", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            var btnReset = new Button
            {
                Text = "🗑 Làm mới đơn",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(203, 213, 225),
                BackColor = Color.FromArgb(51, 65, 85),
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Left,
                Width = 120,
                Margin = new Padding(10, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            btnReset.FlatAppearance.BorderSize = 0;
            btnReset.Click += (s, e) => ResetOrderDraft();

            pnlBottom.Controls.Add(btnReset);
            pnlBottom.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 10 });
            pnlBottom.Controls.Add(btnSave);

            // Context Subject Tracker Bar (Visual Discourse Entity Status)
            var pnlContextBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.FromArgb(20, 30, 48),
                Padding = new Padding(12, 6, 12, 6)
            };
            pnlContextBar.Paint += (s, e) =>
            {
                using var p = new Pen(Color.FromArgb(40, 56, 80));
                e.Graphics.DrawRectangle(p, 0, 0, pnlContextBar.Width - 1, pnlContextBar.Height - 1);
            };

            var lblCtxIcon = new Label
            {
                Text = "🎯 CHỦ THỂ NGỮ CẢNH:",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(226, 232, 240),
                AutoSize = true,
                Location = new Point(14, 7)
            };
            pnlContextBar.Controls.Add(lblCtxIcon);

            _lblContextCustomer = new Label
            {
                Text = "🏢 Khách: (Chưa chọn)",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Location = new Point(175, 7),
                AutoEllipsis = true,
                MaximumSize = new Size(320, 20)
            };
            pnlContextBar.Controls.Add(_lblContextCustomer);

            _lblContextProduct = new Label
            {
                Text = "📦 Hàng hóa: (Chưa chọn)",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Location = new Point(510, 7),
                AutoEllipsis = true,
                MaximumSize = new Size(380, 20)
            };
            pnlContextBar.Controls.Add(_lblContextProduct);

            pnlContextBar.Resize += (s, e) =>
            {
                int mid = Math.Max(260, pnlContextBar.Width / 2);
                _lblContextProduct.Location = new Point(mid, 7);
                _lblContextCustomer.MaximumSize = new Size(mid - 185, 20);
                _lblContextProduct.MaximumSize = new Size(pnlContextBar.Width - mid - 20, 20);
            };

            var spacerTop = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            pnlContainer.Controls.Add(pnlBottom);
            pnlContainer.Controls.Add(pnlContextBar);
            pnlContainer.Controls.Add(spacerTop);
            pnlContainer.Controls.Add(pnlHeaderCard);
            pnlContainer.Controls.Add(_gridItems);
            _gridItems.BringToFront();
            parent.Controls.Add(pnlContainer);
        }

        private void BuildChatDrawerPanel(Panel parent)
        {
            _chatBox = new ZAiChatBox
            {
                Dock = DockStyle.Fill,
                AssistantName = "Trợ lý AI",
                ModelName = "Sales Copilot (Reflex + Vision)"
            };

            // Setup Prompt Chips with Anaphora & Coreference demonstrations
            _chatBox.PromptSuggestions.Add("Thêm cho Không Gian Mới 2 Nồi chiên và 3 Máy chiếu");
            _chatBox.PromptSuggestions.Add("Hàng IPRO001 còn bao nhiêu trong kho?");
            _chatBox.PromptSuggestions.Add("Thêm 5 cái vào đơn cho họ");
            _chatBox.PromptSuggestions.Add("Đổi kho của nó thành Kho 02");
            _chatBox.PromptSuggestions.Add("Đổi số lượng của máy chiếu thành 5");
            _chatBox.PromptSuggestions.Add("Đổi giá của máy chiếu thành 3.5tr");
            _chatBox.PromptSuggestions.Add("Xóa dòng thứ 2");
            _chatBox.PromptSuggestions.Add("Hạn mức công nợ của họ là bao nhiêu?");
            _chatBox.PromptSuggestions.Add("Lưu và ghi sổ đơn hàng");

            _chatBox.AppendAssistantMessage(
                "Xin chào! Tôi là **Trợ lý AI Đơn hàng**.\n\n" +
                "Bạn có thể:\n" +
                "• Nhấn **[📷 Tải hình ảnh]** hoặc **[📄 Tải tệp PDF]** để tự động đọc phiếu đặt hàng.\n" +
                "• Gõ câu lệnh tự nhiên (ví dụ: *'Thêm cho Cty Bình Minh, 20 Ghế xoay...'*).\n" +
                "• Tương tác hàng loạt (ví dụ: *'Cập nhật kho của tất cả hàng hóa thành Kho 01'*).\n\n" +
                "Mọi hành động đều có thể **[↺ Hoàn tác]** bất kỳ lúc nào!");

            _chatBox.SendMessageRequested += async (s, text) => await ProcessChatCommandAsync(text);
            _chatBox.FileAttached += async (s, fileInfo) => await ProcessFileAttachmentAsync(fileInfo.FilePath, fileInfo.FileType);
            _chatBox.UndoRequested += async (s, msgId) => await RollbackUndoAsync();
            _chatBox.ClearChatRequested += (s, e) => ResetOrderDraft();

            parent.Controls.Add(_chatBox);
        }

        private void SetActiveCustomerSubject(MasterCustomer? customer)
        {
            _lastActiveCustomer = customer;
            if (_lblContextCustomer != null && !IsDisposed)
            {
                _lblContextCustomer.Text = customer != null
                    ? $"🏢 Khách: {customer.Name}"
                    : "🏢 Khách: (Chưa chọn)";
                _lblContextCustomer.ForeColor = customer != null
                    ? Color.FromArgb(56, 189, 248)
                    : Color.FromArgb(148, 163, 184);
            }
        }

        private void SetActiveProductSubject(MasterProduct? product, int? lineIndex = null)
        {
            _lastActiveProduct = product;
            _lastActiveLineIndex = lineIndex;
            if (_lblContextProduct != null && !IsDisposed)
            {
                string linePrefix = lineIndex.HasValue ? $"[Dòng {lineIndex.Value}] " : "";
                _lblContextProduct.Text = product != null
                    ? $"📦 Hàng hóa: {linePrefix}{product.Name}"
                    : "📦 Hàng hóa: (Chưa chọn)";
                _lblContextProduct.ForeColor = product != null
                    ? Color.FromArgb(52, 211, 153)
                    : Color.FromArgb(148, 163, 184);
            }
        }

        private void BindCustomerCatalog()
        {
            _cboCustomer.Items.Clear();
            foreach (var c in _matcher.Customers)
            {
                _cboCustomer.Items.Add(c);
            }
            _cboCustomer.DisplayMember = "Name";
            _cboCustomer.ValueMember = "Code";
            if (_cboCustomer.Items.Count > 0)
            {
                _cboCustomer.SelectedIndex = 0;
                if (_cboCustomer.SelectedItem is MasterCustomer first)
                {
                    _currentOrder.CustomerCode = first.Code;
                    _currentOrder.CustomerName = first.Name;
                    _currentOrder.DefaultWarehouse = first.DefaultWarehouse;
                    SetActiveCustomerSubject(first);
                }
            }
        }

        #region Order Form Synchronization & Refresh

        private void RefreshGridFromOrder()
        {
            _gridItems.Rows.Clear();
            int idx = 1;

            foreach (var item in _currentOrder.Items)
            {
                item.LineIndex = idx;
                int rowIdx = _gridItems.Rows.Add(
                    idx,
                    item.Sku,
                    item.ItemName,
                    item.Warehouse,
                    item.Quantity,
                    item.Unit,
                    item.UnitPrice.ToString("#,##0") + " đ",
                    item.TotalAmount.ToString("#,##0") + " đ",
                    $"{item.Confidence * 100:F0}%"
                );

                // Highlight low confidence in amber
                if (item.IsLowConfidence)
                {
                    _gridItems.Rows[rowIdx].DefaultCellStyle.BackColor = Color.FromArgb(69, 26, 3);
                    _gridItems.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.FromArgb(253, 230, 138);
                }

                idx++;
            }

            _txtOrderCode.Text = _currentOrder.OrderCode;
            _lblTotalAmount.Text = $"TỔNG TIỀN: {_currentOrder.TotalAmount:#,##0} đ";

            // Sync Customer
            if (!string.IsNullOrEmpty(_currentOrder.CustomerCode))
            {
                for (int i = 0; i < _cboCustomer.Items.Count; i++)
                {
                    if (_cboCustomer.Items[i] is MasterCustomer c && c.Code == _currentOrder.CustomerCode)
                    {
                        if (_cboCustomer.SelectedIndex != i)
                        {
                            _cboCustomer.SelectedIndex = i;
                        }
                        break;
                    }
                }
            }

            _gridItems.Refresh();
        }

        private void CaptureUndoSnapshot(string description)
        {
            _undoStack.Push(new OrderDraftSnapshot(description, _currentOrder));
        }

        private async Task RollbackUndoAsync()
        {
            if (_undoStack.Count > 0)
            {
                var previous = _undoStack.Pop();
                var orderBeforeRollback = _currentOrder.Clone();
                _currentOrder = previous.State.Clone();
                RefreshGridFromOrder();

                // Restore active subjects from restored order
                if (!string.IsNullOrEmpty(_currentOrder.CustomerCode))
                {
                    var c = _matcher.Customers.FirstOrDefault(x => x.Code == _currentOrder.CustomerCode)
                            ?? new MasterCustomer(_currentOrder.CustomerCode, _currentOrder.CustomerName, _currentOrder.DefaultWarehouse);
                    SetActiveCustomerSubject(c);
                }
                else
                {
                    SetActiveCustomerSubject(null);
                }

                // Analyze what changed: restored items vs removed items
                var restoredItems = _currentOrder.Items
                    .Where(ni => !orderBeforeRollback.Items.Any(oi => oi.Sku.Equals(ni.Sku, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                var removedItems = orderBeforeRollback.Items
                    .Where(oi => !_currentOrder.Items.Any(ni => ni.Sku.Equals(oi.Sku, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (_currentOrder.Items.Count > 0)
                {
                    // Prioritize the restored item as the active product subject so follow-up commands work immediately
                    var activeItem = restoredItems.FirstOrDefault() ?? _currentOrder.Items.Last();
                    var p = _matcher.Products.FirstOrDefault(x => x.Sku == activeItem.Sku)
                            ?? new MasterProduct(activeItem.Sku, activeItem.ItemName, activeItem.UnitPrice, activeItem.Unit, activeItem.Warehouse);
                    SetActiveProductSubject(p, activeItem.LineIndex);
                }
                else
                {
                    SetActiveProductSubject(null, null);
                }

                _chatBox.ShowThinkingIndicator("Đang hoàn tác và khôi phục dữ liệu đơn hàng...");
                await Task.Delay(250);

                string responseMessage;
                if (restoredItems.Count > 0)
                {
                    string restoredList = string.Join("\n", restoredItems.Select(i => $"• Đã thêm lại: **{i.ItemName}** (Số lượng: **{i.Quantity} {i.Unit}** - Đơn giá: **{i.UnitPrice:#,##0} đ** - Kho: **{i.Warehouse}**)"));
                    responseMessage = $"↺ **ĐÃ HOÀN TÁC THÀNH CÔNG (ĐÃ THÊM LẠI HÀNG ĐÃ XÓA)**!\n\n" +
                                      $"Thao tác xóa đã được hủy bỏ và mặt hàng đã được **thêm lại vào đơn hàng**:\n" +
                                      $"{restoredList}\n\n" +
                                      $"👉 **Tổng giá trị đơn hàng hiện tại: {_currentOrder.TotalAmount:#,##0} đ** ({_currentOrder.Items.Count} dòng mặt hàng).\n\n" +
                                      $"*(Bạn có thể tiếp tục thao tác hoặc chỉnh sửa mặt hàng vừa thêm lại)*";
                }
                else if (removedItems.Count > 0)
                {
                    string removedList = string.Join("\n", removedItems.Select(i => $"• Đã rút khỏi đơn: **{i.ItemName}** ({i.Quantity} {i.Unit})"));
                    responseMessage = $"↺ **ĐÃ HOÀN TÁC THÀNH CÔNG**!\n\n" +
                                      $"Thao tác thêm hàng đã được hủy bỏ và các mặt hàng sau đã được rút khỏi đơn:\n" +
                                      $"{removedList}\n\n" +
                                      $"👉 **Tổng giá trị đơn hàng hiện tại: {_currentOrder.TotalAmount:#,##0} đ** ({_currentOrder.Items.Count} dòng mặt hàng).";
                }
                else
                {
                    responseMessage = $"↺ **ĐÃ HOÀN TÁC THÀNH CÔNG**!\n\n" +
                                      $"Đã khôi phục lại trạng thái chứng từ: **{previous.Description}**.\n\n" +
                                      $"👉 **Tổng giá trị đơn hàng hiện tại: {_currentOrder.TotalAmount:#,##0} đ** ({_currentOrder.Items.Count} dòng mặt hàng).";
                }

                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    responseMessage,
                    canUndo: _undoStack.Count > 0);
            }
            else
            {
                _chatBox.AppendAssistantMessage("⚠️ Không còn bước thao tác nào trước đó để hoàn tác.");
            }
        }

        private void ResetOrderDraft()
        {
            CaptureUndoSnapshot("Trước khi làm mới");
            _currentOrder = new OrderDraft();
            if (_cboCustomer.Items.Count > 0 && _cboCustomer.SelectedItem is MasterCustomer c)
            {
                _currentOrder.CustomerCode = c.Code;
                _currentOrder.CustomerName = c.Name;
                _currentOrder.DefaultWarehouse = c.DefaultWarehouse;
                SetActiveCustomerSubject(c);
            }
            else
            {
                SetActiveCustomerSubject(null);
            }
            SetActiveProductSubject(null, null);
            RefreshGridFromOrder();
        }

        #endregion

        #region Copilot Command Processing & Anaphora Resolution

        private string ResolveCopilotAnaphora(string rawInput)
        {
            if (string.IsNullOrWhiteSpace(rawInput)) return string.Empty;
            string text = rawInput.Trim();

            // 1. Customer Anaphora Resolution: "họ", "khách này", "khách hàng này", "bên này", "bên đó", "ông này", "bà này", "công ty này", "cty này", "đơn vị này"
            var activeCus = _lastActiveCustomer;
            if (activeCus == null && !string.IsNullOrEmpty(_currentOrder.CustomerName))
            {
                activeCus = _matcher.Customers.FirstOrDefault(c => c.Name.Equals(_currentOrder.CustomerName, StringComparison.OrdinalIgnoreCase) || c.Code.Equals(_currentOrder.CustomerCode, StringComparison.OrdinalIgnoreCase));
            }

            if (activeCus != null)
            {
                text = Regex.Replace(text, 
                    @"\b(họ|khách này|khách hàng này|bên này|bên đó|ông này|bà này|công ty này|cty này|đơn vị này)\b", 
                    activeCus.Name, 
                    RegexOptions.IgnoreCase);
            }

            // 2. Product Anaphora Resolution: "nó", "mặt hàng này", "sản phẩm này", "mặt hàng đó", "sản phẩm đó", "món này", "món đó", "cái đó", "con đó", "con này", "cái này", "cái vừa thêm", "mặt hàng vừa thêm", "sản phẩm vừa thêm"
            if (_lastActiveProduct != null)
            {
                text = Regex.Replace(text, 
                    @"\b(nó|mặt hàng này|sản phẩm này|mặt hàng đó|sản phẩm đó|món này|món đó|cái đó|con đó|con này|cái này|cái vừa thêm|mặt hàng vừa thêm|sản phẩm vừa thêm)\b", 
                    _lastActiveProduct.Name, 
                    RegexOptions.IgnoreCase);
            }

            // 3. Elliptical Item Addition with Implicit Active Product
            // E.g. "Thêm 5 cái vào đơn", "Thêm 5 cái", "Lấy 2 cái nữa", "Cho 3 cái vào đơn", "Đặt 10 cái cho họ", "Mua 5 con", "Thêm cho họ 5 cái"
            if (_lastActiveProduct != null)
            {
                // Pattern 3A: action [cho <cus>] <qty> [unit] [tail]
                var matchElliptical = Regex.Match(text, 
                    @"^(?:thêm|lấy|cho|đặt|mua|bổ sung)(?:\s+cho\s+([^\d,;]+?))?\s+(\d+)\s*(?:cái|bộ|chiếc|máy|con|hộp|thùng)?(?:\s+(?:vào đơn|vào|nữa|nhé|cho\s+[^\d,;]+))*$", 
                    RegexOptions.IgnoreCase);

                if (matchElliptical.Success && int.TryParse(matchElliptical.Groups[2].Value, out int qElliptical))
                {
                    string leadingCus = matchElliptical.Groups[1].Value.Trim();
                    int choIdx = text.IndexOf(" cho ", StringComparison.OrdinalIgnoreCase);
                    string cusSuffix = (choIdx > 0 && string.IsNullOrEmpty(leadingCus)) ? text.Substring(choIdx) : "";

                    if (!string.IsNullOrEmpty(leadingCus))
                    {
                        text = $"Thêm cho {leadingCus} {qElliptical} {_lastActiveProduct.Name}";
                    }
                    else
                    {
                        text = $"Thêm {qElliptical} {_lastActiveProduct.Name}{cusSuffix}";
                    }
                }
                else
                {
                    // Pattern 3B: "<qty> [unit] [tail]" (e.g. "5 cái", "10 cái vào đơn", "2 bộ nữa")
                    var matchShort = Regex.Match(text, 
                        @"^(\d+)\s*(?:cái|bộ|chiếc|máy|con|hộp|thùng)(?:\s+(?:vào đơn|vào|nữa|nhé))*$", 
                        RegexOptions.IgnoreCase);
                    if (matchShort.Success && int.TryParse(matchShort.Groups[1].Value, out int qShort))
                    {
                        text = $"Thêm {qShort} {_lastActiveProduct.Name}";
                    }
                }
            }

            return text;
        }

        private OrderItemDraft? FindOrderItem(string targetText)
        {
            if (_currentOrder.Items.Count == 0) return null;
            string clean = targetText.Trim();

            // 1. Direct match on line number: "dòng 1", "dòng thứ 2"
            var mLine = Regex.Match(clean, @"^dòng\s*(?:thứ\s*)?(\d+)$", RegexOptions.IgnoreCase);
            if (mLine.Success && int.TryParse(mLine.Groups[1].Value, out int idx))
            {
                if (idx >= 1 && idx <= _currentOrder.Items.Count)
                {
                    return _currentOrder.Items[idx - 1];
                }
            }

            // 2. Exact or substring SKU match
            var bySku = _currentOrder.Items.FirstOrDefault(i => i.Sku.Equals(clean, StringComparison.OrdinalIgnoreCase) || clean.IndexOf(i.Sku, StringComparison.OrdinalIgnoreCase) >= 0);
            if (bySku != null) return bySku;

            // 3. Match via HybridEntityMatcher
            var (matchedProd, _) = _matcher.MatchProduct(clean);
            if (matchedProd != null)
            {
                var byMatched = _currentOrder.Items.FirstOrDefault(i => i.Sku.Equals(matchedProd.Sku, StringComparison.OrdinalIgnoreCase));
                if (byMatched != null) return byMatched;
            }

            // 4. Match by ItemName substring
            var byName = _currentOrder.Items.FirstOrDefault(i => i.ItemName.IndexOf(clean, StringComparison.OrdinalIgnoreCase) >= 0 || clean.IndexOf(i.ItemName, StringComparison.OrdinalIgnoreCase) >= 0);
            if (byName != null) return byName;

            // 5. Fallback: if user target is "nó" or empty and _lastActiveProduct is available
            if ((clean.Equals("nó", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(clean)) && _lastActiveProduct != null)
            {
                var byActive = _currentOrder.Items.FirstOrDefault(i => i.Sku.Equals(_lastActiveProduct.Sku, StringComparison.OrdinalIgnoreCase));
                if (byActive != null) return byActive;
            }

            // 6. Fallback: active line index
            if (_lastActiveLineIndex.HasValue && _lastActiveLineIndex.Value >= 1 && _lastActiveLineIndex.Value <= _currentOrder.Items.Count)
            {
                return _currentOrder.Items[_lastActiveLineIndex.Value - 1];
            }

            return null;
        }

        private bool TryHandleCreditLimitInquiry(string text, out string responseMessage)
        {
            responseMessage = string.Empty;
            if (text.IndexOf("công nợ", StringComparison.OrdinalIgnoreCase) < 0 &&
                text.IndexOf("hạn mức", StringComparison.OrdinalIgnoreCase) < 0 &&
                text.IndexOf("tín dụng", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            var (cus, _) = _matcher.MatchCustomer(text);
            cus ??= _lastActiveCustomer;
            if (cus == null && !string.IsNullOrEmpty(_currentOrder.CustomerCode))
            {
                cus = _matcher.Customers.FirstOrDefault(c => c.Code.Equals(_currentOrder.CustomerCode, StringComparison.OrdinalIgnoreCase));
            }

            if (cus != null)
            {
                SetActiveCustomerSubject(cus);
                decimal currentDebt = cus.CurrentDebt;
                decimal available = Math.Max(0, cus.CreditLimit - currentDebt);
                string creditStatus = available > 0 
                    ? "TỐT (Đủ điều kiện ghi nhận đơn hàng mới)" 
                    : "CẢNH BÁO (Dư nợ vượt quá hạn mức tín dụng)";

                responseMessage = $"💳 **[TRA CỨU CÔNG NỢ & HẠN MỨC TÍN DỤNG]**\n\n" +
                                  $"• Khách hàng: **{cus.Name}** ({cus.Code})\n" +
                                  $"• Hạn mức tín dụng: **{cus.CreditLimit:#,##0} đ**\n" +
                                  $"• Dư nợ hiện tại: **{currentDebt:#,##0} đ**\n" +
                                  $"• Hạn mức còn khả dụng: **{available:#,##0} đ**\n\n" +
                                  $"👉 Trạng thái tín dụng: **{creditStatus}**";
                return true;
            }

            return false;
        }

        private bool ApplyItemQuantityChange(OrderItemDraft item, decimal newQty, int lineNum, out string responseMessage)
        {
            decimal oldQty = item.Quantity;
            CaptureUndoSnapshot($"Trước khi đổi số lượng dòng {lineNum} ({item.ItemName}) từ {oldQty} sang {newQty}");
            item.Quantity = newQty;
            RefreshGridFromOrder();

            var prod = _matcher.Products.FirstOrDefault(p => p.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase))
                       ?? new MasterProduct(item.Sku, item.ItemName, item.UnitPrice, item.Unit, item.Warehouse);
            SetActiveProductSubject(prod, lineNum);

            responseMessage = $"Xong rồi nhé! Mình đã cập nhật số lượng dòng **#{lineNum}** ({item.ItemName}):\n\n" +
                              $"• Số lượng cũ: **{oldQty} {item.Unit}** ➔ Mới: **{item.Quantity} {item.Unit}**\n" +
                              $"• Đơn giá: **{item.UnitPrice:#,##0} đ**\n" +
                              $"• Thành tiền mới: **{item.TotalAmount:#,##0} đ**\n\n" +
                              $"👉 **Tổng giá trị đơn hàng: {_currentOrder.TotalAmount:#,##0} đ**";
            return true;
        }

        private bool TryHandleItemQuantityMutation(string text, out string responseMessage)
        {
            responseMessage = string.Empty;
            if (_currentOrder.Items.Count == 0) return false;

            // Pattern 1A: By Line Number at the start: 
            // "dòng 1 đổi sl thành 40", "dòng 1 đổi sl 40", "dòng 1 sửa sl thành 40", "dòng 1 sl 40", "dòng 1 thành 40", "dòng 1 đổi số lượng 40"
            var mLineLead = Regex.Match(text, 
                @"^dòng\s*(?:thứ\s*|số\s*)?(\d+)\s+(?:(?:đổi|sửa|chỉnh|cập\s*nhật)\s+)?(?:số\s*lượng|sl|số\s*lg|slg|qty)?\s*(?:thành|là|=|lên|xuống|sang)?\s*(\d+)\s*(?:cái|bộ|chiếc|máy|con|hộp|thùng)?$", 
                RegexOptions.IgnoreCase);

            if (mLineLead.Success && 
                int.TryParse(mLineLead.Groups[1].Value, out int lineNumLead) && 
                decimal.TryParse(mLineLead.Groups[2].Value, out decimal newQtyLineLead))
            {
                if (lineNumLead >= 1 && lineNumLead <= _currentOrder.Items.Count)
                {
                    return ApplyItemQuantityChange(_currentOrder.Items[lineNumLead - 1], newQtyLineLead, lineNumLead, out responseMessage);
                }
            }

            // Pattern 1B: Action / Field first with Line Number: 
            // "đổi/sửa [sl/số lượng] dòng (thứ) X [sl/số lượng] thành/là Y" or "sl dòng 1 là 40"
            var mLineAction = Regex.Match(text, 
                @"^(?:(?:đổi|sửa|chỉnh|cập\s*nhật)\s+)?(?:số\s*lượng|sl|số\s*lg|slg|qty)\s+(?:của\s+)?dòng\s*(?:thứ\s*|số\s*)?(\d+)\s*(?:thành|là|=|lên|xuống|sang)?\s*(\d+)\s*(?:cái|bộ|chiếc|máy|con|hộp|thùng)?$", 
                RegexOptions.IgnoreCase);

            if (!mLineAction.Success)
            {
                mLineAction = Regex.Match(text, 
                    @"^(?:đổi|sửa|chỉnh|cập\s*nhật)\s+(?:của\s+)?dòng\s*(?:thứ\s*|số\s*)?(\d+)\s*(?:số\s*lượng|sl|số\s*lg|slg|qty)?\s*(?:thành|là|=|lên|xuống|sang)?\s*(\d+)\s*(?:cái|bộ|chiếc|máy|con|hộp|thùng)?$", 
                    RegexOptions.IgnoreCase);
            }

            if (mLineAction.Success && 
                int.TryParse(mLineAction.Groups[1].Value, out int lineNumAction) && 
                decimal.TryParse(mLineAction.Groups[2].Value, out decimal newQtyLineAction))
            {
                if (lineNumAction >= 1 && lineNumAction <= _currentOrder.Items.Count)
                {
                    return ApplyItemQuantityChange(_currentOrder.Items[lineNumAction - 1], newQtyLineAction, lineNumAction, out responseMessage);
                }
            }

            // Pattern 2A: Target Product Lead: 
            // "chuột đổi sl thành 50", "chuột sửa số lượng thành 5", "nó đổi sl thành 10", "chuột sl 40"
            var mTargetLead = Regex.Match(text, 
                @"^(.+?)\s+(?:đổi|sửa|chỉnh|cập\s*nhật)\s+(?:số\s*lượng|sl|số\s*lg|slg|qty)\s*(?:thành|là|=|sang)?\s*(\d+)\s*(?:cái|bộ|chiếc|máy|con|hộp|thùng)?$", 
                RegexOptions.IgnoreCase);

            if (mTargetLead.Success && decimal.TryParse(mTargetLead.Groups[2].Value, out decimal newQtyTargetLead))
            {
                string targetText = mTargetLead.Groups[1].Value.Trim();
                var item = FindOrderItem(targetText);
                if (item != null)
                {
                    return ApplyItemQuantityChange(item, newQtyTargetLead, item.LineIndex, out responseMessage);
                }
            }

            // Pattern 2B: Action / Field first with Target Product: 
            // "đổi/sửa/chỉnh [sl/số lượng] [của] <target> thành/là/= <qty>" or "sl của chuột là 50"
            var mActionTarget = Regex.Match(text, 
                @"^(?:(?:đổi|sửa|chỉnh|cập\s*nhật)\s+)?(?:số\s*lượng|sl|số\s*lg|slg|qty)\s+(?:của\s+)?(.+?)\s+(?:thành|thành\s+số\s+lượng|thành\s+sl|là|=|lên|xuống|sang)\s*(\d+)\s*(?:cái|bộ|chiếc|máy|con|hộp|thùng)?$", 
                RegexOptions.IgnoreCase);

            if (mActionTarget.Success && decimal.TryParse(mActionTarget.Groups[2].Value, out decimal newQtyActionTarget))
            {
                string targetText = mActionTarget.Groups[1].Value.Trim();
                var item = FindOrderItem(targetText);
                if (item != null)
                {
                    return ApplyItemQuantityChange(item, newQtyActionTarget, item.LineIndex, out responseMessage);
                }
            }

            // Pattern 2C: Direct item target mutation: "đổi/sửa [của] <target> thành <qty> [cái/bộ...]"
            var mDirectTarget = Regex.Match(text, 
                @"^(?:đổi|sửa|chỉnh|cập\s*nhật)\s+(?:của\s+)?(.+?)\s+(?:thành|là|=|sang)\s+(\d+)\s*(?:cái|bộ|chiếc|máy|con|hộp|thùng)?$", 
                RegexOptions.IgnoreCase);

            if (mDirectTarget.Success && decimal.TryParse(mDirectTarget.Groups[2].Value, out decimal newQtyDirect))
            {
                string targetText = mDirectTarget.Groups[1].Value.Trim();
                if (!Regex.IsMatch(targetText, @"^(?:kho|giá|đơn\s*hàng|đơn|khách\s*hàng)$", RegexOptions.IgnoreCase))
                {
                    var item = FindOrderItem(targetText);
                    if (item != null)
                    {
                        return ApplyItemQuantityChange(item, newQtyDirect, item.LineIndex, out responseMessage);
                    }
                }
            }

            // Pattern 3: Relative Increase / Decrease: "tăng/giảm [sl/số lượng] [của] <target> [lên/xuống] <qty>"
            var matchIncDec = Regex.Match(text, 
                @"^(tăng|giảm)\s+(?:số\s*lượng|sl|số\s*lg|slg|qty)?\s*(?:của\s+)?(.+?)\s+(?:thành|lên|xuống)?\s*(\d+)\s*(?:cái|bộ|chiếc|máy|con|hộp|thùng)?$", 
                RegexOptions.IgnoreCase);

            if (!matchIncDec.Success)
            {
                matchIncDec = Regex.Match(text, 
                    @"^(.+?)\s+(tăng|giảm)\s+(?:số\s*lượng|sl|số\s*lg|slg|qty)?\s*(?:thành|lên|xuống)?\s*(\d+)\s*(?:cái|bộ|chiếc|máy|con|hộp|thùng)?$", 
                    RegexOptions.IgnoreCase);
            }

            if (matchIncDec.Success)
            {
                string action = (matchIncDec.Groups[1].Value.Equals("tăng", StringComparison.OrdinalIgnoreCase) || matchIncDec.Groups[1].Value.Equals("giảm", StringComparison.OrdinalIgnoreCase))
                    ? matchIncDec.Groups[1].Value.ToLowerInvariant()
                    : matchIncDec.Groups[2].Value.ToLowerInvariant();
                string targetText = (matchIncDec.Groups[1].Value.Equals("tăng", StringComparison.OrdinalIgnoreCase) || matchIncDec.Groups[1].Value.Equals("giảm", StringComparison.OrdinalIgnoreCase))
                    ? matchIncDec.Groups[2].Value.Trim()
                    : matchIncDec.Groups[1].Value.Trim();
                string deltaStr = matchIncDec.Groups[3].Value;

                if (decimal.TryParse(deltaStr, out decimal deltaQty))
                {
                    var item = FindOrderItem(targetText);
                    if (item != null)
                    {
                        decimal oldQty = item.Quantity;
                        decimal newQty = action == "tăng" ? oldQty + deltaQty : Math.Max(1, oldQty - deltaQty);
                        CaptureUndoSnapshot($"Trước khi {action} số lượng {item.ItemName} từ {oldQty} sang {newQty}");
                        item.Quantity = newQty;
                        RefreshGridFromOrder();

                        var prod = _matcher.Products.FirstOrDefault(p => p.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase))
                                   ?? new MasterProduct(item.Sku, item.ItemName, item.UnitPrice, item.Unit, item.Warehouse);
                        SetActiveProductSubject(prod, item.LineIndex);

                        responseMessage = $"Đã {action} số lượng cho mặt hàng **{item.ItemName}** (dòng #{item.LineIndex}):\n\n" +
                                          $"• Số lượng: **{oldQty} ➔ {newQty} {item.Unit}**\n" +
                                          $"• Thành tiền mới: **{item.TotalAmount:#,##0} đ**\n\n" +
                                          $"👉 **Tổng giá trị đơn hàng: {_currentOrder.TotalAmount:#,##0} đ**";
                        return true;
                    }
                }
            }

            // Pattern 4: Elliptical quantity change when an active product subject is set: "đổi sl thành 10", "sl 10", "số lượng 10"
            if (_lastActiveProduct != null && _currentOrder.Items.Count > 0)
            {
                var matchEllip = Regex.Match(text, 
                    @"^(?:(?:đổi|sửa|chỉnh|cập\s*nhật)\s+)?(?:số\s*lượng|sl|số\s*lg|slg|qty)\s*(?:thành|là|=|sang)?\s*(\d+)\s*(?:cái|bộ|chiếc|máy|con|hộp|thùng)?$", 
                    RegexOptions.IgnoreCase);

                if (matchEllip.Success && decimal.TryParse(matchEllip.Groups[1].Value, out decimal newQtyEllip))
                {
                    var item = FindOrderItem(_lastActiveProduct.Name);
                    if (item != null)
                    {
                        return ApplyItemQuantityChange(item, newQtyEllip, item.LineIndex, out responseMessage);
                    }
                }
            }

            return false;
        }

        private bool ApplyItemPriceChange(OrderItemDraft item, decimal newPrice, int lineNum, out string responseMessage)
        {
            decimal oldPrice = item.UnitPrice;
            CaptureUndoSnapshot($"Trước khi đổi đơn giá dòng {lineNum} ({item.ItemName}) từ {oldPrice:#,##0} đ sang {newPrice:#,##0} đ");
            item.UnitPrice = newPrice;
            RefreshGridFromOrder();

            var prod = _matcher.Products.FirstOrDefault(p => p.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase))
                       ?? new MasterProduct(item.Sku, item.ItemName, item.UnitPrice, item.Unit, item.Warehouse);
            prod.Price = newPrice;
            SetActiveProductSubject(prod, lineNum);

            responseMessage = $"Xong rồi nhé! Mình đã cập nhật đơn giá dòng **#{lineNum}** ({item.ItemName}):\n\n" +
                              $"• Đơn giá cũ: **{oldPrice:#,##0} đ** ➔ Mới: **{item.UnitPrice:#,##0} đ**\n" +
                              $"• Số lượng: **{item.Quantity} {item.Unit}**\n" +
                              $"• Thành tiền mới: **{item.TotalAmount:#,##0} đ**\n\n" +
                              $"👉 **Tổng giá trị đơn hàng: {_currentOrder.TotalAmount:#,##0} đ**";
            return true;
        }

        private bool TryHandleItemPriceMutation(string text, out string responseMessage)
        {
            responseMessage = string.Empty;
            if (_currentOrder.Items.Count == 0) return false;

            // Pattern 1A: Line Lead: "dòng 1 đổi/sửa giá thành 3.5tr" or "dòng 1 giá 3.5tr"
            var mLineLead = Regex.Match(text, 
                @"^dòng\s*(?:thứ\s*|số\s*)?(\d+)\s+(?:(?:đổi|sửa|chỉnh|cập\s*nhật)\s+)?(?:đơn\s+)?giá\s*(?:thành|là|=|sang)?\s*(.+)$", 
                RegexOptions.IgnoreCase);

            if (mLineLead.Success && 
                int.TryParse(mLineLead.Groups[1].Value, out int lineNumLead) && 
                ParseVietnameseCurrency(mLineLead.Groups[2].Value) is decimal newPriceLineLead)
            {
                if (lineNumLead >= 1 && lineNumLead <= _currentOrder.Items.Count)
                {
                    return ApplyItemPriceChange(_currentOrder.Items[lineNumLead - 1], newPriceLineLead, lineNumLead, out responseMessage);
                }
            }

            // Pattern 1B: Action / Field first: "đổi/sửa [đơn] giá dòng (thứ) X thành <price>" or "đổi dòng 1 giá thành <price>"
            var mLineAction = Regex.Match(text, 
                @"^(?:(?:đổi|sửa|chỉnh|cập\s*nhật)\s+)?(?:đơn\s+)?giá\s+(?:của\s+)?dòng\s*(?:thứ\s*|số\s*)?(\d+)\s+(?:thành|là|=|sang|lên|xuống)?\s*(.+)$", 
                RegexOptions.IgnoreCase);

            if (!mLineAction.Success)
            {
                mLineAction = Regex.Match(text, 
                    @"^(?:đổi|sửa|chỉnh|cập\s*nhật)\s+(?:của\s+)?dòng\s*(?:thứ\s*|số\s*)?(\d+)\s*(?:đơn\s+)?giá\s*(?:thành|là|=|sang)?\s*(.+)$", 
                    RegexOptions.IgnoreCase);
            }

            if (mLineAction.Success && 
                int.TryParse(mLineAction.Groups[1].Value, out int lineNumAction) && 
                ParseVietnameseCurrency(mLineAction.Groups[2].Value) is decimal newPriceLineAction)
            {
                if (lineNumAction >= 1 && lineNumAction <= _currentOrder.Items.Count)
                {
                    return ApplyItemPriceChange(_currentOrder.Items[lineNumAction - 1], newPriceLineAction, lineNumAction, out responseMessage);
                }
            }

            // Pattern 2A: Target Lead: "chuột đổi giá thành 200k"
            var mTargetLead = Regex.Match(text, 
                @"^(.+?)\s+(?:đổi|sửa|chỉnh|cập\s*nhật)\s+(?:đơn\s+)?giá\s*(?:thành|là|=|sang)?\s*(.+)$", 
                RegexOptions.IgnoreCase);

            if (mTargetLead.Success && ParseVietnameseCurrency(mTargetLead.Groups[2].Value) is decimal newPriceTargetLead)
            {
                string targetText = mTargetLead.Groups[1].Value.Trim();
                var item = FindOrderItem(targetText);
                if (item != null)
                {
                    return ApplyItemPriceChange(item, newPriceTargetLead, item.LineIndex, out responseMessage);
                }
            }

            // Pattern 2B: Action / Field first: "đổi/sửa/chỉnh [đơn] giá [của] <target> thành/là/= <price>"
            var matchTarget = Regex.Match(text, 
                @"^(?:(?:đổi|sửa|chỉnh|cập\s*nhật)\s+)?(?:đơn\s+)?giá\s+(?:của\s+)?(.+?)\s+(?:thành|là|=|sang)\s+(.+)$", 
                RegexOptions.IgnoreCase);

            if (matchTarget.Success && ParseVietnameseCurrency(matchTarget.Groups[2].Value) is decimal newPriceTarget)
            {
                string targetText = matchTarget.Groups[1].Value.Trim();
                var item = FindOrderItem(targetText);
                if (item != null)
                {
                    return ApplyItemPriceChange(item, newPriceTarget, item.LineIndex, out responseMessage);
                }
            }

            // Pattern 3: Elliptical price change with active product: "đổi giá thành 3tr", "sửa giá là 850k"
            if (_lastActiveProduct != null && _currentOrder.Items.Count > 0)
            {
                var matchEllip = Regex.Match(text, 
                    @"^(?:(?:đổi|sửa|chỉnh|cập\s*nhật)\s+)?(?:đơn\s+)?giá\s+(?:thành|là|=|sang)\s+(.+)$", 
                    RegexOptions.IgnoreCase);

                if (matchEllip.Success && ParseVietnameseCurrency(matchEllip.Groups[1].Value) is decimal newPriceEllip)
                {
                    var item = FindOrderItem(_lastActiveProduct.Name);
                    if (item != null)
                    {
                        return ApplyItemPriceChange(item, newPriceEllip, item.LineIndex, out responseMessage);
                    }
                }
            }

            return false;
        }

        private static decimal? ParseVietnameseCurrency(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string text = raw.Trim().ToLowerInvariant().Replace(" ", "");

            // Check for "k": e.g. "850k", "250.5k"
            var mK = Regex.Match(text, @"^([\d.,]+)k$");
            if (mK.Success && TryParseFlexibleDecimal(mK.Groups[1].Value, out decimal valK))
            {
                return valK * 1000m;
            }

            // Check for compound "tr": e.g. "1tr5" -> 1,500,000
            var mTrCompound = Regex.Match(text, @"^(\d+)tr(\d+)$");
            if (mTrCompound.Success && decimal.TryParse(mTrCompound.Groups[1].Value, out decimal trPart) && decimal.TryParse(mTrCompound.Groups[2].Value, out decimal subPart))
            {
                decimal frac = subPart;
                while (frac >= 10) frac /= 10m;
                frac /= 10m;
                return (trPart + frac) * 1_000_000m;
            }

            // Check for "tr", "triệu": e.g. "3.5tr", "2triệu"
            var mTr = Regex.Match(text, @"^([\d.,]+)(?:tr|triệu)$");
            if (mTr.Success && TryParseFlexibleDecimal(mTr.Groups[1].Value, out decimal valTr))
            {
                return valTr * 1_000_000m;
            }

            // Check for "nghìn", "ngàn": e.g. "500nghìn"
            var mNgan = Regex.Match(text, @"^([\d.,]+)(?:nghìn|ngàn)$");
            if (mNgan.Success && TryParseFlexibleDecimal(mNgan.Groups[1].Value, out decimal valNgan))
            {
                return valNgan * 1000m;
            }

            // Plain numbers with optional đ / vnd
            string cleanNum = Regex.Replace(text, @"[^\d.,]", "");
            if (TryParseFlexibleDecimal(cleanNum, out decimal plainVal))
            {
                return plainVal;
            }

            return null;
        }

        private static bool TryParseFlexibleDecimal(string input, out decimal result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string s = input.Trim();
            if (s.Contains('.') && s.Contains(','))
            {
                if (s.LastIndexOf(',') > s.LastIndexOf('.'))
                {
                    s = s.Replace(".", "").Replace(',', '.');
                }
                else
                {
                    s = s.Replace(",", "");
                }
            }
            else if (s.Contains('.'))
            {
                int lastDot = s.LastIndexOf('.');
                if (s.Length - 1 - lastDot == 3 && s.Count(c => c == '.') > 1)
                {
                    s = s.Replace(".", "");
                }
                else if (s.Length - 1 - lastDot == 3 && s.Length > 6)
                {
                    s = s.Replace(".", "");
                }
            }
            else if (s.Contains(','))
            {
                int lastComma = s.LastIndexOf(',');
                if (s.Length - 1 - lastComma == 3 && s.Count(c => c == ',') > 1)
                {
                    s = s.Replace(",", "");
                }
                else if (s.Length - 1 - lastComma <= 2)
                {
                    s = s.Replace(',', '.');
                }
                else
                {
                    s = s.Replace(",", "");
                }
            }

            return decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result);
        }

        private bool TryHandleItemWarehouseMutation(string text, out string responseMessage)
        {
            responseMessage = string.Empty;

            // Extract warehouse target: Kho 01 | Kho 02 | Kho Tổng
            string targetWh = null!;
            if (Regex.IsMatch(text, @"kho\s*02", RegexOptions.IgnoreCase)) targetWh = "Kho 02";
            else if (Regex.IsMatch(text, @"kho\s*01", RegexOptions.IgnoreCase)) targetWh = "Kho 01";
            else if (Regex.IsMatch(text, @"kho\s*tổng", RegexOptions.IgnoreCase)) targetWh = "Kho Tổng";

            if (string.IsNullOrEmpty(targetWh)) return false;

            // Pattern A0: Line number lead: "dòng 1 đổi/chuyển kho sang Kho Y", "dòng 1 chuyển sang Kho Y", "dòng 1 kho 02"
            var mLineLead = Regex.Match(text, 
                @"^dòng\s*(?:thứ\s*|số\s*)?(\d+)\s+(?:(?:đổi|chuyển|sửa|cập\s*nhật)\s+)?kho\s*(?:thành|sang|qua|về)?\s*kho", 
                RegexOptions.IgnoreCase);

            if (!mLineLead.Success)
            {
                mLineLead = Regex.Match(text, 
                    @"^dòng\s*(?:thứ\s*|số\s*)?(\d+)\s+(?:chuyển\s+)?(?:sang|qua|về)\s+kho", 
                    RegexOptions.IgnoreCase);
            }

            if (mLineLead.Success && int.TryParse(mLineLead.Groups[1].Value, out int lineNumLead))
            {
                if (lineNumLead >= 1 && lineNumLead <= _currentOrder.Items.Count)
                {
                    var item = _currentOrder.Items[lineNumLead - 1];
                    string oldWh = item.Warehouse;
                    CaptureUndoSnapshot($"Trước khi chuyển kho dòng {lineNumLead} từ {oldWh} sang {targetWh}");
                    item.Warehouse = targetWh;
                    RefreshGridFromOrder();

                    var prod = _matcher.Products.FirstOrDefault(p => p.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase))
                               ?? new MasterProduct(item.Sku, item.ItemName, item.UnitPrice, item.Unit, item.Warehouse);
                    SetActiveProductSubject(prod, lineNumLead);

                    responseMessage = $"Đã cập nhật kho xuất của dòng **#{lineNumLead}** ({item.ItemName}):\n\n" +
                                      $"• Kho xuất cũ: **{oldWh}** ➔ Kho mới: **{targetWh}**\n" +
                                      $"• Số lượng: **{item.Quantity} {item.Unit}**";
                    return true;
                }
            }

            // Pattern A: Line number: "đổi kho dòng (thứ) X sang Kho Y"
            var matchLine = Regex.Match(text, 
                @"^(?:đổi|chuyển|sửa|cập\s*nhật)\s+kho\s+(?:dòng\s*(?:thứ\s*)?)(\d+)", 
                RegexOptions.IgnoreCase);

            if (matchLine.Success && int.TryParse(matchLine.Groups[1].Value, out int lineNum))
            {
                if (lineNum >= 1 && lineNum <= _currentOrder.Items.Count)
                {
                    var item = _currentOrder.Items[lineNum - 1];
                    string oldWh = item.Warehouse;
                    CaptureUndoSnapshot($"Trước khi chuyển kho dòng {lineNum} từ {oldWh} sang {targetWh}");
                    item.Warehouse = targetWh;
                    RefreshGridFromOrder();

                    var prod = _matcher.Products.FirstOrDefault(p => p.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase))
                               ?? new MasterProduct(item.Sku, item.ItemName, item.UnitPrice, item.Unit, item.Warehouse);
                    SetActiveProductSubject(prod, lineNum);

                    responseMessage = $"Đã cập nhật kho xuất của dòng **#{lineNum}** ({item.ItemName}):\n\n" +
                                      $"• Kho xuất cũ: **{oldWh}** ➔ Kho mới: **{targetWh}**\n" +
                                      $"• Số lượng: **{item.Quantity} {item.Unit}**";
                    return true;
                }
            }

            // Pattern B: Target product: "đổi/chuyển kho [của] <target> thành/sang/về <kho>"
            var matchProd = Regex.Match(text, 
                @"^(?:đổi|chuyển|sửa|cập\s*nhật)\s+kho\s+(?:của\s+)?(.+?)\s+(?:thành|sang|qua|về)\s+kho", 
                RegexOptions.IgnoreCase);

            if (matchProd.Success)
            {
                string targetText = matchProd.Groups[1].Value.Trim();
                var item = FindOrderItem(targetText);
                if (item != null)
                {
                    string oldWh = item.Warehouse;
                    CaptureUndoSnapshot($"Trước khi chuyển kho {item.ItemName} sang {targetWh}");
                    item.Warehouse = targetWh;
                    RefreshGridFromOrder();

                    var prod = _matcher.Products.FirstOrDefault(p => p.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase))
                               ?? new MasterProduct(item.Sku, item.ItemName, item.UnitPrice, item.Unit, item.Warehouse);
                    SetActiveProductSubject(prod, item.LineIndex);

                    responseMessage = $"Đã chuyển kho cho mặt hàng **{item.ItemName}** (dòng #{item.LineIndex}):\n\n" +
                                      $"• Kho cũ: **{oldWh}** ➔ Kho xuất mới: **{targetWh}**\n" +
                                      $"• Số lượng: **{item.Quantity} {item.Unit}**";
                    return true;
                }
            }

            // Pattern C: "chuyển <target> sang/qua kho..."
            var matchDirect = Regex.Match(text, 
                @"^chuyển\s+(.+?)\s+(?:sang|qua|về)\s+kho", 
                RegexOptions.IgnoreCase);

            if (matchDirect.Success)
            {
                string targetText = matchDirect.Groups[1].Value.Trim();
                var item = FindOrderItem(targetText);
                if (item != null)
                {
                    string oldWh = item.Warehouse;
                    CaptureUndoSnapshot($"Trước khi chuyển kho {item.ItemName} sang {targetWh}");
                    item.Warehouse = targetWh;
                    RefreshGridFromOrder();

                    var prod = _matcher.Products.FirstOrDefault(p => p.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase))
                       ?? new MasterProduct(item.Sku, item.ItemName, item.UnitPrice, item.Unit, item.Warehouse);
                    SetActiveProductSubject(prod, item.LineIndex);

                    responseMessage = $"Đã chuyển mặt hàng **{item.ItemName}** sang **{targetWh}** thành công!";
                    return true;
                }
            }

            // Pattern D: Elliptical warehouse change with active subject: "đổi kho thành Kho 02", "chuyển sang Kho 02"
            if (_lastActiveProduct != null && _currentOrder.Items.Count > 0)
            {
                if (Regex.IsMatch(text, @"^(?:đổi|chuyển|sửa)?\s*kho\s+(?:thành|sang|qua|về)?\s*kho", RegexOptions.IgnoreCase) ||
                    Regex.IsMatch(text, @"^chuyển\s+sang\s+kho", RegexOptions.IgnoreCase))
                {
                    var item = FindOrderItem(_lastActiveProduct.Name);
                    if (item != null)
                    {
                        string oldWh = item.Warehouse;
                        CaptureUndoSnapshot($"Trước khi chuyển kho {item.ItemName} sang {targetWh}");
                        item.Warehouse = targetWh;
                        RefreshGridFromOrder();

                        SetActiveProductSubject(_lastActiveProduct, item.LineIndex);

                        responseMessage = $"Đã chuyển kho của chủ thể đang chọn **{item.ItemName}** (dòng #{item.LineIndex}):\n\n" +
                                          $"• Kho cũ: **{oldWh}** ➔ Kho mới: **{targetWh}**";
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryHandleItemDeletion(string text, out string responseMessage)
        {
            responseMessage = string.Empty;

            // Pattern 1: Delete by line number: "xóa/bỏ/hủy dòng (thứ) X"
            var matchLine = Regex.Match(text, 
                @"^(?:xóa|bỏ|hủy)\s+dòng\s*(?:thứ\s*)?(\d+)", 
                RegexOptions.IgnoreCase);

            if (matchLine.Success && int.TryParse(matchLine.Groups[1].Value, out int lineNum))
            {
                if (lineNum >= 1 && lineNum <= _currentOrder.Items.Count)
                {
                    var item = _currentOrder.Items[lineNum - 1];
                    CaptureUndoSnapshot($"Trước khi xóa dòng {lineNum} ({item.ItemName})");
                    _currentOrder.Items.RemoveAt(lineNum - 1);
                    RefreshGridFromOrder();

                    SetActiveProductSubject(null, null);

                    responseMessage = $"🗑 Đã xóa dòng **#{lineNum}** (**{item.ItemName}**) ra khỏi đơn hàng!\n\n" +
                                      $"• Số mặt hàng còn lại: **{_currentOrder.Items.Count}** dòng\n" +
                                      $"• Tổng giá trị đơn sau khi xóa: **{_currentOrder.TotalAmount:#,##0} đ**\n\n" +
                                      $"*(💡 Bạn có thể bấm [↺ Hoàn tác] hoặc nói 'thêm lại' để phục hồi lại mặt hàng này bất kỳ lúc nào)*";
                    return true;
                }
            }

            // Pattern 2: Delete by item target: "xóa [mặt hàng/sản phẩm] <target>", "bỏ <target> ra"
            var matchTarget = Regex.Match(text, 
                @"^(?:xóa|hủy)\s+(?:mặt\s+hàng\s+|sản\s+phẩm\s+)?(.+?)(?:\s+đi|\s+khỏi\s+đơn)?$", 
                RegexOptions.IgnoreCase);

            if (matchTarget.Success)
            {
                string targetText = matchTarget.Groups[1].Value.Trim();
                if (targetText.Equals("đơn", StringComparison.OrdinalIgnoreCase) ||
                    targetText.Equals("đơn hàng", StringComparison.OrdinalIgnoreCase) ||
                    targetText.Equals("tất cả", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                var item = FindOrderItem(targetText);
                if (item != null)
                {
                    CaptureUndoSnapshot($"Trước khi xóa mặt hàng {item.ItemName}");
                    _currentOrder.Items.Remove(item);
                    RefreshGridFromOrder();

                    SetActiveProductSubject(null, null);

                    responseMessage = $"🗑 Đã xóa mặt hàng **{item.ItemName}** ({item.Sku}) ra khỏi đơn hàng!\n\n" +
                                      $"• Số mặt hàng còn lại: **{_currentOrder.Items.Count}** dòng\n" +
                                      $"• Tổng giá trị đơn sau khi xóa: **{_currentOrder.TotalAmount:#,##0} đ**\n\n" +
                                      $"*(💡 Bạn có thể bấm [↺ Hoàn tác] hoặc nói 'thêm lại' để phục hồi lại mặt hàng này bất kỳ lúc nào)*";
                    return true;
                }
            }

            // Pattern 3: "bỏ <target> ra [khỏi đơn]"
            var matchBo = Regex.Match(text, 
                @"^bỏ\s+(.+?)\s+ra(?:\s+khỏi\s+đơn)?$", 
                RegexOptions.IgnoreCase);

            if (matchBo.Success)
            {
                string targetText = matchBo.Groups[1].Value.Trim();
                var item = FindOrderItem(targetText);
                if (item != null)
                {
                    CaptureUndoSnapshot($"Trước khi bỏ mặt hàng {item.ItemName}");
                    _currentOrder.Items.Remove(item);
                    RefreshGridFromOrder();

                    SetActiveProductSubject(null, null);

                    responseMessage = $"🗑 Đã bỏ mặt hàng **{item.ItemName}** ({item.Sku}) ra khỏi đơn hàng!\n\n" +
                                      $"• Số mặt hàng còn lại: **{_currentOrder.Items.Count}** dòng\n" +
                                      $"• Tổng giá trị đơn sau khi xóa: **{_currentOrder.TotalAmount:#,##0} đ**\n\n" +
                                      $"*(💡 Bạn có thể bấm [↺ Hoàn tác] hoặc nói 'thêm lại' để phục hồi lại mặt hàng này bất kỳ lúc nào)*";
                    return true;
                }
            }

            return false;
        }

        private async Task ProcessChatCommandAsync(string text)
        {
            string clean = text.Trim();
            if (string.IsNullOrWhiteSpace(clean)) return;

            // 0. Pre-Processing: Dialogue State & Anaphora / Coreference Resolution
            string resolved = ResolveCopilotAnaphora(clean);

            // 0.0 Command: Undo / Hoàn tác / Thêm lại
            if (resolved.Equals("hoàn tác", StringComparison.OrdinalIgnoreCase) ||
                resolved.Equals("undo", StringComparison.OrdinalIgnoreCase) ||
                resolved.Equals("quay lại", StringComparison.OrdinalIgnoreCase) ||
                resolved.Equals("thêm lại", StringComparison.OrdinalIgnoreCase) ||
                resolved.Equals("thêm lại đi", StringComparison.OrdinalIgnoreCase) ||
                resolved.Equals("khôi phục lại", StringComparison.OrdinalIgnoreCase) ||
                resolved.IndexOf("thêm lại món vừa xóa", StringComparison.OrdinalIgnoreCase) >= 0 ||
                resolved.IndexOf("thêm lại hàng vừa xóa", StringComparison.OrdinalIgnoreCase) >= 0 ||
                resolved.IndexOf("thêm lại mặt hàng vừa xóa", StringComparison.OrdinalIgnoreCase) >= 0 ||
                resolved.IndexOf("thêm lại sản phẩm vừa xóa", StringComparison.OrdinalIgnoreCase) >= 0 ||
                resolved.IndexOf("thêm lại dòng vừa xóa", StringComparison.OrdinalIgnoreCase) >= 0 ||
                resolved.IndexOf("lấy lại món vừa xóa", StringComparison.OrdinalIgnoreCase) >= 0 ||
                resolved.IndexOf("hoàn tác xóa", StringComparison.OrdinalIgnoreCase) >= 0 ||
                resolved.IndexOf("hủy thao tác xóa", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                await RollbackUndoAsync();
                return;
            }

            // 0.1 Command: Quick Reset
            if (resolved.Equals("làm mới", StringComparison.OrdinalIgnoreCase) ||
                resolved.Equals("làm mới đơn", StringComparison.OrdinalIgnoreCase) ||
                resolved.Equals("xóa đơn", StringComparison.OrdinalIgnoreCase) ||
                resolved.Equals("hủy đơn", StringComparison.OrdinalIgnoreCase))
            {
                ResetOrderDraft();
                _chatBox.ShowThinkingIndicator("Đang làm mới chứng từ...");
                await Task.Delay(200);
                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    $"🗑 Đã làm mới chứng từ đơn hàng thành công!\n\n" +
                    $"• Mã chứng từ mới: **{_currentOrder.OrderCode}**\n" +
                    $"• Khách hàng hiện tại: **{_currentOrder.CustomerName}**\n\n" +
                    "Bạn có thể bắt đầu đọc hoặc gõ câu lệnh để thêm mặt hàng vào đơn.",
                    canUndo: true);
                return;
            }

            // 0.2 Command: Save & Post order
            if (resolved.IndexOf("lưu và ghi sổ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                resolved.IndexOf("lưu đơn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                resolved.IndexOf("ghi sổ", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _lblStatusBadge.Text = "● APPROVED (ĐÃ LƯU & GHI SỔ)";
                _lblStatusBadge.ForeColor = Color.FromArgb(16, 185, 129);
                _chatBox.ShowThinkingIndicator("Đang kiểm tra và ghi sổ chứng từ vào CSDL...");
                await Task.Delay(300);
                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    $"✅ **ĐÃ LƯU & GHI SỔ CHỨNG TỪ THÀNH CÔNG!**\n\n" +
                    $"• Mã chứng từ: **{_currentOrder.OrderCode}**\n" +
                    $"• Khách hàng: **{_currentOrder.CustomerName}**\n" +
                    $"• Tổng số lượng: **{_currentOrder.Items.Sum(i => i.Quantity)}** sản phẩm ({_currentOrder.Items.Count} dòng)\n" +
                    $"• Tổng tiền thanh toán: **{_currentOrder.TotalAmount:#,##0} đ**\n\n" +
                    "Chứng từ đã được chuyển sang trạng thái phê duyệt và khóa sổ.",
                    canUndo: false);
                return;
            }

            // 0.3 Command: Customer Credit Limit Inquiry (e.g. "Hạn mức công nợ của họ là bao nhiêu?", "Công nợ khách này")
            if (TryHandleCreditLimitInquiry(resolved, out string creditMsg))
            {
                _chatBox.ShowThinkingIndicator("Đang truy vấn sổ cái công nợ khách hàng...");
                await Task.Delay(250);
                await _chatBox.AppendAssistantActionMessageAnimatedAsync(creditMsg, canUndo: false);
                return;
            }

            // 1. Single-Item Mutation: Quantity update ("Đổi số lượng của máy chiếu thành 5", "Đổi số lượng của nó thành 10", "Đổi số lượng dòng 1 thành 8")
            if (TryHandleItemQuantityMutation(resolved, out string qtyMsg))
            {
                _chatBox.ShowThinkingIndicator("Đang cập nhật số lượng mặt hàng...");
                await Task.Delay(250);
                await _chatBox.AppendAssistantActionMessageAnimatedAsync(qtyMsg, canUndo: true);
                return;
            }

            // 1.1 Single-Item Mutation: Price update ("Đổi giá của máy chiếu thành 3.5tr", "Sửa giá dòng 1 thành 850k")
            if (TryHandleItemPriceMutation(resolved, out string priceMsg))
            {
                _chatBox.ShowThinkingIndicator("Đang cập nhật đơn giá mặt hàng...");
                await Task.Delay(250);
                await _chatBox.AppendAssistantActionMessageAnimatedAsync(priceMsg, canUndo: true);
                return;
            }

            // 2. Single-Item Mutation: Warehouse update ("Đổi kho của nó thành Kho 02", "Chuyển máy chiếu sang Kho 02", "Đổi kho dòng 2 thành Kho 02")
            if (TryHandleItemWarehouseMutation(resolved, out string whMsg))
            {
                _chatBox.ShowThinkingIndicator("Đang cập nhật kho xuất...");
                await Task.Delay(250);
                await _chatBox.AppendAssistantActionMessageAnimatedAsync(whMsg, canUndo: true);
                return;
            }

            // 3. Single-Item Mutation: Delete item from order ("Xóa máy chiếu", "Xóa nó đi", "Xóa dòng thứ 2", "Bỏ chuột ra")
            if (TryHandleItemDeletion(resolved, out string delMsg))
            {
                _chatBox.ShowThinkingIndicator("Đang xóa mặt hàng khỏi đơn...");
                await Task.Delay(250);
                await _chatBox.AppendAssistantActionMessageAnimatedAsync(delMsg, canUndo: true);
                return;
            }

            // 4. Batch update warehouse (only when explicitly batch: "tất cả", "toàn bộ", "hàng loạt")
            bool isBatchWh = (resolved.IndexOf("tất cả", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              resolved.IndexOf("toàn bộ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              resolved.IndexOf("hàng loạt", StringComparison.OrdinalIgnoreCase) >= 0) &&
                             (resolved.IndexOf("kho", StringComparison.OrdinalIgnoreCase) >= 0);

            if (isBatchWh || (resolved.IndexOf("cập nhật kho", StringComparison.OrdinalIgnoreCase) >= 0 && resolved.IndexOf("của tất cả", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                CaptureUndoSnapshot("Trước khi cập nhật kho hàng loạt");
                string targetWh = "Kho 01";
                if (resolved.IndexOf("Kho 02", StringComparison.OrdinalIgnoreCase) >= 0) targetWh = "Kho 02";
                else if (resolved.IndexOf("Kho Tổng", StringComparison.OrdinalIgnoreCase) >= 0) targetWh = "Kho Tổng";

                foreach (var item in _currentOrder.Items)
                {
                    item.Warehouse = targetWh;
                }
                RefreshGridFromOrder();

                _chatBox.ShowThinkingIndicator($"Đang đồng bộ kho hàng '{targetWh}'...");
                await Task.Delay(250);

                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    $"Xong rồi nhé! Mình đã cập nhật kho của tất cả **{_currentOrder.Items.Count}** mặt hàng thành **{targetWh}**.\n\n" +
                    "Bạn có muốn mình hỗ trợ thêm việc nào khác như cập nhật thông tin khác hay thêm hàng hóa mới không?",
                    canUndo: true);
                return;
            }

            // 5. Command: Sort items
            if (resolved.IndexOf("sắp xếp", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                CaptureUndoSnapshot("Trước khi sắp xếp");
                _currentOrder.Items = _currentOrder.Items.OrderBy(i => i.Sku).ToList();
                RefreshGridFromOrder();

                _chatBox.ShowThinkingIndicator("Đang sắp xếp danh mục mặt hàng...");
                await Task.Delay(200);

                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    $"Đã sắp xếp lại thứ tự **{_currentOrder.Items.Count}** mặt hàng theo mã SKU tăng dần.",
                    canUndo: true);
                return;
            }

            // 6. Command: Inventory query (e.g. "Hàng IPRO001 còn bao nhiêu trong kho?", "Kiểm tra tồn kho nó")
            if (resolved.IndexOf("còn bao nhiêu trong kho", StringComparison.OrdinalIgnoreCase) >= 0 ||
                resolved.IndexOf("tồn kho", StringComparison.OrdinalIgnoreCase) >= 0 ||
                resolved.IndexOf("kiểm tra tồn", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var (p, _) = _matcher.MatchProduct(resolved);
                p ??= _lastActiveProduct;
                p ??= _matcher.Products.FirstOrDefault();

                if (p != null)
                {
                    SetActiveProductSubject(p, null);
                    _chatBox.ShowThinkingIndicator("Đang truy vấn số dư tồn kho thời gian thực...");
                    await Task.Delay(300);

                    var (wh1, wh2, whTong, available) = _matcher.GetProductStock(p.Sku);
                    int total = wh1 + wh2 + whTong;
                    int reserved = Math.Max(0, total - available);

                    await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                        $"📊 **[TRA CỨU TỒN KHO THỜI GIAN THỰC]**\n\n" +
                        $"• Mặt hàng: **{p.Name}** ({p.Sku})\n" +
                        $"• Kho 01: **{wh1} {p.Unit}**\n" +
                        $"• Kho 02: **{wh2} {p.Unit}**\n" +
                        $"• Kho Tổng: **{whTong} {p.Unit}**\n\n" +
                        $"👉 Tổng tồn kho hệ thống: **{total} {p.Unit}** (Khả dụng: **{available} {p.Unit}**, Tạm giữ: **{reserved} {p.Unit}**).\n\n" +
                        $"*(💡 Gợi ý: Bạn có thể gõ 'Thêm 5 cái vào đơn' để đặt ngay mặt hàng này)*",
                        canUndo: false);
                    return;
                }
            }

            // 7. Command: Order status inquiry
            if (resolved.IndexOf("trạng thái", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _chatBox.ShowThinkingIndicator("Đang kiểm tra tiến trình chứng từ...");
                await Task.Delay(250);

                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    $"📋 **[THÔNG TIN CHỨNG TỪ {_currentOrder.OrderCode}]**\n\n" +
                    $"• Khách hàng: **{_currentOrder.CustomerName}**\n" +
                    $"• Số lượng mặt hàng: **{_currentOrder.Items.Count}** dòng\n" +
                    $"• Tổng tiền: **{_currentOrder.TotalAmount:#,##0} đ**\n" +
                    $"• Trạng thái chứng từ: **Đơn tạm (Chờ phê duyệt & Ghi sổ)**",
                    canUndo: false);
                return;
            }

            // 8. Command: Add order / items from text (e.g. "Thêm đơn hàng cho Công ty...", "Tạo đơn mới cho Phú Hưng 1 máy chiếu", "Thêm 5 cái vào đơn cho họ")
            _chatBox.ShowThinkingIndicator("Sales Copilot đang nhận diện khách hàng & bóc tách mặt hàng...");
            await Task.Delay(300);

            CaptureUndoSnapshot("Trước khi xử lý câu lệnh");

            bool isNewOrder = resolved.IndexOf("tạo đơn mới", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              resolved.IndexOf("lập đơn mới", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              resolved.IndexOf("tạo đơn hàng mới", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              resolved.IndexOf("đơn mới", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isNewOrder)
            {
                _currentOrder = new OrderDraft();
            }

            var (matchedCus, addedItems) = ParseAndApplyOrderFromText(resolved);

            if (matchedCus != null)
            {
                SetActiveCustomerSubject(matchedCus);
            }

            if (matchedCus != null && addedItems.Count == 0)
            {
                RefreshGridFromOrder();
                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    $"Xong rồi nhé! Mình đã khởi tạo chứng từ đơn hàng **{_currentOrder.OrderCode}** cho khách hàng:\n\n" +
                    $"• Khách hàng: **{_currentOrder.CustomerName}** ({_currentOrder.CustomerCode})\n" +
                    $"• Kho mặc định: **{_currentOrder.DefaultWarehouse}**\n\n" +
                    "Bạn muốn thêm những mặt hàng nào vào đơn? (Ví dụ: *'Thêm 20 Ghế xoay và 20 Chuột'* hoặc *'2 Nồi chiên và 3 Máy chiếu'*).",
                    canUndo: true);
                return;
            }

            if (addedItems.Count > 0)
            {
                var lastItem = addedItems.Last();
                var lastProd = _matcher.Products.FirstOrDefault(p => p.Sku.Equals(lastItem.Sku, StringComparison.OrdinalIgnoreCase))
                               ?? new MasterProduct(lastItem.Sku, lastItem.ItemName, lastItem.UnitPrice, lastItem.Unit, lastItem.Warehouse);
                SetActiveProductSubject(lastProd, _currentOrder.Items.Count);

                RefreshGridFromOrder();

                string itemsList = string.Join("\n", addedItems.Select(i => $"• **{i.ItemName}** (Số lượng: {i.Quantity} {i.Unit} - Đơn giá: {i.UnitPrice:#,##0} đ - Kho: {i.Warehouse})"));
                string cusDesc = !string.IsNullOrEmpty(_currentOrder.CustomerName)
                    ? $"• Khách hàng: **{_currentOrder.CustomerName}**\n"
                    : "";

                string anaphoraNote = (resolved != clean) 
                    ? $"\n*(🎯 Đã nhận diện đại từ & liên kết chủ thể: \"{_lastActiveProduct?.Name ?? _lastActiveCustomer?.Name}\")*" 
                    : "";

                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    $"Xong rồi nhé! Mình đã hoàn tất các thao tác cho đơn hàng **{_currentOrder.OrderCode}**:\n\n" +
                    cusDesc +
                    $"• Đã thêm các mặt hàng sau vào đơn:\n{itemsList}\n\n" +
                    $"👉 **Tổng giá trị đơn: {_currentOrder.TotalAmount:#,##0} đ** ({_currentOrder.Items.Count} dòng mặt hàng)" +
                    anaphoraNote + "\n\n" +
                    "Bạn có muốn mình hỗ trợ thêm việc nào khác như cập nhật thông tin kho hay thêm hàng hóa mới không?",
                    canUndo: true);
            }
            else
            {
                // Discard the unneeded snapshot because no modifications occurred
                if (_undoStack.Count > 0)
                {
                    _undoStack.Pop();
                }

                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    "Tôi chưa nhận diện được tên khách hàng hoặc sản phẩm trong câu nói.\n\n" +
                    "Bạn có thể nói theo mẫu:\n" +
                    "• *'Thêm cho Cty Bình Minh, 20 Ghế xoay và 20 Chuột máy tính'*\n" +
                    "• *'Thêm cho Không Gian Mới 2 Nồi chiên và 3 Máy chiếu'*\n" +
                    "• *'Tạo đơn mới cho Phú Hưng 1 máy chiếu'*\n" +
                    "• *'Đổi số lượng của nó thành 5'* hoặc *'Đổi kho của nó thành Kho 02'*",
                    canUndo: false);
            }
        }

        private (MasterCustomer? Customer, List<OrderItemDraft> AddedItems) ParseAndApplyOrderFromText(string text)
        {
            var added = new List<OrderItemDraft>();

            // 1. Match Customer: first try fuzzy matcher
            var (matchedCus, _) = _matcher.MatchCustomer(text);

            // If not found in catalog, detect ad-hoc customer introduction pattern: "cho <Customer Name>"
            if (matchedCus == null)
            {
                var matchNewCus = Regex.Match(text, 
                    @"(?:thêm|tạo\s*đơn|lập\s*đơn|đơn|bán|xuất)\s+cho\s+((?:công\s*ty|cty|doanh\s*nghiệp|khách\s*hàng|khách|anh|chị|đại\s*lý)\s+[^,;\d\n]+?)(?:,|\s+(?:với|\d|gồm|đặt|mua|lấy)|\n|$)", 
                    RegexOptions.IgnoreCase);

                if (!matchNewCus.Success)
                {
                    matchNewCus = Regex.Match(text, 
                        @"(?:cho\s+)([A-ZÀ-Ỹ][\w\s]{2,35}?)(?:,|\s+\d|\s+gồm|\s+đặt|\s+mua|\s+lấy|\n|$)", 
                        RegexOptions.IgnoreCase);
                }

                if (matchNewCus.Success)
                {
                    string candidateName = matchNewCus.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(candidateName) && candidateName.Length >= 3)
                    {
                        matchedCus = _matcher.RegisterOrGetCustomer(candidateName);
                    }
                }
            }

            if (matchedCus != null)
            {
                // Sync with Combo Box
                bool exists = false;
                for (int i = 0; i < _cboCustomer.Items.Count; i++)
                {
                    if (_cboCustomer.Items[i] is MasterCustomer mc && mc.Code.Equals(matchedCus.Code, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    _cboCustomer.Items.Add(matchedCus);
                }

                _currentOrder.CustomerCode = matchedCus.Code;
                _currentOrder.CustomerName = matchedCus.Name;
                _currentOrder.DefaultWarehouse = matchedCus.DefaultWarehouse;
            }

            // Split into clauses: comma, semicolon, " và ", " voi ", " với ", "+", newlines
            var parts = text.Split(new[] { ",", ";", " và ", " voi ", " với ", "+", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            var genericPronouns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "cái", "bộ", "chiếc", "máy", "con", "hộp", "thùng", "vào đơn", "vào", "nó", "mặt hàng này", "sản phẩm này", "món này", "nữa", "nhé"
            };

            foreach (var part in parts)
            {
                string rawPart = part.Trim();
                if (string.IsNullOrWhiteSpace(rawPart)) continue;

                // Extract custom price if present: e.g. "giá 3.5tr", "giá: 850k", "đơn giá 1.200.000 đ"
                decimal? customPrice = null;
                var matchPrice = Regex.Match(rawPart, @"(?:đơn\s*)?giá\s*[:=]?\s*([0-9.,]+\s*(?:triệu|tr|nghìn|ngàn|k|đ|vnd)?)", RegexOptions.IgnoreCase);
                if (matchPrice.Success)
                {
                    string priceRaw = matchPrice.Groups[1].Value.Trim();
                    customPrice = ParseVietnameseCurrency(priceRaw);
                    rawPart = rawPart.Remove(matchPrice.Index, matchPrice.Length).Trim();
                }

                decimal qty = 0;
                string prodText = string.Empty;
                string? detectedUnit = null;

                // Pattern A: "<qty> [unit] <product_name>" (e.g. "20 Ghế xoay", "2 cái Nồi chiên", "1 máy chiếu")
                var matchA = Regex.Match(rawPart, @"(\d+)\s*(cái|bộ|máy|chiếc|thùng|hộp|cuộn|con|bình|bao|tấn|kg)?\s+([^\d,;]+)", RegexOptions.IgnoreCase);
                if (matchA.Success && decimal.TryParse(matchA.Groups[1].Value, out decimal qA))
                {
                    qty = qA;
                    detectedUnit = matchA.Groups[2].Success && !string.IsNullOrWhiteSpace(matchA.Groups[2].Value) ? matchA.Groups[2].Value : null;
                    prodText = matchA.Groups[3].Value.Trim();
                }
                else
                {
                    // Pattern B: "<product_name> <qty> [unit]" (e.g. "Ghế xoay 20 cái", "Máy chiếu 3 bộ")
                    var matchB = Regex.Match(rawPart, @"([^\d,;]+?)\s+(\d+)\s*(cái|bộ|máy|chiếc|thùng|hộp|cuộn|con|bình|bao|tấn|kg)?$", RegexOptions.IgnoreCase);
                    if (matchB.Success && decimal.TryParse(matchB.Groups[2].Value, out decimal qB))
                    {
                        qty = qB;
                        detectedUnit = matchB.Groups[3].Success && !string.IsNullOrWhiteSpace(matchB.Groups[3].Value) ? matchB.Groups[3].Value : null;
                        prodText = matchB.Groups[1].Value.Trim();
                    }
                }

                if (qty > 0)
                {
                    // Strip customer or preposition tails if present (e.g. "ghế xoay cho Bình Minh" -> "ghế xoay")
                    int choIdx = prodText.IndexOf(" cho ", StringComparison.OrdinalIgnoreCase);
                    if (choIdx > 0) prodText = prodText.Substring(0, choIdx).Trim();
                    int cuaIdx = prodText.IndexOf(" của ", StringComparison.OrdinalIgnoreCase);
                    if (cuaIdx > 0) prodText = prodText.Substring(0, cuaIdx).Trim();
                    int taiIdx = prodText.IndexOf(" tại ", StringComparison.OrdinalIgnoreCase);
                    if (taiIdx > 0) prodText = prodText.Substring(0, taiIdx).Trim();

                    // Strip customer name if it leaked into prodText
                    if (matchedCus != null && prodText.IndexOf(matchedCus.Name, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        prodText = Regex.Replace(prodText, Regex.Escape(matchedCus.Name), "", RegexOptions.IgnoreCase).Trim();
                    }

                    // Strip leading filler words ("thêm ", "bổ sung ", "lấy ", "mua ", "bán ", "cần ", "đặt ")
                    foreach (var filler in new[] { "thêm ", "bổ sung ", "lấy ", "mua ", "bán ", "cần ", "đặt " })
                    {
                        if (prodText.StartsWith(filler, StringComparison.OrdinalIgnoreCase))
                        {
                            prodText = prodText.Substring(filler.Length).Trim();
                        }
                    }

                    var (prod, conf) = _matcher.MatchProduct(prodText);

                    // If prodText is generic or empty, but we have an active product, bind to active product!
                    if (prod == null && _lastActiveProduct != null && (string.IsNullOrWhiteSpace(prodText) || genericPronouns.Contains(prodText)))
                    {
                        prod = _lastActiveProduct;
                        conf = 0.98f;
                    }

                    // Guard: Check if prodText is a command fragment (e.g. "dòng 1 đổi sl thành", "đổi", "sửa", "sl", "kho")
                    bool isCommandPhrase = Regex.IsMatch(prodText, @"\b(dòng\s*\d*|đổi|sửa|chỉnh|cập\s*nhật|xóa|hủy|bỏ|chuyển|thành|sang|lên|xuống|kho\s*0[12]|kho\s*tổng|số\s*lượng|sl)\b", RegexOptions.IgnoreCase);

                    // Ad-hoc on-the-fly registration if not in catalog:
                    if (prod == null && !string.IsNullOrWhiteSpace(prodText) && !genericPronouns.Contains(prodText) && !isCommandPhrase && prodText.Length >= 2)
                    {
                        string cleanTitle = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(prodText.ToLowerInvariant());
                        decimal defPrice = customPrice ?? 500_000m;
                        string unit = detectedUnit ?? "Cái";
                        prod = _matcher.RegisterOrGetProduct(cleanTitle, defPrice, unit, _currentOrder.DefaultWarehouse);
                        conf = 0.80f; // low confidence (< 0.85f) -> highlighted amber in UI
                    }

                    if (prod != null)
                    {
                        decimal finalPrice = customPrice ?? prod.Price;
                        string finalUnit = detectedUnit ?? prod.Unit;

                        var item = new OrderItemDraft
                        {
                            RawDescription = prodText,
                            Sku = prod.Sku,
                            ItemName = prod.Name,
                            Quantity = qty,
                            Unit = finalUnit,
                            UnitPrice = finalPrice,
                            Warehouse = prod.PreferredWarehouse,
                            Confidence = conf
                        };
                        _currentOrder.Items.Add(item);
                        added.Add(item);
                    }
                }
            }

            return (matchedCus, added);
        }

        private async Task ProcessFileAttachmentAsync(string filePath, string fileType)
        {
            _chatBox.ShowThinkingIndicator($"Đang quét và bóc tách chứng từ {fileType.ToUpper()} ({System.IO.Path.GetFileName(filePath)})...");

            try
            {
                CaptureUndoSnapshot($"Trước khi nhập tệp {System.IO.Path.GetFileName(filePath)}");

                var extraction = await _extractor.ExtractOrderAsync(filePath, fileType);

                _chatBox.ShowThinkingIndicator("Đang so khớp danh mục SKU & phân bổ kho mặc định...");
                await Task.Delay(300);

                // Apply Customer
                var (cus, _) = _matcher.MatchCustomer(extraction.ExtractedCustomerName ?? "");
                if (cus != null)
                {
                    _currentOrder.CustomerCode = cus.Code;
                    _currentOrder.CustomerName = cus.Name;
                    SetActiveCustomerSubject(cus);
                }

                // Add Items
                foreach (var item in extraction.Items)
                {
                    _currentOrder.Items.Add(item);
                }

                if (extraction.Items.Count > 0)
                {
                    var last = extraction.Items.Last();
                    var p = _matcher.Products.FirstOrDefault(x => x.Sku.Equals(last.Sku, StringComparison.OrdinalIgnoreCase))
                            ?? new MasterProduct(last.Sku, last.ItemName, last.UnitPrice, last.Unit, last.Warehouse);
                    SetActiveProductSubject(p, _currentOrder.Items.Count);
                }

                RefreshGridFromOrder();

                string itemsList = string.Join("\n", extraction.Items.Select(i => $"• **{i.ItemName}** (Số lượng: {i.Quantity} {i.Unit} - Đơn giá: {i.UnitPrice:#,##0} đ)"));

                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    $"📄 **[ĐÃ TRÍCH XUẤT THÀNH CÔNG ĐƠN HÀNG TỪ {fileType.ToUpper()}]**\n\n" +
                    $"• Số PO trích xuất: **{extraction.ExtractedPoNumber}**\n" +
                    $"• Khách hàng nhận diện: **{_currentOrder.CustomerName}**\n" +
                    $"• {extraction.RawOcrSummary}\n\n" +
                    $"Đã thêm **{extraction.Items.Count}** mặt hàng vào lưới đơn hàng:\n{itemsList}\n\n" +
                    $"👉 Bạn có thể kiểm tra các ô được tô sáng và bấm **[↺ Hoàn tác]** nếu muốn hủy bỏ.",
                    canUndo: true);
            }
            finally
            {
                _chatBox.HideThinkingIndicator();
            }
        }

        #endregion
    }
}
