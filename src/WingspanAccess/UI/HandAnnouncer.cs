using UnityEngine;

namespace WingspanAccess.UI;

/// <summary>The local player's hand UI and whether it is open (cards raised for choosing).</summary>
internal static class HandAnnouncer
{
    private static PlayerHandUI _hand;

    /// <summary>Whether the local player's hand is open (cards raised for choosing).</summary>
    public static bool IsOpen()
    {
        PlayerHandUI hand = LocalHand();
        return hand != null && hand.IsInCardPreview;
    }

    public static PlayerHandUI LocalHand()
    {
        if (_hand == null)
        {
            GameObject found = GameObject.Find("GAME/MainCamera/LocalGameStateView/PlayerHand");
            _hand = found == null ? null : found.GetComponent<PlayerHandUI>();
        }

        return _hand;
    }
}
