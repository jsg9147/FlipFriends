// MirrorNetworkManager.cs - Mirror ��Ʈ��ũ ���� ���
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
            Debug.LogWarning("�ߺ��� MirrorNetworkManager�� �����Ǿ� �ı��˴ϴ�.");
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
        // ������ base �޼��带 ȣ���Ͽ� �⺻ NetworkRoomPlayer ������ �����մϴ�.
        base.OnRoomServerAddPlayer(conn);

        // �߰��� �÷��̾ �κ� �ν��Ͻ�ȭ
        if (lobbyPlayerPrefab != null)
        {
            GameObject playerInstance = Instantiate(lobbyPlayerPrefab);
            NetworkServer.Spawn(playerInstance, conn); // �������� �����Ͽ� ����
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
