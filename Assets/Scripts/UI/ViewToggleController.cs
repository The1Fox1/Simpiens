using UnityEngine;
using VContainer;
using Simpiens.Diagnostics;
using ZLogger;

namespace Simpiens.UI
{
    /// <summary>
    /// Dumb view controller handling hotkey toggling between the 2D Viewport and the Fullscreen Facility Dashboard.
    /// Strictly handles user input hooks without modifying simulation logic.
    /// </summary>
    public class ViewToggleController : MonoBehaviour
    {
        private FacilityDashboardPresenter _presenter;

        public void Initialize(FacilityDashboardPresenter presenter)
        {
            _presenter = presenter;
        }

        private void Awake()
        {
            if (_presenter == null)
            {
                _presenter = GetComponent<FacilityDashboardPresenter>();
            }
        }

        private void Update()
        {
            bool tabPressed = false;

            // Check Unity New Input System
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame)
            {
                tabPressed = true;
            }

            if (tabPressed && _presenter != null)
            {
                _presenter.ToggleVisibility();
                SimLog.Bootstrap.ZLogDebug($"Interface mode switched; dashboard visible: {_presenter.IsVisible}");
            }
        }
    }
}
