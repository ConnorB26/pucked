using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    [Header("UI")] public Button hostButton;
    public TMP_InputField joinCodeInput; // your "Enter text..." field
    public Button joinButton;
    public TMP_Text statusText; // optional; can be null

    [Header("Config")] [Range(2, 8)] public int maxPlayers = 4;

    private void Awake()
    {
        hostButton.onClick.AddListener(async () => await OnHostClicked());
        joinButton.onClick.AddListener(async () => await OnJoinClicked());
    }

    private async Task OnHostClicked()
    {
        SetInteractable(false);
        SetStatus("Creating lobby...");

        var joinCode = await RelayBootstrap.StartHostWithRelay(maxPlayers);

        if (string.IsNullOrEmpty(joinCode))
        {
            SetStatus("Failed to host lobby.");
            SetInteractable(true);
            return;
        }

        SetStatus($"Lobby created! Join code: {joinCode}");
        GUIUtility.systemCopyBuffer = joinCode; // handy for testing
    }

    private async Task OnJoinClicked()
    {
        var code = joinCodeInput.text.Trim();

        if (string.IsNullOrEmpty(code))
        {
            SetStatus("Please enter a join code.");
            return;
        }

        SetInteractable(false);
        SetStatus($"Joining {code}...");

        var ok = await RelayBootstrap.StartClientWithRelay(code);

        if (!ok)
        {
            SetStatus("Failed to join lobby. Check the code.");
            SetInteractable(true);
            return;
        }

        SetStatus("Joined lobby!");
        // For now we stay on this scene; later you’ll show lobby UI or load the match scene.
    }

    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
        Debug.Log("[LobbyUI] " + msg);
    }

    private void SetInteractable(bool enabled)
    {
        hostButton.interactable = enabled;
        joinButton.interactable = enabled;
        joinCodeInput.interactable = enabled;
    }
}