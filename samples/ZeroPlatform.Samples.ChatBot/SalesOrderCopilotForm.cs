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
            CaptureUndoSnapshot("Khởi tạo đơn rỗng");
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
            if (_undoStack.Count > 1)
            {
                _undoStack.Pop(); // current state
                var previous = _undoStack.Peek();
                _currentOrder = previous.State.Clone();
                RefreshGridFromOrder();

                // Restore active subjects from restored order
                if (!string.IsNullOrEmpty(_currentOrder.CustomerCode))
                {
                    var c = _matcher.Customers.FirstOrDefault(x => x.Code == _currentOrder.CustomerCode);
                    SetActiveCustomerSubject(c);
                }
                else
                {
                    SetActiveCustomerSubject(null);
                }

                if (_currentOrder.Items.Count > 0)
                {
                    var lastItem = _currentOrder.Items.Last();
                    var p = _matcher.Products.FirstOrDefault(x => x.Sku == lastItem.Sku);
                    SetActiveProductSubject(p, _currentOrder.Items.Count);
                }
                else
                {
                    SetActiveProductSubject(null, null);
                }

                _chatBox.ShowThinkingIndicator("Đang hoàn tác trạng thái chứng từ...");
                await Task.Delay(250);
                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    $"↺ **ĐÃ HOÀN TÁC THÀNH CÔNG**!\n\n" +
                    $"Đã khôi phục lại trạng thái chứng từ: **{previous.Description}**.",
                    canUndo: false);
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
                decimal currentDebt = 125_400_000;
                decimal available = Math.Max(0, cus.CreditLimit - currentDebt);

                responseMessage = $"💳 **[TRA CỨU CÔNG NỢ & HẠN MỨC TÍN DỤNG]**\n\n" +
                                  $"• Khách hàng: **{cus.Name}** ({cus.Code})\n" +
                                  $"• Hạn mức tín dụng: **{cus.CreditLimit:#,##0} đ**\n" +
                                  $"• Dư nợ hiện tại: **{currentDebt:#,##0} đ**\n" +
                                  $"• Hạn mức còn khả dụng: **{available:#,##0} đ**\n\n" +
                                  $"👉 Trạng thái tín dụng: **TỐT (Đủ điều kiện ghi nhận đơn hàng mới)**";
                return true;
            }

            return false;
        }

        private bool TryHandleItemQuantityMutation(string text, out string responseMessage)
        {
            responseMessage = string.Empty;

            // Pattern 1: By Line Number: "đổi/sửa số lượng dòng (thứ) X thành/là Y"
            var matchLine = Regex.Match(text, 
                @"^(?:đổi|sửa|chỉnh|cập\s*nhật)\s+số\s+lượng\s+dòng\s*(?:thứ\s*)?(\d+)\s+(?:thành|là|=|lên|xuống)?\s*(\d+)", 
                RegexOptions.IgnoreCase);

            if (matchLine.Success && 
                int.TryParse(matchLine.Groups[1].Value, out int lineNum) && 
                decimal.TryParse(matchLine.Groups[2].Value, out decimal newQtyLine))
            {
                if (lineNum >= 1 && lineNum <= _currentOrder.Items.Count)
                {
                    var item = _currentOrder.Items[lineNum - 1];
                    decimal oldQty = item.Quantity;
                    CaptureUndoSnapshot($"Trước khi đổi số lượng dòng {lineNum} từ {oldQty} sang {newQtyLine}");
                    item.Quantity = newQtyLine;
                    RefreshGridFromOrder();

                    var prod = _matcher.Products.FirstOrDefault(p => p.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase))
                               ?? new MasterProduct(item.Sku, item.ItemName, item.UnitPrice, item.Unit, item.Warehouse);
                    SetActiveProductSubject(prod, lineNum);

                    responseMessage = $"Xong rồi nhé! Mình đã cập nhật số lượng dòng **#{lineNum}** ({item.ItemName}):\n\n" +
                                      $"• Số lượng cũ: **{oldQty} {item.Unit}** ➔ Mới: **{item.Quantity} {item.Unit}**\n" +
                                      $"• Thành tiền mới: **{item.TotalAmount:#,##0} đ**\n\n" +
                                      $"👉 **Tổng giá trị đơn hàng: {_currentOrder.TotalAmount:#,##0} đ**";
                    return true;
                }
            }

            // Pattern 2: By Item Target: "đổi/sửa/chỉnh số lượng [của] <target> thành/là/= <qty>"
            var matchTarget = Regex.Match(text, 
                @"^(?:đổi|sửa|chỉnh|cập\s*nhật)\s+số\s+lượng\s+(?:của\s+)?(.+?)\s+(?:thành|thành\s+số\s+lượng|là|=|lên|xuống)\s*(\d+)", 
                RegexOptions.IgnoreCase);

            if (matchTarget.Success && decimal.TryParse(matchTarget.Groups[2].Value, out decimal newQtyTarget))
            {
                string targetText = matchTarget.Groups[1].Value.Trim();
                var item = FindOrderItem(targetText);
                if (item != null)
                {
                    decimal oldQty = item.Quantity;
                    CaptureUndoSnapshot($"Trước khi đổi số lượng {item.ItemName} từ {oldQty} sang {newQtyTarget}");
                    item.Quantity = newQtyTarget;
                    RefreshGridFromOrder();

                    var prod = _matcher.Products.FirstOrDefault(p => p.Sku.Equals(item.Sku, StringComparison.OrdinalIgnoreCase))
                               ?? new MasterProduct(item.Sku, item.ItemName, item.UnitPrice, item.Unit, item.Warehouse);
                    SetActiveProductSubject(prod, item.LineIndex);

                    responseMessage = $"Xong rồi nhé! Mình đã cập nhật số lượng mặt hàng **{item.ItemName}** (dòng #{item.LineIndex}):\n\n" +
                                      $"• Số lượng cũ: **{oldQty} {item.Unit}** ➔ Mới: **{item.Quantity} {item.Unit}**\n" +
                                      $"• Đơn giá: **{item.UnitPrice:#,##0} đ**\n" +
                                      $"• Thành tiền mới: **{item.TotalAmount:#,##0} đ**\n\n" +
                                      $"👉 **Tổng giá trị đơn hàng: {_currentOrder.TotalAmount:#,##0} đ**";
                    return true;
                }
            }

            // Pattern 3: Relative Increase / Decrease: "tăng/giảm số lượng [của] <target> [lên/xuống] <qty>"
            var matchIncDec = Regex.Match(text, 
                @"^(tăng|giảm)\s+số\s+lượng\s+(?:của\s+)?(.+?)\s+(?:thành|lên|xuống)?\s*(\d+)$", 
                RegexOptions.IgnoreCase);

            if (matchIncDec.Success && decimal.TryParse(matchIncDec.Groups[3].Value, out decimal deltaQty))
            {
                string action = matchIncDec.Groups[1].Value.ToLowerInvariant();
                string targetText = matchIncDec.Groups[2].Value.Trim();
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

            // Pattern 4: Elliptical quantity change when an active product subject is set: "đổi số lượng thành 10", "số lượng 10"
            if (_lastActiveProduct != null && _currentOrder.Items.Count > 0)
            {
                var matchEllip = Regex.Match(text, 
                    @"^(?:đổi|sửa|chỉnh)?\s*số\s+lượng\s+(?:thành|là|=)?\s*(\d+)$", 
                    RegexOptions.IgnoreCase);

                if (matchEllip.Success && decimal.TryParse(matchEllip.Groups[1].Value, out decimal newQtyEllip))
                {
                    var item = FindOrderItem(_lastActiveProduct.Name);
                    if (item != null)
                    {
                        decimal oldQty = item.Quantity;
                        CaptureUndoSnapshot($"Trước khi đổi số lượng {item.ItemName} từ {oldQty} sang {newQtyEllip}");
                        item.Quantity = newQtyEllip;
                        RefreshGridFromOrder();

                        SetActiveProductSubject(_lastActiveProduct, item.LineIndex);

                        responseMessage = $"Đã cập nhật số lượng cho chủ thể đang chọn **{item.ItemName}** (dòng #{item.LineIndex}):\n\n" +
                                          $"• Số lượng: **{oldQty} ➔ {newQtyEllip} {item.Unit}**\n" +
                                          $"• Thành tiền mới: **{item.TotalAmount:#,##0} đ**\n\n" +
                                          $"👉 **Tổng giá trị đơn hàng: {_currentOrder.TotalAmount:#,##0} đ**";
                        return true;
                    }
                }
            }

            return false;
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
                                      $"• Tổng giá trị đơn sau khi xóa: **{_currentOrder.TotalAmount:#,##0} đ**";
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
                                      $"• Tổng giá trị đơn sau khi xóa: **{_currentOrder.TotalAmount:#,##0} đ**";
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
                                      $"• Tổng giá trị đơn sau khi xóa: **{_currentOrder.TotalAmount:#,##0} đ**";
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

                    await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                        $"📊 **[TRA CỨU TỒN KHO THỜI GIAN THỰC]**\n\n" +
                        $"• Mặt hàng: **{p.Name}** ({p.Sku})\n" +
                        $"• Kho 01: **42 {p.Unit}** (Khả dụng: 38 {p.Unit})\n" +
                        $"• Kho 02: **15 {p.Unit}**\n\n" +
                        $"👉 Tổng tồn kho hệ thống: **57 {p.Unit}** (Đủ đáp ứng cho đơn hàng mới).\n\n" +
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

            // Match Customer
            var (matchedCus, _) = _matcher.MatchCustomer(text);
            if (matchedCus != null)
            {
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

                decimal qty = 0;
                string prodText = string.Empty;

                // Pattern A: "<qty> [unit] <product_name>" (e.g. "20 Ghế xoay", "2 cái Nồi chiên", "1 máy chiếu")
                var matchA = Regex.Match(rawPart, @"(\d+)\s*(cái|bộ|máy|chiếc|thùng|hộp|cuộn|con)?\s+([^\d,;]+)", RegexOptions.IgnoreCase);
                if (matchA.Success && decimal.TryParse(matchA.Groups[1].Value, out decimal qA))
                {
                    qty = qA;
                    prodText = matchA.Groups[3].Value.Trim();
                }
                else
                {
                    // Pattern B: "<product_name> <qty> [unit]" (e.g. "Ghế xoay 20 cái", "Máy chiếu 3 bộ")
                    var matchB = Regex.Match(rawPart, @"([^\d,;]+?)\s+(\d+)\s*(cái|bộ|máy|chiếc|thùng|hộp|cuộn|con)?$", RegexOptions.IgnoreCase);
                    if (matchB.Success && decimal.TryParse(matchB.Groups[2].Value, out decimal qB))
                    {
                        qty = qB;
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

                    // Strip leading filler words ("thêm ", "bổ sung ", "lấy ", "mua ")
                    foreach (var filler in new[] { "thêm ", "bổ sung ", "lấy ", "mua ", "bán ", "cần " })
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

                    if (prod != null)
                    {
                        var item = new OrderItemDraft
                        {
                            RawDescription = prodText,
                            Sku = prod.Sku,
                            ItemName = prod.Name,
                            Quantity = qty,
                            Unit = prod.Unit,
                            UnitPrice = prod.Price,
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
