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

    public void Setup(string text, Sprite sprite, System.Action onClick)
    {
        if (label != null) label.text = text;

        if (icon != null)
        {
            icon.sprite = sprite;
            icon.gameObject.SetActive(sprite != null);
        }

        button.onClick.AddListener(() => onClick());
        SetSelected(false);
    }

    public void SetSelected(bool selected)
    {
        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }
}