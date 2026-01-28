using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generic dialogue UI component that displays conversation with a person.
/// Independent of the dialogue type (discussion, interview, etc.)
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI descriptionText, personNameText, contentsText, extraInfoText;
    [SerializeField] Image face;

    Person currentPerson;

    /// <summary>
    /// Setup the dialogue UI using a context that provides all necessary data.
    /// </summary>
    public void Setup(IDialogueContext context)
    {
        extraInfoText.gameObject.SetActive(false);

        currentPerson = context.Person;

        face.color = context.FaceColor;
        face.sprite = context.FaceSprite;
        descriptionText.text = context.Description;
        contentsText.text = context.InitialDialogue;
        personNameText.text = context.PersonName;
    }

    /// <summary>
    /// Update the dialogue contents and refresh the person's face based on current morale.
    /// </summary>
    public void UpdateDialogue(string dialogue)
    {
        contentsText.text = dialogue;
        if (currentPerson != null)
        {
            face.color = currentPerson.GetMoraleColor();
            face.sprite = currentPerson.GetMoraleSprite();
        }
    }

    /// <summary>
    /// Update the dialogue contents with custom face color and sprite.
    /// </summary>
    public void UpdateDialogue(string dialogue, Color faceColor, Sprite faceSprite)
    {
        contentsText.text = dialogue;
        face.color = faceColor;
        face.sprite = faceSprite;
    }

    /// <summary>
    /// Show or update the extra info text (e.g., morale change feedback).
    /// </summary>
    public void UpdateExtraInfo(string info)
    {
        extraInfoText.gameObject.SetActive(true);
        extraInfoText.text = info;
    }

    /// <summary>
    /// Hide the extra info text.
    /// </summary>
    public void HideExtraInfo()
    {
        extraInfoText.gameObject.SetActive(false);
    }
}