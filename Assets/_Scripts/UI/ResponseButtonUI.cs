using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResponseButtonUI : MonoBehaviour
{
    Event.Response response;
    TextMeshProUGUI text;
    Button button;
    ResponseManager manager;

    public void Setup(Event.Response response, ResponseManager manager)
    {
        text = GetComponentInChildren<TextMeshProUGUI>();
        button = GetComponent<Button>();
        text.text = response.ToString();
        this.response = response;
        this.manager = manager;
    }

    public void OnClick()
    {
        manager.ButtonPressed(response);
    }

    public void MakeUninteractable(Event.Response response)
    {
        button.interactable = false;
        if(response == this.response)
        {
            button.image.color = Color.gray;
        }
    }
}
