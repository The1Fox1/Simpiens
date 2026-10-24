using System;
using System.Collections.Generic;

namespace Simpiens.Cognition.Planning
{
    /// <summary>
    /// Represents a sequenced, executable plan of GOAP actions.
    /// Designed for reuse with zero heap allocations during execution.
    /// </summary>
    public class GoapPlan
    {
        private readonly List<GoapAction> _actions;

        public IReadOnlyList<GoapAction> Actions => _actions;
        public int Count => _actions.Count;
        public int CurrentStepIndex { get; private set; }
        public float TotalCost { get; private set; }

        public bool IsEmpty => _actions.Count == 0;
        public bool IsFinished => CurrentStepIndex >= _actions.Count;

        public GoapAction CurrentAction => IsFinished ? null : _actions[CurrentStepIndex];

        public GoapPlan(int initialCapacity = 16)
        {
            _actions = new List<GoapAction>(initialCapacity);
            CurrentStepIndex = 0;
            TotalCost = 0f;
        }

        /// <summary>
        /// Clears all actions and resets step tracking for plan reuse.
        /// </summary>
        public void Clear()
        {
            _actions.Clear();
            CurrentStepIndex = 0;
            TotalCost = 0f;
        }

        /// <summary>
        /// Copies another plan's actions and costs into this reusable plan without allocating.
        /// </summary>
        public void CopyFrom(GoapPlan other)
        {
            if (other == null)
            {
                Clear();
                return;
            }

            _actions.Clear();
            int count = other._actions.Count;
            for (int i = 0; i < count; i++)
            {
                _actions.Add(other._actions[i]);
            }
            TotalCost = other.TotalCost;
            CurrentStepIndex = other.CurrentStepIndex;
        }

        /// <summary>
        /// Appends an action to the end of the plan sequence.
        /// </summary>
        public void AddAction(GoapAction action, float cost)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            _actions.Add(action);
            TotalCost += cost;
        }

        /// <summary>
        /// Advances the plan execution to the next action step.
        /// </summary>
        /// <returns>True if there is another step to execute; false if the plan has completed.</returns>
        public bool AdvanceStep()
        {
            if (!IsFinished)
            {
                CurrentStepIndex++;
            }
            return !IsFinished;
        }

        /// <summary>
        /// Resets the step pointer to the beginning without clearing actions.
        /// </summary>
        public void ResetProgress()
        {
            CurrentStepIndex = 0;
        }

        public override string ToString()
        {
            if (IsEmpty) return "[GoapPlan: Empty]";
            var sb = new System.Text.StringBuilder();
            sb.Append($"[GoapPlan: Step {CurrentStepIndex}/{_actions.Count}, Cost={TotalCost:F1} | ");
            for (int i = 0; i < _actions.Count; i++)
            {
                if (i > 0) sb.Append(" -> ");
                if (i == CurrentStepIndex) sb.Append("*");
                sb.Append(_actions[i].Name);
                if (i == CurrentStepIndex) sb.Append("*");
            }
            sb.Append("]");
            return sb.ToString();
        }
    }
}
