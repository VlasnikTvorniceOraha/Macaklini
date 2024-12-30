using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.SceneManagement;

//Skripta za pracenje i syncanje statea igre, ko je pobjedio runde, kada treba zavrsit i tako
public class GameManager : NetworkBehaviour
{
    private NetworkManager _networkManager;

    private UIManager ui;

    [SerializeField] private GameObject playerPrefab;
    // Start is called before the first frame update
    //lista na serveru za sve igrace
    List<PlayerInfoGame> playerInfosGame = new List<PlayerInfoGame>();

    [SerializeField] private List<string> LevelsToLoad = new List<string>();

    [SerializeField] private int gamesNeededToWin;

    List<PlayerController> playerControllers = new List<PlayerController>();

    private int roundNumber;

    [SerializeField] private GameObject scoreboard;

    List<Transform> playerScorecards = new List<Transform>();

    TMP_Text readyText;

    //moguca stanja runde
    public enum RoundState 
    {
        RoundStarting,
        RoundInProgress,
        RoundEnding,
        GameEnding,
        Intermission
    }

    public RoundState roundState;

    void Start()
    {
        //provjeri je li vec postoji gamemangaer instanca i ako da ubi se
        int instances = FindObjectsOfType<GameManager>().Length;
        if (instances != 1)
        {
            //upucaj se
            Destroy(this.gameObject);
            return;
        }
        DontDestroyOnLoad(this.gameObject);
        _networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        ui = GameObject.Find("UI").GetComponent<UIManager>();
        readyText = ui.transform.Find("ReadyText").GetComponent<TMP_Text>();
        scoreboard = ui.transform.Find("Scoreboard").gameObject;
        roundNumber = 0;
        roundState = RoundState.RoundEnding;

        foreach (Transform playerScorecard in scoreboard.transform.Find("Players"))
        {
            playerScorecards.Add(playerScorecard);
        }

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            playerControllers.Add(player.GetComponent<PlayerController>());
        }

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
        

        if (Input.GetKeyDown(KeyCode.Tab) && ui.gameStarted.Value)
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
        //instanciraj igrace
        InstancePlayers();


        //pocni countdown kada se loada i stavi rundu in progress
        StartRoundClientsRpc();

        
    }

    [Rpc(SendTo.ClientsAndHost)]
    void StartRoundClientsRpc()
    {
        StartCoroutine(ReadyCountdown());
    }
    

    IEnumerator ReadyCountdown()
    {
        //pronadi kojeg igraca posjedujem
        roundState = RoundState.RoundInProgress;
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        Vector3 ownedPlayerPos = Vector3.zero;

        foreach (GameObject player in players)
        {
            if (player.GetComponent<PlayerController>().IsOwner)
            {
                ownedPlayerPos = player.transform.position;
                break;
            }
        }

        //zumiraj kameru na igraca i postavi poziciju na njega
        Camera.main.orthographicSize = 1;
        Camera.main.transform.position = new Vector3(ownedPlayerPos.x, ownedPlayerPos.y, -10);
        readyText.text = "Get Ready!";
        readyText.gameObject.SetActive(true);
        yield return new WaitForSeconds(1.5f);

        Camera.main.orthographicSize = 3;
        readyText.text = "Set!";
        yield return new WaitForSeconds(1.5f);

        Camera.main.orthographicSize = 5;
        Camera.main.transform.position = new Vector3(0, 0, -10);
        readyText.text = "Go!";
        yield return new WaitForSeconds(0.5f);
        readyText.gameObject.SetActive(false);

        

    }

    void EndRoundServer(PlayerInfoGame roundWinner)
    {
        // Server only
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
            //netko je ipak pobijedio
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

        //rollaj random level osim trenutnog
        List<string> levelsToChooseFrom = LevelsToLoad.Where(level => level != currentLevel).ToList();
        Debug.Log(levelsToChooseFrom);

        int index = UnityEngine.Random.Range(0, levelsToChooseFrom.Count);

        LoadSceneRpc(levelsToChooseFrom[index]);
    }

    
    public void CheckForEndOfRound()
    {
        // Server only
        if (!IsServer)
        {
            return;
        }
        //provjeri kraj runde sekundu nakon necije smrti
        //ako je samo jedan igrac ostao ziv -> zavrsi rundu
        //ako nitko nije ziv isto zavrsi rundu
        Debug.Log("Provjeram kraj runde");
        int alive = playerInfosGame.Count;
        List<PlayerInfoGame> alivePlayers = new List<PlayerInfoGame>();

        foreach (PlayerInfoGame player in playerInfosGame)
        {
            if (!player.playerController.isAlive.Value)
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
        }
        else if (alive == 0)
        {
            EndRoundServer(null);
        }

        Debug.Log("Nuh uh, zivo je " + alivePlayers.Count + " igraca!");

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
        ui.LobbyAfterGame();
        SceneManager.LoadScene("MainMenu");
        //natrag u lobby 
        
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
        //za svakog igraca ispuni podatke u scoreboardu
        //ispuni i donji panel sa informacija o levelu i rundi

        foreach (PlayerInfoGame player in playerInfosGame)
        {
            Transform currentPlayerScorecard = playerScorecards[playerInfosGame.IndexOf(player)];

            //Name
            currentPlayerScorecard.Find("Name").GetComponent<TMP_Text>().text = player.PlayerName;
            //RoundsWon
            currentPlayerScorecard.Find("RoundsWon").GetComponent<TMP_Text>().text = player.roundsWon.ToString();
            //Kills
            currentPlayerScorecard.Find("Kills").GetComponent<TMP_Text>().text = player.kills.ToString();
            //Deaths
            currentPlayerScorecard.Find("Deaths").GetComponent<TMP_Text>().text = player.deaths.ToString();
            //ping
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
                player.roundsWon += 1;
                if (player.roundsWon >= gamesNeededToWin)
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
        return;

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
                player.deaths += 1;
                
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
                player.kills += 1;
                
            }
        }
        UpdateScoreBoardRpc(playerInfosGame.ToArray());
    }


    //prikazi igracima scoreove i koliko treba do pobjede
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

        //resetaj playerInfosGame
        playerInfosGame.Clear();

        //dobiti ces informacije od lobbya o igracima u igri i treba prekopirati u ovu skriptu

        foreach (PlayerInfoLobby player in playerInfos)
        {




            //Popunjavanje player info game
            PlayerInfoGame currentPlayer = new PlayerInfoGame();

            currentPlayer.ClientId = player.ClientId;
            currentPlayer.PlayerName = player.PlayerName;
            currentPlayer.PlayerGunster = player.PlayerGunster;
            currentPlayer.roundsWon = 0;
            currentPlayer.deaths = 0;
            currentPlayer.kills = 0;
            
        

            playerInfosGame.Add(currentPlayer);
        }

        //Pocni rundu
        //rollaj random level

        int index = UnityEngine.Random.Range(0, LevelsToLoad.Count);

        LoadSceneRpc(LevelsToLoad[index]);
    }

    void InstancePlayers()
    {
        foreach(PlayerInfoGame currentPlayer in playerInfosGame)
        {
            GameObject playerInstance = Instantiate(playerPrefab);
            playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject((ulong)currentPlayer.ClientId);
            playerInstance.GetComponent<SpriteRenderer>().sprite = ui.GUNsterSpriteovi[currentPlayer.PlayerGunster];
            currentPlayer.playerController = playerInstance.GetComponent<PlayerController>();
        }
    }
}
