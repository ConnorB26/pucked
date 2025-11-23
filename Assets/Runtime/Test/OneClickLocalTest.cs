using System.Collections;
using UnityEngine;

public class OneClickLocalTest : MonoBehaviour
{
    [Header("Scene References")] public LobbyClient hostClient; // a LobbyClient you control
    public LobbyClient[] joinerClients; // 1–3 other LobbyClients

    [Header("Profiles")] public string hostName = "You";
    public string[] botNames = { "Bot A", "Bot B", "Bot C" };
    public int maxPlayers = 4;

    void Start() => StartCoroutine(Run());

    IEnumerator Run()
    {
        // Host creates lobby
        var hostProfile = new PlayerProfile { displayName = hostName };
        hostClient.HostLobby(hostProfile, maxPlayers);
        yield return new WaitForSeconds(0.1f);

        // Get lobby code from LobbyHost state (loopback convenience)
        var host = FindFirstObjectByType<LobbyHost>();
        if (!host)
        {
            Debug.LogError("LobbyHost not found.");
            yield break;
        }

        var stateField = typeof(LobbyHost).GetField("_state",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var state = (LobbyState)stateField.GetValue(host);
        var code = state.lobbyCode;
        if (string.IsNullOrEmpty(code))
        {
            Debug.LogError("No lobby code found.");
            yield break;
        }

        // Joiners connect and ready up
        for (var i = 0; i < joinerClients.Length && i < botNames.Length; i++)
        {
            var profile = new PlayerProfile { displayName = botNames[i] };
            joinerClients[i].JoinLobby(code, profile);
            yield return new WaitForSeconds(0.05f);
            joinerClients[i].ToggleReady(true);
        }

        yield return new WaitForSeconds(0.1f);

        // Host readies and starts the match
        hostClient.ToggleReady(true);
        yield return new WaitForSeconds(0.05f);
        hostClient.StartMatch();
    }
}