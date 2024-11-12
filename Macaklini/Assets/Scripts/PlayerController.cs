using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class PlayerController : NetworkBehaviour
{
    NetworkManager networkManager;
    UnityTransport unityTransport;
    UIManager uiManager;
    Rigidbody2D rb2d;

    public float MovementSpeed = 1f;

    public float JumpForce = 1f;

    float horizontal;
    void Start()
    {
        networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        unityTransport = networkManager.gameObject.GetComponent<UnityTransport>();
        uiManager = GameObject.Find("UI").GetComponent<UIManager>();
        rb2d = this.GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    void Update()
    {
        if (uiManager.gameStarted.Value && IsOwner)
        {
            //igra je pocela, omoguci kretanje
            Movement();
        }
    }

    void FixedUpdate() 
    {
        if (IsOwner)
        {
            rb2d.velocity = new Vector2(horizontal * MovementSpeed, rb2d.velocity.y);
        }
        
    }

    void Movement()
    {
        horizontal = Input.GetAxisRaw("Horizontal");
    }
}
