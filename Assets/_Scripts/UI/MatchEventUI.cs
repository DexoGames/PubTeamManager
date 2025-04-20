using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class MatchEventUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _text;

    internal void SetText(string str)
    {
        _text.text = str;
    }
}
