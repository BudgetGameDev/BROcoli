using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace BudgetGameDev.Games.Brocoli.Tests
{
    public sealed partial class RuntimeSceneSmokeTests
    {
        private static void ExerciseLevelUpGamepad(LevelUpScreen levelUp)
        {
            Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
            try
            {
                SetHierarchyField(levelUp, "lastNavTime", -10f);
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.DpadRight),
                    () => InvokeHierarchy(levelUp, "HandleControllerNavigation")
                );
                Pulse(
                    gamepad,
                    new GamepadState().WithButton(GamepadButton.South),
                    () => InvokeHierarchy(levelUp, "HandleControllerNavigation")
                );
            }
            finally
            {
                InputSystem.RemoveDevice(gamepad);
            }
        }
    }
}
