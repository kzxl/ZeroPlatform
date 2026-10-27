using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace ZeroUI.Wpf.Overlays
{
    /// <summary>
    /// Event arguments for <see cref="ZTour.StepChanged"/> event.
    /// </summary>
    public sealed class ZTourStepChangedEventArgs : EventArgs
    {
        public int PreviousIndex { get; }
        public int NewIndex { get; }
        public ZTourStep Step { get; }

        public ZTourStepChangedEventArgs(int prev, int next, ZTourStep step)
        {
            PreviousIndex = prev;
            NewIndex = next;
            Step = step;
        }
    }

    /// <summary>
    /// Enterprise Interactive Tour / Onboarding Walkthrough Component matching Ant Design Tour standards.
    /// Provides spotlight element highlighting, dark backdrop cutout masking, dynamic popover cards,
    /// dot indicators, and keyboard navigation.
    /// </summary>
    public class ZTour
    {
        private readonly Window _owner;
        private ZTourOverlayWindow? _overlayWindow;
        private TaskCompletionSource<bool>? _tcs;

        /// <summary>
        /// Gets the collection of guided walkthrough steps in this tour.
        /// </summary>
        public ObservableCollection<ZTourStep> Steps { get; } = new ObservableCollection<ZTourStep>();

        /// <summary>
        /// Gets the current active step index (0-based).
        /// </summary>
        public int CurrentIndex { get; private set; } = -1;

        /// <summary>
        /// Gets the current active step configuration.
        /// </summary>
        public ZTourStep? CurrentStep => (CurrentIndex >= 0 && CurrentIndex < Steps.Count) ? Steps[CurrentIndex] : null;

        /// <summary>
        /// Gets a value indicating whether the tour is currently active and visible on screen.
        /// </summary>
        public bool IsActive => _overlayWindow != null && _overlayWindow.IsVisible;

        /// <summary>
        /// Occurs when the active walkthrough step changes.
        /// </summary>
        public event EventHandler<ZTourStepChangedEventArgs>? StepChanged;

        /// <summary>
        /// Occurs when the tour finishes or is cancelled/skipped by the user.
        /// </summary>
        public event EventHandler<bool>? Closed;

        public ZTour(Window owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public ZTour(Window owner, IEnumerable<ZTourStep> steps) : this(owner)
        {
            if (steps != null)
            {
                foreach (var s in steps) Steps.Add(s);
            }
        }

        /// <summary>
        /// Adds a walkthrough step and returns this tour instance for fluent chaining.
        /// </summary>
        public ZTour AddStep(ZTourStep step)
        {
            if (step != null)
            {
                Steps.Add(step);
            }
            return this;
        }

        /// <summary>
        /// Starts the guided walkthrough beginning at the specified step index.
        /// </summary>
        /// <param name="startIndex">Initial step index (default 0).</param>
        /// <returns>A task that completes with true if the user completed all steps, or false if skipped.</returns>
        public Task<bool> StartAsync(int startIndex = 0)
        {
            if (Steps.Count == 0)
                return Task.FromResult(true);

            if (IsActive)
                Close(false);

            _tcs = new TaskCompletionSource<bool>();
            CurrentIndex = Math.Max(0, Math.Min(Steps.Count - 1, startIndex));

            _overlayWindow = new ZTourOverlayWindow(_owner, this);
            _overlayWindow.Show();

            NavigateToStep(CurrentIndex, -1);

            return _tcs.Task;
        }

        /// <summary>
        /// Advances to the next tour step or completes the tour if on the final step.
        /// </summary>
        public void Next()
        {
            if (!IsActive) return;

            if (CurrentIndex < Steps.Count - 1)
            {
                var prevIndex = CurrentIndex;
                CurrentIndex++;
                NavigateToStep(CurrentIndex, prevIndex);
            }
            else
            {
                Close(completed: true);
            }
        }

        /// <summary>
        /// Moves back to the previous tour step.
        /// </summary>
        public void Previous()
        {
            if (!IsActive || CurrentIndex <= 0) return;

            var prevIndex = CurrentIndex;
            CurrentIndex--;
            NavigateToStep(CurrentIndex, prevIndex);
        }

        /// <summary>
        /// Jumps directly to a specific step index.
        /// </summary>
        public void GoTo(int index)
        {
            if (!IsActive || index < 0 || index >= Steps.Count || index == CurrentIndex) return;

            var prevIndex = CurrentIndex;
            CurrentIndex = index;
            NavigateToStep(CurrentIndex, prevIndex);
        }

        /// <summary>
        /// Closes and dismisses the tour walkthrough.
        /// </summary>
        /// <param name="completed">True if finished normally, false if skipped/cancelled.</param>
        public void Close(bool completed = false)
        {
            if (_overlayWindow != null)
            {
                try
                {
                    _overlayWindow.Close();
                }
                catch { }
                _overlayWindow = null;
            }

            if (CurrentStep?.OnLeave != null)
            {
                try { CurrentStep.OnLeave(CurrentStep); } catch { }
            }

            Closed?.Invoke(this, completed);
            _tcs?.TrySetResult(completed);
        }

        private void NavigateToStep(int newIndex, int prevIndex)
        {
            if (prevIndex >= 0 && prevIndex < Steps.Count)
            {
                try { Steps[prevIndex].OnLeave?.Invoke(Steps[prevIndex]); } catch { }
            }

            var step = Steps[newIndex];
            try { step.OnEnter?.Invoke(step); } catch { }

            _overlayWindow?.DisplayStep(step, newIndex, Steps.Count);
            StepChanged?.Invoke(this, new ZTourStepChangedEventArgs(prevIndex, newIndex, step));
        }

        /// <summary>
        /// Convenience static method to quickly launch a tour walkthrough.
        /// </summary>
        public static Task<bool> ShowAsync(Window owner, IEnumerable<ZTourStep> steps, int startIndex = 0)
        {
            var tour = new ZTour(owner, steps);
            return tour.StartAsync(startIndex);
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZTour"/>.
    /// </summary>
    [Obsolete("ZeroTour is deprecated. Please migrate to ZTour instead.")]
    public class ZeroTour : ZTour
    {
        public ZeroTour(Window owner) : base(owner) { }
        public ZeroTour(Window owner, IEnumerable<ZTourStep> steps) : base(owner, steps) { }
    }
}
