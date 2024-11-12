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

public class UIManager : NetworkBehaviour
{
    // Start is called before the first frame update
    NetworkManager networkManager;
    UnityTransport unityTransport;

    public GameObject HostJoin;

    public GameObject serverBrowser;

    public GameObject lobbyScreen;

    public GameObject PlayerPanel;
    public GameObject background;
    private NetworkVariable<int> playersConnected = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    

    

    void Start()
    {
        networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        unityTransport = networkManager.gameObject.GetComponent<UnityTransport>();
        networkManager.OnConnectionEvent += PlayerConnected;
        
        //playersConnected.OnValueChanged = updateReadyNumber;

        playersConnected.OnValueChanged += updateReadyNumber;
        

        
    }


    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            //Log
            Debug.Log(playersConnected.Value);
            Debug.Log(gameStarted.Value);
        }
    }

    

    private void PlayerConnected(NetworkManager manager, ConnectionEventData data)
    {
        Debug.Log(data.EventType);

        if (data.EventType == ConnectionEvent.ClientConnected)
        {
            //ako ima vise od 4, disconnectaj
            if (playersConnected.Value >= 4 || gameStarted.Value)
            {
                Debug.Log("Previse igraca ili kasnis");
                networkManager.Shutdown();
                return;
            }

            //klijent se spojio, syncaj ga
            HostJoin.SetActive(false);
            lobbyScreen.SetActive(true);
            if (IsServer)
            {
                playersConnected.Value += 1;
            }

            Debug.Log("Player connected: " + data.ClientId);
        
            Transform playerPanelList = lobbyScreen.transform.Find("Panel").Find("Players");
            bool[] ukljuceniPaneli = new bool[4] {false, false, false, false};
            int brojac = 0;
            foreach (Transform playerPanel in playerPanelList)
            {
                if (!playerPanel.gameObject.activeSelf)
                {
                    //ukljuci i ispuni tekstom i imenom i slikom
                    playerPanel.gameObject.SetActive(true);
                    playerPanel.Find("Status").GetComponent<TMP_Text>().text = "Connected";
                    //nesto sa slikom ali to kasnije
                    ukljuceniPaneli[brojac] = true;
                    break;
                }
                else
                {
                    ukljuceniPaneli[brojac] = true;
                }
                brojac += 1;
            }
            //syncaj panele na klijentskoj strani
            SyncPanelsRpc(ukljuceniPaneli);


        }
        else if (data.EventType == ConnectionEvent.ClientDisconnected)
        {
            if (playersConnected.Value >= 4 || gameStarted.Value)
            {
                //netko zajebava
                return;
            }

            //klijent se disconnectao, smanji broj spojenih i tako
            if (IsServer)
            {
                playersConnected.Value -= 1;
            }
            //trebalo bi spremiti ideve i to ali neda mi se tako da samo ugasi zadnji ukljuceni panel
            Transform playerPanelList = lobbyScreen.transform.Find("Panel").Find("Players");
            bool[] ukljuceniPaneli = new bool[4] {false, false, false, false};
            int brojac = 0;
            foreach (Transform playerPanel in playerPanelList)
            {
                if (!playerPanel.gameObject.activeSelf)
                {
                    //ugasi prethodni
                    playerPanelList.GetChild(brojac - 1).gameObject.SetActive(false);
                    ukljuceniPaneli[brojac - 1] = false;
                    break;
                }
                else if (playerPanel.gameObject.activeSelf && brojac == 3)
                {
                    //svi su upaljeni, ugasi ovaj
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
        //RPC za syncanje panela na svim klijentima
        Transform lobbyPlayers = lobbyScreen.transform.Find("Panel").Find("Players");

        for (int i = 0; i < ukljuceniPaneli.Length; i++)
        {
            //panel je ukljucen
            if (ukljuceniPaneli[i])
            {
                lobbyPlayers.GetChild(i).gameObject.SetActive(true);
                lobbyPlayers.GetChild(i).Find("Status").GetComponent<TMP_Text>().text = "Connected";
            }
            else
            {
                //panel nije ukljucen
                lobbyPlayers.GetChild(i).gameObject.SetActive(false);
            }
        }

        //imena kasnije isto ig
        
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void StartGameRpc()
    {
        //zapocni igru i osposobi kretanje svim igracima
        lobbyScreen.SetActive(false);
        background.SetActive(false);
    }
    

    public void updateReadyNumber(int prevValue, int newValue)
    {
        Debug.Log("Number of players: " + newValue);
        //updateaj ready text
        lobbyScreen.transform.Find("Panel").Find("Ready").GetComponent<TMP_Text>().text = newValue + "/4";
    }


    public void HostButton()
    {
        Debug.Log("Dugme host");
        //Procitaj IP i port i napravi server
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
        //procitaj IP i port i napravi klijenta -> ako ne postoji server onda izbaci error
        //Procitaj IP i port i napravi server
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
        //izadi iz igre
        EditorApplication.ExitPlaymode();
        Application.Quit();
        
    }
}
