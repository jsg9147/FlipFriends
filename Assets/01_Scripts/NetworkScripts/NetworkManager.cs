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

        // 싱글톤 인스턴스를 설정
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
        // 모든 로비를 요청
        SteamMatchmaking.RequestLobbyList();

        SteamMatchmaking.JoinLobby(lobbyID);
    }

    public void JoinPrivateLobby(string joinCode)
    {
        steamLobbyManager.JoinPrivateLobby(joinCode);
    }

    // GetLobbyListAsync를 NetworkManager에서 호출할 수 있도록 정의
    public async Task<List<SteamLobbyInfo>> GetLobbyListAsync()
    {
        if (steamLobbyManager != null)
        {
            return await steamLobbyManager.GetLobbyListAsync();
        }
        else
        {
            Debug.LogWarning("SteamLobbyManager가 설정되지 않았습니다.");
            return new List<SteamLobbyInfo>();
        }
    }
}
