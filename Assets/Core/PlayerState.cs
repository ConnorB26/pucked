using System.Collections.Generic;

public sealed class PlayerState
{
    public readonly int PlayerId;
    public readonly string Name;
    public readonly int SeatIndex;
    public bool Eliminated;
    public readonly List<int> Hand = new(); // store InstanceIds

    public PlayerState(int playerId, string name, int seatIndex)
    {
        PlayerId = playerId;
        Name = name;
        SeatIndex = seatIndex;
    }
}