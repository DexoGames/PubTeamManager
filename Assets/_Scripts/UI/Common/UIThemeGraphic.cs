using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class UIThemeGraphic : MonoBehaviour
{
    [SerializeField] UITheme theme;
    [SerializeField] UITheme.Role role;
    [SerializeField, Range(0, 1)] float opacity = 1;

    void OnEnable()
    {
        ApplyColor();

#if UNITY_EDITOR
        UITheme.Changed += RefreshTheme;
#endif
    }

    void ApplyColor()
    {
        if (this == null || theme == null || !TryGetComponent(out UnityEngine.UI.Graphic graphic)) return;

        Color color = theme.GetColor(role);
        color.a *= opacity;
        graphic.color = color;
    }

#if UNITY_EDITOR
    void OnDisable()
    {
        UITheme.Changed -= RefreshTheme;
        UnityEditor.EditorApplication.delayCall -= ApplyColor;
    }

    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyColor;
        UnityEditor.EditorApplication.delayCall += ApplyColor;
    }

    void RefreshTheme(UITheme changedTheme)
    {
        if (changedTheme == theme) ApplyColor();
    }
#endif
}
