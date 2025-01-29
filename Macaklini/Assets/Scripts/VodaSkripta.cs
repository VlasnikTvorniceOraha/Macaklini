using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VodaSkripta : MonoBehaviour
{

    private float time = 0.0f;

    void Update()
    {
        time += Time.deltaTime;

        if(time > 10){
            time = 0.0f;
            gameObject.GetComponent<Animator>().Play("Voda");
        }
    }
}
