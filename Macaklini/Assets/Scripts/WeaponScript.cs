using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class WeaponScript : MonoBehaviour
{
    private NetworkManager _networkManager;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private TrailRenderer bulletTrail;
    [SerializeField] private LayerMask whatIsEnemy;
    private float _originalY;
    private bool _isEquipped = false;
    private RaycastHit2D _rayHit;
    
    // Start is called before the first frame update
    void Start()
    {
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

        if (_isEquipped)
        {
            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 direction = mousePosition - transform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            if (Input.GetKeyDown(KeyCode.Mouse0))
            {
                Shoot();
            }
        }
    }

    void Shoot()
    {
        _rayHit = Physics2D.Raycast(shootPoint.position, shootPoint.right, 1000f, whatIsEnemy);
        if (_rayHit)
        {
            TrailRenderer trail = Instantiate(bulletTrail, shootPoint.position, Quaternion.identity);
            StartCoroutine(SpawnTrail(trail, _rayHit));
        }
    }

    private IEnumerator SpawnTrail(TrailRenderer trail, RaycastHit2D rayHit)
    {
        float time = 0;
        Vector3 startPos = trail.transform.position;

        while (time < 1)
        {
            trail.transform.position = Vector3.Lerp(startPos, rayHit.point, time);
            time += Time.deltaTime / trail.time;
            yield return null;
        }

        //FIX PUCANJE U NEBO
        trail.transform.position = rayHit.point;
        //Instantiate(bulletHole, rayHit.point, Quaternion.LookRotation(rayHit.normal));

        Destroy(trail.gameObject, trail.time);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            GetComponent<CircleCollider2D>().enabled = false;
            transform.parent = collision.transform;
            transform.localPosition = new Vector2(-0.25f, -0.05f);
            _isEquipped = true;
        }
    }
}
