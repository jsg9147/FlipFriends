// SteamLobbyManager.cs - Steam API 관련 기능
using UnityEngine;
using Steamworks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance { get; private set; }
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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            //Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!SteamAPI.Init())
        {
            Debug.LogError("SteamAPI 초기화 실패");
            return;
        }
    }

    private void Start()
    {
        if (!SteamManager.Initialized) { return; }

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
        lobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);

        mySteamID = SteamUser.GetSteamID();

        NetworkManager.Instance.SetSteamLobbyManager(this);
    }

    private void OnDestroy()
    {
        if (lobbyCreated != null) lobbyCreated.Unregister();
        if (gameLobbyJoinRequested != null) gameLobbyJoinRequested.Unregister();
        if (lobbyEntered != null) lobbyEntered.Unregister();
        if (lobbyMatchList != null) lobbyMatchList.Unregister();

        SteamAPI.Shutdown();
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
            SteamMatchmaking.LeaveLobby(currentLobbyID);
            Debug.Log("로비를 나갔습니다: " + currentLobbyID);
            currentLobbyID = CSteamID.Nil; // 로비 ID 초기화
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
}




//using UnityEngine;
//using Mirror;
//using Steamworks;
//using System.Security.Cryptography;
//using System.Text;
//using System.Collections.Generic;
//using Mirror.FizzySteam;
//using System.Threading.Tasks;

//public class SteamLobbyManager : NetworkRoomManager
//{
//    public static SteamLobbyManager Instance { get; private set; }

//    public GameObject lobbyPlayerPrefab;

//    public List<SteamLobbyInfo> lobbyInfos = new List<SteamLobbyInfo>();

//    public int maxLobbyMembers = 4;

//    Callback<LobbyCreated_t> lobbyCreated;
//    Callback<GameLobbyJoinRequested_t> gameLobbyJoinRequested;
//    Callback<LobbyEnter_t> lobbyEntered;
//    Callback<LobbyMatchList_t> lobbyMatchList;
//    Callback<LobbyChatUpdate_t> lobbyChatUpdate;

//    private const string HostAddressKey = "FlipFriends";
//    private const string PrivateLobbyKey = "FlipFriendsLobbyKey";
//    private List<CSteamID> lobbyIDs = new List<CSteamID>();
//    public string lobbyKeyStr { get; private set; }

//    private CSteamID mySteamID;
//    public string myName { get; private set; }
//    public CSteamID lobbyKey { get; private set; }

//    private CSteamID currentLobbyID;
//    public string LobbyKeyStr => lobbyKeyStr;

//    public CustomRoomPlayer serverRoomPlayer;

//    public override void Awake()
//    {
//        if (Instance != null && Instance != this)
//        {
//            Debug.LogWarning("중복된 SteamLobbyManager가 감지되어 파괴됩니다.");
//            Destroy(gameObject);
//            return;
//        }

//        Instance = this;

//        DontDestroyOnLoad(gameObject);

//        if (!SteamAPI.Init())
//        {
//            Debug.LogError("SteamAPI 초기화 실패");
//            return;
//        }

//        base.Awake();
//    }

//    public override void Start()
//    {
//        if (!SteamManager.Initialized) { return; }

//        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
//        gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
//        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
//        lobbyMatchList = Callback<LobbyMatchList_t>.Create(OnLobbyMatchList);
//        lobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);

//        transport = FindAnyObjectByType<FizzySteamworks>();
//        AssignTransport();

//        mySteamID = SteamUser.GetSteamID();
//        myName = SteamFriends.GetFriendPersonaName(mySteamID);
//    }

//    public override void OnApplicationQuit()
//    {
//        base.OnApplicationQuit();
//        SteamAPI.Shutdown();
//    }
//    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
//    {
//        base.OnServerAddPlayer(conn);
//        PlayerInstantiateInRoom(conn);
//    }

//    public override void OnRoomServerAddPlayer(NetworkConnectionToClient conn)
//    {
//        // 기본적으로 RoomPlayer가 추가된 후, CustomPlayer를 스폰하는 방식입니다.
//        base.OnRoomServerAddPlayer(conn);
//        PlayerInstantiateInRoom(conn);
//    }

