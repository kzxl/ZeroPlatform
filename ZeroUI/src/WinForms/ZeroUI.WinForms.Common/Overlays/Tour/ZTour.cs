using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Overlays
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
    /// Enterprise Interactive Tour / Onboarding Walkthrough Component for Windows Forms matching Ant Design Tour standards.
    /// Provides spotlight control highlighting, dark backdrop cutout masking, dynamic popover cards,
    /// dot indicators, and keyboard navigation.
    /// </summary>
    public class ZTour
    {
        private readonly Form _owner;
        private ZTourBackdropForm? _backdropForm;
        private ZTourOverlayForm? _overlayForm;

        public ObservableCollection<ZTourStep> Steps { get; } = new ObservableCollection<ZTourStep>();

        public int CurrentIndex { get; private set; } = -1;

        /// <summary>
        /// Default width in pixels for step popover cards (default: 440px).
        /// </summary>
        public int CardWidth { get; set; } = 440;

        public ZTourStep? CurrentStep => (CurrentIndex >= 0 && CurrentIndex < Steps.Count) ? Steps[CurrentIndex] : null;

        public bool IsActive => _overlayForm != null && _overlayForm.Visible;

        public event EventHandler<ZTourStepChangedEventArgs>? StepChanged;
        public event EventHandler<bool>? Closed;

        public ZTour(Form owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public ZTour(Form owner, IEnumerable<ZTourStep> steps) : this(owner)
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

        public void Start(int startIndex = 0)
        {
            if (Steps.Count == 0) return;

            if (IsActive) Close(false);

            CurrentIndex = Math.Max(0, Math.Min(Steps.Count - 1, startIndex));

            _backdropForm = new ZTourBackdropForm(_owner, this);
            _backdropForm.Show(_owner);

            _overlayForm = new ZTourOverlayForm(_owner, this);
            _overlayForm.Show(_backdropForm);

            NavigateToStep(CurrentIndex, -1);
        }

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

        public void Previous()
        {
            if (!IsActive || CurrentIndex <= 0) return;

            var prevIndex = CurrentIndex;
            CurrentIndex--;
            NavigateToStep(CurrentIndex, prevIndex);
        }

        public void GoTo(int index)
        {
            if (!IsActive || index < 0 || index >= Steps.Count || index == CurrentIndex) return;

            var prevIndex = CurrentIndex;
            CurrentIndex = index;
            NavigateToStep(CurrentIndex, prevIndex);
        }

        public void Close(bool completed = false)
        {
            if (_overlayForm != null)
            {
                try
                {
                    _overlayForm.Close();
                    _overlayForm.Dispose();
                }
                catch { }
                _overlayForm = null;
            }

            if (_backdropForm != null)
            {
                try
                {
                    _backdropForm.Close();
                    _backdropForm.Dispose();
                }
                catch { }
                _backdropForm = null;
            }

            if (CurrentStep?.OnLeave != null)
            {
                try { CurrentStep.OnLeave(CurrentStep); } catch { }
            }

            Closed?.Invoke(this, completed);
        }

        private void NavigateToStep(int newIndex, int prevIndex)
        {
            if (prevIndex >= 0 && prevIndex < Steps.Count)
            {
                try { Steps[prevIndex].OnLeave?.Invoke(Steps[prevIndex]); } catch { }
            }

            var step = Steps[newIndex];
            try { step.OnEnter?.Invoke(step); } catch { }

            _backdropForm?.UpdateHole(step);
            _backdropForm?.BringToFront();
            _overlayForm?.DisplayStep(step, newIndex, Steps.Count);
            _overlayForm?.BringToFront();
            StepChanged?.Invoke(this, new ZTourStepChangedEventArgs(prevIndex, newIndex, step));
        }

        public static void Show(Form owner, IEnumerable<ZTourStep> steps, int startIndex = 0)
        {
            var tour = new ZTour(owner, steps);
            tour.Start(startIndex);
        }

        /// <summary>
        /// Renders the tour spotlight overlay directly onto an in-memory <see cref="Graphics"/> canvas.
        /// </summary>
        public void RenderToGraphics(System.Drawing.Graphics g)
        {
            _overlayForm?.RenderOverlay(g);
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZTour"/>.
    /// </summary>
    [Obsolete("ZeroTour is deprecated. Please migrate to ZTour instead.")]
    public class ZeroTour : ZTour
    {
        public ZeroTour(Form owner) : base(owner) { }
        public ZeroTour(Form owner, IEnumerable<ZTourStep> steps) : base(owner, steps) { }
    }
}
