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

        string description = DescribeCard(view);
        bool? marked = Game.DrawAnnouncer.IsMarked(view);
        return marked == true ? $"{Loc.Get("card.marked")}, {description}" : description;
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

    public static string DescribeBird(BaseBirdCardView view) => string.Join(", ", BirdLines(view));

    /// <summary>
    /// A bird card as separate lines: name, board state (eggs, food, tucked cards), points, habitats,
    /// food cost, nest and egg limit, wingspan, power and, for cards in the hand, where it can be played.
    /// </summary>
    public static List<string> BirdLines(BaseBirdCardView view)
    {
        Dictionary<string, string> texts = NamedTexts(view.gameObject);
        BirdData data = Data(view.ID);
        var parts = new List<string>();
        Add(parts, Get(texts, "Name_TMP") ?? Game.GameReader.BirdName((int)view.ID));
        string boardState = BoardState(view);
        Add(parts, boardState);

        string points = Get(texts, "VP_TMP") ?? data?.VictoryPoints.GetDecrypted().ToString();
        if (points != null)
            parts.Add(int.TryParse(points, out int pointCount) ? Loc.Count("card.points", pointCount) : Loc.Get("card.points", points));

        Add(parts, Habitats(data));
        string food = Get(texts, "Food_TMP");
        if (food != null)
            parts.Add(Loc.Get("card.food", food));
        AddNest(parts, data, boardState != null);

        string wingspan = Get(texts, "Wingspan_TMP");
        if (wingspan != null)
            parts.Add(Loc.Get("card.wingspan", wingspan));

        string header = Get(texts, "BirdPowerHeader_TMP");
        string power = Get(texts, "Description_TMP");
        if (power != null)
            parts.Add(header == null ? power : $"{header} {power}");

        // Cards in the hand: where the rules let you play the bird now (habitat, space, food and eggs).
        if (data != null && view.TryCast<BirdCardView>() != null && UiText.PathOf(view.gameObject).Contains("PlayerHand"))
            Add(parts, Playability(data));

        return parts;
    }

    /// <summary>
    /// Lines for a bird without a card on screen (e.g. on another player's board): what the bird
    /// database and the game state know, and the power from the game's card texts.
    /// </summary>
    public static List<string> BirdLines(int id, Rules.Bird bird)
    {
        BirdData data = Data((BirdName)id);
        var parts = new List<string> { Game.GameReader.BirdName(id) };
        if (bird != null)
            Add(parts, BoardState(bird, data));
        if (data != null)
            parts.Add(Loc.Count("card.points", data.VictoryPoints.GetDecrypted()));
        Add(parts, Habitats(data));
        if (data != null)
        {
            string food = UiText.Clean(data.StringifyFood());
            if (food.Length > 0)
                parts.Add(Loc.Get("card.food", food));
        }

        AddNest(parts, data, bird != null);
        if (data != null)
            parts.Add(Loc.Get("card.wingspan", $"{data.WingspanCM.GetDecrypted()}cm"));
        Add(parts, PowerText(id, data));
        return parts;
    }

    /// <summary>
    /// The power as the card prints it ("BEI AKTIVIERUNG: Erhalte 1 Samen ..."), from the game's texts:
    /// the header after the bird's power type, the text after its id (like the name and the trivia).
    /// </summary>
    private static string PowerText(int birdId, BirdData data)
    {
        string nameTerm = TextLocalizationHelper.GetBirdCardNameTerm(birdId);
        if (string.IsNullOrEmpty(nameTerm) || !nameTerm.Contains("/Names/"))
            return null;
        string power = Translate(nameTerm.Replace("/Names/", "/Powers/"));
        if (power == null)
            return null;
        string header = data == null || data.BirdPowerHeader == BirdPowerHeader.None
            ? null
            : Translate("UI/ActionType/" + data.BirdPowerHeader);
        return header == null ? power : $"{header} {power}";
    }

    private static readonly System.Text.RegularExpressions.Regex Unfilled = new(@"\{\[[^\]]*\]\}");

    private static string Translate(string term)
    {
        string text = I2.Loc.LocalizationManager.GetTranslation(term, true, 0, true, false, null, null, true);
        if (string.IsNullOrEmpty(text))
            return null;
        // Values the card fills in itself ("{[DISCARDED_BONUS]}") stay out.
        text = UiText.Clean(Unfilled.Replace(text, string.Empty));
        return text.Length == 0 ? null : text;
    }

    /// <summary>
    /// Adds what the printed card tells about the bird itself: its Latin name, the continents it lives
    /// on and the trivia text the game shows instead of the power when trivia is switched on.
    /// </summary>
    public static List<string> WithFacts(List<string> lines, int birdId, BaseBirdCardView view)
    {
        BirdData data = Data((BirdName)birdId);
        if (data != null)
        {
            string latin = data.LatinName;
            if (!string.IsNullOrEmpty(latin))
                lines.Add(Loc.Get("card.latin", latin));

            var regions = new List<string>();
            if (data.NorthAmerica.GetDecrypted())
                regions.Add(Loc.Get("region.north_america"));
            if (data.SouthAmerica.GetDecrypted())
                regions.Add(Loc.Get("region.south_america"));
            if (data.Europe.GetDecrypted())
                regions.Add(Loc.Get("region.europe"));
            if (data.Africa.GetDecrypted())
                regions.Add(Loc.Get("region.africa"));
            if (data.Asia.GetDecrypted())
                regions.Add(Loc.Get("region.asia"));
            if (data.Australia.GetDecrypted())
                regions.Add(Loc.Get("region.australia"));
            if (regions.Count > 0)
                lines.Add(Loc.Get("card.regions", string.Join(", ", regions)));
        }

        string trivia = Trivia(birdId, view);
        if (!string.IsNullOrEmpty(trivia))
            lines.Add(Loc.Get("card.trivia", trivia));
        return lines;
    }

    /// <summary>
    /// The trivia text of a bird ("fluff" in the game): from its card view, else from the term next to
    /// its name term ("BirdCards/Names/419" and "BirdCards/Fluffs/419").
    /// </summary>
    private static string Trivia(int birdId, BaseBirdCardView view)
    {
        string term = view?.TryCast<BirdCardView>()?.BirdTrivia?.Term;
        string nameTerm = TextLocalizationHelper.GetBirdCardNameTerm(birdId);
        if (string.IsNullOrEmpty(term) && !string.IsNullOrEmpty(nameTerm) && nameTerm.Contains("/Names/"))
            term = nameTerm.Replace("/Names/", "/Fluffs/");
        if (string.IsNullOrEmpty(term))
            return null;
        string text = I2.Loc.LocalizationManager.GetTranslation(term, true, 0, true, false, null, null, true);
        return string.IsNullOrEmpty(text) ? null : UiText.Clean(text);
    }

    /// <summary>The card view of a bird on the local board, if it is in the scene.</summary>
    public static BaseBirdCardView FindBoardView(Rules.Bird bird)
    {
        if (bird == null)
            return null;
        foreach (BirdSlot slot in Object.FindObjectsOfType<BirdSlot>())
        {
            BaseBirdCardView card = slot.Card;
            if (card != null && card.GameStateBird != null && card.GameStateBird.Pointer == bird.Pointer)
                return card;
        }

        return null;
    }

    private static BirdData Data(BirdName id)
    {
        try
        {
            return (int)id > 0 ? BirdDatabase.Get(id) : null;
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"BirdDatabase.Get({id}) failed: {e.Message}");
            return null;
        }
    }

    private static string Habitats(BirdData data)
    {
        if (data == null)
            return null;
        var habitats = new List<string>();
        if (data.HabitatForest.GetDecrypted())
            habitats.Add(Loc.Get("icon.habitat_forest"));
        if (data.HabitatGrass.GetDecrypted())
            habitats.Add(Loc.Get("icon.habitat_grassland"));
        if (data.HabitatWetlands.GetDecrypted())
            habitats.Add(Loc.Get("icon.habitat_wetland"));
        return habitats.Count == 0 ? null : Loc.Get("card.habitats", string.Join(", ", habitats));
    }

    /// <summary>
    /// The nest with the egg limit in one line ("Höhlennest, bis 4 Eier"); for a bird on a board only
    /// the nest, since its eggs line says "0 von 4 Eiern" already.
    /// </summary>
    private static void AddNest(List<string> parts, BirdData data, bool onBoard)
    {
        if (data == null)
            return;
        string nest = Loc.Get("card.nest", Loc.Get("nest." + data.NestType));
        parts.Add(onBoard ? nest : $"{nest}, {Loc.Count("card.eggs", data.EggLimit.GetDecrypted())}");
    }

    private static string Playability(BirdData data)
    {
        Rules.Player player = Game.GameStateCache.LocalPlayer;
        if (player == null)
            return null;

        CanPlayReturn result;
        try
        {
            result = Rulebook.CanPlay(data, player, null, new Il2CppSystem.Nullable<ActionType>(), 0);
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"Rulebook.CanPlay failed: {e.Message}");
            return null;
        }

        var habitats = new List<string>();
        if (result.CanPlayInForest)
            habitats.Add(Loc.Get("icon.habitat_forest"));
        if (result.CanPlayInGrass)
            habitats.Add(Loc.Get("icon.habitat_grassland"));
        if (result.CanPlayInWetlands)
            habitats.Add(Loc.Get("icon.habitat_wetland"));
        return habitats.Count == 0 ? Loc.Get("card.not_playable_now") : Loc.Get("card.playable_in", string.Join(", ", habitats));
    }

    /// <summary>
    /// For birds on a player board: eggs (of the limit), cached food and tucked cards.
    /// Null for cards that are not on a board.
    /// </summary>
    public static string BoardState(BaseBirdCardView view)
    {
        Rules.Bird bird = view.GameStateBird?.TryCast<Rules.Bird>();
        return bird == null ? null : BoardState(bird, Data(view.ID));
    }

    /// <summary>Eggs (of the limit), cached food and tucked cards of a bird on a board.</summary>
    public static string BoardState(Rules.Bird bird, BirdData data)
    {
        if (bird == null)
            return null;

        int limit = data == null ? 0 : data.EggLimit.GetDecrypted();

        // {0} is the limit, so "card.eggs_on_bird.one" fits a limit of one egg.
        var parts = new List<string> { Loc.Count("card.eggs_on_bird", limit, bird.Eggs) };
        if (bird.CachedFoodCount > 0)
            parts.Add(Loc.Get("game.cached", Game.GameReader.FoodList(bird.CachedFood)));
        if (bird.TuckedCardsCount > 0)
            parts.Add(Loc.Get("game.tucked", bird.TuckedCardsCount));
        return string.Join(", ", parts);
    }

    public static string DescribeBonus(BonusCardView view) => UiText.JoinSentences(BonusLines(view));

    /// <summary>A bonus card as lines: name, condition, details, scoring, share of players, current score.</summary>
    public static List<string> BonusLines(BonusCardView view)
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
        return parts;
    }

    /// <summary>A hand card in short: a bird's name, points and where it can be played; a bonus card's name.</summary>
    public static string HandSummary(CardView view)
    {
        BirdCardView bird = view.TryCast<BirdCardView>();
        if (bird != null)
        {
            BirdData data = Data(bird.ID);
            var parts = new List<string> { Game.GameReader.BirdName((int)bird.ID) };
            if (data != null)
            {
                parts.Add(Loc.Count("card.points", data.VictoryPoints.GetDecrypted()));
                Add(parts, Playability(data));
            }

            return string.Join(", ", parts);
        }

        List<string> lines = CardLines(view);
        return lines.Count == 0 ? string.Empty : lines[0];
    }

    /// <summary>Detail lines for any card view.</summary>
    public static List<string> CardLines(CardView view)
    {
        BaseBirdCardView bird = view.TryCast<BaseBirdCardView>();
        if (bird != null)
            return (int)bird.ID > 0 ? BirdLines(bird) : new List<string> { Loc.Get("card.deck") };
        BonusCardView bonus = view.TryCast<BonusCardView>();
        if (bonus != null)
            return BonusLines(bonus);
        return UiText.CollectTexts(view.gameObject);
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
