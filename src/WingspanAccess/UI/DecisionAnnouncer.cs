using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using WingspanAccess.Speech;

namespace WingspanAccess.UI;

/// <summary>
/// In the game, decisions show their instructions in shared HUD or 3D texts, not inside the
/// decision screen. This finds visible instruction texts ("...DescriptionTxt", "...Title...")
/// outside of cards, announces them when a decision opens and whenever they change
/// (e.g. the counter in "Choose 5 (1/5)").
/// </summary>
internal static class DecisionAnnouncer
{
    private static readonly Regex InstructionName = new(@"^Description\w*Txt$|Title|Instruction|Header_?Text", RegexOptions.IgnoreCase);

    private static readonly Regex Counter = new(@"^\d+\s*/\s*\d+$");

    private static readonly HashSet<string> LabelOnlyDecisions = new() { "PlayBird_Decision", "DrawBirds_Decision" };

    private static int _decisionId;
    private static int _announceFrame = -1;
    private static int _nextCheck;
    private static List<string> _texts = new();

    public static void Tick(GamepadScreen top)
    {
        Decision decision = top == null ? null : top.TryCast<Decision>();
        if (decision == null)
        {
            _decisionId = 0;
            return;
        }

        if (decision.GetInstanceID() != _decisionId)
        {
            _decisionId = decision.GetInstanceID();
            _texts = new List<string>();
            _announceFrame = Time.frameCount + 10; // let the decision lay out its texts first
            return;
        }

        if (Time.frameCount < _nextCheck || (_announceFrame >= 0 && Time.frameCount < _announceFrame))
            return;

        _nextCheck = Time.frameCount + 10;
        bool first = _announceFrame >= 0;
        _announceFrame = -1;
        List<string> now = InstructionTexts(decision);
        var changed = now.FindAll(text => !_texts.Contains(text));
        _texts = now;

        // Decisions without a translated name are announced by their title, which is also an instruction text.
        if (first)
            changed.Remove(UiText.ScreenName(decision));

        // A bare counter ("2/2") is read with the label in front of it ("Untergeschobene Karten:").
        for (int i = 0; i < changed.Count; i++)
        {
            int index = now.IndexOf(changed[i]);
            if (Counter.IsMatch(changed[i]) && index > 0 && now[index - 1].EndsWith(":") && !changed.Contains(now[index - 1]))
                changed[i] = $"{now[index - 1]} {changed[i]}";
        }
        if (changed.Count > 0)
            ScreenReader.Say(UiText.JoinSentences(changed), interrupt: false); // after the focus announcement of the same action
    }

    /// <summary>
    /// The decision's own visible texts (outside its buttons) plus visible instruction texts
    /// anywhere in the scene, excluding texts on cards.
    /// </summary>
    public static List<string> InstructionTexts(GamepadScreen decision)
    {
        // Some decisions only carry labels next to icons; dedicated announcers read them instead.
        bool ownTexts = decision != null && !LabelOnlyDecisions.Contains(decision.GetIl2CppType().Name);
        var result = ownTexts ? Screens.PlainTexts(decision.gameObject) : new List<string>();
        foreach (TMPro.TMP_Text text in Object.FindObjectsOfType<TMPro.TMP_Text>())
        {
            if (!InstructionName.IsMatch(text.name) || !UiText.IsShown(text))
                continue;
            if (text.GetComponentsInParent<CardView>(true).Length > 0)
                continue;
            string clean = UiText.Clean(text.text);
            if (clean.Length > 0 && !result.Contains(clean))
                result.Add(clean);
        }

        return result;
    }
}