//    public override void OnDestroy()
//    {
//        base.OnDestroy();

//        if (lobbyCreated != null) lobbyCreated.Unregister();
//        if (gameLobbyJoinRequested != null) gameLobbyJoinRequested.Unregister();
//        if (lobbyEntered != null) lobbyEntered.Unregister();
//        if (lobbyMatchList != null) lobbyMatchList.Unregister();
//        if (lobbyChatUpdate != null) lobbyChatUpdate.Unregister();
//    }

//    private void PlayerInstantiateInRoom(NetworkConnectionToClient conn = null)
//    {
//        // NetworkIdentity를 가진 playerPrefab을 스폰합니다.
//        if (lobbyPlayerPrefab != null)
//        {
//            if (conn != null)
//            {
//                // 이미 플레이어가 연결에 할당되어 있는지 확인
//                if (conn.identity != null)
//                {
//                    GameObject playerInstance = Instantiate(lobbyPlayerPrefab);
//                    NetworkServer.ReplacePlayerForConnection(conn, playerInstance, ReplacePlayerOptions.KeepAuthority);
//                }
//                else
//                {
//                    GameObject playerInstance = Instantiate(lobbyPlayerPrefab);
//                    NetworkServer.AddPlayerForConnection(conn, playerInstance);
//                    Debug.LogWarning("This connection already has a player assigned.");
//                }
//            }
//            else
//            {
//                GameObject playerInstance = Instantiate(lobbyPlayerPrefab);
//                NetworkServer.Spawn(playerInstance);
//            }
//        }
//        else
//        {
//            Debug.LogError("Player Prefab is not assigned in the RoomManager");
//        }
//    }

//    private void AssignTransport()
//    {
//        if (Transport.active == null)
//        {
//            Transport.active = transport;  // transport가 올바르게 FizzySteamworks 인스턴스인지 확인하세요.
//            Debug.Log("Transport가 할당되었습니다.");
//        }
//    }

//    private void OnLobbyCreated(LobbyCreated_t callback)
//    {
//        if (this == null || !this.gameObject)
//        {
//            Debug.LogWarning("SteamLobbyManager 객체가 이미 파괴되었습니다. 콜백을 무시합니다.");
//            return;
//        }

//        if (callback.m_eResult != EResult.k_EResultOK)
//        {
//            return;
//        }

//        transport = FindAnyObjectByType<FizzySteamworks>();
//        AssignTransport();

//        StartHost();

//        // 나머지 초기화 로직
//        lobbyKeyStr = GenerateUniqueLobbyKey();
//        CSteamID hostSteamID = SteamUser.GetSteamID();

//        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HostAddressKey, hostSteamID.ToString());
//        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), PrivateLobbyKey, lobbyKeyStr);
//        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "name", SteamFriends.GetFriendPersonaName(SteamUser.GetSteamID()));
//        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "status", "out_game");
//        SteamMatchmaking.SetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), "Color", "Green");

//        this.lobbyKey = new CSteamID(callback.m_ulSteamIDLobby);
//    }

//    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t callback)
//    {
//        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
//    }

//    private void OnLobbyEntered(LobbyEnter_t callback)
//    {
//        if (NetworkServer.active) { return; }

//        currentLobbyID = new CSteamID(callback.m_ulSteamIDLobby);
//        string hostAddress = SteamMatchmaking.GetLobbyData(currentLobbyID, HostAddressKey);

//        // Steam ID 형식이 아닌 경우 에러 처리
//        if (string.IsNullOrEmpty(hostAddress) || !ulong.TryParse(hostAddress, out _))
//        {
//            Debug.LogError("유효하지 않은 호스트 주소입니다. Steam ID를 확인하세요.");
//            return;
//        }

//        networkAddress = hostAddress;
//        StartClient();
//    }

