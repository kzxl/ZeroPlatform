using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.Security;

namespace ZeroUI.Core.Tests
{
    public class DurableAuditLoggerTests : IDisposable
    {
        private readonly string _tempFile;

        public DurableAuditLoggerTests()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), $"audit_test_{Guid.NewGuid():N}.queue");
        }

        public void Dispose()
        {
            try
            {
                if (File.Exists(_tempFile))
                {
                    File.Delete(_tempFile);
                }
            }
            catch { }
        }

        [Fact]
        public async Task Log_ValidEntry_PersistsToDiskQueueAndCache()
        {
            using var logger = new DurableAuditLogger(_tempFile, capacityPowerOfTwo: 64, slotSize: 1024);

            var entry1 = new OperationLogEntry(
                action: "UserLogin",
                username: "operator_01",
                category: "Security",
                level: OperationLogLevel.Notice,
                details: "Station A-01 RFID badge authenticated");

            var entry2 = new OperationLogEntry(
                action: "SetSetpoint",
                username: "engineer_02",
                targetResource: "Reactor-101",
                oldValue: "75.0",
                newValue: "82.5",
                level: OperationLogLevel.SecurityAudit,
                category: "Recipe",
                details: "Temperature setpoint updated");

            logger.Log(entry1);
            logger.Log(entry2);

            Assert.Equal(2, logger.QueueCount);

            var logs = await logger.QueryLogsAsync();
            Assert.Equal(2, logs.Count);
            Assert.Equal("SetSetpoint", logs[0].Action); // Latest first
            Assert.Equal("UserLogin", logs[1].Action);
        }

        [Fact]
        public async Task QueryLogsAsync_FilterByCriteria_ReturnsExpectedSubset()
        {
            using var logger = new DurableAuditLogger(_tempFile, capacityPowerOfTwo: 64, slotSize: 1024);

            logger.Log(new OperationLogEntry("ActionA", username: "userA", category: "Cat1", level: OperationLogLevel.Info));
            logger.Log(new OperationLogEntry("ActionB", username: "userB", category: "Cat2", level: OperationLogLevel.Warning));
            logger.Log(new OperationLogEntry("ActionC", username: "userA", category: "Cat1", level: OperationLogLevel.Critical));

            // Filter by username
            var userALogs = await logger.QueryLogsAsync(new OperationLogFilter { Username = "userA" });
            Assert.Equal(2, userALogs.Count);

            // Filter by min level
            var warningLogs = await logger.QueryLogsAsync(new OperationLogFilter { MinLevel = OperationLogLevel.Warning });
            Assert.Equal(2, warningLogs.Count); // Warning & Critical

            // Filter by category
            var cat2Logs = await logger.QueryLogsAsync(new OperationLogFilter { Category = "Cat2" });
            Assert.Single(cat2Logs);
            Assert.Equal("ActionB", cat2Logs[0].Action);
        }

        [Fact]
        public void Log_NullEntry_ThrowsArgumentNullException()
        {
            using var logger = new DurableAuditLogger(_tempFile);
            Assert.Throws<ArgumentNullException>(() => logger.Log(null!));
        }

        [Fact]
        public void Dispose_ClosesUnderlyingQueue_ThrowsOnSubsequentCalls()
        {
            var logger = new DurableAuditLogger(_tempFile);
            logger.Dispose();

            Assert.Throws<ObjectDisposedException>(() => logger.Log(new OperationLogEntry("Test")));
        }
    }
}
