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
        // States the game passes through without showing anything (e.g. copying a white power
        // for birds that have none) are not announced.
        if (state == null || !state.HasShownUIToPlayer)
            return;

        string payment = PaymentText(decision);
        int stateId = state.GetInstanceID();
        if (stateId != _stateId)
        {
            _stateId = stateId;
            _lastPayment = payment;
            string typeName = state.GetIl2CppType().Name;
            string key = "playbird." + typeName;
            var parts = new List<string>();
            if (Loc.Has(key))
                parts.Add(Loc.Get(key));
            if (typeName == "CostPaymentStateController" || typeName == "EggsPaymentStateController")
            {
                parts.Add(CostText(decision));
                parts.Add(payment);
                parts.Add(Loc.Get("playbird.confirm_hint"));
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
