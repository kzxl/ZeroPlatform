using System;
using Xunit;
using ZeroUI.Core.Rendering;

namespace ZeroUI.Core.Tests
{
    public class RenderCommandBufferTests
    {
        [Fact]
        public void RenderCommandBuffer_RecordsAndRetrievesCommands()
        {
            using (var buffer = new RenderCommandBuffer(initialCommandCapacity: 16, initialTextCapacity: 64))
            {
                buffer.AddFillRect(10, 20, 100, 50, 0x00FF0000);
                buffer.AddDrawRect(5, 5, 200, 100, 0x0000FF00, lineWidth: 2);
                buffer.AddDrawLine(0, 0, 50, 50, 0x000000FF);
                buffer.AddDrawText(15, 25, 80, 20, "Pressure: 4.2 bar".AsSpan(), textColorRef: 0x00FFFFFF, isBold: true);

                Assert.Equal(4, buffer.Count);

                var commands = buffer.Commands;
                Assert.Equal(RenderCommandType.FillRect, commands[0].Type);
                Assert.Equal(10, commands[0].X);
                Assert.Equal(20, commands[0].Y);
                Assert.Equal(100, commands[0].Width);
                Assert.Equal(50, commands[0].Height);

                Assert.Equal(RenderCommandType.DrawRect, commands[1].Type);
                Assert.Equal((byte)2, commands[1].LineWidth);

                Assert.Equal(RenderCommandType.DrawText, commands[3].Type);
                Assert.True(commands[3].IsBold);
                Assert.Equal("Pressure: 4.2 bar", buffer.GetTextSpan(commands[3]).ToString());

                // Test clear
                buffer.Clear();
                Assert.Equal(0, buffer.Count);
            }
        }

        [Fact]
        public void RenderCommandBuffer_ZeroAllocationOnReuse()
        {
            using (var buffer = new RenderCommandBuffer(initialCommandCapacity: 1024, initialTextCapacity: 8192))
            {
                // Warmup
                for (int i = 0; i < 100; i++)
                {
                    buffer.Clear();
                    buffer.AddFillRect(i, i, 50, 20, 0x00112233);
                    buffer.AddDrawText(i, i, 50, 20, "Cell Data".AsSpan(), 0x00FFFFFF);
                }

                long before = GC.GetAllocatedBytesForCurrentThread();

                for (int i = 0; i < 1000; i++)
                {
                    buffer.Clear();
                    buffer.AddFillRect(i, i, 50, 20, 0x00112233);
                    buffer.AddDrawText(i, i, 50, 20, "Cell Data".AsSpan(), 0x00FFFFFF);
                }

                long after = GC.GetAllocatedBytesForCurrentThread();
                Assert.Equal(0, after - before);
            }
        }
    }
}
