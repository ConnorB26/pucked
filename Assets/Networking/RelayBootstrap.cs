using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;

namespace Networking
{
    public static class RelayBootstrap
    {
        public static async Task<string> StartHostWithRelay(int maxConnections)
        {
            await EnsureServicesAsync();

            // Create allocation for up to `maxConnections` clients
            var alloc = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();

            utp.SetHostRelayData(
                alloc.RelayServer.IpV4,
                (ushort)alloc.RelayServer.Port,
                alloc.AllocationIdBytes,
                alloc.Key, // HMAC key (64 bytes)
                alloc.ConnectionData
            );

            var joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            var started = NetworkManager.Singleton.StartHost();
            if (!started)
            {
                Debug.LogError("[RelayBootstrap] Failed to start host after configuring Relay.");
                return null;
            }

            Debug.Log($"[RelayBootstrap] Host started. Join code: {joinCode}");
            return joinCode;
        }

        public static async Task<bool> StartClientWithRelay(string joinCode)
        {
            await EnsureServicesAsync();

            var join = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();

            utp.SetClientRelayData(
                join.RelayServer.IpV4,
                (ushort)join.RelayServer.Port,
                join.AllocationIdBytes,
                join.Key, // HMAC key (64 bytes)
                join.ConnectionData, // this client's connectionData
                join.HostConnectionData
            );

            var ok = NetworkManager.Singleton.StartClient();
            if (!ok)
            {
                Debug.LogError("[RelayBootstrap] Failed to start client after configuring Relay.");
                return false;
            }

            Debug.Log("[RelayBootstrap] Client started and connecting via Relay.");
            return true;
        }

        private static async Task EnsureServicesAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}