using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

//struktura za pracenje varijabli kad su igraci in game
public class PlayerInfoGame : INetworkSerializable
{
    public int ClientId;
    public string PlayerName;
    public int PlayerGunster;
    public int roundsWon;
    public int kills;
    public int deaths;
    public PlayerController playerController;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
        serializer.SerializeValue(ref PlayerGunster);
        serializer.SerializeValue(ref roundsWon);
        serializer.SerializeValue(ref kills);
        serializer.SerializeValue(ref deaths);
    }
    
}
