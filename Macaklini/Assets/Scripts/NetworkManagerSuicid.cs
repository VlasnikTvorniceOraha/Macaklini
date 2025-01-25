using UnityEngine;

public class NetworkManagerSuicid : MonoBehaviour
{
    void Awake()
    {
        // ako vec postoji NetworkManager instanca, ubi se
        int instances = FindObjectsOfType<NetworkManagerSuicid>().Length;
        if (instances > 1)
        {
            
            Destroy(gameObject);
            return;
        }
    }
}
