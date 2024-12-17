using System.Collections;
using System.Collections.Generic;
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
    // Start is called before the first frame update
    //lista na serveru za sve igrace
    List<PlayerInfoGame> playerInfosGame = new List<PlayerInfoGame>();

    [SerializeField] List<string> LevelsToLoad = new List<string>();

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

    [SerializeField] RoundState roundState;

    void Start()
    {
        DontDestroyOnLoad(this.gameObject);
        _networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        ui = GameObject.Find("UI").GetComponent<UIManager>();
        scoreboard = ui.transform.Find("Scoreboard").gameObject;
        roundNumber = 0;

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
            UpdateScoreBoard();
        }

        if (Input.GetKeyDown(KeyCode.Tab) && ui.gameStarted.Value)
        {
            ToggleScoreboard();
        }
    }

    void RoundCountdown()
    {
        //ukljuci countdown za runde
    }

    void StartRound()
    {
        //odaberi level, resetaj sve igrace i zapocni starting screen
    }

    void EndRound()
    {

    }

    void CheckForEndOfRound()
    {
        //ako je samo jedan igrac ostao ziv -> zavrsi rundu
    }

    void EndGame(PlayerInfoGame winner)
    {

    }

    void ToggleScoreboard()
    {
        scoreboard.SetActive(!scoreboard.activeSelf);
    }

    void UpdateScoreBoard()
    {
        //za svakog igraca ispuni podatke u scoreboardu
        //ispuni i donji panel sa informacija o levelu i rundi

        Transform playersTransform = scoreboard.transform.Find("Players");

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
        UpdateScoreBoard();
        return;

    }

    void AddDeath(int clientId)
    {
        foreach (PlayerInfoGame player in playerInfosGame)
        {
            if (player.ClientId == clientId)
            {
                player.deaths += 1;
                
            }
        }
        UpdateScoreBoard();
    }

    void AddKill(int clientId)
    {
        foreach (PlayerInfoGame player in playerInfosGame)
        {
            if (player.ClientId == clientId)
            {
                player.kills += 1;
                
            }
        }
        UpdateScoreBoard();
    }


    //prikazi igracima scoreove i koliko treba do pobjede
    void Intermission()
    {

    }

    public void ReceivePlayerInfo(List<PlayerInfoLobby> playerInfos)
    {
        //dobiti ces informacije od lobbya o igracima u igri i treba prekopirati u ovu skriptu

        foreach (PlayerInfoLobby player in playerInfos)
        {
            PlayerInfoGame currentPlayer = new PlayerInfoGame();

            currentPlayer.ClientId = player.ClientId;
            currentPlayer.PlayerName = player.PlayerName;
            currentPlayer.roundsWon = 0;
            currentPlayer.deaths = 0;
            currentPlayer.kills = 0;

            playerInfosGame.Add(currentPlayer);
        }
    }
}
