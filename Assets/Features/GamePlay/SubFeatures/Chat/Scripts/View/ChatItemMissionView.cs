using TMPro;
using UnityEngine;

public class ChatItemMissionView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;

    private string _hanzi;
    private string _pinyin;
    private string _vietnamese;
    private bool _isCompleted;

    /// <summary>
    /// The raw hanzi word this item represents.
    /// </summary>
    public string Hanzi => _hanzi;

    /// <summary>
    /// Whether this mission item has been completed (word used by the user).
    /// </summary>
    public bool IsCompleted => _isCompleted;

    /// <summary>
    /// Sets the display text for this mission item.
    /// </summary>
    /// <param name="hanzi">Chinese word.</param>
    /// <param name="pinyin">Pinyin reading.</param>
    /// <param name="vietnamese">Vietnamese meaning.</param>
    public void SetText(string hanzi, string pinyin, string vietnamese)
    {
        _hanzi = hanzi ?? string.Empty;
        _pinyin = pinyin ?? string.Empty;
        _vietnamese = vietnamese ?? string.Empty;
        _isCompleted = false;
        RefreshDisplay();
    }

    /// <summary>
    /// Marks this item as completed — applies strikethrough rich text.
    /// </summary>
    public void MarkCompleted()
    {
        if (_isCompleted)
        {
            return;
        }

        _isCompleted = true;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (text == null)
        {
            return;
        }

        var display = BuildDisplayText();
        if (_isCompleted)
        {
            display = "<s>" + display + "</s>";
        }

        text.text = display;
    }

    private string BuildDisplayText()
    {
        var builder = new System.Text.StringBuilder();
        builder.Append(_hanzi);

        if (!string.IsNullOrWhiteSpace(_pinyin))
        {
            builder.Append("\n");
            builder.Append(" (");
            builder.Append(_pinyin);
            builder.Append(")");
        }

        if (!string.IsNullOrWhiteSpace(_vietnamese))
        {
            builder.Append("\n");
            builder.Append(_vietnamese);
        }

        return builder.ToString();
    }
}