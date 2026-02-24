using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Networking
{
    /// <summary>Listens for disconnect callbacks. Redirects non-host clients to the main menu on disconnect.</summary>
    public class NetworkDisconnectHandler : MonoBehaviour
    {
        public string mainMenuSceneName = "MainMenuScene";

        private void OnEnable()
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (clientId == NetworkManager.Singleton.LocalClientId &&
                !NetworkManager.Singleton.IsServer)
            {
                SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
            }
        }
    }
}