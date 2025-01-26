using System.Collections;
using UnityEngine;
using Unity.Netcode;
using System.Globalization;

public class Weapon : NetworkBehaviour
{
    // Network
    private NetworkManager _networkManager;

    // Weapon Configuration
    [SerializeField] private WeaponScriptableObject _weaponConfig;

    // Private variables
    private int _ammo;
    private float _originalY;
    private bool _isEquipped = false;
    private FollowTransform _followTransform;
    private Transform _playerTransform;

    private void Awake()
    {
        _followTransform = GetComponent<FollowTransform>();
    }

    void Start()
    {
        _ammo = _weaponConfig.ClipSize;
        _originalY = transform.position.y;
        _networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        if (!_networkManager) Debug.LogError("Network manager not found!");
        else Debug.Log("Network manager initialized");
    }

    void Update()
    {
        if (!_isEquipped)
        {
            transform.position = new Vector2(transform.position.x, _originalY + Mathf.Sin(5 * Time.time) * 0.1f);
        }
        
        if (_isEquipped && IsOwner)
        {
            RotateToFollowMouse();

            if (Input.GetKeyDown(KeyCode.Mouse0) && _ammo > 0)
            {
                Shoot();
            } 
            else if (_ammo <= 0)
            {
                Destroy(gameObject, 0.5f);
            }
        }

    }

    private void RotateToFollowMouse()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = mousePosition - transform.position;
        float weaponAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, weaponAngle);
    }

    void Shoot()
    {
        if (IsOwner)
        {
            _ammo--;
            ShootServerRpc();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && collision.TryGetComponent(out NetworkObject networkObject))
        {
            Debug.Log($"{_weaponConfig.name} picked up by player!");

            _playerTransform = collision.transform;

            WeaponPickedUpServerRPC(networkObject.OwnerClientId);
        }
    }

    [Rpc(SendTo.Server)]
    void ShootServerRpc()
    {
        ShootClientRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    void ShootClientRpc()
    {
        Debug.Log("Shoot " + _ammo);
    }

    [Rpc(SendTo.Server)]
    private void WeaponPickedUpServerRPC(ulong ClientId)
    {
        GetComponent<NetworkObject>().ChangeOwnership(ClientId);
        WeaponPickedUpClientRPC(RpcTarget.Single(ClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void WeaponPickedUpClientRPC(RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId == NetworkManager.LocalClientId)
        {
            if (_playerTransform != null)
            {
                _isEquipped = true;
                GetComponent<CircleCollider2D>().enabled = false;
                _followTransform.SetTargetTransform(_playerTransform);
            }
            else
            {
                Debug.LogError("Player transform was not cached correctly!");
            }
        }
    }

}
