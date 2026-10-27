using System;
using Xunit;
using ZeroUI.Core.Editors;

namespace ZeroUI.Core.Tests
{
    public class ProcessNewValueEventArgsTests
    {
        [Fact]
        public void Constructor_InitializesDisplayTextCorrectly()
        {
            var args = new ProcessNewValueEventArgs("New Item 123");

            Assert.Equal("New Item 123", args.DisplayText);
            Assert.Null(args.NewValue);
            Assert.False(args.Handled);
        }

        [Fact]
        public void Constructor_HandlesNullDisplayTextGracefully()
        {
            var args = new ProcessNewValueEventArgs(null!);

            Assert.Equal(string.Empty, args.DisplayText);
            Assert.Null(args.NewValue);
            Assert.False(args.Handled);
        }

        [Fact]
        public void HandledAndNewValue_CanBeUpdated()
        {
            var args = new ProcessNewValueEventArgs("Custom Category");
            
            var createdObj = new { Id = 999, Name = "Custom Category" };
            args.NewValue = createdObj;
            args.Handled = true;

            Assert.True(args.Handled);
            Assert.Same(createdObj, args.NewValue);
        }
    }
}
