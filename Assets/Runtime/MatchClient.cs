using UnityEngine;

public class MatchClient : MonoBehaviour
{
    [Header("Identity")] public int LocalPlayerId = -1;

    private IClientAdapter _clientTx;

    public void BindTransport(IClientAdapter tx)
    {
        _clientTx = tx;
    }

    // === Outgoing intents (UI should call these) ===
    public void RequestPlay(int instanceId, int? targetPlayerId = null)
    {
        _clientTx?.SendIntent(new PlayCardIntent
            { PlayerId = LocalPlayerId, InstanceId = instanceId, TargetPlayerId = targetPlayerId });
    }

    public void RequestDraw()
    {
        _clientTx?.SendIntent(new DrawIntent { PlayerId = LocalPlayerId });
    }

    // === Incoming events (Host → Client) ===
    public void ReceiveEvent(object evt)
    {
        // Switch on your core events and update UI.
        switch (evt)
        {
            case TurnStarted e:
                // update banner, attack debt indicator
                Debug.Log($"[C{LocalPlayerId}] TurnStarted -> P{e.PlayerId}, debt:{e.AttackDebt}");
                break;

            case CardPlayed e:
                Debug.Log($"[C{LocalPlayerId}] P{e.PlayerId} played {e.InstanceId}");
                break;

            case CardDrawn e:
                Debug.Log($"[C{LocalPlayerId}] P{e.PlayerId} drew {(e.IsPuckd ? "Puck’d" : "a card")}.");
                break;

            case PeekResult e:
                if (e.PlayerId == LocalPlayerId)
                    Debug.Log(
                        $"[C{LocalPlayerId}] Peek: top {e.InstanceIds.Length} instanceIds = {string.Join(",", e.InstanceIds)}");
                break;

            case DeckShuffled:
                Debug.Log($"[C{LocalPlayerId}] Deck shuffled.");
                break;

            case PlayerEliminated e:
                Debug.Log($"[C{LocalPlayerId}] P{e.PlayerId} eliminated.");
                break;

            case DrawSkippedDeckEmpty e:
                Debug.Log($"[C{LocalPlayerId}] Deck empty; draw skipped for P{e.PlayerId}.");
                break;

            case GameEnded e:
                Debug.Log($"[C{LocalPlayerId}] Game ended. Winner P{e.WinnerPlayerId}.");
                break;
        }

        var bot = GetComponent<AutoTurnBot>();
        if (bot) bot.OnEvent(evt);
    }
}