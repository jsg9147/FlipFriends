using UnityEngine;
using TMPro;
using Mirror;
public class GameRoomUI : MonoBehaviour
{
    private const string lobbyKeyHideStr = "**********";

    public TMP_Text lobbyKeyText;

    private bool hideKey = true;

    private void Start()
    {
        lobbyKeyText.text = lobbyKeyHideStr;
    }

    public void ShowLobbyKey()
    {
        hideKey = !hideKey;

        if (hideKey)
        {
            lobbyKeyText.text = lobbyKeyHideStr;
        }
        else
        {
            lobbyKeyText.text = SteamLobbyManager.Instance.lobbyKeyStr;
        }
    }

    public void CopyLobbyKey()
    {
        GUIUtility.systemCopyBuffer = SteamLobbyManager.Instance.lobbyKeyStr; // �ؽ�Ʈ�� Ŭ�����忡 ����
    }

    public void ExitRoom()
    {
        NetworkManager.Instance.LeaveLobby();
        NetworkRoomManager.singleton.StopHost();
    }
}
