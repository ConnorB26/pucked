using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;

namespace Networking
{
    /// <summary>
    /// Static helper that initializes Unity Services, configures the UTP transport with Relay data,
    /// and starts NetworkManager as host or client.
    /// </summary>
    public static class RelayBootstrap
    {
        public static string LastJoinCode { get; private set; }
        public static int MaxConnections { get; private set; }

        /// <summary>Creates a Relay allocation, configures host transport, and starts NetworkManager as host. Returns the join code.</summary>
        public static async Task<string> StartHostWithRelay(int maxConnections)
        {
            await EnsureServicesAsync();

            var alloc = await RelayService.Instance.CreateAllocationAsync(maxConnections);

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();

            utp.SetHostRelayData(
                alloc.RelayServer.IpV4,
                (ushort)alloc.RelayServer.Port,
                alloc.AllocationIdBytes,
                alloc.Key,
                alloc.ConnectionData
            );

            var joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            var started = NetworkManager.Singleton.StartHost();
            if (!started)
            {
                Debug.LogError("[RelayBootstrap] Failed to start host after configuring Relay.");
                return null;
            }

            LastJoinCode = joinCode;
            MaxConnections = maxConnections;

            Debug.Log($"[RelayBootstrap] Host started. Join code: {joinCode}");
            return joinCode;
        }

        /// <summary>Joins an existing Relay allocation by code, configures client transport, and starts NetworkManager as client.</summary>
        public static async Task<bool> StartClientWithRelay(string joinCode)
        {
            await EnsureServicesAsync();

            var join = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();

            utp.SetClientRelayData(
                join.RelayServer.IpV4,
                (ushort)join.RelayServer.Port,
                join.AllocationIdBytes,
                join.Key,
                join.ConnectionData,
                join.HostConnectionData
            );

            var ok = NetworkManager.Singleton.StartClient();
            if (!ok)
            {
                Debug.LogError("[RelayBootstrap] Failed to start client after configuring Relay.");
                return false;
            }

            LastJoinCode = joinCode;

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