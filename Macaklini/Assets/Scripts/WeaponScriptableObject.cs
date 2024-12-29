using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Weapon", menuName = "Weapons/Weapon", order = 1)]
public class WeaponScriptableObject : ScriptableObject
{
    [Header("Weapon Specs")]
    public WeaponType WeaponType;
    public int ClipSize;
    public float FireRate;
    public Vector3 Spread;

    [Header("Weapon Model")]
    public Vector3 HoldPoint;       // Local coordinates of the Weapon when it is picked up
    public Vector3 ShootPoint;

    [Header("Bullet Specs")]
    public float ShootingRange;
    public float BulletSpeed;
    public float Damage;

}
