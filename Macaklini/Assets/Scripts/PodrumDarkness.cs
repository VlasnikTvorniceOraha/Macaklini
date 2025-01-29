using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PodrumDarkness : MonoBehaviour
{
    
    public Image image;
    void Start () 
    {
          image = GetComponent<Image>();
          var tempColor = image.color;
          tempColor.a = 1f;
          image.color = tempColor;
    }

    private float time = 0.0f;

    void Update () {
    time += Time.deltaTime;

    if (time >= 2 && time <= 4 || time >= 6 && time <= 8 ) {
        var image = GetComponent<Image>();
        var tempColor = image.color;
        tempColor.a = 0.9f;
        image.color = tempColor;
        
    }

    if(time > 4 && time < 6 || time > 8 && time <= 10 ){
        var tempColor = image.color;
        tempColor.a = 0.75f;
        image.color = tempColor;
    }

    if(time > 10){
        time = 0.0f;
        var tempColor = image.color;
        tempColor.a = 1f;
        image.color = tempColor;
    }
}
}
