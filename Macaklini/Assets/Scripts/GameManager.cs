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

    //moguca stanja runde
    private enum RoundState 
    {
        RoundStarting,
        RoundInProgress,
        RoundEnding,
        GameEnding,
        Intermission
    }

    [SerializeField] private RoundState roundState;

    void Start()
    {
        DontDestroyOnLoad(this.gameObject);
        _networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        ui = GameObject.Find("UI").GetComponent<UIManager>();
        scoreboard = ui.transform.Find("Scoreboard").gameObject;
        roundNumber = 0;
        roundState = RoundState.RoundStarting;

        foreach (Transform playerScorecard in scoreboard.transform.Find("Players"))
        {
            playerScorecards.Add(playerScorecard);
        }

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            playerControllers.Add(player.GetComponent<PlayerController>());
        }
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
            CheckForEndOfRound();
        }

        if (Input.GetKeyDown(KeyCode.K) && IsServer)
        {
            StartRound();
        }

        if (Input.GetKeyDown(KeyCode.Tab) && ui.gameStarted.Value)
        {
            ToggleScoreboard();
        }
    }

    void RoundCountdown()
    {
        // Server only
        if (!IsServer)
        {
            return;
        }
        
        //ukljuci countdown za runde
    }

    void StartRound()
    {
        // Server only
        if (!IsServer)
        {
            return;
        }
        Debug.Log("Runda pocinje");
        roundState = RoundState.RoundStarting;
        //odaberi level, resetaj sve igrace i zapocni starting screen
        string currentLevel = SceneManager.GetActiveScene().name;

        //rollaj random level osim trenutnog
        List<string> levelsToChooseFrom = LevelsToLoad.Where(level => level != currentLevel).ToList();
        Debug.Log(levelsToChooseFrom);

        int index = UnityEngine.Random.Range(0, levelsToChooseFrom.Count);

        //ovo postaviti svim klijentima
        SceneManager.LoadSceneAsync(levelsToChooseFrom[index]);

        roundState = RoundState.RoundInProgress;
    }

    void EndRound(PlayerInfoGame roundWinner)
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
            //Svi mrtvi
        }
        else
        {
            //netko je ipak pobijedio
        }

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
        Debug.Log("Provjerama kraj runde");
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
            EndRound(alivePlayers[0]);
        }
        else if (alive == 0)
        {
            EndRound(null);
        }

    }

    void EndGame(PlayerInfoGame winner)
    {
        if (!IsServer)
        {
            return;
        }
        Debug.Log("Igra zavrsava");
        roundState = RoundState.GameEnding;

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

        //dobiti ces informacije od lobbya o igracima u igri i treba prekopirati u ovu skriptu

        foreach (PlayerInfoLobby player in playerInfos)
        {
            //OVDJE INSTANCIRATI IGRACA

            GameObject playerInstance = Instantiate(playerPrefab);
            playerInstance.GetComponent<NetworkObject>().SpawnAsPlayerObject((ulong)player.ClientId);
            playerInstance.GetComponent<SpriteRenderer>().sprite = ui.GUNsterSpriteovi[player.PlayerGunster];



            //Popunjavanje player info game
            PlayerInfoGame currentPlayer = new PlayerInfoGame();

            currentPlayer.ClientId = player.ClientId;
            currentPlayer.PlayerName = player.PlayerName;
            currentPlayer.roundsWon = 0;
            currentPlayer.deaths = 0;
            currentPlayer.kills = 0;
            currentPlayer.playerController = playerInstance.GetComponent<PlayerController>();
        

            playerInfosGame.Add(currentPlayer);
        }
    }
}
