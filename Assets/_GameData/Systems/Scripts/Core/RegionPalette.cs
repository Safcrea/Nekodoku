using System;
using UnityEngine;

namespace Meowdoku
{
    /// <summary>
    /// Identifies a puzzle region color by name, independent of its RGB value. Named generically
    /// (Color A..Color I) rather than by actual hue - the web level editor mirrors this same
    /// generic naming for its own per-region art, so both sides can be re-themed by swapping
    /// sprites/images without ever renaming anything that identifies a region.
    /// </summary>
    public enum RegionColorId
    {
        [InspectorName("Color A")] ColorA,
        [InspectorName("Color B")] ColorB,
        [InspectorName("Color C")] ColorC,
        [InspectorName("Color D")] ColorD,
        [InspectorName("Color E")] ColorE,
        [InspectorName("Color F")] ColorF,
        [InspectorName("Color G")] ColorG,
        [InspectorName("Color H")] ColorH,
        [InspectorName("Color I")] ColorI
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
            new RegionSpriteEntry { Id = RegionColorId.ColorA },
            new RegionSpriteEntry { Id = RegionColorId.ColorB },
            new RegionSpriteEntry { Id = RegionColorId.ColorC },
            new RegionSpriteEntry { Id = RegionColorId.ColorD },
            new RegionSpriteEntry { Id = RegionColorId.ColorE },
            new RegionSpriteEntry { Id = RegionColorId.ColorF },
            new RegionSpriteEntry { Id = RegionColorId.ColorG },
            new RegionSpriteEntry { Id = RegionColorId.ColorH },
            new RegionSpriteEntry { Id = RegionColorId.ColorI }
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
