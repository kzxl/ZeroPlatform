using System;
using Xunit;
using ZeroUI.Core.Editors;

namespace ZeroUI.Core.Tests
{
    public class ButtonGroupTests
    {
        [Fact]
        public void ButtonGroupSizeMode_ShouldHaveExpectedModes()
        {
            Assert.True(Enum.IsDefined(typeof(ButtonGroupSizeMode), ButtonGroupSizeMode.AutoFit));
            Assert.True(Enum.IsDefined(typeof(ButtonGroupSizeMode), ButtonGroupSizeMode.EqualWidth));
            Assert.True(Enum.IsDefined(typeof(ButtonGroupSizeMode), ButtonGroupSizeMode.Fill));
        }

        [Fact]
        public void ButtonGroupItem_Properties_ShouldInitializeAndAssignCorrectly()
        {
            var item = new ButtonGroupItem("btn_save", "Save", "💾", ButtonGroupItemType.Push)
            {
                ShowBadgeDot = true,
                BadgeColorHex = "#EF4444",
                MinWidth = 80,
                CustomPaddingHorizontal = 20,
                CustomBackColorHex = "#1E293B",
                CustomForeColorHex = "#F8FAFC",
                BadgeText = "3",
                DropDownMenu = new object()
            };

            Assert.Equal("btn_save", item.Id);
            Assert.Equal("Save", item.Text);
            Assert.Equal("💾", item.IconGlyph);
            Assert.True(item.ShowBadgeDot);
            Assert.Equal("#EF4444", item.BadgeColorHex);
            Assert.Equal(80, item.MinWidth);
            Assert.Equal(20, item.CustomPaddingHorizontal);
            Assert.Equal("#1E293B", item.CustomBackColorHex);
            Assert.Equal("#F8FAFC", item.CustomForeColorHex);
            Assert.Equal("3", item.BadgeText);
            Assert.NotNull(item.DropDownMenu);
        }

        [Fact]
        public void ButtonGroupModel_SingleSelect_EnforcesMutualExclusion()
        {
            var model = new ButtonGroupModel
            {
                SelectionMode = ButtonGroupSelectionMode.SingleSelect
            };

            var item1 = new ButtonGroupItem("opt1", "Option 1", null, ButtonGroupItemType.Toggle);
            var item2 = new ButtonGroupItem("opt2", "Option 2", null, ButtonGroupItemType.Toggle);
            var item3 = new ButtonGroupItem("opt3", "Option 3", null, ButtonGroupItemType.Toggle);

            model.Add(item1);
            model.Add(item2);
            model.Add(item3);

            model.TriggerClick(0);
            Assert.True(item1.IsChecked);
            Assert.False(item2.IsChecked);
            Assert.False(item3.IsChecked);
            Assert.Same(item1, model.SelectedItem);

            model.TriggerClick(1);
            Assert.False(item1.IsChecked);
            Assert.True(item2.IsChecked);
            Assert.False(item3.IsChecked);
            Assert.Same(item2, model.SelectedItem);
        }

        [Fact]
        public void ButtonGroupModel_MultiSelect_AllowsConcurrentSelection()
        {
            var model = new ButtonGroupModel
            {
                SelectionMode = ButtonGroupSelectionMode.MultiSelect
            };

            var item1 = new ButtonGroupItem("bold", "Bold", null, ButtonGroupItemType.Toggle);
            var item2 = new ButtonGroupItem("italic", "Italic", null, ButtonGroupItemType.Toggle);

            model.AddRange(new[] { item1, item2 });

            model.TriggerClick(0);
            model.TriggerClick(1);

            Assert.True(item1.IsChecked);
            Assert.True(item2.IsChecked);
        }

        [Fact]
        public void ButtonGroupModel_SetChecked_And_SetBadge_WorkAsExpected()
        {
            var model = new ButtonGroupModel
            {
                SelectionMode = ButtonGroupSelectionMode.SingleSelect
            };

            var item = new ButtonGroupItem("notify", "Notifications", "🔔", ButtonGroupItemType.Toggle);
            model.Add(item);

            bool checkResult = model.SetChecked("notify", true);
            Assert.True(checkResult);
            Assert.True(item.IsChecked);

            bool badgeResult = model.SetBadge("notify", "99+", showBadgeDot: true, badgeColorHex: "#10B981");
            Assert.True(badgeResult);
            Assert.Equal("99+", item.BadgeText);
            Assert.True(item.ShowBadgeDot);
            Assert.Equal("#10B981", item.BadgeColorHex);
        }

        [Fact]
        public void ButtonGroupModel_ActionDispatch_TriggersOnItemClick()
        {
            var model = new ButtonGroupModel();
            bool actionInvoked = false;
            ButtonGroupItem? clickedFromEvent = null;

            var item = new ButtonGroupItem("action_btn", "Execute", null, ButtonGroupItemType.Push, itm =>
            {
                actionInvoked = true;
            });

            model.ItemClicked += (s, e) =>
            {
                clickedFromEvent = e.Item;
            };

            model.Add(item);
            model.TriggerClick(0);

            Assert.True(actionInvoked);
            Assert.Same(item, clickedFromEvent);
        }
    }
}
