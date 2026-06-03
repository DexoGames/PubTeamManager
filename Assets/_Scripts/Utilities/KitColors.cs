using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Deterministically assigns home/away kit colours from a stable string seed
/// (derived from a pub's identity), so the same pub always yields the same kit
/// across runs and save/load — but the choice looks pseudo-random.
///
/// Home colour: picked from a curated palette of nice, distinct colours.
/// Away colour: picked from the subset of the palette that visually contrasts
/// with the chosen home colour (high luminance or hue difference), so a red home
/// gets aways like white / blue / yellow / black rather than another red.
/// </summary>
public static class KitColors
{
    // Curated palette — distinct, kit-like colours that read well on screen.
    private static readonly Color[] Palette = new Color[]
    {
        Hex(0xD32F2F), // red
        Hex(0xB71C1C), // crimson
        Hex(0xFF5252), // bright red
        Hex(0xF57C00), // orange
        Hex(0xFFB300), // amber
        Hex(0xFDD835), // yellow
        Hex(0xC0CA33), // lime
        Hex(0x43A047), // green
        Hex(0x1B5E20), // forest green
        Hex(0x2E7D32), // emerald
        Hex(0x00897B), // teal
        Hex(0x00ACC1), // cyan
        Hex(0x4FC3F7), // sky blue
        Hex(0x1976D2), // blue
        Hex(0x283593), // royal blue
        Hex(0x0D1B4C), // navy
        Hex(0x3949AB), // indigo
        Hex(0x7B1FA2), // purple
        Hex(0x9C27B0), // violet
        Hex(0xC2185B), // magenta
        Hex(0xEC407A), // pink
        Hex(0x7A263A), // claret
        Hex(0x4E1227), // burgundy
        Hex(0x5D4037), // brown
        Hex(0x37474F), // charcoal
        Hex(0x1A1A1A), // near black
        Hex(0x9E9E9E), // grey
        Hex(0xCFD8DC), // silver
        Hex(0xF5F5F5), // white
        Hex(0xC9A227), // gold
    };

    /// <summary>Deterministic home colour for a seed (e.g. a pub's identity).</summary>
    public static Color GetHomeColor(string seed)
    {
        return Palette[(int)(Fnv1a(seed) % (uint)Palette.Length)];
    }

    /// <summary>
    /// Deterministic away colour for a seed, chosen from palette entries that
    /// contrast with <paramref name="home"/>. Falls back to white/black if no
    /// palette entry qualifies (shouldn't happen with the current palette).
    /// </summary>
    public static Color GetAwayColor(string seed, Color home)
    {
        List<Color> candidates = new List<Color>();
        foreach (Color c in Palette)
        {
            if (ContrastsWith(home, c)) candidates.Add(c);
        }

        if (candidates.Count == 0)
            return Luminance(home) < 0.5f ? Palette[28] /*white*/ : Palette[25] /*near black*/;

        // Salt the seed so away differs from home but stays deterministic.
        return candidates[(int)(Fnv1a(seed + "_away") % (uint)candidates.Count)];
    }

    // —————————————————————— helpers ——————————————————————

    private static bool ContrastsWith(Color a, Color b)
    {
        float lumDiff = Mathf.Abs(Luminance(a) - Luminance(b));
        return lumDiff >= 0.30f || HueDistance(a, b) >= 0.20f;
    }

    private static float Luminance(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

    private static float HueDistance(Color a, Color b)
    {
        Color.RGBToHSV(a, out float ha, out float sa, out _);
        Color.RGBToHSV(b, out float hb, out float sb, out _);
        // Hue is meaningless for near-greys; treat those as no hue separation.
        if (sa < 0.15f || sb < 0.15f) return 0f;
        float d = Mathf.Abs(ha - hb);
        return Mathf.Min(d, 1f - d);
    }

    /// <summary>Stable FNV-1a hash — deterministic across runs/platforms, unlike string.GetHashCode.</summary>
    private static uint Fnv1a(string s)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= 16777619;
            }
            return hash;
        }
    }

    private static Color Hex(int rgb)
    {
        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            1f);
    }
}
