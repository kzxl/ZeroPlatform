using System;
using System.Drawing;
using System.Windows.Forms;
using Xunit;
using ZeroUI.Core.Validation;
using ZeroUI.Core.Barcode;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Notification;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.Data;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Feedback;
using ZeroUI.WinForms.Navigation;
using ZeroUI.WinForms.Overlays;
using ZeroUI.WinForms.Theme;
using ZeroUI.WinForms.Validation;
using ZeroTabPage = ZeroUI.WinForms.Navigation.ZeroTabPage;

namespace ZeroUI.Desktop.Tests
{
    public class CoreInputInspectionTests
    {
        [Fact]
        public void TestCoreInputShowcaseInitialization()
        {
            StaTestRunner.Run(() =>
            {
                var tab = new ZeroTabPage("Core Input Controls", "🎛️");
                var colors = ZeroTheme.Colors;
                tab.BackColor = colors.Background;

                var leftPanel = new Panel
                {
                    Name = "leftShowcasePanel",
                    Dock = DockStyle.Left,
                    Width = 460,
                    Padding = new Padding(20),
                    AutoScroll = true,
                    BackColor = colors.Surface
                };

                // Section 1
                var btnPrimary = new ZeroButton { Text = "Primary Action", ButtonStyle = ZeroButtonStyle.Primary };
                var btnBadge = new ZeroButton { Text = "Notifications", ButtonStyle = ZeroButtonStyle.Primary, BadgeText = "9+" };
                leftPanel.Controls.Add(btnPrimary);
                leftPanel.Controls.Add(btnBadge);

                // Section 2
                var prog1 = new ZeroProgressBar { Value = 78 };
                var prog2 = new ZeroProgressBar { IsIndeterminate = true };
                leftPanel.Controls.Add(prog1);
                leftPanel.Controls.Add(prog2);

                // Section 3
                var searchDemo = new ZeroSearchBox { PlaceholderText = "Search..." };
                leftPanel.Controls.Add(searchDemo);

                // Section 4
                var swDemo = new ZeroSwitch { Checked = true };
                var tag1 = new ZeroTag { TagType = TagType.Success, Text = "Active" };
                leftPanel.Controls.Add(swDemo);
                leftPanel.Controls.Add(tag1);

                // Section 5
                var segDemo = new ZeroSegmented { Items = new[] { "All", "Daily" } };
                leftPanel.Controls.Add(segDemo);

                // Section 6
                var stat1 = new ZeroStatistic { Title = "TOTAL INVENTORY", Value = "1,248,500" };
                leftPanel.Controls.Add(stat1);

                // Section 7
                var cmbDeliveryMode = new ComboBox();
                cmbDeliveryMode.Items.AddRange(new object[] {
                    ZeroNotificationDeliveryMode.Auto,
                    ZeroNotificationDeliveryMode.InAppOnly,
                    ZeroNotificationDeliveryMode.SystemOnly,
                    ZeroNotificationDeliveryMode.Dual
                });
                cmbDeliveryMode.SelectedItem = ZeroToastStackManager.DeliveryMode;
                var chkRouteAlarms = new ZeroCheckBox { Checked = ZeroToastStackManager.RouteAlarmsToSystem };
                leftPanel.Controls.Add(cmbDeliveryMode);
                leftPanel.Controls.Add(chkRouteAlarms);

                // Section 8
                var txtRegular = new ZeroTextBox { PlaceholderText = "Enter...", ShowClearButton = true };
                var txtPassword = new ZeroTextBox { UseSystemPasswordChar = true, ShowClearButton = true };
                leftPanel.Controls.Add(txtRegular);
                leftPanel.Controls.Add(txtPassword);

                // Section 9
                var chk1 = new ZeroCheckBox { Text = "Enable", Checked = true };
                leftPanel.Controls.Add(chk1);

                // Section 10
                var datePicker = new ZeroDatePicker { Value = DateTime.Today };
                var numBox = new ZeroNumericBox { Value = 12500 };
                leftPanel.Controls.Add(datePicker);
                leftPanel.Controls.Add(numBox);

                // Section 11
                var txtValidate = new ZeroTextBox { Text = "TEST-9999" };
                var orderCodeValidator = new Validator<string?>()
                    .NotEmpty("Required")
                    .Must(text => text != null && text.StartsWith("ORD-"), "Must start with ORD-", ValidationSeverity.Warning);
                var errorProvider = new ZeroErrorProvider();
                errorProvider.SetResult(txtValidate, orderCodeValidator.Validate(txtValidate.Text));
                leftPanel.Controls.Add(txtValidate);

                // Section 12
                var radioGroup = new ZeroRadioGroup { Items = new[] { "A", "B" } };
                leftPanel.Controls.Add(radioGroup);

                // Section 13
                var cmbOptions = new ZeroComboBox();
                cmbOptions.SetItems(new[] { "Option 1", "Option 2" });
                leftPanel.Controls.Add(cmbOptions);

                // Section 14
                var memoEdit = new ZeroMemoEdit { Text = "Sample memo text" };
                leftPanel.Controls.Add(memoEdit);

                // Section 15
                var sliderDemo = new ZeroSlider { Minimum = 0, Maximum = 100, Value = 75 };
                leftPanel.Controls.Add(sliderDemo);

                // Section 16
                var timePicker1 = new ZeroTimePicker { Value = new TimeSpan(8, 0, 0) };
                leftPanel.Controls.Add(timePicker1);

                // Section 17
                var txtIp = new ZeroMaskedTextBox { Mask = "000.000.000.000", Text = "192.168.001.100" };
                leftPanel.Controls.Add(txtIp);

                // Right panel
                var rightPanel = new Panel { Dock = DockStyle.Fill };
                var showcaseLog = new ZeroListView { Dock = DockStyle.Fill };
                showcaseLog.AddLog(LogSeverity.Info, "Init");
                rightPanel.Controls.Add(showcaseLog);

                // Mid panel
                var midPanel = new Panel { Dock = DockStyle.Left, Width = 470 };
                var btnEdit = new ButtonEdit { Text = "C:\\test.bin" };
                var calcEdit = new CalcEdit { Value = 18450.75m };
                var colorPick = new ColorPickEdit { SelectedColor = Color.Blue };
                var ratingCtrl = new RatingControl { Value = 4.5m };
                var rangeSlider = new RangeSlider { Minimum = 0, Maximum = 120, LowerValue = 25, UpperValue = 85 };
                var timeSpanEdit = new TimeSpanEdit { Value = TimeSpan.FromHours(7.5) };
                var ipEdit = new IPAddressEdit { Text = "192.168.10.45" };
                var linkEdit = new HyperlinkEdit { TargetUrl = "https://github.com", DisplayText = "Github" };
                var barcode128 = new BarcodeBox { Text = "LOT-123", Symbology = BarcodeSymbology.Code128 };
                var barcodeQr = new BarcodeBox { Text = "LOT-123", Symbology = BarcodeSymbology.QrCode };
                var pic1 = new PictureEdit { FallbackText = "PV", Status = AvatarStatus.Online };

                midPanel.Controls.Add(btnEdit);
                midPanel.Controls.Add(calcEdit);
                midPanel.Controls.Add(colorPick);
                midPanel.Controls.Add(ratingCtrl);
                midPanel.Controls.Add(rangeSlider);
                midPanel.Controls.Add(timeSpanEdit);
                midPanel.Controls.Add(ipEdit);
                midPanel.Controls.Add(linkEdit);
                midPanel.Controls.Add(barcode128);
                midPanel.Controls.Add(barcodeQr);
                midPanel.Controls.Add(pic1);

                tab.Controls.Add(rightPanel);
                tab.Controls.Add(midPanel);
                tab.Controls.Add(leftPanel);

                Assert.Equal(3, tab.Controls.Count);
            });
        }
    }
}
