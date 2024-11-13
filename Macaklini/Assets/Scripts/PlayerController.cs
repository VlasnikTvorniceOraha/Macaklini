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
    Vector3 spawnPoint1 = new Vector3(-5f,-2f,0f);
    Vector3 spawnPoint2 = new Vector3(5f,-2f,0f);

    public Transform groundCheck;
    public LayerMask groundLayer;
    bool isGrounded;

    public float MovementSpeed = 1f;

    public float JumpForce = 2f;

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
        rb2d.bodyType = RigidbodyType2D.Dynamic;
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
    
            rb2d.velocity = new Vector2(horizontal * MovementSpeed, rb2d.velocity.y * Time.deltaTime);
            isGrounded = Physics2D.OverlapCapsule(groundCheck.position, new Vector2(1f, 0.2f), CapsuleDirection2D.Horizontal, 0, groundLayer);
            if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            {
                Jump();
            }
        }
        
    }

    void Movement()
    {
        horizontal = Input.GetAxisRaw("Horizontal");
    }

    void Jump()
    {
        Debug.Log("JUMP");
        rb2d.velocity = new Vector2(rb2d.velocity.x , JumpForce);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        

        //if you are the owner and the host, set the player to spawnPoint1 and rename the player to "Host"
        if (IsOwner && IsHost)
        {
            transform.SetPositionAndRotation(spawnPoint1, new Quaternion());
            //playerName.Value = "Host";
        }
        //if you are the owner and the client, set the player to spawnPoint2 and rename the player to "Client"
        else if (IsOwner && IsClient)
        {
            transform.SetPositionAndRotation(spawnPoint2, new Quaternion());
            //playerName.Value = "Client";
        }
    }

}
