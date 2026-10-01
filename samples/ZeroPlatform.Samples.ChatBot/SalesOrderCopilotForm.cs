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

            var spacerTop = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };

            pnlContainer.Controls.Add(pnlBottom);
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
            }
            RefreshGridFromOrder();
        }

        #endregion

        #region Copilot Command Processing

        private async Task ProcessChatCommandAsync(string text)
        {
            string clean = text.Trim();

            // 0. Command: Quick Reset
            if (clean.Equals("làm mới", StringComparison.OrdinalIgnoreCase) ||
                clean.Equals("làm mới đơn", StringComparison.OrdinalIgnoreCase) ||
                clean.Equals("xóa đơn", StringComparison.OrdinalIgnoreCase) ||
                clean.Equals("hủy đơn", StringComparison.OrdinalIgnoreCase))
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

            // 0.1 Command: Save & Post order
            if (clean.IndexOf("lưu và ghi sổ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                clean.IndexOf("lưu đơn", StringComparison.OrdinalIgnoreCase) >= 0 ||
                clean.IndexOf("ghi sổ", StringComparison.OrdinalIgnoreCase) >= 0)
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

            // 5. Command: Add order / items from text (e.g. "Thêm đơn hàng cho Công ty...", "Tạo đơn mới cho Phú Hưng 1 máy chiếu")
            _chatBox.ShowThinkingIndicator("Sales Copilot đang nhận diện khách hàng & bóc tách mặt hàng...");
            await Task.Delay(300);

            CaptureUndoSnapshot("Trước khi xử lý câu lệnh");

            bool isNewOrder = clean.IndexOf("tạo đơn mới", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              clean.IndexOf("lập đơn mới", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              clean.IndexOf("tạo đơn hàng mới", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              clean.IndexOf("đơn mới", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isNewOrder)
            {
                _currentOrder = new OrderDraft();
            }

            var (matchedCus, addedItems) = ParseAndApplyOrderFromText(clean);

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
                RefreshGridFromOrder();

                string itemsList = string.Join("\n", addedItems.Select(i => $"• **{i.ItemName}** (Số lượng: {i.Quantity} {i.Unit} - Đơn giá: {i.UnitPrice:#,##0} đ - Kho: {i.Warehouse})"));
                string cusDesc = !string.IsNullOrEmpty(_currentOrder.CustomerName)
                    ? $"• Đã chọn Khách hàng: **{_currentOrder.CustomerName}**\n"
                    : "";

                await _chatBox.AppendAssistantActionMessageAnimatedAsync(
                    $"Xong rồi nhé! Mình đã hoàn tất các thao tác cho đơn hàng **{_currentOrder.OrderCode}**:\n\n" +
                    cusDesc +
                    $"• Đã thêm các mặt hàng sau vào đơn:\n{itemsList}\n\n" +
                    $"👉 **Tổng giá trị đơn: {_currentOrder.TotalAmount:#,##0} đ** ({_currentOrder.Items.Count} dòng mặt hàng)\n\n" +
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
                    "• *'Tạo đơn mới cho Phú Hưng 1 máy chiếu'*",
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

                if (qty > 0 && !string.IsNullOrWhiteSpace(prodText))
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
