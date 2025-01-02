using Unity.Netcode;

// struktura za pracenje varijabli kad su igraci in game
public class PlayerInfoGame : INetworkSerializable
{
    public int ClientId;
    public string PlayerName;
    public int PlayerGunster;
    public int RoundsWon;
    public int Kills;
    public int Deaths;
    public PlayerController PlayerController;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref PlayerGunster);
        serializer.SerializeValue(ref RoundsWon);
        serializer.SerializeValue(ref Kills);
        serializer.SerializeValue(ref Deaths);
    }
    
}
