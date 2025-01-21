using UnityEngine;

public class FollowTransform : MonoBehaviour
{
    [SerializeField] private WeaponScriptableObject weaponConfig;
    private Transform targetTransform;
    private Vector3 offset;

    public void SetTargetTransform(Transform targetTransform)
    {
        this.targetTransform = targetTransform;
        offset = weaponConfig != null
            ? new Vector3(weaponConfig.HoldPoint.x, weaponConfig.HoldPoint.y, 0)
            : Vector3.zero;
    }

    private void LateUpdate()
    {
        if (targetTransform == null) return;

        transform.position = targetTransform.position + offset;
    }
}
