using Xunit;
using ZeroUI.Core.Scada.Safety;

namespace ZeroUI.Core.Tests.Scada
{
    public class DeviceSafetySuiteTests
    {
        [Fact]
        public void DeviceStatusFlags_None_HasNoBitsSet()
        {
            var flags = DeviceStatusFlags.None;
            Assert.Equal(0, (int)flags);
            Assert.False(flags.HasFlag(DeviceStatusFlags.LockedOut));
            Assert.False(flags.HasFlag(DeviceStatusFlags.TaggedOut));
            Assert.False(flags.HasFlag(DeviceStatusFlags.Interlocked));
            Assert.False(flags.HasFlag(DeviceStatusFlags.Maintenance));
            Assert.False(flags.HasFlag(DeviceStatusFlags.Fault));
            Assert.False(flags.HasFlag(DeviceStatusFlags.ManualOverride));
        }

        [Fact]
        public void DeviceStatusFlags_LotoCombined_PreservesIndividualFlags()
        {
            var flags = DeviceStatusFlags.LockedOut | DeviceStatusFlags.TaggedOut | DeviceStatusFlags.Maintenance;

            Assert.True(flags.HasFlag(DeviceStatusFlags.LockedOut));
            Assert.True(flags.HasFlag(DeviceStatusFlags.TaggedOut));
            Assert.True(flags.HasFlag(DeviceStatusFlags.Maintenance));
            Assert.False(flags.HasFlag(DeviceStatusFlags.Interlocked));
            Assert.False(flags.HasFlag(DeviceStatusFlags.Fault));
        }

        [Fact]
        public void DeviceStatusFlags_ClearFlag_WorksBitwise()
        {
            var flags = DeviceStatusFlags.LockedOut | DeviceStatusFlags.Fault;
            flags &= ~DeviceStatusFlags.LockedOut;

            Assert.False(flags.HasFlag(DeviceStatusFlags.LockedOut));
            Assert.True(flags.HasFlag(DeviceStatusFlags.Fault));
        }
    }
}
