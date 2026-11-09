using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;
using WingspanAccess.UI;

namespace WingspanAccess.Game;

/// <summary>
/// Playing a bird runs through states (habitat, food payment, egg payment ...). The payment
/// panels only show icons, so this announces each state with the cost and the payment the
/// game proposes, read from PlayBird_Decision's PlayBirdData.
/// </summary>
internal static class PlayBirdAnnouncer
{
    private static int _stateId;
    private static int _candidateId;
    private static string _lastPayment;
    private static int _nextCheck;

    public static void Tick(GamepadScreen top)
    {
        PlayBird_Decision decision = top == null ? null : top.TryCast<PlayBird_Decision>();
        if (decision == null)
        {
            _stateId = 0;
            _lastPayment = null;
            return;
        }

        if (Time.frameCount < _nextCheck)
            return;
        _nextCheck = Time.frameCount + 10;

        var state = decision.CurrentState;
        if (state == null)
            return;

        string payment = PaymentText(decision);
        int stateId = state.GetInstanceID();
        if (stateId != _stateId)
        {
            // States the game passes through without stopping (e.g. copying a white power for
            // birds that have none) are not announced: a state must stay for two checks.
            if (stateId != _candidateId)
            {
                _candidateId = stateId;
                return;
            }

            _stateId = stateId;
            _lastPayment = payment;
            string typeName = state.GetIl2CppType().Name;
            string key = "playbird." + typeName;
            var parts = new List<string>();
            if (Loc.Has(key))
                parts.Add(Loc.Get(key));
            if (typeName == "CostPaymentStateController")
            {
                parts.Add(CostText(decision));
                parts.Add(payment);
                if (payment != Loc.Get("playbird.choose_payment"))
                    parts.Add(Loc.Get("playbird.confirm_hint"));
            }
            else if (typeName == "EggsPaymentStateController")
            {
                parts.Add(Loc.Count("playbird.egg_cost", EggCost(decision)));
                parts.Add(payment);
                parts.Add(Loc.Get("playbird.egg_hint"));
            }

            Plugin.Logger.LogInfo($"[PlayBird] state {typeName}");
            ScreenReader.Say(UiText.JoinSentences(parts), interrupt: false);
            return;
        }

        if (payment != _lastPayment)
        {
            _lastPayment = payment;
            ScreenReader.Say(payment);
        }
    }

    /// <summary>
    /// The food payment as the cost panel shows it: the game's proposal in the payment panel, or
    /// the tokens picked one by one after "Change" (before the game writes them into the payment).
    /// Null outside the food cost state.
    /// </summary>
    private static string ShownPaymentText(PlayBird_Decision decision)
    {
        var cost = decision.CurrentState?.TryCast<View.Decisions.PlayBird.States.CostPaymentStateController>();
        if (cost == null)
            return null;

        var change = cost.ChangePaymentPanel;
        var choose = cost.ChoosePaymentPanel;
        bool manual = change != null && change.gameObject.activeInHierarchy;
        FoodAboutToPay shown = manual ? change.foodAboutToPay
            : choose != null && choose.gameObject.activeInHierarchy ? choose.FoodAboutToPay : null;
        if (shown == null)
        {
            var pictures = manual ? null : PictureFood(cost);
            return pictures == null || pictures.Count == 0
                ? null
                : Loc.Get("playbird.payment", string.Join(", ", pictures.ConvertAll(pair => $"{pair.Value} {pair.Key}")));
        }

        var counts = new List<KeyValuePair<string, int>>();
        var buttons = shown.FoodButtons;
        for (int i = 0; buttons != null && i < buttons.Count; i++)
        {
            FoodButton button = buttons[i];
            if (button == null || !button.gameObject.activeInHierarchy)
                continue;
            string name = GameReader.FoodTypeName(button.FoodType);
            int index = counts.FindIndex(pair => pair.Key == name);
            if (index < 0)
                counts.Add(new KeyValuePair<string, int>(name, 1));
            else
                counts[index] = new KeyValuePair<string, int>(name, counts[index].Value + 1);
        }

        // The proposal panel can show the payment as plain pictures (e.g. when only one payment is
        // possible); then the pictures next to "Pay" are read.
        if (counts.Count == 0 && !manual)
            counts = PictureFood(cost);
        if (counts.Count == 0)
            return manual ? Loc.Get("playbird.choose_food") : null;
        string food = string.Join(", ", counts.ConvertAll(pair => $"{pair.Value} {pair.Key}"));
        string text = Loc.Get("playbird.payment", food);
        if (!manual)
            return text;
        var parts = new List<string> { text };
        if (change.TooMuchFoodWarning != null && UiText.IsShown(change.TooMuchFoodWarning))
            parts.Add(UiText.Clean(change.TooMuchFoodWarning.text));
        parts.Add(Loc.Get(NextButton.IsAvailable() ? "dice.confirm" : "playbird.not_enough"));
        return UiText.JoinSentences(parts);
    }

