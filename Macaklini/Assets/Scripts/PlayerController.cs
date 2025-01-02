using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class PlayerController : NetworkBehaviour
{
    public Transform groundCheck;
    public LayerMask groundLayer;
    public NetworkVariable<bool> isAlive = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public float MovementSpeed; // originalno 200 u prefabu
    public float JumpForce; // originalno 600 u prefabu
    
    private NetworkManager _networkManager;
    private UnityTransport _unityTransport;
    private UIManager _uiManager;
    private Rigidbody2D _rb2d;
    private SpriteRenderer _spriteRenderer;
    private GameManager _gameManager;
    
    // movement variables
    private float horizontalInput;
    private float verticalInput;
    private bool shouldJump;
    private bool isGroundedLeft;
    private bool isGroundedRight;
    private bool isGrounded;
    private Vector2 _jumpDirection = Vector2.up;
    private int _updatesSinceLastGrounded;
    
    // not grounded at the edge of a platform fix
    private Vector2 _bottomLeftCorner;
    private Vector2 _bottomRightCorner;

    
    
    void Start()
    {
        _networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        _unityTransport = _networkManager.gameObject.GetComponent<UnityTransport>();
        _uiManager = GameObject.Find("UI").GetComponent<UIManager>();
        _rb2d = GetComponent<Rigidbody2D>();
        _rb2d.bodyType = RigidbodyType2D.Dynamic;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
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
        // horizontal movement
        horizontalInput = Input.GetAxisRaw("Horizontal");
        if (horizontalInput < 0)
        {
            _spriteRenderer.flipX = false;
        }
        else if (horizontalInput > 0)
        {
            _spriteRenderer.flipX = true;
        }
        
        // vertical movement
        verticalInput = Input.GetAxisRaw("Vertical");

        // jumping
        _bottomLeftCorner = new Vector2(-gameObject.GetComponent<BoxCollider2D>().size.x / 2 + gameObject.transform.position.x, 
            -gameObject.GetComponent<BoxCollider2D>().size.y / 2 + gameObject.transform.position.y);
        _bottomRightCorner = new Vector2(gameObject.GetComponent<BoxCollider2D>().size.x / 2 + gameObject.transform.position.x, 
            -gameObject.GetComponent<BoxCollider2D>().size.y / 2 + gameObject.transform.position.y);
        isGroundedLeft = Physics2D.Raycast(_bottomLeftCorner, Vector2.down, 1, groundLayer);
        isGroundedRight = Physics2D.Raycast(_bottomRightCorner, Vector2.down, 1, groundLayer);
        isGrounded = isGroundedLeft || isGroundedRight;
        if (isGrounded)
        {
            _updatesSinceLastGrounded = 0;
        }
        // this is done to avoid jittering with collider switching between grounded and not grounded between calls
        // tldr: disable jump fatigue
        else if (_updatesSinceLastGrounded < 3)
        {
            isGrounded = true;
            _updatesSinceLastGrounded++;
        }
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // shouldJump = true;
            if (isGrounded && _updatesSinceLastGrounded < 3)
            {
                shouldJump = true;
            }
            else
            {
                shouldJump = false;
            }
        }
        
        // debug logging
        if (Input.GetKey(KeyCode.L))
        {
            Debug.LogFormat("isGrounded: {0}", isGrounded);
        }
    }

    
    
    void Jump()
    {
        _rb2d.AddForce(_jumpDirection * JumpForce, ForceMode2D.Force);
        _jumpDirection = Vector2.up;
        _rb2d.gravityScale = 1.5f;
    }
    
    

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log("Spawnan igrac");
        isAlive.Value = true;
        isAlive.OnValueChanged += CheckForEndOfRoundAfterPlayerDeath;

        // if you are the owner and the host, set the player to spawnPoint1
        if (IsOwner && IsHost)
        {
            transform.SetPositionAndRotation(SpawnLocations.SampleSceneSpawnLocations[0], new Quaternion());
            //playerName.Value = "Host";
        }
        // if you are the owner and the client, set the player to spawnPoint2
        else if (IsOwner && IsClient)
        {
            transform.SetPositionAndRotation(SpawnLocations.SampleSceneSpawnLocations[1], new Quaternion());
            //playerName.Value = "Client";
        }
    }
    
    

    void CheckForEndOfRoundAfterPlayerDeath(bool prevValue, bool newValue)
    {
        if (newValue == false)
        {
            // igrac je umro
            _gameManager.CheckForEndOfRound();
        }
    }
    
    
    
    public void OnCollisionEnter2D(Collision2D other)
    {
        if (!other.gameObject.CompareTag("Sticky"))
        {
            return;
        }
        
        // vector start is player, vector end is the sticky wall which the player has collided with
        // cumulatedContactDirection because there can be multiple contact points on collision because of BoxCollider2D
        Vector2 cumulatedContactDirection = new Vector2(); 
        foreach (var contact in other.contacts)
        {
            cumulatedContactDirection.x += contact.point.x - gameObject.transform.position.x;
            cumulatedContactDirection.y += contact.point.y - gameObject.transform.position.y;
        }
        Debug.LogFormat("cumulatedContactDirection: {0}, {1}", cumulatedContactDirection.x, cumulatedContactDirection.y);

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

    
    
    public void OnCollisionExit2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Sticky"))
        {
            Debug.LogFormat("exit from sticky");
        }
    }
}
