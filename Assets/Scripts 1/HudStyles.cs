using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared cache of IMGUI label styles.
/// OnGUI() runs several times per frame (Layout + Repaint + input events), and the HUD code used to
/// build ~30 new GUIStyle objects on every call. That is constant garbage-collector pressure and shows
/// up as hitches during big chain reactions. Styles are now built once per (size, weight, alignment)
/// and reused.
///
/// Usage (only inside OnGUI, because GUI.skin is only valid there):
///     GUIStyle s = HudStyles.Label(14, FontStyle.Bold, TextAnchor.MiddleLeft);
///     s.normal.textColor = Color.green;   // always set the colour you want after fetching
///     GUI.Label(rect, "text", s);
///
/// Note: the same instance is returned for the same (size, weight, alignment), and its text colour is
/// reset to the skin default on every fetch. IMGUI draws immediately, so this is safe as long as you
/// finish using a style before fetching another one with identical parameters.
/// </summary>
public static class HudStyles
{
    private static readonly Dictionary<int, GUIStyle> cache = new Dictionary<int, GUIStyle>();

    public static GUIStyle Label(int fontSize, FontStyle fontStyle = FontStyle.Normal, TextAnchor alignment = TextAnchor.UpperLeft)
    {
        // fontSize in bits 8+, fontStyle (0-3) in bits 4-7, alignment (0-8) in bits 0-3
        int key = (fontSize << 8) | ((int)fontStyle << 4) | (int)alignment;

        if (!cache.TryGetValue(key, out GUIStyle style) || style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                alignment = alignment
            };
            cache[key] = style;
        }

        // Reset so a colour set by one HUD element can never leak into another
        style.normal.textColor = GUI.skin.label.normal.textColor;
        return style;
    }
}
