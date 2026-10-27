using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Process
{
    /// <summary>
    /// ANSI/ISA-88 (IEC 61512) and PackML standard procedural batch execution state machine.
    /// </summary>
    public enum S88BatchState
    {
        Idle,
        Running,
        Pausing,
        Paused,
        Holding,
        Held,
        Restarting,
        Completing,
        Complete,
        Aborting,
        Aborted
    }

    /// <summary>
    /// Type of step within a Sequential Function Chart (SFC) procedural model.
    /// </summary>
    public enum SfcStepType
    {
        Initial,
        NormalStep,
        ParallelBranch,
        AlternativeBranch,
        Terminal
    }

    /// <summary>
    /// Runtime state of an individual SFC recipe step.
    /// </summary>
    public enum SfcStepState
    {
        Inactive,
        Active,
        Completed,
        Held,
        Skipped
    }

    /// <summary>
    /// Represents an individual unit operation or phase step within an ISA-88 recipe.
    /// </summary>
    public class SfcStep
    {
        public int StepId { get; set; }
        public string StepTag { get; set; } = "STEP-01";
        public string StepName { get; set; } = "Step Name";
        public SfcStepType Type { get; set; } = SfcStepType.NormalStep;
        public SfcStepState State { get; set; } = SfcStepState.Inactive;
        public double AllocatedDurationSec { get; set; } = 60.0;
        public double ElapsedDurationSec { get; set; } = 0.0;
        public bool RequiresSignOff { get; set; } = false;
        public string? SignedOffBy { get; set; }
        public DateTime? SignOffTimestamp { get; set; }

        public double ProgressPct => AllocatedDurationSec > 0.0
            ? Math.Max(0.0, Math.Min(100.0, (ElapsedDurationSec / AllocatedDurationSec) * 100.0))
            : (State == SfcStepState.Completed ? 100.0 : 0.0);

        public double RemainingDurationSec => Math.Max(0.0, AllocatedDurationSec - ElapsedDurationSec);
    }

    /// <summary>
    /// Transition condition connecting sequential steps in an SFC chart.
    /// </summary>
    public class SfcTransition
    {
        public int TransitionId { get; set; }
        public int FromStepId { get; set; }
        public int ToStepId { get; set; }
        public string ConditionDescription { get; set; } = "Condition";
        public bool IsSatisfied { get; set; } = false;
    }

    /// <summary>
    /// Pure computation and state execution engine for ISA-88 Batch Recipes and SFC tracking.
    /// Manages automated timer advancement, step transitions, 21 CFR Part 11 sign-offs, and holding logic.
    /// </summary>
    public class SfcRecipeExecutionEngine
    {
        private readonly List<SfcStep> _steps = new List<SfcStep>();
        private readonly List<SfcTransition> _transitions = new List<SfcTransition>();

        public string RecipeName { get; set; } = "Monoclonal Antibody Harvest Recipe";
        public string BatchId { get; set; } = "B2609-MAB-042";
        public S88BatchState State { get; set; } = S88BatchState.Running;
        public string? HoldReason { get; set; }
        public int ActiveStepIndex { get; private set; } = 0;

        public List<SfcStep> Steps => _steps;
        public List<SfcTransition> Transitions => _transitions;

        public SfcRecipeExecutionEngine()
        {
            SeedDefaultRecipe();
        }

        public void SeedDefaultRecipe()
        {
            _steps.Clear();
            _transitions.Clear();

            _steps.Add(new SfcStep { StepId = 1, StepTag = "INIT", StepName = "Vessel Pre-Check & Sterility", Type = SfcStepType.Initial, AllocatedDurationSec = 30, State = SfcStepState.Completed, ElapsedDurationSec = 30 });
            _steps.Add(new SfcStep { StepId = 2, StepTag = "MEDIA", StepName = "Sterile Media Charge & Temp Equil", Type = SfcStepType.NormalStep, AllocatedDurationSec = 60, State = SfcStepState.Active, ElapsedDurationSec = 22 });
            _steps.Add(new SfcStep { StepId = 3, StepTag = "INOC", StepName = "Seed Flask Inoculation & Agitation", Type = SfcStepType.NormalStep, AllocatedDurationSec = 90, RequiresSignOff = true });
            _steps.Add(new SfcStep { StepId = 4, StepTag = "FEED", StepName = "Fed-Batch Glucose & Nutrient Pacing", Type = SfcStepType.NormalStep, AllocatedDurationSec = 120 });
            _steps.Add(new SfcStep { StepId = 5, StepTag = "HARV", StepName = "Biomass Clarification & Harvest", Type = SfcStepType.Terminal, AllocatedDurationSec = 45 });

            for (int i = 0; i < _steps.Count - 1; i++)
            {
                _transitions.Add(new SfcTransition
                {
                    TransitionId = i + 1,
                    FromStepId = _steps[i].StepId,
                    ToStepId = _steps[i + 1].StepId,
                    ConditionDescription = $"Phase {_steps[i].StepTag} Done",
                    IsSatisfied = _steps[i].State == SfcStepState.Completed
                });
            }

            ActiveStepIndex = 1; // Media charge active
        }

        public SfcStep? CurrentStep => ActiveStepIndex >= 0 && ActiveStepIndex < _steps.Count ? _steps[ActiveStepIndex] : null;

        /// <summary>
        /// Advances batch execution clock by delta seconds.
        /// </summary>
        public void AdvanceTime(double deltaSeconds)
        {
            if (State != S88BatchState.Running || deltaSeconds <= 0.0)
                return;

            var step = CurrentStep;
            if (step == null)
            {
                State = S88BatchState.Complete;
                return;
            }

            step.ElapsedDurationSec += deltaSeconds;

            if (step.ElapsedDurationSec >= step.AllocatedDurationSec)
            {
                // Check if sign-off is required before completing
                if (step.RequiresSignOff && string.IsNullOrEmpty(step.SignedOffBy))
                {
                    // Hold batch waiting for electronic signature
                    State = S88BatchState.Holding;
                    HoldReason = $"Operator Sign-off required for Step: {step.StepName}";
                    step.State = SfcStepState.Held;
                    return;
                }

                // Complete current step
                step.State = SfcStepState.Completed;
                step.ElapsedDurationSec = step.AllocatedDurationSec;

                // Satisfy corresponding transition
                if (ActiveStepIndex < _transitions.Count)
                {
                    _transitions[ActiveStepIndex].IsSatisfied = true;
                }

                // Advance to next step
                ActiveStepIndex++;
                if (ActiveStepIndex < _steps.Count)
                {
                    _steps[ActiveStepIndex].State = SfcStepState.Active;
                    _steps[ActiveStepIndex].ElapsedDurationSec = 0.0;
                }
                else
                {
                    State = S88BatchState.Complete;
                }
            }
        }

        /// <summary>
        /// Signs off an active or held step compliant with 21 CFR Part 11 electronic records.
        /// </summary>
        public bool SignOffStep(string operatorName, string? note = null)
        {
            if (string.IsNullOrEmpty(operatorName)) return false;

            var step = CurrentStep;
            if (step == null) return false;

            step.SignedOffBy = operatorName;
            step.SignOffTimestamp = DateTime.UtcNow;

            if (State == S88BatchState.Holding && step.State == SfcStepState.Held)
            {
                step.State = SfcStepState.Active;
                State = S88BatchState.Running;
                HoldReason = null;
            }

            return true;
        }

        public void HoldBatch(string reason)
        {
            State = S88BatchState.Holding;
            HoldReason = reason ?? "Operator Initiated Hold";
            if (CurrentStep != null)
                CurrentStep.State = SfcStepState.Held;
        }

        public void ResumeBatch()
        {
            if (State == S88BatchState.Holding || State == S88BatchState.Held || State == S88BatchState.Paused)
            {
                State = S88BatchState.Running;
                HoldReason = null;
                if (CurrentStep != null && CurrentStep.State == SfcStepState.Held)
                    CurrentStep.State = SfcStepState.Active;
            }
        }

        public void AbortBatch(string reason)
        {
            State = S88BatchState.Aborted;
            HoldReason = reason ?? "Batch Aborted";
        }
    }
}
