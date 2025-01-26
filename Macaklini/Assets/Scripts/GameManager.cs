using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// skripta za pracenje i syncanje statea igre, ko je pobjedio runde, kada treba zavrsit i tako
public class GameManager : NetworkBehaviour
{
    // moguca stanja runde
    public enum RoundState 
    {
        RoundStarting,
        RoundInProgress,
        RoundEnding,
        GameEnding,
        Intermission
    }
    
    public RoundState roundState;
    
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private List<string> LevelsToLoad = new List<string>();
    [SerializeField] private int gamesNeededToWin;
    [SerializeField] private GameObject scoreboard;
    
    private NetworkManager _networkManager;
    private UIManager _uiManager;
    private List<PlayerInfoGame> playerInfosGame = new List<PlayerInfoGame>(); // lista na serveru za sve igrace
    private int roundNumber;
    private List<Transform> playerScorecards = new List<Transform>();
    private TMP_Text readyText;

    private GameObject[] _weaponSpawnPoints;
    [SerializeField] private GameObject[] weaponPrefabs;
    
    // Start is called before the first frame update
    void Start()
    {
        // ako vec postoji GameManager instanca, unisti ovu
        int instances = FindObjectsOfType<GameManager>().Length;
        if (instances > 1) // promijenjeno u > umjesto != jer kaj ak se istovremeno spawnaju 2 instance
        {
            Destroy(gameObject);
            return;
        }
        
        // inital GameManager setup
        DontDestroyOnLoad(gameObject);
        _networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        _uiManager = GameObject.Find("UI").GetComponent<UIManager>();
        readyText = _uiManager.transform.Find("ReadyText").GetComponent<TMP_Text>();
        scoreboard = _uiManager.transform.Find("Scoreboard").gameObject;
        roundNumber = 0;
        roundState = RoundState.RoundEnding;

        foreach (Transform playerScorecard in scoreboard.transform.Find("Players"))
        {
            playerScorecards.Add(playerScorecard);
        }

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        SceneManager.sceneLoaded += StartRoundServer;
    }

    
    
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P) && IsServer)
        {
            UpdateScoreBoardRpc(playerInfosGame.ToArray());
        }

        if (Input.GetKeyDown(KeyCode.L) && IsServer)
        {
            
        }
        
        if (Input.GetKeyDown(KeyCode.K) && IsServer)
        {
            CheckForEndOfRound();
        }
        

        if (Input.GetKeyDown(KeyCode.Tab) && _uiManager.gameStarted.Value)
        {
            ToggleScoreboard();
        }
    }
    
    

    void StartRoundServer(Scene scena, LoadSceneMode loadMode)
    {
        // Server only
        if (!IsServer || roundState != RoundState.RoundEnding)
        {
            return;
        }
        
        Debug.Log("Runda pocinje");
        roundState = RoundState.RoundStarting;
        InstancePlayers();
        InstanceWeapons();

        
        
        // pocni countdown kada se loada i stavi rundu in progress
        StartRoundClientsRpc();
    }

    
    
    [Rpc(SendTo.ClientsAndHost)]
    void StartRoundClientsRpc()
    {
        StartCoroutine(ReadyCountdown());
    }
    
    
    
    IEnumerator ReadyCountdown()
    {
        // pronadi kojeg igraca posjedujem
        roundState = RoundState.RoundInProgress;
        
        Vector3 ownedPlayerPos = Vector3.zero;
        PlayerController ownedController = null;

        foreach (PlayerInfoGame player in playerInfosGame)
        {
            if (player.PlayerController.IsOwner)
            {
                ownedController = player.PlayerController;
                ownedController.canMove = false;
                ownedPlayerPos = ownedController.transform.position;
                break;
            }
        }

        // zumiraj kameru na igraca i postavi poziciju na njega
        Camera mainCamera = Camera.main; // this is done to avoid a small CPU overhead at every call of Camera.main
        if (mainCamera)
        {
            mainCamera.orthographicSize = 1;
            mainCamera.transform.position = new Vector3(ownedPlayerPos.x, ownedPlayerPos.y, -10);
        }

        readyText.text = "Get Ready!";
        readyText.gameObject.SetActive(true);
        yield return new WaitForSeconds(1.5f);

        if (mainCamera)
        {
            mainCamera.orthographicSize = 3;
        }
        readyText.text = "Set!";
        yield return new WaitForSeconds(1.5f);

        if (mainCamera)
        {
            mainCamera.orthographicSize = 5;
            mainCamera.transform.position = new Vector3(0, 0, -10);
        }
        readyText.text = "Go!";
        yield return new WaitForSeconds(0.5f);
        readyText.gameObject.SetActive(false);
        ownedController.canMove = true;

    }

    
    
    public void CheckForEndOfRound()
    {
        // Server only
        if (!IsServer)
        {
            return;
        }
        // provjeri kraj runde sekundu nakon necije smrti
        // ako je samo jedan igrac ostao ziv -> zavrsi rundu
        // ako nitko nije ziv isto zavrsi rundu
        Debug.Log("Provjeram kraj runde");
        int alive = playerInfosGame.Count;
        List<PlayerInfoGame> alivePlayers = new List<PlayerInfoGame>();

        foreach (PlayerInfoGame player in playerInfosGame)
        {
            if (!player.PlayerController.isAlive.Value)
            {
                alive -= 1;
            }
            else 
            {
                alivePlayers.Add(player);
            }
        }

        if (alive == 1)
        {
            EndRoundServer(alivePlayers[0]);
            return;
        }
        else if (alive == 0)
        {
            EndRoundServer(null);
            return;
        }

        Debug.Log("Nuh uh, zivo je " + alivePlayers.Count + " igraca!");

    }
    
    
    
    void EndRoundServer(PlayerInfoGame roundWinner)
    {
        // Server only method
        if (!IsServer)
        {
            return;
        }

        Debug.Log("Runda zavrsava");
        roundState = RoundState.RoundEnding;

        if (roundWinner == null)
        {
            EndRoundClientsRpc();
        }
        else
        {
            // netko je ipak pobijedio
            WinRound(roundWinner.ClientId);
        }
    }

    
    
    [Rpc(SendTo.ClientsAndHost)]
    void EndRoundClientsRpc()
    {
        StartCoroutine(EndRoundCoroutine());
    }
    
    
    
    IEnumerator EndRoundCoroutine()
    {
        roundState = RoundState.RoundEnding;
        yield return new WaitForSeconds(5f);
        string currentLevel = SceneManager.GetActiveScene().name;

        // rollaj random level osim trenutnog
        List<string> levelsToChooseFrom = LevelsToLoad.Where(level => level != currentLevel).ToList();
        Debug.Log(levelsToChooseFrom);
        int index = Random.Range(0, levelsToChooseFrom.Count);
        LoadSceneRpc(levelsToChooseFrom[index]);
    }

    
    
    void EndGame(PlayerInfoGame winner)
    {
        if (!IsServer)
        {
            return;
        }
        
        Debug.Log("Igra zavrsava");
        roundState = RoundState.GameEnding;
        EndGameClientsRpc();
    }
    
    

    [Rpc(SendTo.ClientsAndHost)]
    void EndGameClientsRpc()
    {
        roundState = RoundState.GameEnding;
        // natrag u lobby
        _uiManager.LobbyAfterGame();
        SceneManager.LoadScene("MainMenu");
    }
    
    

    void ToggleScoreboard()
    {
        scoreboard.SetActive(!scoreboard.activeSelf);
    }

    
    
    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateScoreBoardRpc(PlayerInfoGame[] playerInfoGames)
    {
        UpdateScoreBoard(playerInfoGames.ToList());
    }
    
    

    [Rpc(SendTo.ClientsAndHost)]
    private void LoadSceneRpc(string sceneName)
    {
        SceneManager.LoadSceneAsync(sceneName);
    }

    
    
    void UpdateScoreBoard(List<PlayerInfoGame> playerInfoGames)
    {
        // za svakog igraca ispuni podatke u scoreboardu
        // ispuni i donji panel sa informacija o levelu i rundi

        foreach (PlayerInfoGame player in playerInfosGame)
        {
            Transform currentPlayerScorecard = playerScorecards[playerInfosGame.IndexOf(player)];

            // Name
            currentPlayerScorecard.Find("Name").GetComponent<TMP_Text>().text = player.PlayerName;
            // RoundsWon
            currentPlayerScorecard.Find("RoundsWon").GetComponent<TMP_Text>().text = player.RoundsWon.ToString();
            // Kills
            currentPlayerScorecard.Find("Kills").GetComponent<TMP_Text>().text = player.Kills.ToString();
            // Deaths
            currentPlayerScorecard.Find("Deaths").GetComponent<TMP_Text>().text = player.Deaths.ToString();
            // ping
            currentPlayerScorecard.Find("Ping").GetComponent<TMP_Text>().text = _networkManager.NetworkConfig.NetworkTransport.GetCurrentRtt((ulong)player.ClientId).ToString() + " ms";
            
        }

        Transform donjiPanel = scoreboard.transform.Find("Bottom").Find("Panel");

        donjiPanel.Find("Level").GetComponent<TMP_Text>().text = "Level: " + SceneManager.GetActiveScene().name;
        donjiPanel.Find("RoundNumber").GetComponent<TMP_Text>().text = "Round number: " + roundNumber.ToString();
    }



    void WinRound(int winnerClientId)
    {
        // Server only
        if (!IsServer)
        {
            return;
        }


        foreach (PlayerInfoGame player in playerInfosGame)
        {
            if (player.ClientId == winnerClientId)
            {
                player.RoundsWon += 1;
                if (player.RoundsWon >= gamesNeededToWin)
                {
                    roundState = RoundState.GameEnding;
                    EndGame(player);
                    return;
                }
            }
        }

        roundState = RoundState.RoundEnding;
        UpdateScoreBoardRpc(playerInfosGame.ToArray());
        EndRoundClientsRpc();
    }

    void AddDeath(int clientId)
    {
        // Server only
        if (!IsServer)
        {
            return;
        }

        foreach (PlayerInfoGame player in playerInfosGame)
        {
            if (player.ClientId == clientId)
            {
                player.Deaths += 1;
                
            }
        }
        StartCoroutine(AfterDeathCheck());
        UpdateScoreBoardRpc(playerInfosGame.ToArray());
    }

    IEnumerator AfterDeathCheck()
    {
        yield return new WaitForSeconds(1.0f);

        if (roundState == RoundState.RoundInProgress)
        {
            CheckForEndOfRound();
        }

        
    }

    
    
    void AddKill(int clientId)
    {
        // Server only
        if (!IsServer)
        {
            return;
        }

        foreach (PlayerInfoGame player in playerInfosGame)
        {
            if (player.ClientId == clientId)
            {
                player.Kills += 1;
                
            }
        }
        UpdateScoreBoardRpc(playerInfosGame.ToArray());
    }


    
    // prikazi igracima scoreove i koliko treba do pobjede
    void Intermission()
    {

    }

    
    
    public void ReceivePlayerInfo(List<PlayerInfoLobby> playerInfos)
    {
        // Server only
        if (!IsServer)
        {
            return;
        }

        // reset playerInfosGame
        playerInfosGame.Clear();

        // dobiti ces informacije od lobbya o igracima u igri i treba prekopirati u ovu skriptu

        foreach (PlayerInfoLobby player in playerInfos)
        {
            // popunjavanje player info game
            PlayerInfoGame currentPlayer = new PlayerInfoGame();
            currentPlayer.ClientId = player.ClientId;
            currentPlayer.PlayerName = player.PlayerName;
            currentPlayer.PlayerGunster = player.PlayerGunster;
            currentPlayer.RoundsWon = 0;
            currentPlayer.Deaths = 0;
            currentPlayer.Kills = 0;
            playerInfosGame.Add(currentPlayer);
        }

        // pocni rundu
        // rollaj random level
        int index = Random.Range(0, LevelsToLoad.Count);
        LoadSceneRpc(LevelsToLoad[index]);
    }

    
    
    void InstancePlayers()
    {
        foreach(PlayerInfoGame currentPlayer in playerInfosGame)
        {
            GameObject playerInstance = Instantiate(playerPrefab);
            playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject((ulong)currentPlayer.ClientId);
            playerInstance.GetComponent<SpriteRenderer>().sprite = _uiManager.GUNsterSpriteovi[currentPlayer.PlayerGunster];
            currentPlayer.PlayerController = playerInstance.GetComponent<PlayerController>();
            
        }
    }

    void InstanceWeapons()
    {
        _weaponSpawnPoints = GameObject.FindGameObjectsWithTag("WeaponSpawnPoint");
        Debug.Log($"{_weaponSpawnPoints.Length} spawn points found");
        foreach (GameObject weaponSpawnPoint in _weaponSpawnPoints)
        {
            int index = Random.Range(0, 2);
            Debug.Log($"{weaponSpawnPoint.name}{weaponSpawnPoint.transform.position}; random index: {index}");
            GameObject weapon = Instantiate(weaponPrefabs[index], weaponSpawnPoint.transform.position, Quaternion.identity);
            weapon.GetComponent<NetworkObject>().Spawn();

        }
    }
}
