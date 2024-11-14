using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class PlayerController : NetworkBehaviour
{
    private NetworkManager _networkManager;
    private UnityTransport _unityTransport;
    private UIManager _uiManager;
    private Rigidbody2D _rb2d;
    private SpriteRenderer _spriteRenderer;

    public Transform groundCheck;
    public LayerMask groundLayer;
    
    // movement variables
    public float MovementSpeed = 1f;
    public float JumpForce = 20f;
    private float horizontalInput;
    private float verticalInput;
    private bool shouldJump = false;
    private bool isGrounded;
    private Vector2 _jumpDirection = Vector2.up;
    
    void Start()
    {
        _networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        _unityTransport = _networkManager.gameObject.GetComponent<UnityTransport>();
        _uiManager = GameObject.Find("UI").GetComponent<UIManager>();
        _rb2d = this.GetComponent<Rigidbody2D>();
        _rb2d.bodyType = RigidbodyType2D.Dynamic;
        _spriteRenderer = this.GetComponent<SpriteRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        // igra traje, provjeri input
        if (_uiManager.gameStarted.Value && IsOwner)
        {
            CheckForMovementInput();
        }
    }
    
    void FixedUpdate() 
    {
        if (IsOwner)
        {
            _rb2d.velocity = new Vector2(MovementSpeed * Time.fixedDeltaTime * horizontalInput, _rb2d.velocity.y);
            if (!isGrounded)
            {
                _rb2d.gravityScale = 8;
            }
            else
            {
                _rb2d.gravityScale = 1.5f;
            }
            
            if (shouldJump)
            {
                shouldJump = false;
                Jump();
            }
        }
    }

    void CheckForMovementInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal");
        if (horizontalInput < 0)
        {
            _spriteRenderer.flipX = false;
        }
        else if (horizontalInput > 0)
        {
            _spriteRenderer.flipX = true;
        }
        
        verticalInput = Input.GetAxisRaw("Vertical");

        isGrounded = Physics2D.Raycast(groundCheck.position, Vector2.down, 1, groundLayer);
        if (Input.GetKey(KeyCode.L))
        {
            Debug.LogFormat("isGrounded: {0}", isGrounded);
        }
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            shouldJump = true;
        }
    }

    void Jump()
    {
        Debug.Log("JUMP");
        _rb2d.AddForce(_jumpDirection * JumpForce, ForceMode2D.Force);
        _jumpDirection = Vector2.up;
        _rb2d.gravityScale = 1.5f;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // if you are the owner and the host, set the player to spawnPoint1 and rename the player to "Host"
        if (IsOwner && IsHost)
        {
            transform.SetPositionAndRotation(SpawnLocations.SampleSceneSpawnLocations[0], new Quaternion());
            //playerName.Value = "Host";
        }
        // if you are the owner and the client, set the player to spawnPoint2 and rename the player to "Client"
        else if (IsOwner && IsClient)
        {
            transform.SetPositionAndRotation(SpawnLocations.SampleSceneSpawnLocations[1], new Quaternion());
            //playerName.Value = "Client";
        }
    }
    
    public void OnCollisionEnter2D(Collision2D other)
    {
        return;
        //Debug.LogFormat("PlayerController::OnCollisionEnter2D");
        if (other.gameObject.CompareTag($"Sticky"))
        {
            //isGrounded = true;
            //_rb2d.gravityScale = 0f;
            // vector start is player, vector end is the sticky wall which the player has collided with
            Vector2 cumulatedContactDirection = new Vector2(); 
            foreach (var contact in other.contacts)
            {
                cumulatedContactDirection.x += contact.point.x - gameObject.transform.position.x;
                cumulatedContactDirection.y += contact.point.y - gameObject.transform.position.y;
            }
            //Debug.LogFormat("cumulatedContactDirection: {0}, {1}", cumulatedContactDirection.x, cumulatedContactDirection.y);

            if (MathF.Abs(cumulatedContactDirection.x) > MathF.Abs(cumulatedContactDirection.y))
            {
                // sticky wall is left of the player
                if (cumulatedContactDirection.x < 0)
                {
                    Debug.LogFormat("sticky wall is left of the player");
                    //_jumpDirection = Vector2.right;
                }
                // sticky wall is right of the player
                else if (cumulatedContactDirection.x > 0)
                {
                    Debug.LogFormat("sticky wall is right of the player");
                    //_jumpDirection = Vector2.left;
                }
            }
            else if (MathF.Abs(cumulatedContactDirection.x) < MathF.Abs(cumulatedContactDirection.y))
            {
                // sticky wall is on top of the player
                if (cumulatedContactDirection.y > 0)
                {
                    Debug.LogFormat("sticky wall is on top of the player");
                    //_jumpDirection = Vector2.down;
                }
                // sticky wall is bottom of the player
                else if (cumulatedContactDirection.y < 0)
                {
                    Debug.LogFormat("sticky wall is below the player");
                    //_jumpDirection = Vector2.down;
                }
            }
        }
    }

    public void OnCollisionExit2D(Collision2D other)
    {
        return;
        //Debug.LogFormat("PlayerController::OnCollisionExit2D");
        if (other.gameObject.CompareTag($"Sticky"))
        {
            Debug.LogFormat("exit from sticky");
        }
    }
}
