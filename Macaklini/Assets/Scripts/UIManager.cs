using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.UIElements;
using Unity.VisualScripting;
using UnityEditor;
using System;
using System.Linq;
using DefaultNamespace;

public class UIManager : NetworkBehaviour
{
    NetworkManager networkManager;
    UnityTransport unityTransport;

    // UI screen for server creation/joining
    public GameObject HostJoin;
    // idk
    public GameObject serverBrowser;
    // UI screen that shows up after entering a lobby
    public GameObject lobbyScreen;
    // player info that shows up when a player enters a lobby
    public GameObject PlayerPanel;
    // UI background color
    public GameObject background;
    
    private NetworkVariable<int> playersConnected = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    // Start is called before the first frame update
    void Start()
    {
        networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        unityTransport = networkManager.gameObject.GetComponent<UnityTransport>();
        networkManager.OnConnectionEvent += PlayerConnected;
        
        //playersConnected.OnValueChanged = updateReadyNumber;

        playersConnected.OnValueChanged += UpdateReadyNumber;
        

        
    }


    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            // Log
            Debug.LogFormat("playersConnected: {0}", playersConnected.Value);
            Debug.LogFormat("gameStarted value: {0}", gameStarted.Value);
        }
    }

    private void PlayerConnected(NetworkManager manager, ConnectionEventData data)
    {
        Debug.LogFormat("EventType: {0}", data.EventType);

        if (data.EventType == ConnectionEvent.ClientConnected)
        {
            // ako ima vise od 4, disconnectaj
            if (playersConnected.Value >= 4 || gameStarted.Value)
            {
                Debug.LogFormat("Previse igraca ili kasnis");
                networkManager.Shutdown();
                return;
            }

            // klijent se spojio, syncaj ga
            HostJoin.SetActive(false);
            lobbyScreen.SetActive(true);
            if (IsServer)
            {
                playersConnected.Value += 1;
            }

            Debug.LogFormat("Player connected: {0}", data.ClientId);
        
            Transform playerPanelList = lobbyScreen.transform.Find("Panel").Find("Players");
            bool[] ukljuceniPaneli = new bool[4] {false, false, false, false};
            int brojac = 0;
            foreach (Transform playerPanel in playerPanelList)
            {
                if (!playerPanel.gameObject.activeSelf)
                {
                    // ukljuci i ispuni tekstom i imenom i slikom
                    playerPanel.gameObject.SetActive(true);
                    playerPanel.Find("Status").GetComponent<TMP_Text>().text = "Connected";
                    // nesto sa slikom ali to kasnije
                    ukljuceniPaneli[brojac] = true;
                    break;
                }
                else
                {
                    ukljuceniPaneli[brojac] = true;
                }
                brojac += 1;
            }
            // syncaj panele na klijentskoj strani
            SyncPanelsRpc(ukljuceniPaneli);


        }
        else if (data.EventType == ConnectionEvent.ClientDisconnected)
        {
            if (playersConnected.Value >= 4 || gameStarted.Value)
            {
                // netko zajebava
                return;
            }

            // klijent se disconnectao, smanji broj spojenih i tako
            if (IsServer)
            {
                playersConnected.Value -= 1;
            }
            // trebalo bi spremiti ideve i to ali neda mi se tako da samo ugasi zadnji ukljuceni panel
            Transform playerPanelList = lobbyScreen.transform.Find("Panel").Find("Players");
            bool[] ukljuceniPaneli = new bool[4] {false, false, false, false};
            int brojac = 0;
            foreach (Transform playerPanel in playerPanelList)
            {
                if (!playerPanel.gameObject.activeSelf)
                {
                    // ugasi prethodni
                    playerPanelList.GetChild(brojac - 1).gameObject.SetActive(false);
                    ukljuceniPaneli[brojac - 1] = false;
                    break;
                }
                else if (playerPanel.gameObject.activeSelf && brojac == 3)
                {
                    // svi su upaljeni, ugasi ovaj
                    playerPanel.gameObject.SetActive(false);
                    ukljuceniPaneli[brojac] = false;
                }
                else
                {
                   ukljuceniPaneli[brojac] = true; 
                }
                brojac += 1;
            }

            SyncPanelsRpc(ukljuceniPaneli);

        }



    }

    [Rpc(SendTo.NotServer)]
    private void SyncPanelsRpc(bool[] ukljuceniPaneli)
    {
        // RPC za syncanje panela na svim klijentima
        Transform lobbyPlayers = lobbyScreen.transform.Find("Panel").Find("Players");

        for (int i = 0; i < ukljuceniPaneli.Length; i++)
        {
            if (ukljuceniPaneli[i])
            {
                // panel je ukljucen
                lobbyPlayers.GetChild(i).gameObject.SetActive(true);
                lobbyPlayers.GetChild(i).Find("Status").GetComponent<TMP_Text>().text = "Connected";
            }
            else
            {
                // panel nije ukljucen
                lobbyPlayers.GetChild(i).gameObject.SetActive(false);
            }
        }

        // todo: imena kasnije isto ig
        
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void StartGameRpc()
    {
        // zapocni igru i osposobi kretanje svim igracima
        lobbyScreen.SetActive(false);
        background.SetActive(false);
    }
    

    public void UpdateReadyNumber(int prevValue, int newValue)
    {
        Debug.LogFormat("Number of players: {0}", newValue);
        // updateaj ready text
        lobbyScreen.transform.Find("Panel").Find("Ready").GetComponent<TMP_Text>().text = newValue + "/4";
    }


    public void HostButton()
    {
        Debug.Log("Dugme host");
        // Procitaj IP i port i napravi server
        Transform HostButton = HostJoin.transform.Find("Host");
        TMP_InputField IPfield = HostButton.Find("IP").GetComponent<TMP_InputField>();
        TMP_InputField Portfield = HostButton.Find("Port").GetComponent<TMP_InputField>();
        GameObject Error = HostButton.Find("Error").gameObject;
        
        int port;
        if (!int.TryParse(Portfield.text, out port))
        {
            Error.SetActive(true);
            return;
        }

        unityTransport.SetConnectionData(IPfield.text, (ushort)port);
        networkManager.StartHost();
    }

    public void JoinButton()
    {
        // procitaj IP i port i napravi klijenta -> ako ne postoji server onda izbaci error
        // Procitaj IP i port i napravi server
        Transform JoinButton = HostJoin.transform.Find("Join");
        TMP_InputField IPfield = JoinButton.Find("IP").GetComponent<TMP_InputField>();
        TMP_InputField Portfield = JoinButton.Find("Port").GetComponent<TMP_InputField>();
        GameObject Error = JoinButton.Find("Error").gameObject;

        int port;

        if (!int.TryParse(Portfield.text, out port))
        {
            Error.SetActive(true);
            return;
        }

        unityTransport.SetConnectionData(IPfield.text, (ushort)port);
        networkManager.StartClient();
    }

    public void StartGameButton()
    {
        if (!IsServer)
        {
            Debug.Log("Nemoj zajebavati");
            return;
        }
        
        // set spawn locations for each of the players
        for (int i = 0; i < networkManager.ConnectedClients.Keys.Count(); i++)
        {
            ulong playerKey = networkManager.ConnectedClients.Keys.ElementAt(i);
            NetworkClient player = networkManager.ConnectedClients[playerKey];
            player.PlayerObject.transform.position = SpawnLocations.spawnLocations[player.ClientId];
            Rigidbody2D rb = player.PlayerObject.GetComponent<Rigidbody2D>();
            if (!rb)
            {
                Debug.LogFormat("nije naso rb2D, nekaj ne valja");
            }
            else
            {
                rb.bodyType = RigidbodyType2D.Dynamic;
            }
        }

        gameStarted.Value = true;
        StartGameRpc();
    }

    

    public void ExitLobby()
    {
        networkManager.Shutdown();
        HostJoin.SetActive(true);
        lobbyScreen.SetActive(false);
    }
    
    public void ExitButton()
    {
        // izadi iz igre
        EditorApplication.ExitPlaymode();
        Application.Quit();
    }
}
