using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;

public class UIManager : NetworkBehaviour
{
    // Start is called before the first frame update
    private NetworkManager _networkManager;
    private UnityTransport _unityTransport;

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
    
    void Start()
    {
        _networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        _unityTransport = _networkManager.gameObject.GetComponent<UnityTransport>();
        _networkManager.OnConnectionEvent += PlayerConnected;
        
        //playersConnected.OnValueChanged = updateReadyNumber;
        playersConnected.OnValueChanged += updateReadyNumber;
    }

    // Update is called once per frame
    void Update()
    {
        // press L for logging
        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.LogFormat("playersConnected.Value: {0}", playersConnected.Value);
            Debug.LogFormat("gameStarted.Value: {0}", gameStarted.Value);
        }
    }
    
    private void PlayerConnected(NetworkManager manager, ConnectionEventData data)
    {
        Debug.LogFormat("data.EventType: {0}", data.EventType);

        if (data.EventType == ConnectionEvent.ClientConnected)
        {
            // ako ima vise od 4, disconnectaj
            if (playersConnected.Value >= 4 || gameStarted.Value)
            {
                Debug.Log("Previse igraca ili kasnis");
                _networkManager.Shutdown();
                return;
            }

            // klijent se spojio, syncaj ga
            HostJoin.SetActive(false);
            lobbyScreen.SetActive(true);
            if (IsServer)
            {
                playersConnected.Value += 1;
            }

            Debug.LogFormat("Player with ClientId {0} connected", data.ClientId);
        
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

    // RPC za syncanje panela na svim klijentima
    [Rpc(SendTo.NotServer)]
    private void SyncPanelsRpc(bool[] ukljuceniPaneli)
    {
        Transform lobbyPlayers = lobbyScreen.transform.Find("Panel").Find("Players");

        for (int i = 0; i < ukljuceniPaneli.Length; i++)
        {
            // panel je ukljucen
            if (ukljuceniPaneli[i])
            {
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

    // zapocni igru i osposobi kretanje svim igracima
    [Rpc(SendTo.ClientsAndHost)]
    private void StartGameRpc()
    {
        lobbyScreen.SetActive(false);
        background.SetActive(false);
    }
    
    // updateaj panel s brojem spremnih igraca
    public void updateReadyNumber(int prevValue, int newValue)
    {
        Debug.Log("Number of players: " + newValue);
        lobbyScreen.transform.Find("Panel").Find("Ready").GetComponent<TMP_Text>().text = newValue + "/4";
    }

    public void HostButton()
    {
        Debug.Log("UIManager::HostButton");
        
        // Procitaj IP i port i napravi server
        Transform hostButton = HostJoin.transform.Find("Host");
        TMP_InputField IPfield = hostButton.Find("IP").GetComponent<TMP_InputField>();
        TMP_InputField portField = hostButton.Find("Port").GetComponent<TMP_InputField>();
        GameObject error = hostButton.Find("Error").gameObject;

        if (!int.TryParse(portField.text, out int port))
        {
            error.SetActive(true);
            return;
        }

        _unityTransport.SetConnectionData(IPfield.text, (ushort)port);
        _networkManager.StartHost();
    }

    public void JoinButton()
    {
        // procitaj IP i port i napravi klijenta -> ako ne postoji server onda izbaci error
        Transform joinButton = HostJoin.transform.Find("Join");
        TMP_InputField IPfield = joinButton.Find("IP").GetComponent<TMP_InputField>();
        TMP_InputField portField = joinButton.Find("Port").GetComponent<TMP_InputField>();
        GameObject error = joinButton.Find("Error").gameObject;

        if (!int.TryParse(portField.text, out int port))
        {
            error.SetActive(true);
            return;
        }

        _unityTransport.SetConnectionData(IPfield.text, (ushort)port);
        _networkManager.StartClient();
    }

    public void StartGameButton()
    {
        if (!IsServer)
        {
            Debug.Log("Only the host can start the game!");
            return;
        }

        gameStarted.Value = true;
        StartGameRpc();
    }

    public void ExitLobby()
    {
        // ugasi lobby i ukljuci lobby creation/join screen
        _networkManager.Shutdown();
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
