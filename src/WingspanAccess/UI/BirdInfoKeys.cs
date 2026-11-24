using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using WingspanAccess.Game;
using WingspanAccess.Localization;
using WingspanAccess.Speech;

namespace WingspanAccess.UI;

/// <summary>
/// Single keys answer one question about the bird under the zone cursor or the selected card,
/// without stepping through its lines: L where it lives (its habitats, its place on a board, where
/// you can play it now), F its food cost, E its eggs (and what else lies on it), N its nest,
/// P its power, W its wingspan, B the direction of its beak.
/// </summary>
internal static class BirdInfoKeys
{
    private static readonly (KeyCode Key, Func<SelectedBird, string> Answer)[] Questions =
    {
        (KeyCode.L, Habitat),
        (KeyCode.F, Food),
        (KeyCode.E, Eggs),
        (KeyCode.N, Nest),
        (KeyCode.P, Power),
        (KeyCode.W, Wingspan),
        (KeyCode.B, Beak),
    };

    public static void Tick()
    {
        if (Input.KeyboardBridge.TextFieldHasFocus() || ModifierHeld())
            return;

        foreach (var (key, answer) in Questions)
        {
            if (!UnityEngine.Input.GetKeyDown(key))
                continue;

            SelectedBird bird = SelectedBird.Find();
            string text = bird == null ? Loc.Get("sound.no_bird") : answer(bird);
            Plugin.Logger.LogInfo($"[BirdInfo] {key} for bird {bird?.Id ?? 0}" +
                (bird?.OnBoard != null ? $" on the board ({bird.Habitat}, place {bird.Place})" : bird?.InHand == true ? " in the hand" : string.Empty));
            ScreenReader.SayTransient(string.IsNullOrEmpty(text) ? Loc.Get("birdinfo.unknown") : text);
            return;
        }
    }

