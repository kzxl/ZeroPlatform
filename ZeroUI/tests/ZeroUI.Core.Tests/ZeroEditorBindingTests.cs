using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Editors;

namespace ZeroUI.Core.Tests
{
    public class ZeroEditorBindingTests
    {
        private sealed class MockEditor : IZeroEditor
        {
            private object? _value;

            public object? EditValue
            {
                get => _value;
                set
                {
                    _value = value;
                    IsModified = true;
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            public bool IsModified { get; set; }
            public bool ReadOnly { get; set; }

            public event EventHandler? EditValueChanged;

            public void Reset()
            {
                _value = null;
                IsModified = false;
                EditValueChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Clear() => Reset();
        }

        private class SampleCustomerModel
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal CreditLimit { get; set; }
            public bool IsActive { get; set; }
            public DateTime RegisteredDate { get; set; }
        }

        [Fact]
        public void ZeroEditor_BasicContract_TriggersEventsAndTracksDirty()
        {
            var editor = new MockEditor();
            bool eventFired = false;
            editor.EditValueChanged += (s, e) => eventFired = true;

            Assert.False(editor.IsModified);
            Assert.Null(editor.EditValue);

            editor.EditValue = "Hello Enterprise";
            Assert.True(eventFired);
            Assert.True(editor.IsModified);
            Assert.Equal("Hello Enterprise", editor.EditValue);

            editor.Reset();
            Assert.False(editor.IsModified);
            Assert.Null(editor.EditValue);
        }

        [Fact]
        public void ZeroDataBinder_Populate_DistributesModelPropertiesToEditors()
        {
            var model = new SampleCustomerModel
            {
                Id = 101,
                Name = "Acme Corp",
                CreditLimit = 50000.75m,
                IsActive = true,
                RegisteredDate = new DateTime(2026, 1, 15)
            };

            var txtName = new MockEditor();
            var spinLimit = new MockEditor();
            var chkActive = new MockEditor();
            var dtReg = new MockEditor();

            var map = new Dictionary<string, IZeroEditor>
            {
                [nameof(SampleCustomerModel.Name)] = txtName,
                [nameof(SampleCustomerModel.CreditLimit)] = spinLimit,
                [nameof(SampleCustomerModel.IsActive)] = chkActive,
                [nameof(SampleCustomerModel.RegisteredDate)] = dtReg
            };

            ZeroDataBinder.Populate(model, map);

            Assert.Equal("Acme Corp", txtName.EditValue);
            Assert.Equal(50000.75m, spinLimit.EditValue);
            Assert.Equal(true, chkActive.EditValue);
            Assert.Equal(new DateTime(2026, 1, 15), dtReg.EditValue);

            // Populate resets IsModified so the form starts Clean
            Assert.False(txtName.IsModified);
            Assert.False(spinLimit.IsModified);
            Assert.False(chkActive.IsModified);
            Assert.False(dtReg.IsModified);
            Assert.False(ZeroDataBinder.IsModified(map.Values));
        }

        [Fact]
        public void ZeroDataBinder_Collect_GathersAndConvertsValuesIntoModel()
        {
            var txtName = new MockEditor { EditValue = "Global Tech Logistics" };
            var spinLimit = new MockEditor { EditValue = "125000.50" }; // string to decimal conversion
            var chkActive = new MockEditor { EditValue = true };
            var dtReg = new MockEditor { EditValue = new DateTime(2026, 9, 5) };

            var map = new Dictionary<string, IZeroEditor>
            {
                [nameof(SampleCustomerModel.Name)] = txtName,
                [nameof(SampleCustomerModel.CreditLimit)] = spinLimit,
                [nameof(SampleCustomerModel.IsActive)] = chkActive,
                [nameof(SampleCustomerModel.RegisteredDate)] = dtReg
            };

            var updatedModel = ZeroDataBinder.Collect<SampleCustomerModel>(new SampleCustomerModel(), map);

            Assert.Equal("Global Tech Logistics", updatedModel.Name);
            Assert.Equal(125000.50m, updatedModel.CreditLimit);
            Assert.True(updatedModel.IsActive);
            Assert.Equal(new DateTime(2026, 9, 5), updatedModel.RegisteredDate);
        }

        [Fact]
        public void ZeroDataBinder_DirtyTrackingAndReset_WorksReliably()
        {
            var ed1 = new MockEditor();
            var ed2 = new MockEditor();
            var list = new[] { ed1, ed2 };

            Assert.False(ZeroDataBinder.IsModified(list));

            ed2.EditValue = "Changed";
            Assert.True(ZeroDataBinder.IsModified(list));

            ZeroDataBinder.Reset(list);
            Assert.False(ZeroDataBinder.IsModified(list));
            Assert.Null(ed1.EditValue);
            Assert.Null(ed2.EditValue);
        }
    }
}