//    private void OnLobbyMatchList(LobbyMatchList_t callback)
//    {
//        lobbyIDs.Clear();
//        for (int i = 0; i < callback.m_nLobbiesMatching; i++)
//        {
//            CSteamID lobbyID = SteamMatchmaking.GetLobbyByIndex(i);
//            lobbyIDs.Add(lobbyID);
//        }
//    }

//    private void OnLobbyChatUpdate(LobbyChatUpdate_t callback)
//    {
//        // 로비에 들어온 유저의 SteamID를 얻음
//        CSteamID steamIDLobby = new CSteamID(callback.m_ulSteamIDLobby);
//        CSteamID steamIDUserChanged = new CSteamID(callback.m_ulSteamIDUserChanged);
//        CSteamID steamIDMakingChange = new CSteamID(callback.m_ulSteamIDMakingChange);

//        // 상태 변경을 확인
//        if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
//        {
//            Debug.Log($"User {steamIDUserChanged} entered the lobby {steamIDLobby}");
//        }
//        else if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft) != 0)
//        {
//            Debug.Log($"User {steamIDUserChanged} left the lobby {steamIDLobby}");
//            ulong steamID = callback.m_ulSteamIDUserChanged;

//            // Mirror에서 해당 플레이어의 연결 해제
//            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
//            {
//                CustomRoomPlayer player = conn.identity.GetComponent<CustomRoomPlayer>();
//                if (player != null && player.playerInfo.m_SteamID == steamID)
//                {
//                    conn.Disconnect();
//                    break;
//                }
//            }
//        }
//        else if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeDisconnected) != 0)
//        {
//            Debug.Log($"User {steamIDUserChanged} disconnected from the lobby {steamIDLobby}");
//            ulong steamID = callback.m_ulSteamIDUserChanged;

//            // Mirror에서 해당 플레이어의 연결 해제
//            foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
//            {
//                CustomRoomPlayer player = conn.identity.GetComponent<CustomRoomPlayer>();
//                if (player != null && player.playerInfo.m_SteamID == steamID)
//                {
//                    conn.Disconnect();
//                    break;
//                }
//            }
//        }
//    }
//    private string GenerateUniqueLobbyKey(int length = 8)
//    {
//        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
//        string newKey;
//        bool isUnique = false;
//        do
//        {
//            using (var rng = new RNGCryptoServiceProvider())
//            {
//                byte[] data = new byte[length];
//                rng.GetBytes(data);
//                StringBuilder result = new StringBuilder(length);
//                foreach (byte b in data)
//                {
//                    result.Append(chars[b % chars.Length]);
//                }
//                newKey = result.ToString();
//            }

//            // 중복 여부 확인
//            isUnique = !IsKeyInUse(newKey);
//        } while (!isUnique);

//        return newKey;
//    }

//    private bool IsKeyInUse(string key)
//    {
//        // 현재 로비 목록에서 키가 사용 중인지 확인
//        SteamMatchmaking.RequestLobbyList();

//        foreach (CSteamID lobbyID in lobbyIDs)
//        {
//            string existingKey = SteamMatchmaking.GetLobbyData(lobbyID, HostAddressKey);
//            if (existingKey == key)
//            {
//                return true;
//            }
//        }

//        return false;
//    }

//    public void HostLobby()
//    {
//        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxLobbyMembers);
//    }

//    public async Task<List<SteamLobbyInfo>> GetLobbyListAsync()
//    {
//        lobbyInfos.Clear();
//        SteamMatchmaking.RequestLobbyList();

//        var tcs = new TaskCompletionSource<List<SteamLobbyInfo>>();

//        lobbyMatchList = Callback<LobbyMatchList_t>.Create((LobbyMatchList_t callback) =>
//        {
//            for (int i = 0; i < callback.m_nLobbiesMatching; i++)
//            {
//                SteamLobbyInfo lobbyInfo = new SteamLobbyInfo(SteamMatchmaking.GetLobbyByIndex(i));
//                string existingKey = SteamMatchmaking.GetLobbyData(lobbyInfo.LobbyID, HostAddressKey);
//                if (existingKey != "")
//                {
//                    lobbyInfos.Add(lobbyInfo);
//                }
//            }
//            tcs.SetResult(lobbyInfos);
//        });

