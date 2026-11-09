using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Localization;

namespace WingspanAccess.UI;

/// <summary>
/// Spoken descriptions of bird and bonus cards. Texts printed on the card are read from the card
/// view (already localized); habitats, nest and egg limit, which the card only shows as icons,
/// come from the bird database.
/// </summary>
internal static class CardDescriber
{
    /// <summary>Description for a selected card (or card slot), or null if the element is no card.</summary>
    public static string Describe(GameObject selected)
    {
        GamepadCardSlot gamepadSlot = selected.GetComponent<GamepadCardSlot>();
        if (gamepadSlot != null && gamepadSlot._slot != null && gamepadSlot._slot.TryCast<DeckSlot>() != null)
            return Loc.Get("card.deck");

        CardView view = FindCardView(selected);
        if (view == null)
            return null;

        // The face-down deck card is a bird view without a bird.
        BaseBirdCardView bird = view.TryCast<BaseBirdCardView>();
        if (bird != null && (int)bird.ID <= 0)
            return Loc.Get("card.deck");

        return DescribeCard(view);
    }

    public static CardView FindCardView(GameObject selected)
    {
        GamepadCardSlot gamepadSlot = selected.GetComponent<GamepadCardSlot>();
        if (gamepadSlot != null)
        {
            if (gamepadSlot._slot != null && gamepadSlot._slot.Card != null)
                return gamepadSlot._slot.Card;
            if (gamepadSlot._birdSlot != null && gamepadSlot._birdSlot.Card != null)
                return gamepadSlot._birdSlot.Card;
        }

        foreach (CardView view in selected.GetComponentsInParent<CardView>(true))
            return view;
        return null;
    }

    public static string DescribeCard(CardView view)
    {
        BaseBirdCardView bird = view.TryCast<BaseBirdCardView>();
        if (bird != null)
            return DescribeBird(bird);

        BonusCardView bonus = view.TryCast<BonusCardView>();
        if (bonus != null)
            return DescribeBonus(bonus);

        return string.Join(", ", UiText.CollectTexts(view.gameObject));
    }

    public static string DescribeBird(BaseBirdCardView view)
    {
        Dictionary<string, string> texts = NamedTexts(view.gameObject);
        var parts = new List<string>();
        Add(parts, Get(texts, "Name_TMP"));

        BirdData data = null;
        try
        {
            data = BirdDatabase.Get(view.ID);
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"BirdDatabase.Get({view.ID}) failed: {e.Message}");
        }

        string points = Get(texts, "VP_TMP") ?? data?.VictoryPoints.GetDecrypted().ToString();
        if (points != null)
            parts.Add(int.TryParse(points, out int pointCount) ? Loc.Count("card.points", pointCount) : Loc.Get("card.points", points));

        if (data != null)
        {
            var habitats = new List<string>();
            if (data.HabitatForest.GetDecrypted())
                habitats.Add(Loc.Get("icon.habitat_forest"));
            if (data.HabitatGrass.GetDecrypted())
                habitats.Add(Loc.Get("icon.habitat_grassland"));
            if (data.HabitatWetlands.GetDecrypted())
                habitats.Add(Loc.Get("icon.habitat_wetland"));
            if (habitats.Count > 0)
                parts.Add(Loc.Get("card.habitats", string.Join(", ", habitats)));
        }

        string food = Get(texts, "Food_TMP");
        if (food != null)
            parts.Add(Loc.Get("card.food", food));

        if (data != null)
        {
            parts.Add(Loc.Get("card.nest", Loc.Get("nest." + data.NestType)));
            parts.Add(Loc.Get("card.eggs", data.EggLimit.GetDecrypted()));
        }

        string wingspan = Get(texts, "Wingspan_TMP");
        if (wingspan != null)
            parts.Add(Loc.Get("card.wingspan", wingspan));

        string header = Get(texts, "BirdPowerHeader_TMP");
        string power = Get(texts, "Description_TMP");
        if (power != null)
            parts.Add(header == null ? power : $"{header} {power}");

        string boardState = BoardState(view);
        if (boardState != null)
            parts.Insert(1, boardState);

