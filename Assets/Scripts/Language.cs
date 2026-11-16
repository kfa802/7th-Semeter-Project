using UnityEngine;

/// <summary>
/// The language chosen in the start menu (StartMenu saves it as "da" or "en").
/// Every script asks this which text / voice clip to use.
/// </summary>
public static class Language
{
    private const string PrefKey = "language";

    /// <summary>True when Danish was chosen (also the default).</summary>
    public static bool IsDanish => PlayerPrefs.GetString(PrefKey, "da") != "en";

    /// <summary>Returns the Danish text if Danish is chosen and it isn't empty, otherwise the English.</summary>
    public static string Pick(string english, string danish)
    {
        return IsDanish && !string.IsNullOrWhiteSpace(danish) ? danish : english;
    }

    /// <summary>Same for voice clips: falls back to English if there is no Danish clip.</summary>
    public static T Pick<T>(T english, T danish) where T : Object
    {
        return IsDanish && danish != null ? danish : english;
    }

    /// <summary>Same for lists (e.g. subtitles): falls back to English if the Danish list is empty.</summary>
    public static System.Collections.Generic.List<T> Pick<T>(
        System.Collections.Generic.List<T> english,
        System.Collections.Generic.List<T> danish)
    {
        return IsDanish && danish != null && danish.Count > 0 ? danish : english;
    }
}