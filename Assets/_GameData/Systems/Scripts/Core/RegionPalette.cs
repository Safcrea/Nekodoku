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

    /// <summary>Pairs a region's identity with the sprite drawn on that region's cells. Editable in the Inspector.</summary>
    [Serializable]
    public struct RegionSpriteEntry
    {
        public RegionColorId Id;
        public Sprite Sprite;
    }

    /// <summary>
    /// The single source of truth for puzzle region visuals - a ScriptableObject asset so the
    /// actual sprites are editable in the Inspector instead of hardcoded. Every region on
    /// the board is identified by index (0..Size-1); this maps that index to a named region's sprite.
    /// Both the gameplay board (<see cref="GridCell"/>) and the level editor reference the same
    /// asset, so the palette only ever needs to change in one place.
    /// </summary>
    [CreateAssetMenu(menuName = "Meowdoku/Region Palette", fileName = "RegionPalette")]
    public sealed class RegionPalette : ScriptableObject
    {
        [SerializeField]
        private RegionSpriteEntry[] entries =
        {
            new RegionSpriteEntry { Id = RegionColorId.Blush },
            new RegionSpriteEntry { Id = RegionColorId.Mint },
            new RegionSpriteEntry { Id = RegionColorId.Sky },
            new RegionSpriteEntry { Id = RegionColorId.Peach },
            new RegionSpriteEntry { Id = RegionColorId.Lavender },
            new RegionSpriteEntry { Id = RegionColorId.Butter },
            new RegionSpriteEntry { Id = RegionColorId.Terracotta },
            new RegionSpriteEntry { Id = RegionColorId.Teal },
            new RegionSpriteEntry { Id = RegionColorId.Lime }
        };

        public Sprite SpriteForRegion(int regionIndex)
        {
            if (entries == null || entries.Length == 0)
            {
                return null;
            }

            return entries[regionIndex % entries.Length].Sprite;
        }
    }
}
