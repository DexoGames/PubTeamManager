using UnityEngine;

[CreateAssetMenu(menuName = "UI/Theme")]
public class UITheme : ScriptableObject
{
    public enum Role
    {
        Background,
        Panel,
        Inset,
        Oak,
        Velvet,
        Felt,
        Brass,
        Text,
        MutedText
    }

    [Header("Surfaces")]
    [SerializeField] Color background = new Color32(36, 25, 23, 255);
    [SerializeField] Color panel = new Color32(61, 42, 34, 255);
    [SerializeField] Color inset = new Color32(47, 32, 28, 255);

    [Header("Controls")]
    [SerializeField] Color oak = new Color32(104, 77, 55, 255);
    [SerializeField] Color velvet = new Color32(112, 51, 60, 255);
    [SerializeField] Color felt = new Color32(67, 85, 56, 255);
    [SerializeField] Color brass = new Color32(228, 183, 116, 255);

    [Header("Text")]
    [SerializeField] Color text = new Color32(249, 234, 211, 255);
    [SerializeField] Color mutedText = new Color32(217, 191, 161, 255);

    public Color GetColor(Role role)
    {
        return role switch
        {
            Role.Background => background,
            Role.Panel => panel,
            Role.Inset => inset,
            Role.Oak => oak,
            Role.Velvet => velvet,
            Role.Felt => felt,
            Role.Brass => brass,
            Role.MutedText => mutedText,
            _ => text
        };
    }

#if UNITY_EDITOR
    internal static event System.Action<UITheme> Changed;

    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= NotifyChanged;
        UnityEditor.EditorApplication.delayCall += NotifyChanged;
    }

    void NotifyChanged()
    {
        if (this != null) Changed?.Invoke(this);
    }
#endif
}