    private static readonly (string Part, FoodType Type)[] FoodSprites =
    {
        ("invertebrate", FoodType.Invertebrate), ("seed", FoodType.Seed), ("fruit", FoodType.Fruit),
        ("fish", FoodType.Fish), ("rodent", FoodType.Rodent), ("nectar", FoodType.Nectar),
    };

    private static string _loggedPictures;

    /// <summary>Food shown as pictures in the cost panel's payment row (the food panel, without its buttons).</summary>
    private static List<KeyValuePair<string, int>> PictureFood(View.Decisions.PlayBird.States.CostPaymentStateController cost)
    {
        var counts = new List<KeyValuePair<string, int>>();
        Transform panel = null;
        foreach (Transform child in cost.GetComponentsInChildren<Transform>(false))
        {
            if (child.name == "FoodPanel")
            {
                panel = child;
                break;
            }
        }

        if (panel == null)
            return counts;
        var names = new List<string>();
        foreach (UnityEngine.UI.Image image in panel.GetComponentsInChildren<UnityEngine.UI.Image>(false))
        {
            if (image.sprite == null || image.transform.parent == null || UiText.PathOf(image.gameObject).Contains("/Buttons/"))
                continue;
            string sprite = image.sprite.name;
            names.Add(sprite);
            string lower = sprite.ToLowerInvariant();
            foreach (var (part, type) in FoodSprites)
            {
                if (!lower.Contains(part))
                    continue;
                string name = GameReader.FoodTypeName(type);
                int index = counts.FindIndex(pair => pair.Key == name);
                if (index < 0)
                    counts.Add(new KeyValuePair<string, int>(name, 1));
                else
                    counts[index] = new KeyValuePair<string, int>(name, counts[index].Value + 1);
                break;
            }
        }

        string logged = string.Join(", ", names);
        if (logged != _loggedPictures)
        {
            _loggedPictures = logged;
            Plugin.Logger.LogInfo($"[PlayBird] payment pictures: {logged}");
        }

        return counts;
    }

    /// <summary>Eggs chosen to pay for the bird's place: "1 von 1 Ei gewählt".</summary>
    private static string EggText(PlayBird_Decision decision)
    {
        BirdPayment payment = decision._currentData?.BirdPayment;
        int cost = EggCost(decision);
        int chosen = payment?.EggPay?.Count ?? 0;
        string text = Loc.Count("playbird.eggs_chosen", cost, chosen);
        return chosen >= cost ? $"{text}. {Loc.Get("dice.confirm")}" : text;
    }

    /// <summary>Eggs the chosen place costs; the payment only knows it once the eggs are paid.</summary>
    private static int EggCost(PlayBird_Decision decision)
    {
        try
        {
            if (decision._playerController != null && decision._currentData != null)
                return View.Decisions.PlayBird.States.EggsPaymentStateController.GetEggsCost(decision._playerController, decision._currentData);
        }
        catch (System.Exception e)
        {
            Plugin.Logger.LogWarning($"GetEggsCost failed: {e.Message}");
        }

        return decision._currentData?.BirdPayment?.EggsCost ?? 0;
    }

    private static string CostText(PlayBird_Decision decision)
    {
        BirdCardView view = decision._currentBirdCardView;
        string food = null;
        if (view != null)
        {
            foreach (TMPro.TMP_Text text in view.GetComponentsInChildren<TMPro.TMP_Text>(true))
            {
                if (text.name == "Food_TMP")
                {
                    food = UiText.Clean(text.text);
                    break;
                }
            }
        }

        return Loc.Get("playbird.cost", food ?? "-");
    }

    private static string PaymentText(PlayBird_Decision decision)
    {
        if (decision.CurrentState?.TryCast<View.Decisions.PlayBird.States.EggsPaymentStateController>() != null)
            return EggText(decision);

        string shown = ShownPaymentText(decision);
        if (shown != null)
            return shown;

        BirdPayment payment = decision._currentData?.BirdPayment;
        if (payment == null)
            return string.Empty;

        var parts = new List<string>();
        string food = GameReader.FoodList(payment.Food);
        if (food.Length > 0)
            parts.Add(food);
        if (payment.CardsPayment != null && payment.CardsPayment.Count > 0)
            parts.Add(Loc.Get("playbird.cards", payment.CardsPayment.Count));
        if (payment.EggsCost > 0)
            parts.Add(Loc.Get("playbird.eggs", payment.EggsCost));

        if (parts.Count == 0)
            return Loc.Get("playbird.choose_payment");
        string text = Loc.Get("playbird.payment", string.Join(", ", parts));
        if (!payment.EnoughFood)
            text = $"{text}. {Loc.Get("playbird.not_enough")}";
        return text;
    }
}
