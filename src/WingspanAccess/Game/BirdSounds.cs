using UnityEngine;
using UnityEngine.EventSystems;
using WingspanAccess.Localization;
using WingspanAccess.Speech;
using WingspanAccess.UI;

namespace WingspanAccess.Game;

/// <summary>
/// S plays the call of the bird under the zone cursor (or of the selected card), V its spoken trivia,
/// like the sound buttons of the bird atlas.
/// </summary>
internal static class BirdSounds
{
    public static void Tick()
    {
        bool song = UnityEngine.Input.GetKeyDown(KeyCode.S);
        bool voice = UnityEngine.Input.GetKeyDown(KeyCode.V);
        if ((!song && !voice) || Input.KeyboardBridge.TextFieldHasFocus() || ModifierHeld())
            return;

        int id = CurrentBird(out BaseBirdCardView view);
        if (id <= 0)
        {
            if (UI.Zones.ZoneNavigator.IsActive || CardSelected())
                ScreenReader.SayTransient(Loc.Get("sound.no_bird"));
            return;
        }

        BirdAudioManager audio = BirdAudioManager.I;
        if (audio == null)
        {
            ScreenReader.SayTransient(Loc.Get("sound.unavailable"));
            return;
        }

        try
        {
            if (song)
            {
                float length = audio.PlayMain((BirdName)id);
                Plugin.Logger.LogInfo($"[Sound] call of bird {id}: {length:0.0}s");
                if (length <= 0f)
                    ScreenReader.SayTransient(Loc.Get("sound.no_song", GameReader.BirdName(id)));
            }
            else
            {
                BirdCardView card = view?.TryCast<BirdCardView>();
                Plugin.Logger.LogInfo($"[Sound] trivia of bird {id} (card view {card != null})");
                if (card != null)
                    card.PlayVoiceOver();
                else
                    audio.PlayVoiceOver((BirdName)id, true, false);
            }
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Bird sound for {id} failed: {e.Message}");
            ScreenReader.SayTransient(Loc.Get("sound.unavailable"));
        }
    }

    private static bool ModifierHeld() =>
        UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl) ||
        UnityEngine.Input.GetKey(KeyCode.LeftAlt) || UnityEngine.Input.GetKey(KeyCode.RightAlt) ||
        UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);

    /// <summary>The bird under the zone cursor, else the bird card the game has selected.</summary>
    private static int CurrentBird(out BaseBirdCardView view)
    {
        view = null;
        int id = UI.Zones.ZoneNavigator.CurrentBirdId;
        if (id > 0)
        {
            view = FindView(id);
            return id;
        }

        GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        CardView card = selected == null ? null : CardDescriber.FindCardView(selected);
        view = card?.TryCast<BaseBirdCardView>();
        return view == null ? 0 : (int)view.ID;
    }

    private static bool CardSelected()
    {
        GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
        return selected != null && CardDescriber.FindCardView(selected) != null;
    }

    private static BaseBirdCardView FindView(int id)
    {
        foreach (BirdCardView card in Object.FindObjectsOfType<BirdCardView>())
        {
            if (card != null && (int)card.ID == id)
                return card;
        }

        return null;
    }
}
