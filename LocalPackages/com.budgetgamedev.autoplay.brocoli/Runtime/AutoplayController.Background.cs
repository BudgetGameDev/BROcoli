using BudgetGameDev.Shared;
using UnityEngine;

namespace BudgetGameDev.Games.Brocoli
{
    public partial class AutoplayController
    {
        private bool _backgroundPolicyApplied;
        private bool _backgroundBeforeRun;
        private bool _focusSuppressionBeforeRun;

        internal void BeginBackgroundExecution()
        {
            if (_backgroundPolicyApplied)
                return;
            _backgroundBeforeRun = Application.runInBackground;
            _focusSuppressionBeforeRun = ForceLandscapeAspect.SuppressFocusLossPause;
            _backgroundPolicyApplied = true;
            Application.runInBackground = true;
            ForceLandscapeAspect.SuppressFocusLossPause = true;
        }

        private void RestoreBackgroundExecution()
        {
            if (!_backgroundPolicyApplied)
                return;
            Application.runInBackground = _backgroundBeforeRun;
            ForceLandscapeAspect.SuppressFocusLossPause = _focusSuppressionBeforeRun;
            _backgroundPolicyApplied = false;
        }

        private void OnEnable()
        {
            if (_config != null)
                BeginBackgroundExecution();
        }

        private void OnDisable() => RestoreBackgroundExecution();
    }
}
