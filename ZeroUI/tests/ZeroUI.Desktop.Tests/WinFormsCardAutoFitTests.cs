using System.Drawing;
using System.Windows.Forms;
using Xunit;
using ZeroUI.WinForms.Containers;

namespace ZeroUI.Desktop.Tests
{
    public class WinFormsCardAutoFitTests
    {
        [Fact]
        public void Card_AutoFitContent_ExpandsHeight_WhenControlsAdded()
        {
            StaTestRunner.Run(() =>
            {
                using var card = new Card
                {
                    Width = 300,
                    AutoFitContent = true,
                    Title = "Test Card"
                };

                int initialHeight = card.Height;

                // Add a child panel with 150px height
                var childPanel = new Panel
                {
                    Location = new Point(0, 0),
                    Size = new Size(200, 150)
                };
                card.ContentPanel.Controls.Add(childPanel);

                // Height should expand to at least header (44) + 150 + bottomPadding (12) = 206
                Assert.True(card.Height >= 200, $"Expected height >= 200, actual: {card.Height}");
                Assert.True(card.Height > initialHeight, $"Card height {card.Height} should be greater than initial {initialHeight}");
            });
        }

        [Fact]
        public void Card_AutoFitContent_CalculatesFlowLayoutPanelWrappedHeight()
        {
            StaTestRunner.Run(() =>
            {
                using var card = new Card
                {
                    Width = 260, // Content width ~ 236px
                    AutoFitContent = true,
                    Title = "Flow Card"
                };

                var flow = new FlowLayoutPanel
                {
                    Dock = DockStyle.Top,
                    WrapContents = true,
                    AutoSize = true,
                    Padding = new Padding(4)
                };

                // Add 4 buttons, each width 110px. In width 236px, max 2 buttons per row -> 2 rows.
                for (int i = 0; i < 4; i++)
                {
                    flow.Controls.Add(new Button { Size = new Size(110, 35), Margin = new Padding(2) });
                }

                card.ContentPanel.Controls.Add(flow);

                // Two rows of 35px (+ margins) = at least 74px + header 44 + padding 12 >= 130px
                Assert.True(card.Height >= 125, $"Expected wrapped height >= 125, actual: {card.Height}");
            });
        }

        [Fact]
        public void Card_ControlsAdd_RedirectsToContentPanel()
        {
            StaTestRunner.Run(() =>
            {
                using var card = new Card
                {
                    Width = 300,
                    Title = "Redirect Test"
                };

                var btn = new Button { Text = "Action", Size = new Size(100, 30) };

                // Adding directly to card.Controls should redirect to card.ContentPanel.Controls
                card.Controls.Add(btn);

                Assert.True(card.ContentPanel.Controls.Contains(btn));
                Assert.False(card.Controls.Contains(btn));
            });
        }
    }
}