        // Cards in the hand: say whether the game would let you play it in the habitat in view.
        BirdCardView handCard = view.TryCast<BirdCardView>();
        if (handCard != null && UIGod.I != null && UiText.PathOf(view.gameObject).Contains("PlayerHand"))
        {
            bool playable = UIGod.I.CanPlayInCurrentHabitat(handCard);
            parts.Add(Loc.Get(playable ? "card.playable" : "card.not_playable",
                Loc.Get("habitat." + UIGod.I.GetCurrentHabitat())));
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// For birds on a player board: eggs (of the limit), cached food and tucked cards.
    /// Null for cards that are not on a board.
    /// </summary>
    public static string BoardState(BaseBirdCardView view)
    {
        Rules.Bird bird = view.GameStateBird?.TryCast<Rules.Bird>();
        if (bird == null)
            return null;

        int limit = 0;
        try
        {
            limit = BirdDatabase.Get(view.ID).EggLimit.GetDecrypted();
        }
        catch (System.Exception)
        {
            // the limit is optional information
        }

        var parts = new List<string> { Loc.Get("card.eggs_on_bird", bird.Eggs, limit) };
        if (bird.CachedFoodCount > 0)
            parts.Add(Loc.Get("game.cached", Game.GameReader.FoodList(bird.CachedFood)));
        if (bird.TuckedCardsCount > 0)
            parts.Add(Loc.Get("game.tucked", bird.TuckedCardsCount));
        return string.Join(", ", parts);
    }

    public static string DescribeBonus(BonusCardView view)
    {
        var parts = new List<string>();
        var visibility = new Visibility(view.transform);
        string name = null, description = null, details = null, percent = null, total = null;
        var scoring = new List<string>();
        foreach (TMPro.TMP_Text text in view.GetComponentsInChildren<TMPro.TMP_Text>(false))
        {
            string clean = UiText.Clean(text.text);
            if (clean.Length == 0 || !visibility.IsVisible(text.transform))
                continue;

            string parent = text.transform.parent == null ? string.Empty : text.transform.parent.name;
            switch (text.name)
            {
                case "Name_TMP": name = clean; break;
                case "Description_TMP": description = clean; break;
                case "Details_TMP": details = clean; break;
                case "Percent_TMP": percent = clean; break;
                case "VP_TMP" when parent == "TotalScore": total = clean; break;
                case "VP_TMP": scoring.Add(ScoringLine(text.transform.parent, clean)); break;
            }
        }

        Add(parts, name);
        Add(parts, description);
        Add(parts, details);
        if (scoring.Count > 0)
            parts.Add(Loc.Get("card.scoring", string.Join("; ", scoring)));
        Add(parts, percent);
        if (total != null)
            parts.Add(Loc.Get("card.current", total));
        return UiText.JoinSentences(parts);
    }

    /// <summary>A bonus card scoring row: its condition label (e.g. "5 - 6 Vögel:", "pro Vogel") and the points.</summary>
    private static string ScoringLine(Transform row, string points)
    {
        string label = null;
        if (row != null)
        {
            foreach (TMPro.TMP_Text text in row.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if (text.name == "VP_TMP")
                    continue;
                string clean = UiText.Clean(text.text);
                if (clean.Length > 0)
                    label = clean;
            }
        }

        string value = Loc.Get("card.points", points);
        if (label == null)
            return value;
        return label.EndsWith(":") ? $"{label} {value}" : $"{value} {label}";
    }

    /// <summary>Visible texts of a card by object name (first occurrence wins).</summary>
    private static Dictionary<string, string> NamedTexts(GameObject root)
    {
        var result = new Dictionary<string, string>();
        foreach (TMPro.TMP_Text text in root.GetComponentsInChildren<TMPro.TMP_Text>(false))
        {
            string clean = UiText.Clean(text.text);
            if (clean.Length > 0 && !result.ContainsKey(text.name))
                result[text.name] = clean;
        }

        return result;
    }

    private static string Get(Dictionary<string, string> texts, string name) =>
        texts.TryGetValue(name, out string value) ? value : null;

    private static void Add(List<string> parts, string value)
    {
        if (!string.IsNullOrEmpty(value))
            parts.Add(value);
    }
}
