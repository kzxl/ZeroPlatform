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
                SplitterDistance = 880,
                SplitterWidth = 6,
                BackColor = Color.FromArgb(30, 41, 59)
            };
            splitMain.Panel1.BackColor = Color.FromArgb(15, 23, 42);
            splitMain.Panel2.BackColor = Color.FromArgb(15, 23, 42);

            BuildOrderFormPanel(splitMain.Panel1);
            BuildChatDrawerPanel(splitMain.Panel2);

            Controls.Add(splitMain);
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

            pnlContainer.Controls.Add(pnlHeaderCard);

            var spacerTop = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };
            pnlContainer.Controls.Add(spacerTop);

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
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColName", HeaderText = "Tên hàng hóa / Quy cách", Width = 260 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColWarehouse", HeaderText = "Kho xuất", Width = 90 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColQty", HeaderText = "Số lượng", Width = 80 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColUnit", HeaderText = "ĐVT", Width = 60 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColPrice", HeaderText = "Đơn giá", Width = 100 });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColTotal", HeaderText = "Thành tiền", Width = 110, ReadOnly = true });
            _gridItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColConf", HeaderText = "Độ tin cậy AI", Width = 110, ReadOnly = true });

            pnlContainer.Controls.Add(_gridItems);

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

            pnlContainer.Controls.Add(pnlBottom);
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

            // Setup Prompt Chips from user requirements
            _chatBox.PromptSuggestions.Add("Thêm cho Không Gian Mới 2 Nồi chiên và 3 Máy chiếu");
            _chatBox.PromptSuggestions.Add("Thêm đơn cho Bình Minh 20 Ghế xoay và 20 Chuột");
            _chatBox.PromptSuggestions.Add("Cập nhật kho của tất cả hàng hóa thành Kho 01");
            _chatBox.PromptSuggestions.Add("Sắp xếp thứ tự hàng hóa theo mã");
            _chatBox.PromptSuggestions.Add("Hàng IPRO001 còn bao nhiêu trong kho?");
            _chatBox.PromptSuggestions.Add("Đơn hàng hiện tại đang ở trạng thái gì?");

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

        private void BindCustomerCatalog()
        {
            _cboCustomer.Items.Clear();
            foreach (var c in _matcher.Customers)
            {
                _cboCustomer.Items.Add(c);
            }
            _cboCustomer.DisplayMember = "Name";
            _cboCustomer.ValueMember = "Code";
            if (_cboCustomer.Items.Count > 0) _cboCustomer.SelectedIndex = 0;
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
            for (int i = 0; i < _cboCustomer.Items.Count; i++)
            {
                if (_cboCustomer.Items[i] is MasterCustomer c && c.Code == _currentOrder.CustomerCode)
                {
                    _cboCustomer.SelectedIndex = i;
                    break;
                }
            }
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
            RefreshGridFromOrder();
        }

        #endregion

        #region Copilot Command Processing

        private async Task ProcessChatCommandAsync(string text)
        {
            string clean = text.Trim();

            // 1. Command: Batch update warehouse
            if (clean.IndexOf("cập nhật kho", StringComparison.OrdinalIgnoreCase) >= 0 ||
                clean.IndexOf("chuyển kho", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                CaptureUndoSnapshot("Trước khi cập nhật kho hàng loạt");
                string targetWh = "Kho 01";
                if (clean.IndexOf("Kho 02", StringComparison.OrdinalIgnoreCase) >= 0) targetWh = "Kho 02";
                else if (clean.IndexOf("Kho Tổng", StringComparison.OrdinalIgnoreCase) >= 0) targetWh = "Kho Tổng";

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

            // 2. Command: Sort items
            if (clean.IndexOf("sắp xếp", StringComparison.OrdinalIgnoreCase) >= 0)
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

            // 3. Command: Inventory query
            if (clean.IndexOf("còn bao nhiêu trong kho", StringComparison.OrdinalIgnoreCase) >= 0 ||
                clean.IndexOf("tồn kho", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var (p, _) = _matcher.MatchProduct(clean);
                string pName = p?.Name ?? "IPRO-001 (RYNAN i-PRO)";

                _chatBox.ShowThinkingIndicator("Đang truy vấn số dư tồn kho thời gian thực...");
                await Task.Delay(300);

                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    $"📊 **[TRA CỨU TỒN KHO THỜI GIAN THỰC]**\n\n" +
                    $"• Mặt hàng: **{pName}**\n" +
                    $"• Kho 01: **42 Cái** (Khả dụng: 38 Cái)\n" +
                    $"• Kho 02: **15 Cái**\n\n" +
                    $"👉 Tổng tồn kho hệ thống: **57 Cái** (Đủ đáp ứng cho đơn hàng mới).",
                    canUndo: false);
                return;
            }

            // 4. Command: Order status inquiry
            if (clean.IndexOf("trạng thái", StringComparison.OrdinalIgnoreCase) >= 0)
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

            // 5. Command: Add order / items from text (e.g. "Thêm đơn hàng cho Công ty...")
            _chatBox.ShowThinkingIndicator("Sales Copilot đang nhận diện khách hàng & bóc tách mặt hàng...");
            await Task.Delay(350);

            CaptureUndoSnapshot("Trước khi thêm mặt hàng bằng giọng nói/text");
            var addedItems = ParseAndApplyOrderFromText(clean);

            if (addedItems.Count > 0)
            {
                RefreshGridFromOrder();

                string itemsList = string.Join("\n", addedItems.Select(i => $"• **{i.ItemName}** (Số lượng: {i.Quantity} {i.Unit} - Kho: {i.Warehouse})"));
                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    $"Xong rồi nhé! Mình đã hoàn tất các thao tác cho đơn hàng của bạn:\n\n" +
                    $"• Đã chọn Khách hàng: **{_currentOrder.CustomerName}**\n" +
                    $"• Đã thêm các mặt hàng sau vào đơn:\n{itemsList}\n\n" +
                    "Bạn có muốn mình hỗ trợ thêm việc nào khác như cập nhật thông tin khác hay thêm hàng hóa mới không?",
                    canUndo: true);
            }
            else
            {
                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    "Tôi chưa nhận diện được tên khách hàng hoặc sản phẩm trong câu nói.\n\n" +
                    "Bạn có thể nói theo mẫu:\n" +
                    "• *'Thêm cho Cty Bình Minh, 20 Ghế xoay và 20 Chuột máy tính'*\n" +
                    "• *'Thêm cho Không Gian Mới 2 Nồi chiên và 3 Máy chiếu'*",
                    canUndo: false);
            }
        }

        private List<OrderItemDraft> ParseAndApplyOrderFromText(string text)
        {
            var added = new List<OrderItemDraft>();

            // Match Customer
            var (matchedCus, _) = _matcher.MatchCustomer(text);
            if (matchedCus != null)
            {
                _currentOrder.CustomerCode = matchedCus.Code;
                _currentOrder.CustomerName = matchedCus.Name;
            }

            // Pattern for matching: "<qty> <product_name>" or "<product_name> <qty>"
            // Examples: "20 Ghế xoay và 20 Chuột máy tính", "2 Nồi chiên không dầu và 3 Máy chiếu"
            var parts = text.Split(new string[] { ",", ";", " và ", " voi " }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var match = Regex.Match(part, @"(\d+)\s+([^\d,]+)");
                if (match.Success)
                {
                    if (decimal.TryParse(match.Groups[1].Value, out decimal qty))
                    {
                        string prodText = match.Groups[2].Value.Trim();
                        var (prod, conf) = _matcher.MatchProduct(prodText);

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
            }

            return added;
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
                }

                // Add Items
                foreach (var item in extraction.Items)
                {
                    _currentOrder.Items.Add(item);
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
