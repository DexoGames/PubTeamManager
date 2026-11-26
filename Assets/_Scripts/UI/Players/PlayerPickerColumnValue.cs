using UnityEngine;

public readonly struct PlayerPickerColumnValue
{
    public string Text { get; }
    public Sprite Icon { get; }
    public Color IconColor { get; }

    public PlayerPickerColumnValue(string text)
        : this(null, Color.white, text)
    {
    }

    public PlayerPickerColumnValue(Sprite icon, Color iconColor, string text = null)
    {
        Text = text;
        Icon = icon;
        IconColor = iconColor;
    }

    public static PlayerPickerColumnValue FromMorale(Player player)
    {
        return new PlayerPickerColumnValue(player.GetMoraleSprite(), player.GetMoraleColor());
    }
}
