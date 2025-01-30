using System;
using Unity.Netcode;
using UnityEngine;

public class HealthScript : NetworkBehaviour
{
    public int MaxHealth = 100;
    public int CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0;

    private GameManager _gameManager;

    private PlayerController playerController;
    
    
    
    public void Start()
    {
        CurrentHealth = MaxHealth;
        _gameManager = FindObjectOfType<GameManager>();
        playerController = GetComponent<PlayerController>();
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
            Debug.Log("BABABOIIIIIIIIIIIIIIIIII" + IsAlive);
            // this is my ID, I am dead :)
            ulong deadPlayerId = GetComponent<NetworkObject>().OwnerClientId;
            CurrentHealth = 0;
            Debug.LogFormat("i am client {0} and I died", deadPlayerId);
            
            if (_gameManager != null)
            {
                PlayerDeathServerRpc(deadPlayerId, shooterId);
            }
        }
    }

    [Rpc(SendTo.Server)]
    private void PlayerDeathServerRpc(ulong playerID, int shooterId)
    {
        Debug.Log("SMRT FASIZMU");
        int deadPlayerId = (int)playerID;
        // mrežno poručiti protivniku da smo mu slomili koljena i da se vise ne moze kretati
        _gameManager.DisablePlayerMovementRpc(deadPlayerId);
        _gameManager.AddDeath(deadPlayerId);
        _gameManager.AddKill(shooterId);
        //playerController.isAlive.Value = false;
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