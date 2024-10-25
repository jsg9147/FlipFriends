using Steamworks;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    private SteamLobbyManager steamLobbyManager;
    private MirrorRoomManager mirrorNetwork;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // �̱��� �ν��Ͻ��� ����
        Instance = this;

        DontDestroyOnLoad(gameObject);

    }

    public void SetSteamLobbyManager(SteamLobbyManager s) => steamLobbyManager = s;
    public void SetMirrorRoomManager(MirrorRoomManager m) => mirrorNetwork = m;

    public void HostLobby()
    {
        steamLobbyManager.HostLobby();
        mirrorNetwork.StartHosting();
    }

    public void LeaveLobby()
    {
        //if (mirrorNetwork != null)
        //    mirrorNetwork.LeaveGame();
        if (steamLobbyManager != null)
            steamLobbyManager.LeaveLobby();
    }

    public void JoinLobby(CSteamID lobbyID)
    {
        // ��� �κ� ��û
        SteamMatchmaking.RequestLobbyList();

        SteamMatchmaking.JoinLobby(lobbyID);
    }

    public void JoinPrivateLobby(string joinCode)
    {
        steamLobbyManager.JoinPrivateLobby(joinCode);
    }

    // GetLobbyListAsync�� NetworkManager���� ȣ���� �� �ֵ��� ����
    public async Task<List<SteamLobbyInfo>> GetLobbyListAsync()
    {
        if (steamLobbyManager != null)
        {
            return await steamLobbyManager.GetLobbyListAsync();
        }
        else
        {
            Debug.LogWarning("SteamLobbyManager�� �������� �ʾҽ��ϴ�.");
            return new List<SteamLobbyInfo>();
        }
    }
}
