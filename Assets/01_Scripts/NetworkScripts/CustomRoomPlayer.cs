using UnityEngine;
using Mirror;

public class CustomRoomPlayer : NetworkRoomPlayer
{
    public GameObject playerControllerPrefab;
    public PlayerInfo playerInfo;

    private GameObject playerController;

    [Command]
    private void CmdSetPlayerName(string playerName)
    {
        playerInfo.playerName = playerName; // �÷��̾� ���� ������Ʈ
        RpcSetPlayerName(playerName);
    }

    [ClientRpc]
    private void RpcSetPlayerName(string playerName)
    {
        SetPlayerName(playerName);
    }

    private void SetPlayerName(string playerName)
    {
        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.nameText.text = playerName;
        }
        else
        {
            Debug.LogError("PlayerController ������Ʈ�� ã�� �� �����ϴ�.");
        }
    }
}
