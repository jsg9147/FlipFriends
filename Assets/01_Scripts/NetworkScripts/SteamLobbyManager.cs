// SteamLobbyManager.cs - Steam API ���� ���
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
            Debug.LogError("SteamAPI �ʱ�ȭ ����");
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
            Debug.LogWarning("��ġ�ϴ� �κ� ã�� �� �����ϴ�.");
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
            Debug.Log("�κ� �������ϴ�: " + currentLobbyID);
            currentLobbyID = CSteamID.Nil; // �κ� ID �ʱ�ȭ
        }
        else
        {
            Debug.LogWarning("���� ���� ���� �κ� �����ϴ�.");
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
//            Debug.LogWarning("�ߺ��� SteamLobbyManager�� �����Ǿ� �ı��˴ϴ�.");
//            Destroy(gameObject);
//            return;
//        }

//        Instance = this;

//        DontDestroyOnLoad(gameObject);

//        if (!SteamAPI.Init())
//        {
//            Debug.LogError("SteamAPI �ʱ�ȭ ����");
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
//        // �⺻������ RoomPlayer�� �߰��� ��, CustomPlayer�� �����ϴ� ����Դϴ�.
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
//        // NetworkIdentity�� ���� playerPrefab�� �����մϴ�.
//        if (lobbyPlayerPrefab != null)
//        {
//            if (conn != null)
//            {
//                // �̹� �÷��̾ ���ῡ �Ҵ�Ǿ� �ִ��� Ȯ��
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
//            Transport.active = transport;  // transport�� �ùٸ��� FizzySteamworks �ν��Ͻ����� Ȯ���ϼ���.
//            Debug.Log("Transport�� �Ҵ�Ǿ����ϴ�.");
//        }
//    }

//    private void OnLobbyCreated(LobbyCreated_t callback)
//    {
//        if (this == null || !this.gameObject)
//        {
//            Debug.LogWarning("SteamLobbyManager ��ü�� �̹� �ı��Ǿ����ϴ�. �ݹ��� �����մϴ�.");
//            return;
//        }

//        if (callback.m_eResult != EResult.k_EResultOK)
//        {
//            return;
//        }

//        transport = FindAnyObjectByType<FizzySteamworks>();
//        AssignTransport();

//        StartHost();

//        // ������ �ʱ�ȭ ����
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

//        // Steam ID ������ �ƴ� ��� ���� ó��
//        if (string.IsNullOrEmpty(hostAddress) || !ulong.TryParse(hostAddress, out _))
//        {
//            Debug.LogError("��ȿ���� ���� ȣ��Ʈ �ּ��Դϴ�. Steam ID�� Ȯ���ϼ���.");
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
//        // �κ� ���� ������ SteamID�� ����
//        CSteamID steamIDLobby = new CSteamID(callback.m_ulSteamIDLobby);
//        CSteamID steamIDUserChanged = new CSteamID(callback.m_ulSteamIDUserChanged);
//        CSteamID steamIDMakingChange = new CSteamID(callback.m_ulSteamIDMakingChange);

//        // ���� ������ Ȯ��
//        if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
//        {
//            Debug.Log($"User {steamIDUserChanged} entered the lobby {steamIDLobby}");
//        }
//        else if ((callback.m_rgfChatMemberStateChange & (uint)EChatMemberStateChange.k_EChatMemberStateChangeLeft) != 0)
//        {
//            Debug.Log($"User {steamIDUserChanged} left the lobby {steamIDLobby}");
//            ulong steamID = callback.m_ulSteamIDUserChanged;

//            // Mirror���� �ش� �÷��̾��� ���� ����
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

//            // Mirror���� �ش� �÷��̾��� ���� ����
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

//            // �ߺ� ���� Ȯ��
//            isUnique = !IsKeyInUse(newKey);
//        } while (!isUnique);

//        return newKey;
//    }

//    private bool IsKeyInUse(string key)
//    {
//        // ���� �κ� ��Ͽ��� Ű�� ��� ������ Ȯ��
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
//        // ��� �κ� ��û
//        SteamMatchmaking.RequestLobbyList();

//        // �κ� ����� ��ȯ�Ǹ� OnLobbyMatchList �ݹ��� ȣ��˴ϴ�.
//        lobbyMatchList = Callback<LobbyMatchList_t>.Create((LobbyMatchList_t callback) =>
//        {
//            for (int i = 0; i < callback.m_nLobbiesMatching; i++)
//            {
//                this.lobbyKey = SteamMatchmaking.GetLobbyByIndex(i);
//                string existingKey = SteamMatchmaking.GetLobbyData(this.lobbyKey, HostAddressKey);

//                Debug.Log($"�κ� ID: {this.lobbyKey}, �κ� Ű: {existingKey}");

//                // �Էµ� Ű�� ��ġ�ϴ� �κ� ã��
//                if (existingKey == joinCode)
//                {
//                    // �ش� �κ� ����
//                    SteamMatchmaking.JoinLobby(this.lobbyKey);
//                    lobbyKeyStr = joinCode;
//                    Debug.Log("�κ� �����Ͽ����ϴ�: " + this.lobbyKey);
//                    return;
//                }
//            }

//            Debug.LogWarning("��ġ�ϴ� �κ� ã�� �� �����ϴ�.");
//        });
//    }

//    public void JoinLobby(CSteamID lobbyID)
//    {
//        // ��� �κ� ��û
//        SteamMatchmaking.RequestLobbyList();

//        SteamMatchmaking.JoinLobby(lobbyID);
//    }

//    public void LeaveLobby()
//    {
//        if (currentLobbyID != CSteamID.Nil)
//        {
//            SteamMatchmaking.LeaveLobby(currentLobbyID);
//            Debug.Log("�κ� �������ϴ�: " + currentLobbyID);
//            currentLobbyID = CSteamID.Nil; // �κ� ID �ʱ�ȭ
//        }
//        else
//        {
//            Debug.LogWarning("���� ���� ���� �κ� �����ϴ�.");
//        }

//        if (NetworkServer.active && NetworkClient.isConnected)
//        {
//            // ������ ���� ���� ��: ������ �����ϰ� ��� Ŭ���̾�Ʈ ���� ����
//            StopHost();
//        }
//        else if (NetworkClient.isConnected)
//        {
//            // Ŭ���̾�Ʈ�� ���� ���� ��: Ŭ���̾�Ʈ ���� ����
//            StopClient();
//        }
//    }

//    public void KickPlayer(ulong targetSteamID)
//    {
//        // �κ��� ȣ��Ʈ�� ���� ����� ���� �� �ֵ��� ó��
//        if (IsHost(currentLobbyID))
//        {
//            // ���� ��� Ŭ���̾�Ʈ���� RPC �Ǵ� �޽����� ���� ���� ó��
//            KickPlayer(FindPlayerInLobby(targetSteamID));
//        }
//    }

//    private void KickPlayer(CSteamID targetSteamID)
//    {
//        // ���� �޽����� ���� Ŭ���̾�Ʈ�� �κ� ����
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

//            // targetSteamID�� ��ġ�ϴ� SteamID�� ã��
//            if (memberSteamID.m_SteamID == targetSteamID)
//            {
//                return memberSteamID;
//            }
//        }

//        // ã�� ���� ��� 0 ��ȯ
//        return new CSteamID();
//    }
//}

public class PlayerInfo
{
    public ulong m_SteamID;
    public string playerName;
    public Color playerColor;
}
