using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class FitCardGrid : MonoBehaviour
{
    [SerializeField, Min(1)] int columns = 5;
    [SerializeField, Min(1)] int rows = 5;
    GridLayoutGroup grid;

    void OnEnable() { Resize(); }
    void OnRectTransformDimensionsChange() { Resize(); }

    void Resize()
    {
        if (!grid) grid = GetComponent<GridLayoutGroup>();
        var bounds = ((RectTransform)transform).rect;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.cellSize = new Vector2(
            Mathf.Max(1, (bounds.width - grid.padding.horizontal - (columns - 1) * grid.spacing.x) / columns - 0.1f),
            Mathf.Max(1, (bounds.height - grid.padding.vertical - (rows - 1) * grid.spacing.y) / rows - 0.1f));
    }
}
