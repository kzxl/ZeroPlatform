using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ZeroUI.Core.Workflow
{
    /// <summary>
    /// Represents the execution state of an individual workflow step.
    /// </summary>
    public enum StepStatus
    {
        /// <summary>
        /// Step is awaiting arrival in the workflow pipeline.
        /// </summary>
        Pending,

        /// <summary>
        /// Step is currently active and in progress.
        /// </summary>
        Current,

        /// <summary>
        /// Step has been successfully executed/approved.
        /// </summary>
        Completed,

        /// <summary>
        /// Step encountered an error, rejection, or failure condition.
        /// </summary>
        Error,

        /// <summary>
        /// Step is disabled or skipped in this specific pipeline execution.
        /// </summary>
        Disabled
    }

    /// <summary>
    /// Model representing an individual step in an enterprise multi-step workflow.
    /// </summary>
    public class StepItem
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public StepStatus Status { get; set; } = StepStatus.Pending;
        public bool Enabled { get; set; } = true;
        public object? Tag { get; set; }

        public StepItem() { }

        public StepItem(string key, string title, string? description = null, StepStatus status = StepStatus.Pending)
        {
            Key = key;
            Title = title;
            Description = description;
            Status = status;
        }
    }

    /// <summary>
    /// Pure, headless mathematical and state-management model for workflow steps and progress pipelines.
    /// Decoupled from any UI framework, suitable for WinForms, WPF, Web, and backend state machines.
    /// </summary>
    public class StepBarModel
    {
        private readonly List<StepItem> _steps = new List<StepItem>();
        private int _currentIndex = -1;

        public event EventHandler? ModelChanged;
        public event EventHandler<int>? CurrentIndexChanged;
        public event EventHandler<(int Index, StepStatus OldStatus, StepStatus NewStatus)>? StepStatusChanged;

        public IReadOnlyList<StepItem> Steps => _steps;
        public int Count => _steps.Count;

        public int CurrentIndex
        {
            get => _currentIndex;
            set => SetCurrentStep(value);
        }

        public void AddStep(StepItem step)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            _steps.Add(step);
            if (_currentIndex == -1 && step.Status == StepStatus.Current)
            {
                _currentIndex = _steps.Count - 1;
            }
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddStep(string key, string title, string? description = null, StepStatus status = StepStatus.Pending)
        {
            AddStep(new StepItem(key, title, description, status));
        }

        public void SetSteps(IEnumerable<StepItem> steps)
        {
            _steps.Clear();
            _currentIndex = -1;
            if (steps != null)
            {
                int idx = 0;
                foreach (var step in steps)
                {
                    _steps.Add(step);
                    if (_currentIndex == -1 && step.Status == StepStatus.Current)
                    {
                        _currentIndex = idx;
                    }
                    idx++;
                }
            }
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            _steps.Clear();
            _currentIndex = -1;
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetStepStatus(int index, StepStatus status)
        {
            if (index < 0 || index >= _steps.Count) return;
            var old = _steps[index].Status;
            if (old != status)
            {
                _steps[index].Status = status;
                if (status == StepStatus.Current)
                {
                    _currentIndex = index;
                    CurrentIndexChanged?.Invoke(this, index);
                }
                StepStatusChanged?.Invoke(this, (index, old, status));
                ModelChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SetCurrentStep(int index)
        {
            if (index < 0 || index >= _steps.Count) return;
            if (_currentIndex == index) return;

            // Automatically update statuses: steps before index become Completed (if Pending), current becomes Current
            for (int i = 0; i < _steps.Count; i++)
            {
                if (i < index)
                {
                    if (_steps[i].Status == StepStatus.Pending || _steps[i].Status == StepStatus.Current)
                    {
                        _steps[i].Status = StepStatus.Completed;
                    }
                }
                else if (i == index)
                {
                    _steps[i].Status = StepStatus.Current;
                }
                else
                {
                    if (_steps[i].Status == StepStatus.Current)
                    {
                        _steps[i].Status = StepStatus.Pending;
                    }
                }
            }

            _currentIndex = index;
            CurrentIndexChanged?.Invoke(this, index);
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool NextStep()
        {
            if (_currentIndex + 1 < _steps.Count)
            {
                SetCurrentStep(_currentIndex + 1);
                return true;
            }
            return false;
        }

        public bool PrevStep()
        {
            if (_currentIndex - 1 >= 0)
            {
                SetCurrentStep(_currentIndex - 1);
                return true;
            }
            return false;
        }
    }
}
