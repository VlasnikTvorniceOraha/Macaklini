using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine.UI;
using Unity.Services.Authentication;

public class UIManager : NetworkBehaviour
{
    [SerializeField] public List<Sprite> GUNsterSpriteovi = new List<Sprite>();
    
    public NetworkVariable<bool> gameStarted = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    [SerializeField] private GameObject HostJoin; // UI screen for server creation/joining
    [SerializeField] private GameObject lobbyScreen; // UI screen that shows up after entering a lobby
    [SerializeField] private GameObject PlayerPanel; // player info that shows up when a player enters a lobby
    [SerializeField] private GameObject background; // UI background color
    [SerializeField] private GameObject userInfo; // UserInfo panel
    [SerializeField] private NetworkVariable<int> playersConnected = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkManager _networkManager;
    private UnityTransport _unityTransport;
    private List<PlayerInfoLobby> playerInfos = new List<PlayerInfoLobby>(); // lista imena i gunstera za svakog igraca u lobbyu, spremljena na serveru 
    private PlayerInfoLobby localPlayerInfo = new PlayerInfoLobby(); // play info klijenta

    GameManager _gameManager;
    
    private bool receivedRpc = false;
    private bool receivedPlayerInfo = false;
    private bool pickedGunster = false;
    
    
    
    // Start is called before the first frame update
    void Start()
    {
        int instances = FindObjectsOfType<UIManager>().Length;
        if (instances > 1) // promijenjeno u > umjesto != jer kaj ak se istovremeno spawnaju 2 instance
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(this.gameObject);

        _networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        _unityTransport = _networkManager.gameObject.GetComponent<UnityTransport>();
        _networkManager.OnConnectionEvent += PlayerConnected;
        
        _gameManager = GameObject.Find("GameManager").GetComponent<GameManager>();
        
        // callback da se poveca broj u lobbyu
        playersConnected.OnValueChanged += UpdateReadyNumber;
    }
    
    

    // Update is called once per frame
    void Update()
    {
        // press L for logging
        if (Input.GetKeyDown(KeyCode.L))
        {
            Debug.LogFormat("playersConnected.Value: {0}", playersConnected.Value);
            Debug.LogFormat("gameStarted.Value: {0}", gameStarted.Value);
            Debug.LogFormat("playerinfos.count: {0}", playerInfos.Count);
            
            foreach (PlayerInfoLobby player in playerInfos)
            {
                Debug.Log("Id: " + player.ClientId + ", name: " + player.PlayerName + ", gunster: " + player.PlayerGunster);
            }
        }
    }
    
    

    private void PlayerConnected(NetworkManager manager, ConnectionEventData data)
    {
        Debug.LogFormat("data.EventType: {0}", data.EventType);
        
        if (IsServer)
        {
            StartCoroutine(PlayerConnectedServerCoroutine(manager, data));
        }
        if (IsClient)
        {
            StartCoroutine(PlayerConnectedClientCoroutine(manager, data));
        }
        
    }
    
    

    // funkcija samo za server jer host zajebava
    private IEnumerator PlayerConnectedServerCoroutine(NetworkManager manager, ConnectionEventData data)
    {
        if (data.EventType == ConnectionEvent.ClientConnected)
        {
            Debug.LogFormat("Player with ClientId {0} connected", data.ClientId);
         
            //server treba povecati playersConnected value, poslati igracu listu spriteova u lobbyu te syncati panele
            playersConnected.Value += 1;
            Debug.Log("Server kod");

            // posalji listu spriteova
            GetPlayerInfoRpc(playerInfos.ToArray(), RpcTarget.Single(data.ClientId, RpcTargetUse.Temp));

            // sada cekamo da player postavi sav svoj info i onda syncamo panele svim korisnicima kada primimo info
            yield return new WaitUntil(() => receivedPlayerInfo);
            receivedPlayerInfo = false;
            Debug.Log("Syncanje panela");
            // retardirano syncanje panela, popraviti
            // syncaj panele na klijentskoj strani
            SyncPanelsRpc(playerInfos.ToArray());
        }
        else if (data.EventType == ConnectionEvent.ClientDisconnected)
        {
            // treba izbaciti klijenta iz liste server infoa i syncati panele ponovno AKO su u lobiju
            // klijent se disconnectao, smanji broj spojenih i tako
            
            playersConnected.Value -= 1;

            PlayerInfoLobby playerToRemove = new PlayerInfoLobby();
            foreach (PlayerInfoLobby playerInfo in playerInfos)
            {
                if (playerInfo.ClientId == (int)data.ClientId)
                {
                    playerToRemove = playerInfo;
                }
            }

            playerInfos.Remove(playerToRemove);

            SyncPanelsRpc(playerInfos.ToArray());
        }
    }
    
    
    
