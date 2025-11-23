using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerProfile
{
    public string displayName;
    public Texture2D avatar; // optional for later UI
    public Color preferredColor = Color.clear; // optional hint; host will assign a unique color
}

public enum LobbyPhase
{
    Waiting, // people can join/leave, ready up
    InMatch, // game running; no joins allowed
    PostMatch, // match finished; host may restart or close
    Closed
}

[Serializable]
public class LobbyPlayer
{
    public int playerId; // authoritative
    public int seatIndex; // 0..N-1
    public PlayerProfile profile;
    public Color assignedColor;
    public bool isReady;
    public bool isHost;
}

[Serializable]
public class LobbyState
{
    public string lobbyCode;
    public LobbyPhase phase = LobbyPhase.Waiting;
    public int maxPlayers = 4;
    public List<LobbyPlayer> players = new();
}