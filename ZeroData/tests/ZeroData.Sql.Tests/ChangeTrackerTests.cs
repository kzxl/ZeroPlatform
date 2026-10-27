using ZeroData.Sql.ChangeTracking;
using Xunit;

namespace ZeroData.Sql.Tests
{
    public class ChangeTrackerTests
    {
        [Fact]
        public void TrackInsert_AddsPendingChange()
        {
            var tracker = new ChangeTracker();
            var product = new Product { Name = "Test" };

            tracker.TrackInsert(product);

            var changes = tracker.GetPendingChanges();
            Assert.Single(changes);
            Assert.Equal(EntityState.Insert, changes[0].State);
            Assert.Same(product, changes[0].Entity);
        }

        [Fact]
        public void TrackDelete_AddsPendingChange()
        {
            var tracker = new ChangeTracker();
            var product = new Product { Id = 1 };

            tracker.TrackDelete(product);

            var changes = tracker.GetPendingChanges();
            Assert.Single(changes);
            Assert.Equal(EntityState.Delete, changes[0].State);
        }

        [Fact]
        public void HasChanges_TrueWhenPending()
        {
            var tracker = new ChangeTracker();
            Assert.False(tracker.HasChanges);

            tracker.TrackInsert(new Product());
            Assert.True(tracker.HasChanges);
        }

        [Fact]
        public void AcceptChanges_ClearsPendingChanges()
        {
            var tracker = new ChangeTracker();
            tracker.TrackInsert(new Product());
            tracker.TrackDelete(new Product { Id = 1 });

            tracker.AcceptChanges();

            Assert.False(tracker.HasChanges);
            Assert.Empty(tracker.GetPendingChanges());
        }

        [Fact]
        public void MultipleInserts_TrackedSeparately()
        {
            var tracker = new ChangeTracker();
            tracker.TrackInsert(new Product { Name = "A" });
            tracker.TrackInsert(new Product { Name = "B" });
            tracker.TrackDelete(new Product { Id = 1 });

            var changes = tracker.GetPendingChanges();
            Assert.Equal(3, changes.Count);
        }
    }
}