    private static bool ModifierHeld() =>
        UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl) ||
        UnityEngine.Input.GetKey(KeyCode.LeftAlt) || UnityEngine.Input.GetKey(KeyCode.RightAlt) ||
        UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);

    /// <summary>The habitats on the card, then where the bird sits on a board or where you can play it now.</summary>
    private static string Habitat(SelectedBird bird)
    {
        var parts = new List<string>();
        string habitats = CardDescriber.Habitats(bird.Data);
        if (habitats != null)
            parts.Add(habitats);

        if (bird.OnBoard != null)
        {
            string habitat = Loc.Get("habitat." + bird.Habitat);
            parts.Add(bird.Owner != null && bird.Owner.IsLocalPlayer()
                ? Loc.Get("birdinfo.lives_own", habitat, bird.Place)
                : Loc.Get("birdinfo.lives_other", GameReader.PlayerName(bird.Owner), habitat, bird.Place));
        }
        else if (bird.InHand && bird.Data != null)
        {
            string playable = CardDescriber.Playability(bird.Data);
            if (playable != null)
                parts.Add(playable);
        }

        return string.Join(", ", parts);
    }

    private static string Food(SelectedBird bird)
    {
        if (bird.Data == null)
            return null;
        string food = UiText.Clean(bird.Data.StringifyFood());
        return food.Length == 0 ? Loc.Get("birdinfo.no_food") : Loc.Get("card.food", food);
    }

    /// <summary>On a board: eggs of the limit, cached food and tucked cards; elsewhere the egg limit.</summary>
    private static string Eggs(SelectedBird bird)
    {
        if (bird.OnBoard != null)
            return CardDescriber.BoardState(bird.OnBoard, bird.Data);
        return bird.Data == null ? null : Loc.Count("card.eggs", bird.Data.EggLimit.GetDecrypted());
    }

    private static string Nest(SelectedBird bird) =>
        bird.Data == null ? null : Loc.Get("card.nest", Loc.Get("nest." + bird.Data.NestType));

    /// <summary>The colour of the power and its text as the card prints it.</summary>
    private static string Power(SelectedBird bird)
    {
        if (bird.Data == null)
            return null;
        var parts = new List<string>();
        string colour = "power_color." + bird.Data.PowerType;
        if (Loc.Has(colour))
            parts.Add(Loc.Get(colour));
        string text = bird.Data.PowerType == BirdPowerType.None ? null : CardDescriber.PowerText(bird.Id, bird.Data);
        if (text != null)
            parts.Add(text);
        return string.Join(", ", parts);
    }

    private static string Wingspan(SelectedBird bird) => CardDescriber.Wingspan(bird.Data);

    private static string Beak(SelectedBird bird)
    {
        string key = bird.Data == null ? null : "beak." + bird.Data.BeakDirection;
        return key != null && Loc.Has(key) ? Loc.Get(key) : null;
    }

    /// <summary>The bird a question is about, with what the game state knows of it.</summary>
    private sealed class SelectedBird
    {
        public int Id;
        public BirdData Data;

        /// <summary>The bird on a board, its owner, habitat and place (1 = leftmost); null if it is on none.</summary>
        public Rules.Bird OnBoard;
        public Rules.Player Owner;
        public ActionType Habitat;
        public int Place;

        /// <summary>In your hand, or being played.</summary>
        public bool InHand;

        /// <summary>
        /// The bird under the zone cursor; outside zones the bird the atlas shows, the selected bird card,
        /// else the bird being played (while its habitat is chosen or its food paid). Null if none.
        /// </summary>
        public static SelectedBird Find()
        {
            Rules.Bird boardBird = null;
            bool playing = false;
            int id;
            if (Zones.ZoneNavigator.IsActive)
            {
                id = Zones.ZoneNavigator.CurrentBirdId;
            }
            else
            {
                id = AtlasDescriber.CurrentBirdId;
                if (id <= 0)
                {
                    GameObject selected = EventSystem.current == null ? null : EventSystem.current.currentSelectedGameObject;
                    BaseBirdCardView view = selected == null ? null : CardDescriber.FindCardView(selected)?.TryCast<BaseBirdCardView>();
                    id = view == null ? 0 : (int)view.ID;
                    boardBird = view?.GameStateBird?.TryCast<Rules.Bird>();
                }

                if (id <= 0)
                {
                    BirdData played = Screens.Find<PlayBird_Decision>()?._currentData?.BirdData;
                    id = played == null ? 0 : (int)played.BirdName;
                    playing = id > 0;
                }
            }

            if (id <= 0)
                return null;

            var bird = new SelectedBird { Id = id, Data = CardDescriber.DataOf(id) };
            bird.FindOnBoard(boardBird);
            bird.InHand = playing || (bird.OnBoard == null && InLocalHand(id));
            return bird;
        }

        /// <summary>The board, habitat and place of the bird (the card's own bird where the card knows it).</summary>
        private void FindOnBoard(Rules.Bird known)
        {
            Rules.GameState state = GameStateCache.State;
            if (state?.Players == null)
                return;
            ActionType[] habitats = { ActionType.GainFood, ActionType.LayEggs, ActionType.DrawBirds };
            for (int p = 0; p < state.Players.Count; p++)
            {
                Rules.Player player = state.Players[p];
                if (player == null)
                    continue;
                foreach (ActionType habitat in habitats)
                {
                    var birds = player.GetAllBirdsInHabitat(habitat);
                    for (int i = 0; birds != null && i < birds.Count; i++)
                    {
                        Rules.Bird candidate = birds[i];
                        if (candidate == null || (known != null ? candidate.Pointer != known.Pointer : candidate.ID != Id))
                            continue;
                        OnBoard = candidate;
                        Owner = player;
                        Habitat = habitat;
                        Place = i + 1;
                        return;
                    }
                }
            }
        }

        private static bool InLocalHand(int id)
        {
            var cards = GameStateCache.LocalPlayer?.P_BirdCards;
            for (int i = 0; cards != null && i < cards.Count; i++)
            {
                if (cards[i].GetDecrypted() == id)
                    return true;
            }

            return false;
        }
    }
}
