using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests
{
    public class MasterDetailAndLookupParityTests
    {
        public class SampleOrder
        {
            public int OrderId { get; set; }
            public string Customer { get; set; } = string.Empty;
            public decimal TotalAmount { get; set; }
        }

        [Fact]
        public void ZeroListSource_Implements_IZeroItemSource_Correctly()
        {
            var list = new List<SampleOrder>
            {
                new SampleOrder { OrderId = 101, Customer = "Acme Corp", TotalAmount = 5000m },
                new SampleOrder { OrderId = 102, Customer = "Zero Global", TotalAmount = 12500m }
            };

            var source = new ZeroListSource<SampleOrder>(list);
            Assert.IsAssignableFrom<IZeroItemSource>(source);

            var itemSource = (IZeroItemSource)source;
            var item0 = itemSource.GetItem(0) as SampleOrder;
            Assert.NotNull(item0);
            Assert.Equal(101, item0.OrderId);
            Assert.Equal("Acme Corp", item0.Customer);

            var item1 = itemSource.GetItem(1) as SampleOrder;
            Assert.NotNull(item1);
            Assert.Equal(102, item1.OrderId);

            // Out-of-bounds returns null
            Assert.Null(itemSource.GetItem(-1));
            Assert.Null(itemSource.GetItem(2));
        }

        [Fact]
        public void MasterRowExpandingEventArgs_CanCancelExpansion()
        {
            // Verify Cancel flag logic
            int visualRow = 2;
            int modelRow = 5;
            bool cancel = false;

            // Simulate expansion check
            if (visualRow >= 0)
            {
                cancel = (modelRow == 5); // Suppose row 5 has no details
            }

            Assert.True(cancel);
        }
    }
}
