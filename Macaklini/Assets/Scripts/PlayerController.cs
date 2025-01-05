using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class PlayerController : NetworkBehaviour
{
    public Transform groundCheck;
    public LayerMask groundLayer;
    public NetworkVariable<bool> isAlive = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    private NetworkManager _networkManager;
    private UnityTransport _unityTransport;
    private UIManager _uiManager;
    private Rigidbody2D _rb2d;
    private SpriteRenderer _spriteRenderer;
    private GameManager _gameManager;
    
    // variables used for isGrounded check
    private BoxCollider2D _boxCollider;
    private Vector2 _bottomLeftCorner;
    private Vector2 _bottomRightCorner;
    private bool _isGroundedLeft;
    private bool _isGroundedRight;
    private bool _isGrounded;
    
    private float _horizontalInput;
    private float _verticalInput;
    private float _horizontalSpeed = 4f;
    private float _jumpingPower = 15f;
    
    // jump direction changes if we are on a sticky wall
    private Vector2 _jumpDirection = Vector2.up;
    
    // coyote time:  a brief period of time after running off a platform where the game will still register the player pressing the jump button
    // jump buffer: same, but for pressing the jump button before landing on the ground
    private float _coyoteTime = 0.2f; // the bigger the value, the more time the player has to jump button after going over the edge
    private float _coyoteTimeCounter;
    private float _jumpBuffertime = 0.1f; // the bigger the value, the more time the player has to jump before landing on the ground 
    private float _jumpBufferCounter;
    private bool _isJumping;
    
    
    void Start()
    {
        _networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        _unityTransport = _networkManager.gameObject.GetComponent<UnityTransport>();
        _uiManager = GameObject.Find("UI").GetComponent<UIManager>();
        _rb2d = GetComponent<Rigidbody2D>();
        _rb2d.bodyType = RigidbodyType2D.Dynamic;
        _boxCollider = GetComponent<BoxCollider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
    }
    
    
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log("Spawnan igrac");
        isAlive.Value = true;
        isAlive.OnValueChanged += CheckForEndOfRoundAfterPlayerDeath;
        
        if (IsOwner && IsHost)
        {
            transform.SetPositionAndRotation(SpawnLocations.SampleSceneSpawnLocations[0], new Quaternion());
        }
        else if (IsOwner && IsClient)
        {
            transform.SetPositionAndRotation(SpawnLocations.SampleSceneSpawnLocations[1], new Quaternion());
        }
    }

    
    
    // Update is called once per frame
    void Update()
    {
        if (_uiManager.gameStarted.Value && IsOwner)
        {
            CheckForMovementInput();
        }
    }
    
    
    
    void FixedUpdate() 
    {
        if (IsOwner)
        {
            // coyote time and jump buffer update
            _rb2d.velocity = new Vector2(_horizontalInput * _horizontalSpeed, _rb2d.velocity.y);
        }
        
        // rkunstek, 5/1/2025, ideja:
            // ako smo odskocili od sticky zida,
            // CheckForMovementInput(): u njemu je _rb2d.velocity = new Vector2(odskocniSpeed, _rb2d.velocity.y);
            // FixedUpdate: u njemu je _rb2d.velocity = new Vector2(_rb2d.velocity.x +- horizontalInput * horizontalSpeed * 0.1f, _rb2d.velocity.y);
            // znaci da je horizontal input oslabljen sve dok opet ne landamo na tlo
            // dodatno, mozda i da maksimiziramo horizontalnu brzinu (da nemre biti veca od odskocne brzine)
    }
    
    
    
    void CheckForMovementInput()
    {
        // horizontal movement
        _horizontalInput = Input.GetAxisRaw("Horizontal");
        if (_horizontalInput < 0)
        {
            _spriteRenderer.flipX = false;
        }
        else if (_horizontalInput > 0)
        {
            _spriteRenderer.flipX = true;
        }

        // isGrounded check
        _bottomLeftCorner = new Vector2(-_boxCollider.size.x / 2 + gameObject.transform.position.x, 
            -_boxCollider.size.y / 2 + gameObject.transform.position.y);
        _bottomRightCorner = new Vector2(_boxCollider.size.x / 2 + gameObject.transform.position.x, 
            -_boxCollider.size.y / 2 + gameObject.transform.position.y);
        _isGroundedLeft = Physics2D.Raycast(_bottomLeftCorner, Vector2.down, 0.2f, groundLayer);
        _isGroundedRight = Physics2D.Raycast(_bottomRightCorner, Vector2.down, 0.2f, groundLayer);
        _isGrounded = _isGroundedLeft || _isGroundedRight;

        
        if (_isGrounded)
        {
            _coyoteTimeCounter = _coyoteTime;
        }
        else
        {
            _coyoteTimeCounter -= Time.deltaTime;
        }

        if (Input.GetButtonDown("Jump"))
        {
            _jumpBufferCounter = _jumpBuffertime;
        }
        else
        {
            _jumpBufferCounter -= Time.deltaTime;
        }

        if (_coyoteTimeCounter > 0f && _jumpBufferCounter > 0f && !_isJumping)
        {
            _rb2d.velocity = new Vector2(_rb2d.velocity.x, _jumpingPower);
            _jumpBufferCounter = 0f;
            StartCoroutine(JumpCooldown());
        }

        // jump higher is the jump button is pressed for longer
        if (Input.GetButtonUp("Jump") && _rb2d.velocity.y > 0f)
        {
            _rb2d.velocity = new Vector2(_rb2d.velocity.x, _rb2d.velocity.y * 0.5f);
            _coyoteTimeCounter = 0f;
        }
    }


    
    private IEnumerator JumpCooldown()
    {
        _isJumping = true;
        yield return new WaitForSeconds(0.4f);
        _isJumping = false;
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
