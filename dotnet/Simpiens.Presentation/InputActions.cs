namespace Simpiens.Presentation
{
    /// <summary>
    /// What the player can ask of the game with a key (review U1): the device's keys map to these through a bindings
    /// table (<c>KeyBindings</c> in the client), so a key can be rebound, and touch gestures (M4) can map to the same
    /// actions. <see cref="KeyInput"/> is a frame's actions.
    /// </summary>
    public enum InputAction
    {
        // Held: they act every frame the key is down
        PanLeft,
        PanRight,
        PanUp,
        PanDown,

        // Pressed: they act once, on the frame the key goes down
        Reframe,
        Close,
        TogglePanels,
        WallStyle,
        ToggleView,
        Advance,
        DemoCutscene,
        Timings,
        Pause,
        Speed1,
        Speed2,
        Speed3,
        Speed4,
    }
}
