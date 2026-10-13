using UnityEngine;
using UnityStubs.Gui;

namespace Ustas.RimAI.Communication.Relations.UI
{
    /// <summary>
    /// Dependencies: the engine's GUI text backend.
    /// Responsibility: say where in a text area a character stands, so a chip
    /// can be drawn over the variable it replaces.
    ///
    /// Unity answered this on the style itself, GetCursorPixelPosition. The
    /// engine RimWorld 1.7 runs on keeps text layout in its GUI backend and
    /// asks it the same question for its own caret, so the answer here is the
    /// layout the text area is actually drawn with - same font, same wrap
    /// width - and not a second measurement that could disagree with it by a
    /// word at the end of a line.
    /// </summary>
    internal static class TextCursorGeometry
    {
        /// <summary>
        /// The top left of the cursor standing before character
        /// <paramref name="index"/>, in the coordinates of
        /// <paramref name="position"/> - the rectangle the text area is drawn in.
        /// </summary>
        public static Vector2 PixelPosition(GUIStyle style, Rect position, string text, int index)
        {
            RectOffset padding = style?.padding ?? new RectOffset();
            var origin = new Vector2(position.x + padding.left, position.y + padding.top);
            IGuiBackend backend = GuiBackend.Current;
            if (backend == null || string.IsNullOrEmpty(text))
            {
                return origin;
            }

            bool wraps = style == null || style.wordWrap;
            float wrapWidth = wraps ? Mathf.Max(0f, position.width - padding.left - padding.right) : -1f;
            Vector2 local = backend.TextCursorPos(
                style?.FontIndex ?? 1,
                0f,
                FontStyle.Normal,
                text,
                wrapWidth,
                Mathf.Clamp(index, 0, text.Length),
                out _);
            return origin + local;
        }
    }
}
