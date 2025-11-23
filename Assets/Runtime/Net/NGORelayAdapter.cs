using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class NGORelayAdapter : MonoBehaviour, IHostAdapter, IClientAdapter
{
    // Names for NGO custom messages
    private const string MsgIntent = "PCK_INTENT";
    private const string MsgEvent = "PCK_EVENT";

    // References to route messages
    [SerializeField] private LobbyHost lobbyHost;
    [SerializeField] private MatchHost matchHost;
    [SerializeField] private LobbyClient lobbyClient;
    [SerializeField] private MatchClient matchClient;

    // --- IClientAdapter: used by LobbyClient / MatchClient (local peer) ---
    public void SendIntent(object intent)
    {
        // Clients: send intents to the Host (ServerClientId).
        // Host: also allowed to loop-back (e.g., AI or local actions)
        var dest = NetworkManager.Singleton.IsServer
            ? NetworkManager.ServerClientId
            : NetworkManager.ServerClientId;

        using var writer = BuildPayload(intent);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MsgIntent, dest, writer);
    }

    // Clients in lobby need to pass sender for pending-join (NOT needed with NGO):
    public void SendIntent(object intent, LobbyClient _unusedSender) => SendIntent(intent);

    public void BindHost(LobbyHost host)
    {
        /* not needed in NGO adapter */
    }

    // --- IHostAdapter: used by LobbyHost / MatchHost (authoritative peer) ---
    public void Broadcast(object evt)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        using var writer = BuildPayload(evt);
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            // Do not send to server self unless you want local feedback
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MsgEvent, clientId, writer);
        }

        // Optionally, also deliver to local client scripts:
        DeliverEventLocally(evt);
    }

    public void SendTo(int playerId, object evt)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        var clientId = (ulong)playerId; // IMPORTANT: playerId == clientId assumption

        using var writer = BuildPayload(evt);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(MsgEvent, clientId, writer);

        // If targeted client is the host (playerId == server id), also loopback:
        if (clientId == NetworkManager.ServerClientId)
            DeliverEventLocally(evt);
    }

    public void RegisterClient(LobbyClient client)
    {
        throw new NotImplementedException();
    }

    public void UnregisterClient(LobbyClient client)
    {
        throw new NotImplementedException();
    }

    public void RegisterMatchClient(MatchClient client)
    {
        throw new NotImplementedException();
    }

    public void UnregisterMatchClient(MatchClient client)
    {
        throw new NotImplementedException();
    }

    // --- Setup NGO message handlers ---
    private void Awake()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[NGORelayAdapter] NetworkManager not found in scene.");
            return;
        }

        nm.OnClientConnectedCallback += OnClientConnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;

        nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgIntent, OnIntentReceivedFromClient);
        nm.CustomMessagingManager.RegisterNamedMessageHandler(MsgEvent, OnEventReceivedFromHost);
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton == null) return;
        var nm = NetworkManager.Singleton;

        nm.OnClientConnectedCallback -= OnClientConnected;
        nm.OnClientDisconnectCallback -= OnClientDisconnected;

        if (nm.CustomMessagingManager == null) return;
        nm.CustomMessagingManager.UnregisterNamedMessageHandler(MsgIntent);
        nm.CustomMessagingManager.UnregisterNamedMessageHandler(MsgEvent);
    }

    private void OnClientConnected(ulong clientId)
    {
        // Host callback: a client socket is live.
        // You can auto-join them to the Lobby here (as "connected but not ready"),
        // then wait for their UpdateProfileIntent to set name/avatar.
        if (!NetworkManager.Singleton.IsServer) return;

        // Example: tell LobbyHost someone arrived (no code needed if you already handle joins in LobbyHost)
        // lobbyHost.InternalOnSocketConnected((int)clientId);

        // If the host itself connects (ServerClientId), you may also want to trigger local welcome.
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        // Route to LobbyHost existing leave flow:
        // Non-host leave during InMatch: reclaim cards & eliminate
        // Host leave: close lobby (but in NGO, stopping the host will end session anyway)
        lobbyHost?.ReceiveIntent(new LeaveLobbyIntent { playerId = (int)clientId });
    }

    // --- NGO message handlers ---
    private void OnIntentReceivedFromClient(ulong senderClientId, FastBufferReader reader)
    {
        // Host receives client intents here
        if (!NetworkManager.Singleton.IsServer) return;

        var json = ReadString(ref reader);
        var wrapper = JsonUtility.FromJson<NetworkMessage>(json);
        var type = Type.GetType(wrapper.type);
        var intent = JsonUtility.FromJson(wrapper.json, type);

        // (Recommended) Use playerId == (int)senderClientId
        // If your LobbyHost expects JoinLobbyIntent with a "code", you can still send that as the first intent
        // but identity comes from senderClientId, so you never need extra routing.
        lobbyHost?.ReceiveIntent(intent);
        matchHost?.ReceiveIntent(intent);
    }

    private void OnEventReceivedFromHost(ulong senderClientId, FastBufferReader reader)
    {
        // Clients receive events from host here
        if (NetworkManager.Singleton.IsServer) return;

        var json = ReadString(ref reader);
        var wrapper = JsonUtility.FromJson<NetworkMessage>(json);
        var type = Type.GetType(wrapper.type);
        var evt = JsonUtility.FromJson(wrapper.json, type);

        lobbyClient?.ReceiveEvent(evt);
        matchClient?.ReceiveEvent(evt);
    }

    // --- Helpers ---
    private static FastBufferWriter BuildPayload(object obj)
    {
        var outer = new NetworkMessage
        {
            type = obj.GetType().AssemblyQualifiedName, // robust type resolution
            json = JsonUtility.ToJson(obj)
        };
        var json = JsonUtility.ToJson(outer);

        var writer = new FastBufferWriter(1024, Allocator.Temp);
        WriteString(ref writer, json);
        return writer;
    }

    private static void WriteString(ref FastBufferWriter writer, string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s);
        writer.WriteValueSafe(bytes.Length);
        writer.WriteBytesSafe(bytes, 0, bytes.Length);
    }

    private static string ReadString(ref FastBufferReader reader)
    {
        reader.ReadValueSafe(out int len);
        var bytes = new byte[len];
        reader.ReadBytesSafe(ref bytes, len);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private void DeliverEventLocally(object evt)
    {
        // Deliver to local LobbyClient/MatchClient (host’s own UI)
        lobbyClient?.ReceiveEvent(evt);
        matchClient?.ReceiveEvent(evt);
    }
}

[Serializable]
public class NetworkMessage
{
    public string type;
    public string json;
}