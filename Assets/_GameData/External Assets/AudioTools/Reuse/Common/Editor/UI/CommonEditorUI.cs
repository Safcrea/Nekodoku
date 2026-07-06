using UnityEditor;
using UnityEngine;

namespace ImpossibleRobert.Common
{
    /// <summary>
    /// Base class for editor windows with common utility methods.
    /// </summary>
    public abstract class CommonEditorUI : EditorWindow
    {
        /// <summary>
        /// Draws a label with text value side by side.
        /// </summary>
        /// <param name="label">The label text</param>
        /// <param name="value">The value text</param>
        /// <param name="labelWidth">Width of the label</param>
        /// <param name="maxWidth">Maximum width for the value</param>
        protected void GUILabelWithText(string label, string value, int labelWidth = 85, int maxWidth = 500)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.Width(labelWidth));
            EditorGUILayout.LabelField(value, GUILayout.MaxWidth(maxWidth));
            EditorGUILayout.EndHorizontal();
        }

        protected static void BeginIndentBlock(int widthOverride = 0)
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.Space(widthOverride > 0 ? widthOverride : CommonUIStyles.INDENT_WIDTH, false);
            GUILayout.BeginVertical();
        }

        protected static void EndIndentBlock(bool autoSpace = true)
        {
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            if (autoSpace) EditorGUILayout.Space();
        }

        /// <summary>
        /// Gets a popup position at the current mouse location.
        /// </summary>
        public static Rect GetPopupPositionAtMouse()
        {
            return new Rect(Event.current.mousePosition.x, Event.current.mousePosition.y, 0, 0);
        }
    }
}