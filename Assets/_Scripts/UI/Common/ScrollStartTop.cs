using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class ScrollStartTop : MonoBehaviour
{
    ScrollRect scroll;

    private void OnEnable()
    {
        if(scroll == null)
        {
            scroll = GetComponent<ScrollRect>();
        }

        scroll.verticalNormalizedPosition = 1f;
    }
}
