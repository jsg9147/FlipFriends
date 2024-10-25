using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks; // Task�� ����ϱ� ���� �߰�

public class PublicLobbyUI : MonoBehaviour
{
    public LobbyItem lobbyItemPrefab;
    public Transform lobbyItemContent;

    public GameObject gameModeUI;

    private SteamLobbyInfo lobbyInfo;

    private async void OnEnable()
    {
        if (SteamManager.Initialized)
            await LobbyListUpdate();
    }

    private void OnDisable()
    {
        LobbyListReset();
    }

    public async Task LobbyListUpdate()
    {
        LobbyListReset();

        List<SteamLobbyInfo> lobbyInfoList = await NetworkManager.Instance.GetLobbyListAsync();

        foreach (SteamLobbyInfo info in lobbyInfoList)
        {
            LobbyItem item = Instantiate(lobbyItemPrefab, lobbyItemContent);
            item.SetLobbyInfo(info);
        }
    }

    void LobbyListReset()
    {
        foreach (Transform child in lobbyItemContent)
        {
            Destroy(child.gameObject);
        }
    }

    public void Back()
    {
        gameModeUI.SetActive(true);
        gameObject.SetActive(false);
    }
    // ��ư�� ���� ���� �޼��� �߰�
    public void OnLobbyListUpdateButtonClicked()
    {
        // �񵿱� �۾��� �����ϴ� ���
        LobbyListUpdate().ConfigureAwait(false);
    }
}
