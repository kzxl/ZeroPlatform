using Xunit;

// Disable parallel test execution across test classes in ZeroUI.Desktop.Tests
// to prevent thread deadlocks on STA UI elements, Dispatcher message pumps, and static themes.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
