using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkManagerSuicid : MonoBehaviour
{
    // Start is called before the first frame update
    void Awake()
    {
        //provjeri je li vec postoji gamemangaer instanca i ako da ubi se
        int instances = FindObjectsOfType<NetworkManagerSuicid>().Length;
        if (instances != 1)
        {
            //upucaj se
            Destroy(this.gameObject);
            return;
        }
    }

    
}
