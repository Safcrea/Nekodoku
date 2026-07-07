using System;
using UnityEngine;

namespace Meowdoku
{
    /// <summary>
    /// Identifies a puzzle region color by name, independent of its RGB value.
    /// </summary>
    public enum RegionColorId
    {
        Blush,
        Mint,
        Sky,
        Peach,
        Lavender,
        Butter,
        Terracotta,
        Teal,
        Lime
    }

    /// <summary>Pairs a region color's identity with its actual color value. Editable in the Inspector.</summary>
    [Serializable]
    public struct RegionColorEntry
    {
        public RegionColorId Id;
        public Color Color;
    }

    /// <summary>
    /// The single source of truth for puzzle region colors - a ScriptableObject asset so the
    /// actual color values are editable in the Inspector instead of hardcoded. Every region on
    /// the board is identified by index (0..Size-1); this maps that index to a named color.
    /// Both the gameplay board (<see cref="GridCell"/>) and the level editor reference the same
    /// asset, so the palette only ever needs to change in one place.
    /// </summary>
    [CreateAssetMenu(menuName = "Meowdoku/Region Palette", fileName = "RegionPalette")]
    public sealed class RegionPalette : ScriptableObject
    {
        [SerializeField]
        private RegionColorEntry[] entries =
        {
            new RegionColorEntry { Id = RegionColorId.Blush, Color = new Color(1.00f, 0.72f, 0.76f, 1f) },
            new RegionColorEntry { Id = RegionColorId.Mint, Color = new Color(0.67f, 0.88f, 0.77f, 1f) },
            new RegionColorEntry { Id = RegionColorId.Sky, Color = new Color(0.62f, 0.80f, 0.96f, 1f) },
            new RegionColorEntry { Id = RegionColorId.Peach, Color = new Color(1.00f, 0.82f, 0.58f, 1f) },
            new RegionColorEntry { Id = RegionColorId.Lavender, Color = new Color(0.79f, 0.71f, 0.93f, 1f) },
            new RegionColorEntry { Id = RegionColorId.Butter, Color = new Color(0.96f, 0.91f, 0.55f, 1f) },
            new RegionColorEntry { Id = RegionColorId.Terracotta, Color = new Color(0.93f, 0.62f, 0.50f, 1f) },
            new RegionColorEntry { Id = RegionColorId.Teal, Color = new Color(0.53f, 0.82f, 0.85f, 1f) },
            new RegionColorEntry { Id = RegionColorId.Lime, Color = new Color(0.74f, 0.88f, 0.56f, 1f) }
        };

        public Color ColorForRegion(int regionIndex)
        {
            if (entries == null || entries.Length == 0)
            {
                return Color.white;
            }

            return entries[regionIndex % entries.Length].Color;
        }
    }
}
