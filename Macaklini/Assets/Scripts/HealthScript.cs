using System;
using Unity.Netcode;
using UnityEngine;

public class HealthScript : MonoBehaviour
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
        if (amount < 0)
        {
            throw new ArgumentException("Damage amount cannot be negative");
        }
        CurrentHealth -= amount;
        if (CurrentHealth <= 0)
        {
            // this is my ID, I am dead :)
            int deadPlayerId = (int)GetComponent<NetworkObject>().OwnerClientId;
            CurrentHealth = 0;
            Debug.LogFormat("i am client {0} and I died", deadPlayerId);
            
            if (_gameManager != null)
            {
                // mrežno poručiti protivniku da smo mu slomili koljena i da se vise ne moze kretati
                _gameManager.DisablePlayerMovementRpc(deadPlayerId);
                _gameManager.AddDeath(deadPlayerId);
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