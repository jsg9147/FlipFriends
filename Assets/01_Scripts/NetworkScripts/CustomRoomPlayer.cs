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
        playerInfo.playerName = playerName; // 플레이어 정보 업데이트
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
            Debug.LogError("PlayerController 컴포넌트를 찾을 수 없습니다.");
        }
    }
}
