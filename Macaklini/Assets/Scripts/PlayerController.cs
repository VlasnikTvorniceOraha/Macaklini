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

    public Transform groundCheck;
    public LayerMask groundLayer;
    bool isGrounded;

    public float MovementSpeed = 1f;
    public float JumpForce = 20f;

    float horizontal;
    bool shouldJump = false;
    SpriteRenderer spriteRenderer;
    void Start()
    {
        networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        unityTransport = networkManager.gameObject.GetComponent<UnityTransport>();
        uiManager = GameObject.Find("UI").GetComponent<UIManager>();
        rb2d = this.GetComponent<Rigidbody2D>();
        spriteRenderer = this.GetComponent<SpriteRenderer>();
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
            rb2d.velocity = new Vector2(MovementSpeed * Time.fixedDeltaTime * horizontal, rb2d.velocity.y);
            if (!isGrounded)
            {
                rb2d.gravityScale = 8;
            }
            else
            {
                rb2d.gravityScale = 1.5f;
            }
            
            if (shouldJump)
            {
                shouldJump = false;
                Jump();
            }
        }
        
    }

    void Movement()
    {
        horizontal = Input.GetAxisRaw("Horizontal");
        if (horizontal < 0)
        {
            spriteRenderer.flipX = false;
        }
        else if (horizontal > 0)
        {
            spriteRenderer.flipX = true;
        }

        isGrounded = Physics2D.Raycast(groundCheck.position, Vector2.down, 1, groundLayer);
        Debug.LogFormat("isGrounded: {0}", isGrounded);
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            shouldJump = true;
        }
    }

    void Jump()
    {
        Debug.Log("JUMP");
        rb2d.AddForce(Vector2.up * JumpForce, ForceMode2D.Force);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        //if you are the owner and the host, set the player to spawnPoint1 and rename the player to "Host"
        if (IsOwner && IsHost)
        {
            transform.SetPositionAndRotation(SpawnLocations.SampleSceneSpawnLocations[0], new Quaternion());
            //playerName.Value = "Host";
        }
        //if you are the owner and the client, set the player to spawnPoint2 and rename the player to "Client"
        else if (IsOwner && IsClient)
        {
            transform.SetPositionAndRotation(SpawnLocations.SampleSceneSpawnLocations[1], new Quaternion());
            //playerName.Value = "Client";
        }
    }
}
