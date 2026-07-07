using UnityEngine;
using UnityEngine.UI;

namespace Meowdoku
{
    /// <summary>
    /// Keeps a sibling <see cref="GridLayoutGroup"/>'s cell size in lockstep with this
    /// RectTransform's own size, so an NxN grid always fills its parent and stays square
    /// no matter how that parent is resized. Spacing/padding stay entirely inspector-driven
    /// on the GridLayoutGroup; this component only ever touches <c>cellSize</c>.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class BoardGridSizer : MonoBehaviour
    {
        private RectTransform rectTransform;
        private GridLayoutGroup gridLayoutGroup;
        private int columns = 1;

        private void OnEnable()
        {
            CacheReferences();
            Recalculate();
        }

        private void OnRectTransformDimensionsChange()
        {
            Recalculate();
        }

        public void SetColumns(int newColumns)
        {
            CacheReferences();
            columns = Mathf.Max(1, newColumns);
            gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayoutGroup.constraintCount = columns;
            Recalculate();
        }

        private void CacheReferences()
        {
            if (rectTransform == null)
            {
                rectTransform = (RectTransform)transform;
            }

            if (gridLayoutGroup == null)
            {
                gridLayoutGroup = GetComponent<GridLayoutGroup>();
            }
        }

        private void Recalculate()
        {
            CacheReferences();
            if (gridLayoutGroup == null || columns <= 0)
            {
                return;
            }

            Vector2 spacing = gridLayoutGroup.spacing;
            RectOffset padding = gridLayoutGroup.padding;
            float availableWidth = rectTransform.rect.width - padding.horizontal - (spacing.x * (columns - 1));
            float availableHeight = rectTransform.rect.height - padding.vertical - (spacing.y * (columns - 1));
            float cellSize = Mathf.Max(0f, Mathf.Min(availableWidth, availableHeight) / columns);
            gridLayoutGroup.cellSize = new Vector2(cellSize, cellSize);
        }
    }
}
