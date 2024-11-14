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
    private float horizontal;
    private bool shouldJump = false;
    private bool isGrounded;
    
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
            _rb2d.velocity = new Vector2(MovementSpeed * Time.fixedDeltaTime * horizontal, _rb2d.velocity.y);
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
        horizontal = Input.GetAxisRaw("Horizontal");
        if (horizontal < 0)
        {
            _spriteRenderer.flipX = false;
        }
        else if (horizontal > 0)
        {
            _spriteRenderer.flipX = true;
        }

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
        _rb2d.AddForce(Vector2.up * JumpForce, ForceMode2D.Force);
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
}
