using System;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Ustas.RimAI.Communication.Relations.UI
{
    public class EnhancedTextArea
    {
        #region 字段

        // Textcontents
        private string text = "";

        private Vector2 scrollPosition = Vector2.zero;

        private string controlName;
        private bool hasFocus = false;
        private bool wasFocused = false;

        private int maxLength = int.MaxValue;
        private bool enforceLimit = false;

        private Color normalBorderColor = new Color(0.3f, 0.3f, 0.3f);
        private Color focusedBorderColor = new Color(0.4f, 0.6f, 0.9f);
        private Color exceededBorderColor = new Color(0.9f, 0.3f, 0.3f);
        private Color exceededBackgroundColor = new Color(0.2f, 0.1f, 0.1f, 0.3f);

        private bool showCharacterCount = true;
        private Vector2 countLabelPosition = new Vector2(5f, 2f);

        public event Action<string> OnTextChanged;
        public event Action OnFocusGained;
        public event Action OnFocusLost;
        public event Action OnTextSubmitted;

        // Content height cache to avoid per-frame style calculations (A optimization)
        private string _cachedHeightText;
        private float _cachedHeightWidth;
        private float _cachedContentHeight = 20f;
        private GUIStyle _cachedTextAreaStyle;

        #endregion

        #region 属性

        public string Text
        {
            get => text;
            set
            {
                if (text != value)
                {
                    text = value ?? "";
                    EnforceLengthLimit();
                }
            }
        }

        public int MaxLength
        {
            get => maxLength;
            set
            {
                maxLength = value;
                enforceLimit = maxLength > 0 && maxLength < int.MaxValue;
                EnforceLengthLimit();
            }
        }

        public int CurrentLength => text?.Length ?? 0;
        public bool IsAtLimit => enforceLimit && CurrentLength >= maxLength;
        public bool HasExceededLimit => CurrentLength > maxLength;
        public bool IsFocused => hasFocus;

        #endregion

        #region 构造函数

        public EnhancedTextArea(string controlName, int maxLength = int.MaxValue)
        {
            this.controlName = controlName ?? $"EnhancedTextArea_{Rand.Int}";
            this.maxLength = maxLength;
            this.enforceLimit = maxLength > 0 && maxLength < int.MaxValue;
        }

        #endregion

        #region 公共方法

        public void Draw(Rect rect)
        {
            DrawBackground(rect);

            Rect innerRect = rect.ContractedBy(2f);

            float scrollbarWidth = 16f;
            Rect textViewRect = new Rect(0, 0, innerRect.width - scrollbarWidth, CalculateContentHeight(innerRect.width - scrollbarWidth));
            Rect textVisibleRect = new Rect(innerRect.x, innerRect.y, innerRect.width - scrollbarWidth, innerRect.height);

            ClampScrollPosition(textViewRect, textVisibleRect);

            GUI.SetNextControlName(controlName);
            scrollPosition = GUI.BeginScrollView(textVisibleRect, scrollPosition, textViewRect, false, true);

            Rect editRect = new Rect(0, 0, textViewRect.width, Mathf.Max(textViewRect.height, textVisibleRect.height));

            UpdateFocusState();

            HandleKeyboardShortcuts();

            string newText = GUI.TextArea(editRect, text, GetTextAreaStyle());

            if (enforceLimit && newText.Length > maxLength)
            {
                newText = newText.Substring(0, maxLength);
                GUI.changed = true;
            }

            GUI.EndScrollView();

            if (newText != text)
            {
                text = newText;
                OnTextChanged?.Invoke(text);
            }

            if (showCharacterCount)
            {
                DrawCharacterCount(rect);
            }

            DrawBorder(rect);

            wasFocused = hasFocus;
        }

        public void Focus()
        {
            GUI.FocusControl(controlName);
        }

        public void Blur()
        {
            if (hasFocus)
            {
                GUI.UnfocusWindow();
            }
        }

        public void SelectAll()
        {
            TextEditor editor = GetTextEditor();
            if (editor != null)
            {
                editor.SelectAll();
            }
        }

        public void Clear()
        {
            if (!string.IsNullOrEmpty(text))
            {
                text = "";
                OnTextChanged?.Invoke(text);
            }
        }

        public void InsertAtCursor(string insertText)
        {
            if (string.IsNullOrEmpty(insertText)) return;

            TextEditor editor = GetTextEditor();
            if (editor != null)
            {
                int cursorIndex = editor.cursorIndex;
                text = text.Insert(cursorIndex, insertText);
                editor.cursorIndex = cursorIndex + insertText.Length;
                OnTextChanged?.Invoke(text);
            }
            else
            {
                text += insertText;
                OnTextChanged?.Invoke(text);
            }
        }

        public void ScrollToBottom()
        {
            scrollPosition.y = float.MaxValue;
        }

        public void ScrollToTop()
        {
            scrollPosition.y = 0;
        }

        #endregion

        #region 私有方法

        private void DrawBackground(Rect rect)
        {
            if (HasExceededLimit)
            {
                Widgets.DrawBoxSolid(rect, exceededBackgroundColor);
            }
            else
            {
                Widgets.DrawBoxSolid(rect, new Color(0.15f, 0.15f, 0.15f, 0.8f));
            }
        }

        private void DrawBorder(Rect rect)
        {
            Color borderColor;
            if (HasExceededLimit)
            {
                borderColor = exceededBorderColor;
            }
            else if (hasFocus)
            {
                borderColor = focusedBorderColor;
            }
            else
            {
                borderColor = normalBorderColor;
            }

            GUI.color = borderColor;
            Widgets.DrawBox(rect, 2);
            GUI.color = Color.white;
        }

        private void DrawCharacterCount(Rect rect)
        {
            string countText = $"{CurrentLength}/{maxLength}";

            float labelWidth = 80f;
            float labelHeight = 18f;
            Rect countRect = new Rect(
                rect.xMax - labelWidth - 5f,
                rect.yMax - labelHeight - 2f,
                labelWidth,
                labelHeight
            );

            float usageRatio = (float)CurrentLength / maxLength;
            Color countColor;
            if (usageRatio > 1f)
            {
                countColor = Color.red;
            }
            else if (usageRatio > 0.9f)
            {
                countColor = Color.yellow;
            }
            else
            {
                countColor = new Color(0.6f, 0.6f, 0.6f);
            }

            Widgets.DrawBoxSolid(countRect, new Color(0.1f, 0.1f, 0.1f, 0.7f));

            TextAnchor oldAnchor = Verse.Text.Anchor;
            Verse.Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = countColor;
            Verse.Text.Font = GameFont.Tiny;
            Widgets.Label(countRect, countText);
            Verse.Text.Font = GameFont.Small;
            GUI.color = Color.white;
            Verse.Text.Anchor = oldAnchor;
        }

        private float CalculateContentHeight(float width)
        {
            if (string.IsNullOrEmpty(text)) return 20f;

            // Cache content height to avoid per-frame style calculations (A optimization)
            GUIStyle style = GetTextAreaStyle();
            if (_cachedHeightText == text && Mathf.Abs(_cachedHeightWidth - width) < 0.5f && _cachedTextAreaStyle == style)
            {
                return _cachedContentHeight;
            }

            _cachedHeightText = text;
            _cachedHeightWidth = width;
            _cachedTextAreaStyle = style;
            float height = style.CalcHeight(new GUIContent(text), width);
            _cachedContentHeight = Mathf.Max(height + 20f, 50f);
            return _cachedContentHeight;
        }

        private void ClampScrollPosition(Rect viewRect, Rect visibleRect)
        {
            float maxScrollY = Mathf.Max(0, viewRect.height - visibleRect.height);
            scrollPosition.y = Mathf.Clamp(scrollPosition.y, 0, maxScrollY);
        }

        private void UpdateFocusState()
        {
            hasFocus = GUI.GetNameOfFocusedControl() == controlName;

            if (hasFocus && !wasFocused)
            {
                OnFocusGained?.Invoke();
            }
            else if (!hasFocus && wasFocused)
            {
                OnFocusLost?.Invoke();
            }
        }

        private void HandleKeyboardShortcuts()
        {
            if (!hasFocus) return;

            Event e = Event.current;
            if (e.type != EventType.KeyDown) return;

            if (e.control && e.keyCode == KeyCode.A)
            {
                SelectAll();
                e.Use();
            }
            else if (e.control && e.keyCode == KeyCode.X)
            {
                TextEditor editor = GetTextEditor();
                if (editor != null && editor.hasSelection)
                {
                    editor.Cut();
                    e.Use();
                }
            }
            else if (e.control && e.keyCode == KeyCode.V)
            {
                if (enforceLimit)
                {
                }
            }
            else if (e.keyCode == KeyCode.Tab)
            {
                InsertAtCursor("    ");
                e.Use();
            }
            else if (e.keyCode == KeyCode.Return && e.control)
            {
                OnTextSubmitted?.Invoke();
                e.Use();
            }
        }

        private void EnforceLengthLimit()
        {
            if (enforceLimit && text.Length > maxLength)
            {
                text = text.Substring(0, maxLength);
            }
        }

        private GUIStyle GetTextAreaStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.textArea);
            style.wordWrap = true;
            style.padding = new RectOffset(6, 6, 4, 4);
            style.fontSize = 12;
            return style;
        }

        private TextEditor GetTextEditor()
        {
            try
            {
                var field = typeof(GUIUtility).GetField("s_TextEditor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                if (field != null)
                {
                    return field.GetValue(null) as TextEditor;
                }
            }
            catch { }
            return null;
        }

        #endregion
    }
}
