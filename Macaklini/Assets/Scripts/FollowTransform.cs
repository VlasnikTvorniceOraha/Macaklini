using UnityEngine;
using Unity.Netcode;

public class FollowTransform : NetworkBehaviour
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

        if (IsOwner)
        {
            UpdateWeaponPositionServerRpc(transform.position);
        }
    }

    [Rpc(SendTo.Server)]
    private void UpdateWeaponPositionServerRpc(Vector3 newPosition)
    {
        if (!IsServer) return;
        UpdateWeaponPositionClientRpc(newPosition);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateWeaponPositionClientRpc(Vector3 newPosition)
    {
        if (IsOwner) return;
        transform.position = newPosition;
    }

}
