using System.Collections.Generic;
using UnityEngine;
using WingspanAccess.Localization;
using WingspanAccess.Speech;
using NewsCenter = View.UI.MainMenu.NewsCenter;

namespace WingspanAccess.UI;

/// <summary>
/// The news shown at start: entries are read with title, date and their "new" or "pinned" marks;
/// a news item opened with Enter is read with its header and text.
/// </summary>
internal static class NewsDescriber
{
    private static int _screenId;
    private static string _shownHeader;
    private static int _nextCheck;

    /// <summary>Description for a news entry in the list, or null if the element is none.</summary>
    public static string Describe(GameObject selected)
    {
        NewsCenter.NewsDataButton entry = selected.GetComponent<NewsCenter.NewsDataButton>();
        if (entry == null)
            return null;

        var parts = new List<string>();
        AddText(parts, entry._titleText);
        AddText(parts, entry._dateText);
        if (IsShown(entry._unreadNewsIndicator))
            parts.Add(Loc.Get("news.unread"));
        if (IsShown(entry._pinnedNewsIndicator))
            parts.Add(Loc.Get("news.pinned"));
        parts.Add(Loc.Get("role.button"));
        return string.Join(", ", parts);
    }

    /// <summary>Reads the news the display panel switches to; the one it shows on opening stays silent.</summary>
    public static void Tick(GamepadScreen top)
    {
        NewsCenter.NewsCenter_Screen screen = top == null ? null : top.TryCast<NewsCenter.NewsCenter_Screen>();
        if (screen == null)
        {
            _screenId = 0;
            return;
        }

        if (Time.frameCount < _nextCheck)
            return;
        _nextCheck = Time.frameCount + 10;

        NewsCenter.NewsDisplayPanel panel = screen._newsDisplayPanel;
        string header = panel == null || panel._headerText == null ? null : UiText.Clean(panel._headerText.text);
        if (screen.GetInstanceID() != _screenId)
        {
            _screenId = screen.GetInstanceID();
            _shownHeader = header;
            return;
        }

        if (string.IsNullOrEmpty(header) || header == _shownHeader)
            return;
        _shownHeader = header;

        var parts = new List<string> { header };
        string content = panel._contentText == null ? null : UiText.Clean(panel._contentText.text);
        if (!string.IsNullOrEmpty(content))
            parts.Add(content);
        Plugin.Logger.LogInfo($"[News] shown: {header} ({content?.Length ?? 0} chars)");
        ScreenReader.Say(UiText.JoinSentences(parts), interrupt: false);
    }

    private static void AddText(List<string> parts, TMPro.TMP_Text text)
    {
        string value = text == null || !UiText.IsShown(text) ? null : UiText.Clean(text.text);
        if (!string.IsNullOrEmpty(value))
            parts.Add(value);
    }

    private static bool IsShown(Component marker) => marker != null && marker.gameObject.activeInHierarchy;

    private static bool IsShown(GameObject marker) => marker != null && marker.activeInHierarchy;
}
