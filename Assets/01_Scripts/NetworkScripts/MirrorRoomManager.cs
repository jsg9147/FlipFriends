// MirrorNetworkManager.cs - Mirror 네트워크 관련 기능
using UnityEngine;
using Mirror;
using Steamworks;
using Mirror.FizzySteam;
using System;

public class MirrorRoomManager : NetworkRoomManager
{
    public static MirrorRoomManager Instance { get; private set; }

    public GameObject lobbyPlayerPrefab;

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
        base.Awake();
    }

    public override void Start()
    {
        transport = FindAnyObjectByType<FizzySteamworks>();

        NetworkManager.Instance.SetMirrorRoomManager(this);
    }

    public override void OnApplicationQuit()
    {
        base.OnApplicationQuit();
    }

    public override void OnRoomServerAddPlayer(NetworkConnectionToClient conn)
    {
        // 기존의 base 메서드를 호출하여 기본 NetworkRoomPlayer 로직을 수행합니다.
        base.OnRoomServerAddPlayer(conn);

        // 추가로 플레이어를 로비에 인스턴스화
        if (lobbyPlayerPrefab != null)
        {
            GameObject playerInstance = Instantiate(lobbyPlayerPrefab);
            NetworkServer.Spawn(playerInstance, conn); // 소유권을 설정하여 스폰
        }
        else
        {
            Debug.LogError("Player Prefab is not assigned in the RoomManager");
        }
    }

    public void StartHosting()
    {
        StartHost();
    }

    public void StartJoining(string networkAddress)
    {
        this.networkAddress = networkAddress;
        StartClient();
    }

    public void LeaveGame()
    {
        try
        {
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                StopHost();
            }
            else if (NetworkClient.isConnected)
            {
                StopClient();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error when leaving the game: {ex.Message}");
        }
    }
}
