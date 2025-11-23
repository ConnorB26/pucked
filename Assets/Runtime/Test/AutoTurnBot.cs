using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MatchClient))]
public class AutoTurnBot : MonoBehaviour
{
    [Range(0f, 1f)] public float playProbability = 0.55f;
    public float thinkSeconds = 0.35f;

    private MatchClient _client;
    private readonly HashSet<int> _hand = new(); // track instanceIds we know about

    void Awake()
    {
        _client = GetComponent<MatchClient>();
    }

    // This gets called by MatchClient.ReceiveEvent (see small patch below).
    public void OnEvent(object evt)
    {
        switch (evt)
        {
            case CardDrawn cd:
                if (cd.PlayerId == _client.LocalPlayerId && !cd.IsPuckd)
                    _hand.Add(cd.InstanceId);
                break;

            case CardPlayed cp:
                if (cp.PlayerId == _client.LocalPlayerId)
                    _hand.Remove(cp.InstanceId);
                break;

            case PlayerEliminated pe:
                if (pe.PlayerId == _client.LocalPlayerId)
                    _hand.Clear();
                break;

            case TurnStarted ts:
                if (ts.PlayerId == _client.LocalPlayerId)
                    Invoke(nameof(DecideAndAct), thinkSeconds);
                break;
        }
    }

    private void DecideAndAct()
    {
        // 1) Try to play a random card we know about with some probability
        if (_hand.Count > 0 && Random.value < playProbability)
        {
            var pick = _hand.ElementAt(Random.Range(0, _hand.Count));
            _client.RequestPlay(pick, targetPlayerId: null); // host will reject illegal plays
        }

        // 2) Always follow up with a draw shortly after (if it's still our turn).
        // If our play ended the turn, the draw will be ignored by the host.
        Invoke(nameof(SafetyDraw), 0.15f);
    }

    private void SafetyDraw()
    {
        _client.RequestDraw();
    }
}