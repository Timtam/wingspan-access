namespace WingspanAccess.Game;

/// <summary>The game's shared "Next" button (bottom right), which decisions enable once they can be confirmed.</summary>
internal static class NextButton
{
    private const string Path = "GAME/Game_Canvas/NEXT_NotchPadding/NEXT";

    private static MCButton _button;

    public static MCButton Find()
    {
        if (_button == null)
        {
            UnityEngine.GameObject found = UnityEngine.GameObject.Find(Path);
            _button = found == null ? null : found.GetComponent<MCButton>();
        }

        return _button;
    }

    /// <summary>Whether the current decision can be confirmed now.</summary>
    public static bool IsAvailable()
    {
        MCButton button = Find();
        return button != null && button.interactable && button.gameObject.activeInHierarchy;
    }
}
