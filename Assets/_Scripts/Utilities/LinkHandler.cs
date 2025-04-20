using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class LinkHandler : MonoBehaviour, IPointerClickHandler
{
    private TextMeshProUGUI textMeshPro;

    void Awake()
    {
        textMeshPro = GetComponent<TextMeshProUGUI>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClick();
    }

    public void OnClick()
    {
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(textMeshPro, Input.mousePosition, null);
        if (linkIndex != -1)
        {
            TMP_LinkInfo linkInfo = textMeshPro.textInfo.linkInfo[linkIndex];
            HandleLinkClick(linkInfo.GetLinkID());
        }
    }

    public void HandleLinkClick(string linkID)
    {
        Debug.Log("handling " + linkID);

        string[] splitID = linkID.Split('/');

        if (splitID.Length != 2)
        {
            Debug.LogError("Invalid link format");
            return;
        }

        string type = splitID[0];
        string id = splitID[1];

        if(type == "event")
        {
            string[] parts = id.Split("-");
            if(parts.Length != 2)
            {
                Debug.LogError("Invalid link format");
                return;
            }
            UIManager.Instance.ShowDiscussion(int.Parse(parts[0]), int.Parse(parts[1]));
            return;
        }

        if (int.TryParse(id, out int result))
        {
            switch (type)
            {
                case "player":
                    UIManager.Instance.ShowPlayerDetails(result);
                    Debug.Log($"Clicked player with id: {id}");
                    break;
                case "club":
                case "team":
                    UIManager.Instance.ShowTeamDetails(result);
                    Debug.Log($"Clicked club with id: {id}");
                    break;
                case "tactics":
                    var team = TeamManager.Instance.GetTeam(result);
                    UIManager.Instance.ShowTactics(team);
                    Debug.Log($"Clicked club with id: {id}");
                    break;
                case "manager":
                    UIManager.Instance.ShowManagerDetails(result);
                    Debug.Log($"Clicked manager with id: {id}");
                    break;
                default:
                    Debug.LogError("Unknown link type");
                    break;
            }
        }
    }
}
