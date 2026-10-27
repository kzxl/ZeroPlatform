using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Overlays;

namespace ZeroUI.Core.Tests.Overlays
{
    public class ToastStackManagerTests
    {
        [Fact]
        public void ToastEngine_TopRight_StacksVerticallyWithGaps()
        {
            var engine = new ToastStackEngine
            {
                Position = ToastStackPosition.TopRight,
                MarginX = 20,
                MarginY = 30,
                Gap = 10,
                MaxVisibleToasts = 5
            };

            int containerW = 1000;
            int containerH = 800;

            var t1 = new ToastNotificationItem("Item 1", "Title 1", CoreToastType.Info, 3000, width: 300, height: 60);
            var t2 = new ToastNotificationItem("Item 2", "Title 2", CoreToastType.Success, 3000, width: 300, height: 60);

            bool enqueued1 = engine.Enqueue(t1, containerW, containerH);
            bool enqueued2 = engine.Enqueue(t2, containerW, containerH);

            Assert.True(enqueued1);
            Assert.True(enqueued2);
            Assert.Equal(2, engine.ActiveItems.Count);
            Assert.Equal(0, engine.PendingCount);

            // Item 1: X = 1000 - 300 - 20 = 680, Y = 30
            Assert.Equal(680, t1.TargetX);
            Assert.Equal(30, t1.TargetY);

            // Item 2: X = 680, Y = 30 + 60 + 10 = 100
            Assert.Equal(680, t2.TargetX);
            Assert.Equal(100, t2.TargetY);
        }

        [Fact]
        public void ToastEngine_BottomRight_StacksUpward()
        {
            var engine = new ToastStackEngine
            {
                Position = ToastStackPosition.BottomRight,
                MarginX = 20,
                MarginY = 20,
                Gap = 10
            };

            int containerW = 1200;
            int containerH = 900;

            var t1 = new ToastNotificationItem("Item 1", "", CoreToastType.Warning, 3000, width: 320, height: 50);
            var t2 = new ToastNotificationItem("Item 2", "", CoreToastType.Error, 3000, width: 320, height: 50);

            engine.Enqueue(t1, containerW, containerH);
            engine.Enqueue(t2, containerW, containerH);

            // Item 1: X = 1200 - 320 - 20 = 860, Y = 900 - 20 - 50 = 830
            Assert.Equal(860, t1.TargetX);
            Assert.Equal(830, t1.TargetY);

            // Item 2: X = 860, Y = 900 - 20 - 50 - (50 + 10) = 770
            Assert.Equal(860, t2.TargetX);
            Assert.Equal(770, t2.TargetY);
        }

        [Fact]
        public void ToastEngine_ExceedMaxVisible_EnqueuesInPendingFIFO()
        {
            var engine = new ToastStackEngine
            {
                MaxVisibleToasts = 2
            };

            var t1 = new ToastNotificationItem("Item 1");
            var t2 = new ToastNotificationItem("Item 2");
            var t3 = new ToastNotificationItem("Item 3");

            Assert.True(engine.Enqueue(t1, 800, 600));
            Assert.True(engine.Enqueue(t2, 800, 600));
            Assert.False(engine.Enqueue(t3, 800, 600)); // Queued!

            Assert.Equal(2, engine.ActiveItems.Count);
            Assert.Equal(1, engine.PendingCount);

            // Remove t1 -> t3 should be automatically activated and promoted
            bool removed = engine.Remove(t1.Id, 800, 600, 0, 0, out var activated);
            Assert.True(removed);
            Assert.NotNull(activated);
            Assert.Equal(t3.Id, activated!.Id);
            Assert.Equal(2, engine.ActiveItems.Count);
            Assert.Equal(0, engine.PendingCount);
        }

        [Fact]
        public void ToastEngine_RemoveMiddleItem_RelocatesSubsequentToasts()
        {
            var engine = new ToastStackEngine
            {
                Position = ToastStackPosition.TopRight,
                MarginX = 10,
                MarginY = 10,
                Gap = 10
            };

            var t1 = new ToastNotificationItem("1", "", CoreToastType.Info, 3000, 200, 50);
            var t2 = new ToastNotificationItem("2", "", CoreToastType.Info, 3000, 200, 50);
            var t3 = new ToastNotificationItem("3", "", CoreToastType.Info, 3000, 200, 50);

            engine.Enqueue(t1, 800, 600);
            engine.Enqueue(t2, 800, 600);
            engine.Enqueue(t3, 800, 600);

            // Initially:
            // t1: Y = 10
            // t2: Y = 10 + 50 + 10 = 70
            // t3: Y = 70 + 50 + 10 = 130
            Assert.Equal(130, t3.TargetY);

            // Remove t2
            engine.Remove(t2.Id, 800, 600, 0, 0, out _);

            // Now t3 should shift up to Y = 70!
            Assert.Equal(2, engine.ActiveItems.Count);
            Assert.Equal(10, t1.TargetY);
            Assert.Equal(70, t3.TargetY);
        }
    }
}