//        return await tcs.Task;
//    }

//    public void JoinLobby(string joinCode)
//    {
//        // 모든 로비를 요청
//        SteamMatchmaking.RequestLobbyList();

//        // 로비 목록이 반환되면 OnLobbyMatchList 콜백이 호출됩니다.
//        lobbyMatchList = Callback<LobbyMatchList_t>.Create((LobbyMatchList_t callback) =>
//        {
//            for (int i = 0; i < callback.m_nLobbiesMatching; i++)
//            {
//                this.lobbyKey = SteamMatchmaking.GetLobbyByIndex(i);
//                string existingKey = SteamMatchmaking.GetLobbyData(this.lobbyKey, HostAddressKey);

//                Debug.Log($"로비 ID: {this.lobbyKey}, 로비 키: {existingKey}");

//                // 입력된 키와 일치하는 로비 찾기
//                if (existingKey == joinCode)
//                {
//                    // 해당 로비에 참가
//                    SteamMatchmaking.JoinLobby(this.lobbyKey);
//                    lobbyKeyStr = joinCode;
//                    Debug.Log("로비에 참가하였습니다: " + this.lobbyKey);
//                    return;
//                }
//            }

//            Debug.LogWarning("일치하는 로비를 찾을 수 없습니다.");
//        });
//    }

//    public void JoinLobby(CSteamID lobbyID)
//    {
//        // 모든 로비를 요청
//        SteamMatchmaking.RequestLobbyList();

//        SteamMatchmaking.JoinLobby(lobbyID);
//    }

//    public void LeaveLobby()
//    {
//        if (currentLobbyID != CSteamID.Nil)
//        {
//            SteamMatchmaking.LeaveLobby(currentLobbyID);
//            Debug.Log("로비를 나갔습니다: " + currentLobbyID);
//            currentLobbyID = CSteamID.Nil; // 로비 ID 초기화
//        }
//        else
//        {
//            Debug.LogWarning("현재 참가 중인 로비가 없습니다.");
//        }

//        if (NetworkServer.active && NetworkClient.isConnected)
//        {
//            // 방장이 방을 나갈 때: 서버를 중지하고 모든 클라이언트 연결 해제
//            StopHost();
//        }
//        else if (NetworkClient.isConnected)
//        {
//            // 클라이언트가 방을 나갈 때: 클라이언트 연결 해제
//            StopClient();
//        }
//    }

//    public void KickPlayer(ulong targetSteamID)
//    {
//        // 로비의 호스트만 강퇴 명령을 내릴 수 있도록 처리
//        if (IsHost(currentLobbyID))
//        {
//            // 강퇴 대상 클라이언트에게 RPC 또는 메시지를 보내 강퇴 처리
//            KickPlayer(FindPlayerInLobby(targetSteamID));
//        }
//    }

//    private void KickPlayer(CSteamID targetSteamID)
//    {
//        // 강퇴 메시지를 받은 클라이언트는 로비를 나감
//        if (SteamMatchmaking.GetLobbyOwner(currentLobbyID) == SteamUser.GetSteamID())
//        {
//            SteamMatchmaking.LeaveLobby(currentLobbyID);
//        }
//    }

//    private bool IsHost(CSteamID steamIDLobby)
//    {
//        return SteamMatchmaking.GetLobbyOwner(steamIDLobby) == SteamUser.GetSteamID();
//    }

//    private CSteamID FindPlayerInLobby(ulong targetSteamID)
//    {
//        int memberCount = SteamMatchmaking.GetNumLobbyMembers(currentLobbyID);

//        for (int i = 0; i < memberCount; i++)
//        {
//            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(currentLobbyID, i);

//            // targetSteamID와 일치하는 SteamID를 찾음
//            if (memberSteamID.m_SteamID == targetSteamID)
//            {
//                return memberSteamID;
//            }
//        }

//        // 찾지 못한 경우 0 반환
//        return new CSteamID();
//    }
//}

public class PlayerInfo
{
    public ulong m_SteamID;
    public string playerName;
    public Color playerColor;
}
