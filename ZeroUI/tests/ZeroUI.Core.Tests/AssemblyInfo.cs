using Xunit;

// Disable parallel test execution across test classes in ZeroUI.Core.Tests
// to prevent race conditions on static engines (ZeroTagEngine, UiDispatcher, ScadaAlarmEngine).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
