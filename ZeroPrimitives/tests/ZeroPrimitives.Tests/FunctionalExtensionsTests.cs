using System.Threading.Tasks;
using Xunit;
using ZeroPrimitives.Extensions;

namespace ZeroPrimitives.Tests
{
    public class FunctionalExtensionsTests
    {
        public class UserProfile
        {
            public string Name { get; set; } = null!;
            public string Email { get; set; } = null!;
            public string Note { get; set; } = "Existing";
        }

        [Fact]
        public void Let_ExecutesWhenNotNull()
        {
            string? name = "Alice";
            int length = name.Let(n => n.Length);
            Assert.Equal(5, length);

            string? nullName = null;
            int nullLength = nullName.Let(n => n.Length);
            Assert.Equal(0, nullLength);

            bool actionExecuted = false;
            name.Let(_ => actionExecuted = true);
            Assert.True(actionExecuted);
        }

        [Fact]
        public async Task AsTask_WrapsValueInCompletedTask()
        {
            int val = 42;
            Task<int> task = val.AsTask();
            Assert.True(task.IsCompleted);
            Assert.Equal(42, await task);
        }

        [Fact]
        public void ReplaceNullStrings_SetsEmptyStringsForNullProperties()
        {
            var profile = new UserProfile
            {
                Name = null!,
                Email = null!,
                Note = "Existing"
            };

            profile.ReplaceNullStrings();

            Assert.Equal("", profile.Name);
            Assert.Equal("", profile.Email);
            Assert.Equal("Existing", profile.Note);
        }
    }
}
