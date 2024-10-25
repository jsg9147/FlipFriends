using Steamworks;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public class SteamLobbyInfo
{
    // �κ��� Steam ID
    public CSteamID LobbyID { get; private set; }

    // �κ��� �̸�
    public string LobbyName { get; private set; }

    // �κ��� �ִ� �ο� ��
    public int MaxMembers { get; private set; }

    // ���� �κ� ������ ��� ��
    public int CurrentMemberCount { get; private set; }

    // �κ� �ִ� ������� Steam ID ���
    public List<CSteamID> MemberIDs { get; private set; }

    // �κ� ���� ������ ����
    public bool IsInGame { get; private set; }

    public string colorStr { get; private set; }

    // ������
    public SteamLobbyInfo(CSteamID lobbyID)
    {
        LobbyID = lobbyID;
        UpdateLobbyInfo();
    }

    // �κ� ���� ������Ʈ
    public void UpdateLobbyInfo()
    {
        if (!SteamManager.Initialized)
        {
            throw new Exception("Steam is not initialized.");
        }

        LobbyName = SteamMatchmaking.GetLobbyData(LobbyID, "name");
        colorStr = SteamMatchmaking.GetLobbyData(LobbyID, "Color");
        MaxMembers = SteamMatchmaking.GetLobbyMemberLimit(LobbyID);
        CurrentMemberCount = SteamMatchmaking.GetNumLobbyMembers(LobbyID);
        IsInGame = SteamMatchmaking.GetLobbyData(LobbyID, "status") == "in_game";
        MemberIDs = new List<CSteamID>();
        for (int i = 0; i < CurrentMemberCount; i++)
        {
            MemberIDs.Add(SteamMatchmaking.GetLobbyMemberByIndex(LobbyID, i));
        }
    }

    // ������� �̸� ��� ��������
    public List<string> GetMemberNames()
    {
        List<string> memberNames = new List<string>();
        foreach (CSteamID memberID in MemberIDs)
        {
            memberNames.Add(SteamFriends.GetFriendPersonaName(memberID));
        }
        return memberNames;
    }
}