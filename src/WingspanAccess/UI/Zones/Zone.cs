using System;
using System.Collections.Generic;

namespace WingspanAccess.UI.Zones;

/// <summary>
/// One thing in a zone: a short summary for Left/Right, detail lines for Up/Down and, when the current
/// task lets the player choose it, the game control that stands for it (selected so Enter acts on it).
/// </summary>
internal sealed class ZoneItem
{
    public string Summary;
    public Func<List<string>> Details;
    public Func<UnityEngine.UI.Selectable> Control;
    public object Tag;

    /// <summary>The bird the item stands for (0 if none), for its sound and spoken trivia.</summary>
    public int BirdId;
}

/// <summary>A part of the game the player can browse with the arrow keys (a habitat, the hand ...).</summary>
internal abstract class Zone
{
    /// <summary>Spoken when the zone is entered: its name and a short overview.</summary>
    public abstract string Title();

    /// <summary>The zone's items as they are now; rebuilt for every key press.</summary>
    public abstract List<ZoneItem> Items();

    public virtual void OnEnter()
    {
    }

    public virtual void OnLeave()
    {
    }

    /// <summary>Whether Enter belongs to the zone instead of the game (e.g. choosing whose board to read).</summary>
    public virtual bool HandlesEnter => false;

    public virtual void Activate(ZoneItem item)
    {
    }

    /// <summary>Index of the item the game has selected, so the cursor follows the game; -1 if none.</summary>
    public virtual int SelectedIndex(List<ZoneItem> items) => -1;

    /// <summary>Makes the game select the item, so Enter and Space act on it. False if the game does not offer it now.</summary>
    public virtual bool Select(ZoneItem item)
    {
        UnityEngine.UI.Selectable control = item.Control?.Invoke();
        if (control == null || !control.gameObject.activeInHierarchy || !control.interactable)
            return false;
        control.Select();
        return true;
    }
}
