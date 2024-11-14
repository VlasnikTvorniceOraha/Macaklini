using System.Collections;
using UnityEngine;
using Unity.Netcode;
using System.Globalization;

public class WeaponScript : NetworkBehaviour
{
    private NetworkManager _networkManager;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private TrailRenderer bulletTrail;
    [SerializeField] private LayerMask whatIsEnemy;
    [SerializeField] private GameObject DEBUG_POINT;
    [SerializeField] private int maxAmmo = 5;
    private int ammo;
    private float _originalY;
    private bool _isEquipped = false;
    private RaycastHit2D _rayHit;
    private NetworkVariable<ulong> ownerClientId = new NetworkVariable<ulong>();

    // Start is called before the first frame update
    void Start()
    {
        ammo = maxAmmo;
        _originalY = transform.position.y;
        _networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
    }

    // Update is called once per frame
    void Update()
    {
        if (GetComponent<CircleCollider2D>().enabled)
        {
            transform.position = new Vector2(transform.position.x, _originalY + Mathf.Sin(5 * Time.time) * 0.1f);
        }

        if (_isEquipped && ownerClientId.Value == NetworkManager.Singleton.LocalClientId)
        {
            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 direction = mousePosition - transform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
            Debug.Log("METAK");
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
            _rayHit = Physics2D.Raycast(shootPoint.position, shootPoint.right, 1000f, whatIsEnemy);
            if (_rayHit)
            {
                ammo--;
                ShootServerRpc(_rayHit.point);
            }
        }
    }

    [ServerRpc]
    void ShootServerRpc(Vector2 hitPoint)
    {
        ShootClientRpc(hitPoint);
    }

    [ClientRpc]
    void ShootClientRpc(Vector2 hitPoint)
    {
        TrailRenderer trail = Instantiate(bulletTrail, shootPoint.position, Quaternion.identity);
        StartCoroutine(SpawnTrail(trail, hitPoint));
    }

    private IEnumerator SpawnTrail(TrailRenderer trail, Vector2 hitPoint)
    {
        float time = 0;
        Vector3 startPos = trail.transform.position;

        while (time < 1)
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
            GetComponent<CircleCollider2D>().enabled = false;
            transform.parent = collision.transform;
            transform.localPosition = Vector2.zero;
            _isEquipped = true;

            if (IsServer)
            {
                ownerClientId.Value = networkObject.OwnerClientId;
            }
        }
    }
}
