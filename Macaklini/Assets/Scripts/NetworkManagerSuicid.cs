using UnityEngine;

public class NetworkManagerSuicid : MonoBehaviour
{
    void Awake()
    {
        // ako vec postoji GameManager instanca, ubi se
        int instances = FindObjectsOfType<NetworkManagerSuicid>().Length;
        if (instances > 1)
        {
            Destroy(gameObject);
        }
    }
}
