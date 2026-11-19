using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuizOptionButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Image icon;

    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.6f, 0.85f, 0.6f);

    [Header("Label-below-icon layout")]
    [SerializeField, Range(0.4f, 0.9f)] private float iconAreaHeight = 0.7f;
    [SerializeField] private float padding = 8f;
    [SerializeField] private float minFontSize = 14f;

    public void Setup(string text, Sprite sprite, System.Action onClick, bool labelBelowIcon = false)
    {
        if (label != null) label.text = text;

        if (icon != null)
        {
            icon.sprite = sprite;
            icon.gameObject.SetActive(sprite != null);
        }

        if (labelBelowIcon && sprite != null && icon != null && label != null)
            ApplyIconAboveLabelLayout();

        button.onClick.AddListener(() => onClick());
        SetSelected(false);
    }

    private void ApplyIconAboveLabelLayout()
    {
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = new Vector2(0f, 1f - iconAreaHeight);
        iconRect.anchorMax = new Vector2(1f, 1f);
        iconRect.offsetMin = new Vector2(padding, padding * 0.5f);
        iconRect.offsetMax = new Vector2(-padding, -padding);
        icon.preserveAspect = true;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f - iconAreaHeight);
        labelRect.offsetMin = new Vector2(padding, padding);
        labelRect.offsetMax = new Vector2(-padding, 0f);

        label.alignment = TextAlignmentOptions.Center;
        label.fontSizeMax = label.fontSize;
        label.fontSizeMin = minFontSize;
        label.enableAutoSizing = true;
    }

    public void SetSelected(bool selected)
    {
        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }
}