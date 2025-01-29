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
    public NetworkVariable<int> ownerId = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
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
    
    // movement variables
    private float _horizontalInput;
    private float _verticalInput;
    private bool _isJumping;
    
    // bool to prevent multiple side jumps
    private bool _bStickyJumpUsed = true;
    // bool used to determine if the player is stuck to the sticky wall or if they (slowly) slide down
    private bool _bStickyWallSlidingEnabled = true;

    public bool canMove = false;
    
    // jump direction changes if we are on a sticky wall
    private Vector2 _jumpDirection = Vector2.up;
    
    // coyote time and jump buffer variables
    private float _coyoteTimeCounter;
    private float _jumpBufferCounter;
    
    // fixed values
    private float _horizontalSpeed = 4f;
    private float _jumpingPower = 15f;
    private float _coyoteTime = 0.2f; // the bigger the value, the more time the player has to jump button after going over the edge
    private float _jumpBuffertime = 0.1f; // the bigger the value, the more time the player has to jump before landing on the ground
    private float _defaultPlayerGravityScale = 3f;

    // player spawning points
    private GameObject[] _playerSpawnPoints;


    public bool HasWeaponEquipped { get; private set; } = false;

    public void EquipWeapon()
    {
        HasWeaponEquipped = true;
    }

    public void DropWeapon()
    {
        HasWeaponEquipped = false;
    }

    void Start()
    {
        _networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        _unityTransport = _networkManager.gameObject.GetComponent<UnityTransport>();
        _uiManager = GameObject.Find("UI").GetComponent<UIManager>();
        _rb2d = GetComponent<Rigidbody2D>();
        _rb2d.bodyType = RigidbodyType2D.Dynamic;
        _rb2d.gravityScale = _defaultPlayerGravityScale;
        _boxCollider = GetComponent<BoxCollider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        _playerSpawnPoints = GameObject.FindGameObjectsWithTag("PlayerSpawnPoint");
    }
    
    //zahtjevam svoj info od servera za postavljanje spritea i tako
    private void RequestPlayerInfoRpc()
    {

    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log("Spawnan igrac");
        isAlive.Value = true;
        isAlive.OnValueChanged += CheckForEndOfRoundAfterPlayerDeath;
        
        if (IsOwner && IsHost)
        {
            transform.SetPositionAndRotation(_playerSpawnPoints[0].transform.position, new Quaternion());
        }
        else if (IsOwner && IsClient)
        {
            transform.SetPositionAndRotation(_playerSpawnPoints[1].transform.position, new Quaternion());
        }
    }

    
    
    // Update is called once per frame
    void Update()
    {
        if (_uiManager.gameStarted.Value && IsOwner && canMove)
        {
            CheckForMovementInput();
        }
    }
    
    
    
    void FixedUpdate() 
    {
        if (IsOwner)
        {
            if (_jumpDirection == Vector2.up)
            {
                _rb2d.velocity = new Vector2(_horizontalInput * _horizontalSpeed, _rb2d.velocity.y);
            }
            else if (_jumpDirection == Vector2.left)
            {
                _rb2d.velocity = new Vector2(0.9f * _rb2d.velocity.x, _rb2d.velocity.y);

                if (_bStickyWallSlidingEnabled && !_bStickyJumpUsed && _horizontalInput <= 0)
                {
                    _rb2d.gravityScale = 0.05f * _defaultPlayerGravityScale;
                }
                else if (_bStickyWallSlidingEnabled && !_bStickyJumpUsed && _horizontalInput > 0)
                {
                    _rb2d.gravityScale = 0;
                    _rb2d.velocity = new Vector2(_rb2d.velocity.x, 0.0f);
                }
            }
            else if (_jumpDirection == Vector2.right)
            {
                _rb2d.velocity = new Vector2(0.9f * _rb2d.velocity.x, _rb2d.velocity.y);
                
                if (_bStickyWallSlidingEnabled && !_bStickyJumpUsed && _horizontalInput >= 0)
                {
                    _rb2d.gravityScale = 0.05f * _defaultPlayerGravityScale;
                }
                else if (_bStickyWallSlidingEnabled && !_bStickyJumpUsed && _horizontalInput < 0)
                {
                    _rb2d.gravityScale = 0;
                    _rb2d.velocity = new Vector2(_rb2d.velocity.x, 0.0f);
                }
            }
            
        }
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
        _isGroundedLeft = Physics2D.Raycast(_bottomLeftCorner, Vector2.down, 0.1f, groundLayer);
        _isGroundedRight = Physics2D.Raycast(_bottomRightCorner, Vector2.down, 0.1f, groundLayer);
        _isGrounded = _isGroundedLeft || _isGroundedRight;
        
        if (_isGrounded)
        {
            _coyoteTimeCounter = _coyoteTime;
            _rb2d.gravityScale = _defaultPlayerGravityScale;
            _jumpDirection = Vector2.up;
        }

        // normal jump
        if (_jumpDirection == Vector2.up)
        {
            if (!_isGrounded)
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

            // jump higher if the jump button is pressed for longer
            if (Input.GetButtonUp("Jump") && _rb2d.velocity.y > 0f)
            {
                _rb2d.velocity = new Vector2(_rb2d.velocity.x, _rb2d.velocity.y * 0.5f);
                _coyoteTimeCounter = 0f;
            }
        }
        // side jump left (if the sticky wall is on the right)
        else if (_jumpDirection == Vector2.left)
        {
            if (Input.GetButtonDown("Jump") && !_bStickyJumpUsed)
            {
                _rb2d.velocity = new Vector2(-_jumpingPower, _rb2d.velocity.y);
            }
        }
        // side jump right
        else if (_jumpDirection == Vector2.right)
        {
            if (Input.GetButtonDown("Jump") && !_bStickyJumpUsed)
            {
                _rb2d.velocity = new Vector2(_jumpingPower, _rb2d.velocity.y);
            }
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
                _jumpDirection = Vector2.right;
                _rb2d.velocity = new Vector2(0, 0);
                _rb2d.gravityScale = 0;
                _bStickyJumpUsed = false;
            }
            // sticky wall is right of the player
            else if (cumulatedContactDirection.x > 0)
            {
                Debug.LogFormat("sticky wall is right of the player");
                _jumpDirection = Vector2.left;
                _rb2d.velocity = new Vector2(0, 0);
                _rb2d.gravityScale = 0;
                _bStickyJumpUsed = false;
            }
        }
        // sticky wall is top/bottom of the player, currently not in use
        else if (MathF.Abs(cumulatedContactDirection.x) < MathF.Abs(cumulatedContactDirection.y)) 
        { 
            // sticky wall is on top of the player
            if (cumulatedContactDirection.y > 0)
            {
                Debug.LogFormat("sticky wall is on top of the player");
            }
            // sticky wall is bottom of the player
            else if (cumulatedContactDirection.y < 0)
            {
                Debug.LogFormat("sticky wall is below the player");
            }
        }
    }

    
    
    public void OnCollisionExit2D(Collision2D other)
    {
        if (other.gameObject.CompareTag("Sticky"))
        {
            Debug.LogFormat("exit from sticky");
            _rb2d.gravityScale = _defaultPlayerGravityScale;
            _bStickyJumpUsed = true;
        }
    }
}
