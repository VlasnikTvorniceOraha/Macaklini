using System;
using Unity.Netcode;
using UnityEngine;

public class HealthManager : MonoBehaviour
{
    public int MaxHealth = 100;
    public int CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0;

    private GameManager _gameManager;
    
    
    
    public void Start()
    {
        CurrentHealth = MaxHealth;
        _gameManager = FindObjectOfType<GameManager>();
    }
    
    

    public void TakeDamage(int amount, int shooterId)
    {
        Debug.LogFormat("taking {0} damage from client {1}", amount, shooterId);
        if (amount < 0)
        {
            throw new ArgumentException("Damage amount cannot be negative");
        }
        
        CurrentHealth -= amount;
        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            
            if (_gameManager != null)
            {
                _gameManager.AddDeath((int)GetComponent<NetworkObject>().OwnerClientId);
                _gameManager.AddKill(shooterId);
            }
        }
    }
    
    

    public void Heal(int amount)
    {
        if (amount < 0)
        {
            throw new ArgumentException("Heal amount cannot be negative");
        }
        
        CurrentHealth += amount;
        if (CurrentHealth > MaxHealth)
        {
            CurrentHealth = MaxHealth;
        }
    }
    
    

    public void ResetHealth()
    {
        CurrentHealth = MaxHealth;
    }
}