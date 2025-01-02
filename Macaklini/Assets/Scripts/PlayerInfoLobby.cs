using Unity.Netcode;

public class PlayerInfoLobby : INetworkSerializable
{
    public int ClientId;
    public string PlayerName;
    public int PlayerGunster;

    // INetworkSerializable
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref PlayerGunster);
    }
    
    // ~INetworkSerializable
    
}
