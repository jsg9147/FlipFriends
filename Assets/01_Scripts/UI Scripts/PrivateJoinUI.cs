using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PrivateJoinUI : MonoBehaviour
{
    public GameObject gameModeUI;
    public TMP_InputField joinCodeInputField;
    public Button joinButton;

    void Start()
    {
        if (joinCodeInputField != null)
        {
            joinCodeInputField.onValueChanged.AddListener(delegate { JoinButtonEnable(); });
            TextUpperChange();
        }
        JoinButtonEnable(); // �ʱ� ���� ����
    }

    void JoinButtonEnable()
    {
        // �Է� �ʵ��� ���̰� 8�̰�, ��� ���ڰ� ������ ��� ��ư Ȱ��ȭ
        if (joinCodeInputField.text.Length == 8)
        {
            joinButton.interactable = true;
        }
        else
        {
            joinButton.interactable = false;
        }
    }

    void TextUpperChange()
    {
        joinCodeInputField.text = joinCodeInputField.text.ToUpper();
    }


    bool IsAllDigits(string text)
    {
        foreach (char c in text)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }
        return true;
    }

    public void JoinSteamLobby()
    {
        NetworkManager.Instance.JoinPrivateLobby(joinCodeInputField.text);
    }

    public void ExitJoinUI()
    {
        gameModeUI.gameObject.SetActive(true);
        gameObject.SetActive(false);
    }
}
