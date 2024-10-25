using UnityEngine;
using Mirror;
using Steamworks;
using Mirror.FizzySteam;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public class MirrorRoomManager : NetworkRoomManager
{
    public static MirrorRoomManager Instance { get; private set; }

    public GameObject lobbyPlayerPrefab;
    public int maxLobbyMembers = 4;
    public List<SteamLobbyInfo> lobbyInfos = new List<SteamLobbyInfo>();
    public string lobbyKeyStr { get; private set; }

    private CSteamID mySteamID;
    public CSteamID currentLobbyID { get; private set; }
    private List<CSteamID> lobbyIDs = new List<CSteamID>();

    Callback<LobbyCreated_t> lobbyCreated;
    Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
    Callback<LobbyEnter_t> lobbyEntered;
    Callback<LobbyMatchList_t> lobbyMatchList;

    private const string HostAddressKey = "FlipFriends";
    private const string PrivateLobbyKey = "FlipFriendsLobbyKey";

    public override void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("중복된 MirrorNetworkManager가 감지되어 파괴됩니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!SteamAPI.Init())
        {
            Debug.LogError("SteamAPI 초기화 실패");
            return;
        }

        base.Awake();
    }

    public override void Start()
    {
        transport = FindAnyObjectByType<FizzySteamworks>();

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);

        mySteamID = SteamUser.GetSteamID();

        //NetworkManager.Instance.SetMirrorRoomManager(this);
    }

    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
        SteamAPI.Shutdown();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);

        if (lobbyPlayerPrefab != null)
        {
            GameObject playerInstance = Instantiate(lobbyPlayerPrefab);
            NetworkServer.Spawn(playerInstance, conn);
        }
        else
        {
            Debug.LogError("Player Prefab is not assigned in the RoomManager");
        }
    }

    public override void OnRoomServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnRoomServerAddPlayer(conn);
    }

    public void StartHosting()
    {
        StartHost();
        HostLobby();
    }

    public void StartJoining(string networkAddress)
    {
        this.networkAddress = networkAddress;
        StartClient();
    }

    public void HostLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxLobbyMembers);
    }

    public void JoinPrivateLobby(string joinCode)
    {
        SteamMatchmaking.RequestLobbyList();
        lobbyMatchList = Callback<LobbyMatchList_t>.Create((LobbyMatchList_t callback) =>
        {
            for (int i = 0; i < callback.m_nLobbiesMatching; i++)
            {
                CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
                string existingKey = SteamMatchmaking.GetLobbyData(lobbyID, HostAddressKey);

                if (existingKey == joinCode)
                {
                    SteamMatchmaking.JoinLobby(lobbyID);
                    lobbyKeyStr = joinCode;
                    return;
                }
            }
            Debug.LogWarning("일치하는 로비를 찾을 수 없습니다.");
        });
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK) { return; }
        lobbyKeyStr = GenerateUniqueLobbyKey();
        CSteamID hostSteamID = SteamUser.GetSteamID();

        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HostAddressKey, hostSteamID.ToString());
        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), PrivateLobbyKey, lobbyKeyStr);
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
    }

    private string GenerateUniqueLobbyKey(int length = 8)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        using (var rng = new RNGCryptoServiceProvider())
        {
            byte[] data = new byte[length];
            rng.GetBytes(data);
            StringBuilder result = new StringBuilder(length);
            foreach (byte b in data)
            {
                result.Append(chars[b % chars.Length]);
            }
            return result.ToString();
        }
    }

    private void OnLobbyMatchList(LobbyMatchList_t callback)
    {
        lobbyIDs.Clear();
        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
        {
            CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
            lobbyIDs.Add(lobbyID);
        }
    }

    public void LeaveLobby()
    {
        if (currentLobbyID != CSteamID.Nil)
        {
            StopHost();
            SteamMatchmaking.LeaveLobby(currentLobbyID);
            Debug.Log("로비를 나갔습니다: " + currentLobbyID);
            currentLobbyID = CSteamID.Nil;
        }
        else
        {
            Debug.LogWarning("현재 참가 중인 로비가 없습니다.");
        }
    }

    public async Task<List<SteamLobbyInfo>> GetLobbyListAsync()
    {
        lobbyInfos.Clear();
        SteamMatchmaking.RequestLobbyList();

        var tcs = new TaskCompletionSource<List<SteamLobbyInfo>>();

        lobbyMatchList = Callback<LobbyMatchList_t>.Create((LobbyMatchList_t callback) =>
        {
            for (int i = 0; i < callback.m_nLobbiesMatching; i++)
            {
                SteamLobbyInfo lobbyInfo = new SteamLobbyInfo(SteamMatchmaking.GetLobbyByIndex(i));
                string existingKey = SteamMatchmaking.GetLobbyData(lobbyInfo.LobbyID, HostAddressKey);
                if (existingKey != "")
                {
                    lobbyInfos.Add(lobbyInfo);
                }
            }
            tcs.SetResult(lobbyInfos);
        });

        return await tcs.Task;
    }

    public void JoinLobby(CSteamID lobbyID)
    {
        // 모든 로비를 요청
        SteamMatchmaking.RequestLobbyList();

        SteamMatchmaking.JoinLobby(lobbyID);
    }
}

public class PlayerInfo
{
    public ulong m_SteamID;
    public string playerName;
    public Color playerColor;
}
