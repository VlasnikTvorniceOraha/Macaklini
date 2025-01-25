using System.Collections;
using UnityEngine;
using Unity.Netcode;
using System.Globalization;

public class Weapon : NetworkBehaviour
{
    // Network
    private NetworkManager _networkManager;
    private NetworkVariable<ulong> ownerClientId = new NetworkVariable<ulong>();

    // Weapon Configuration
    [SerializeField] private WeaponScriptableObject weaponConfig;

    // Private variables
    private int ammo;
    private float _originalY;
    private bool _isEquipped = false;
    private FollowTransform followTransform;

    private void Awake()
    {
        followTransform = GetComponent<FollowTransform>();
    }

    void Start()
    {
        ammo = weaponConfig.ClipSize;
        _originalY = transform.position.y;
        _networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        if (!_networkManager) Debug.LogError("Network manager not found!");
        else Debug.Log("Network manager initialized");
        GetComponent<NetworkObject>().Spawn();
    }

    void Update()
    {
        //Debug.Log(weaponConfig.name + " is equipped: " + _isEquipped);
        if (!_isEquipped)
        {
            transform.position = new Vector2(transform.position.x, _originalY + Mathf.Sin(5 * Time.time) * 0.1f);
        }
        Debug.Log(weaponConfig.name + " ownership: " + IsOwner);
        if (_isEquipped && IsOwner)
        {
            //Debug.Log("Equipped!");
            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 direction = mousePosition - transform.position;
            float weaponAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, weaponAngle);

            if (Input.GetKeyDown(KeyCode.Mouse0) && ammo > 0)
            {
                ammo--;
                Shoot();
            }

            if (ammo <= 0)
            {
                Destroy(gameObject, 0.5f);
            }
        }

    }

    void Shoot()
    {
        if (IsOwner)
        {
            ammo--;
            ShootServerRpc();
        }
    }

    [ServerRpc]
    void ShootServerRpc()
    {
        ShootClientRpc();
    }

    [ClientRpc]
    void ShootClientRpc()
    {
        Debug.Log("Shoot " + ammo);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && collision.TryGetComponent(out NetworkObject networkObject))
        {
            Debug.Log(weaponConfig.name + " picked up by player!");
            GetComponent<CircleCollider2D>().enabled = false;

            followTransform.SetTargetTransform(collision.transform);
            //transform.parent = collision.transform;
            //transform.localPosition = new Vector2(-0.25f, -0.05f);
            //transform.localPosition = Vector2.zero;

            _isEquipped = true;
            Debug.Log(IsOwnedByServer + " " + networkObject.IsOwnedByServer + " " + networkObject);
            if (networkObject.IsOwnedByServer)
            {
                Debug.Log("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
                GetComponent<NetworkObject>().ChangeOwnership(networkObject.NetworkObjectId);
                ownerClientId.Value = networkObject.NetworkObjectId;
            }
        }
    }

}
