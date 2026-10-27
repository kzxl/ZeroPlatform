using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Input;

namespace ZeroUI.Core.Tests
{
    public class SelectionModelTests
    {
        [Fact]
        public void InitialState_NoSelection_CorrectDefaults()
        {
            var model = new SelectionModel<string>(new[] { "A", "B", "C" });
            Assert.Equal(3, model.Count);
            Assert.Equal(-1, model.SelectedIndex);
            Assert.Null(model.SelectedItem);
            Assert.False(model.HasSelection);
        }

        [Fact]
        public void SelectIndex_ValidIndex_SelectsItemAndRaisesEvent()
        {
            var model = new SelectionModel<string>(new[] { "Alpha", "Beta", "Gamma" });
            int eventCount = 0;
            int recordedOldIndex = -99;
            int recordedNewIndex = -99;

            model.SelectionChanged += (s, e) =>
            {
                eventCount++;
                recordedOldIndex = e.OldIndex;
                recordedNewIndex = e.NewIndex;
            };

            bool changed = model.SelectIndex(1);
            Assert.True(changed);
            Assert.Equal(1, model.SelectedIndex);
            Assert.Equal("Beta", model.SelectedItem);
            Assert.True(model.HasSelection);
            Assert.Equal(1, eventCount);
            Assert.Equal(-1, recordedOldIndex);
            Assert.Equal(1, recordedNewIndex);

            // Setting same index should return false and not fire event
            bool sameChanged = model.SelectIndex(1);
            Assert.False(sameChanged);
            Assert.Equal(1, eventCount);
        }

        [Fact]
        public void SelectIndex_OutOfBounds_ClearsSelection()
        {
            var model = new SelectionModel<string>(new[] { "One", "Two" });
            model.SelectIndex(1);
            Assert.Equal(1, model.SelectedIndex);

            model.SelectIndex(10);
            Assert.Equal(-1, model.SelectedIndex);
            Assert.Null(model.SelectedItem);
            Assert.False(model.HasSelection);
        }

        [Fact]
        public void MoveNext_WithWrapAround_CyclesThroughItems()
        {
            var model = new SelectionModel<string>(new[] { "First", "Second", "Third" }) { WrapAround = true };

            // From -1, MoveNext goes to 0
            model.MoveNext();
            Assert.Equal(0, model.SelectedIndex);

            model.MoveNext();
            Assert.Equal(1, model.SelectedIndex);

            model.MoveNext();
            Assert.Equal(2, model.SelectedIndex);

            // Wraps to 0
            model.MoveNext();
            Assert.Equal(0, model.SelectedIndex);
        }

        [Fact]
        public void MoveNext_WithoutWrapAround_StopsAtEnd()
        {
            var model = new SelectionModel<string>(new[] { "A", "B" }) { WrapAround = false };
            model.SelectIndex(1);

            bool moved = model.MoveNext();
            Assert.False(moved);
            Assert.Equal(1, model.SelectedIndex);
        }

        [Fact]
        public void MovePrevious_WithWrapAround_CyclesBackward()
        {
            var model = new SelectionModel<string>(new[] { "X", "Y", "Z" }) { WrapAround = true };
            model.SelectIndex(0);

            // Wrapping backward from 0 goes to last index (2)
            model.MovePrevious();
            Assert.Equal(2, model.SelectedIndex);
            Assert.Equal("Z", model.SelectedItem);

            model.MovePrevious();
            Assert.Equal(1, model.SelectedIndex);
        }

        [Fact]
        public void SelectItem_FindsAndSelectsTarget()
        {
            var items = new List<string> { "Apple", "Banana", "Cherry" };
            var model = new SelectionModel<string>(items);

            bool found = model.SelectItem("Banana");
            Assert.True(found);
            Assert.Equal(1, model.SelectedIndex);
            Assert.Equal("Banana", model.SelectedItem);

            bool notFound = model.SelectItem("Durian");
            Assert.False(notFound);
            Assert.Equal(1, model.SelectedIndex); // Unchanged
        }

        [Fact]
        public void FuncSource_AccessesDynamicCollection()
        {
            var list = new List<int> { 100, 200, 300 };
            var model = new SelectionModel<int>(() => list.Count, i => list[i]);

            Assert.Equal(3, model.Count);
            model.SelectIndex(2);
            Assert.Equal(300, model.SelectedItem);

            list.Add(400);
            Assert.Equal(4, model.Count);
            model.MoveNext();
            Assert.Equal(3, model.SelectedIndex);
            Assert.Equal(400, model.SelectedItem);
        }
    }
}