    private IEnumerator PlayerConnectedClientCoroutine(NetworkManager manager, ConnectionEventData data)
    {
        
        if (data.EventType == ConnectionEvent.ClientConnected)
        {
            //ako si igrac koji se spojio treba otvoriti screen za biranje imena i gunstera i poslati to serveru
            if (_networkManager.LocalClientId == data.ClientId)
            {
                // ako ima vise od 4, disconnectaj
                if (playersConnected.Value >= 4 || gameStarted.Value)
                {
                    Debug.Log("Previse igraca ili kasnis");
                    _networkManager.Shutdown();
                    yield break;
                }
                
                // klijent se spojio, syncaj ga
                HostJoin.SetActive(false);
                
                // pricekaj dok se bool ne promijeni sto znaci da je klijent sigurno dobio poruku
                yield return new WaitUntil(() => receivedRpc);
                receivedRpc = false;
                
                // dohvati listu odabranih spriteova od igraca u lobbyu
                Debug.Log("Klijent je dobio spriteove");
                
                // korisnik mora odabrati ime i gunstera
                userInfo.SetActive(true);
                localPlayerInfo.ClientId = (int)data.ClientId;
                
                // iskljuci gunstere koji su vec u lobbyu
                foreach (PlayerInfoLobby playerInfo in playerInfos)
                {
                    int gunsterId = playerInfo.PlayerGunster;
                    userInfo.transform.Find("CharSelect").Find("Gunster" + gunsterId).GetComponent<Button>().interactable = false;
                }

                // cekaj dok ne klikne gunstera
                yield return new WaitUntil(() => pickedGunster);
                pickedGunster = false;
                Debug.Log("Klijent odabrao gunstera i ime");
                userInfo.SetActive(false);
                lobbyScreen.SetActive(true);
                Transform playerPanelList = lobbyScreen.transform.Find("Panel").Find("Players");
                foreach (Transform panel in playerPanelList)
                {
                    panel.transform.Find("winner").gameObject.SetActive(false);
                }
                
                // sad imamo ispunjeni playerinfo, posalji serveru i klijent je gotov
                SendPlayerInfoRpc(localPlayerInfo);
            }
            else if (IsClient)
            {
                // kod za ostale klijente ako je potreban, ne bi trebao biti
                Debug.Log("Javlja se klijent " + _networkManager.LocalClientId);
            }

        }
        else if (data.EventType == ConnectionEvent.ClientDisconnected)
        {
            if (_networkManager.LocalClientId == data.ClientId)
            {
                //resetaj na main menu il nes idk neki handling logic
            }
        }
    }
    
    

    // RPC za syncanje panela na svim klijentima
    [Rpc(SendTo.ClientsAndHost)]
    private void SyncPanelsRpc(PlayerInfoLobby[] serverInfo, int lastRoundWinnerId = -1)
    {
        Transform playerPanelList = lobbyScreen.transform.Find("Panel").Find("Players");
        foreach (Transform panel in playerPanelList)
        {
            panel.gameObject.SetActive(false);
        }
        
        // server ce poslati info i svaki klijent i host ce si postaviti panele
        int brojac = 0;
        foreach (PlayerInfoLobby playerInfo in serverInfo)
        {
            // paneli se uvijek aktiviraju po redu ovisno o kako je server spremio info
            GameObject panel = playerPanelList.GetChild(brojac).gameObject;
            brojac++;
            panel.SetActive(true);
            panel.transform.Find("Ime").GetComponent<TMP_Text>().text = playerInfo.PlayerName;
            panel.transform.Find("Status").GetComponent<TMP_Text>().text = "Connected with id: " + playerInfo.ClientId;
            panel.transform.Find("Slika").GetComponent<Image>().sprite = GUNsterSpriteovi[playerInfo.PlayerGunster];
            if (playerInfo.ClientId == lastRoundWinnerId)
            {
                panel.transform.Find("winner").gameObject.SetActive(true);
            }

        }

    }
    
    

    // zapocni igru i osposobi kretanje svim igracima
    [Rpc(SendTo.ClientsAndHost)]
    private void StartGameRpc()
    {
        lobbyScreen.SetActive(false);
        background.SetActive(false);
        _gameManager.roundState = GameManager.RoundState.RoundEnding;
    }
    

    
    //spremi informacije u listu na serveru
    [Rpc(SendTo.Server)]
    private void SendPlayerInfoRpc(PlayerInfoLobby playerInfo)
    {
        playerInfos.Add(playerInfo);
        receivedPlayerInfo = true;
        
        
    }
    
    
    
    //dohvati serverove informacije o igracima
    [Rpc(SendTo.SpecifiedInParams)]
    private void GetPlayerInfoRpc(PlayerInfoLobby[] playerInfoSent ,RpcParams rpcParams = default)
    {
        playerInfos = playerInfoSent.ToList();
        receivedRpc = true;
    }
    
    
    
    // updateaj panel s brojem spremnih igraca
    public void UpdateReadyNumber(int prevValue, int newValue)
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
        _gameManager.ReceivePlayerInfo(playerInfos);
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
        //EditorApplication.ExitPlaymode();
        Application.Quit();
    }

    
    
    // User info i PlayerPrefs



    public void ToggleCharacterSelect()
    {
        GameObject charSelect = userInfo.transform.Find("CharSelect").gameObject;
        charSelect.SetActive(!charSelect.activeSelf);
    }
    
    

    public void SelectGunsterAndName(int gunster)
    {
        
        localPlayerInfo.PlayerName = userInfo.transform.Find("Name").GetComponent<TMP_InputField>().text;
        if (localPlayerInfo.PlayerName == "")
        {
            localPlayerInfo.PlayerName = "Gunster" + localPlayerInfo.ClientId;
        }
        localPlayerInfo.PlayerGunster = gunster;
        pickedGunster = true;
    }

    public void LobbyAfterGame(PlayerInfoGame winner)
    {
        //mozda neka kruna za pobjednika?
        Transform playerPanelList = lobbyScreen.transform.Find("Panel").Find("Players");
        foreach (Transform panel in playerPanelList)
        {
            panel.transform.Find("winner").gameObject.SetActive(false);
        }
        
        //igraci su vec odigrali rundu i vraca ih se na mainmenu scenu tj u lobby
        userInfo.SetActive(false);
        HostJoin.SetActive(false);
        lobbyScreen.SetActive(true);
        background.SetActive(true);
        gameStarted.Value = false;
        SyncPanelsRpc(playerInfos.ToArray(), winner.ClientId);

    }
}
