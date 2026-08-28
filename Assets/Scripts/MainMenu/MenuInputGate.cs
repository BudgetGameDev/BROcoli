using UnityEngine;

/// <summary>
/// Serialises menu confirm/cancel presses across the main-menu panels.
/// The menu, the mode selector and the settings panel each read the raw
/// keyboard/gamepad state, so without a gate one press is delivered to every
/// panel that becomes active during that same frame - which is how confirming
/// "Play" used to open the mode panel and immediately launch Waves.
/// </summary>
public static class MenuInputGate
{
    private static int submitFrame = -1;
    private static int cancelFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        submitFrame = -1;
        cancelFrame = -1;
    }

    /// <summary>Claims this frame's confirm press; false if something already handled it.</summary>
    public static bool TryConsumeSubmit()
    {
        if (submitFrame == Time.frameCount)
            return false;

        submitFrame = Time.frameCount;
        return true;
    }

    /// <summary>Claims this frame's cancel press; false if something already handled it.</summary>
    public static bool TryConsumeCancel()
    {
        if (cancelFrame == Time.frameCount)
            return false;

        cancelFrame = Time.frameCount;
        return true;
    }
}
