using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class WeaponScript : NetworkBehaviour
{
    [SerializeField] private Transform shootPoint;
    [SerializeField] private TrailRenderer bulletTrail;
    [SerializeField] private LayerMask whatIsEnemy;
    [SerializeField] private GameObject DEBUG_POINT;
    [SerializeField] private int maxAmmo = 5;
    [SerializeField] private bool isAutomatic = false;
    [SerializeField] private bool isShotgun = false;
    [SerializeField] private float fireRate = 70;

    private AudioSource sound;
    private bool readyToShoot = true;
    private NetworkManager _networkManager;
    private NetworkObject _networkObject;
    private int ammo;
    private float _originalY;
    private bool _isEquipped = false;
    private RaycastHit2D _rayHit;
    private FollowTransform _followTransform;
    private Transform _playerTransform;
    PlayerController playerWeaponManager;
    private void Awake()
    {
        _followTransform = GetComponent<FollowTransform>();
        sound = GetComponent<AudioSource>();
    }
    // Start is called before the first frame update
    void Start()
    {
        ammo = maxAmmo;
        _originalY = transform.position.y;
        _networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        _networkObject = GetComponent<NetworkObject>();
        if (!_networkManager) Debug.LogError("Network manager not found!");
        else Debug.Log("Network manager initialized");
    }

    // Update is called once per frame
    void Update()
    {
        if (!_isEquipped)
        {
            transform.position = new Vector2(transform.position.x, _originalY + Mathf.Sin(5 * Time.time) * 0.1f);
        }

        if (_isEquipped && IsOwner)
        {
            RotateToFollowMouse();

            bool shooting = false;

            if (isAutomatic)
            {
                shooting = Input.GetKey(KeyCode.Mouse0);
            }
            else
            {
                shooting = Input.GetKeyDown(KeyCode.Mouse0);
            }

            if (shooting && ammo > 0 && readyToShoot && !isShotgun)
            {
                sound.Play();
                readyToShoot = false;

                Invoke(nameof(ResetShot), 60f / fireRate);

                ammo--;
                Shoot();
            }

            if (shooting && ammo > 0 && readyToShoot && isShotgun)
            {
                sound.Play();
                readyToShoot = false;

                Invoke(nameof(ResetShot), 60f / fireRate);

                ammo--;
                shootPoint.localEulerAngles = new Vector3(0, 0, UnityEngine.Random.Range(-13, 13));
                Shoot();
                shootPoint.localEulerAngles = new Vector3(0, 0, UnityEngine.Random.Range(-13, 13));
                Shoot();
                shootPoint.localEulerAngles = new Vector3(0, 0, UnityEngine.Random.Range(-13, 13));
                Shoot();
                shootPoint.localEulerAngles = new Vector3(0, 0, 0);
            }
            else if (ammo <= 0)
            {
                playerWeaponManager.DropWeapon();
                Destroy(gameObject, 0.5f);
            }
        }
    }

    private void ResetShot() 
    {
        readyToShoot = true;
    }

    private void RotateToFollowMouse()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 direction = mousePosition - transform.position;
        float weaponAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, weaponAngle);

        Vector3 localScale = Vector3.one;
        if (weaponAngle > 90 || weaponAngle < -90)
        {
            localScale.y = -1f;
        }
        else
        {
            localScale.y = 1f;
        }

        transform.localScale = localScale;
    }

    void Shoot()
    {
        if (IsOwner)
        {
            _rayHit = Physics2D.Raycast(shootPoint.position, shootPoint.right, 1000f, whatIsEnemy);
            if (_rayHit)
            {
                Debug.LogFormat("we are client with id {0} and we shot a 2D collider of an object with tag {1}", _networkObject.OwnerClientId, _rayHit.collider.gameObject.tag);
                // neka od ovih stvari je null
                // _rayHit.collider.gameObject.GetComponent<HealthScript>().TakeDamage(10, (int)_networkObject.OwnerClientId); 
                ShootServerRpc(_rayHit.point);
            }
        }
    }



    [Rpc(SendTo.Server)]
    void ShootServerRpc(Vector2 hitPoint)
    {
        ShootClientRpc(hitPoint);
    }



    [Rpc(SendTo.ClientsAndHost)]
    void ShootClientRpc(Vector2 hitPoint)
    {
        TrailRenderer trail = Instantiate(bulletTrail, shootPoint.position, Quaternion.identity);
        StartCoroutine(SpawnTrail(trail, hitPoint));
    }



    private IEnumerator SpawnTrail(TrailRenderer trail, Vector2 hitPoint)
    {
        float time = 0;
        Vector3 startPos = trail.transform.position;

        while (time < 0.3)
        {
            trail.transform.position = Vector3.Lerp(startPos, hitPoint, time);
            time += Time.deltaTime / trail.time;
            yield return null;
        }

        trail.transform.position = hitPoint;
        Instantiate(DEBUG_POINT, hitPoint, Quaternion.identity);
        Destroy(trail.gameObject, trail.time);
    }



    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && collision.TryGetComponent(out NetworkObject networkObject))
        {
            playerWeaponManager = collision.GetComponent<PlayerController>();


            if (!playerWeaponManager.HasWeaponEquipped)
            {
                Debug.Log($"{transform.gameObject.name} picked up by player!");
                _playerTransform = collision.transform;
                playerWeaponManager.EquipWeapon();
                WeaponPickedUpServerRPC(networkObject.OwnerClientId);
            }
            else
            {
                Debug.Log("Player already has a weapon equipped!");
            }
        }
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
