using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.UIElements;
using Unity.VisualScripting;
using UnityEditor;

public class UIManager : NetworkBehaviour
{
    // Start is called before the first frame update
    NetworkManager networkManager;
    UnityTransport unityTransport;

    public GameObject HostJoin;

    public GameObject serverBrowser;

    public GameObject lobbyScreen;

    public GameObject PlayerPanel;
    NetworkVariable<int> playersConnected = new NetworkVariable<int>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    

    void Start()
    {
        networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        unityTransport = networkManager.gameObject.GetComponent<UnityTransport>();
        networkManager.OnClientConnectedCallback += PlayerConnected;
        playersConnected.OnValueChanged += updateReadyNumber;
        

        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            //Log
            Debug.Log(playersConnected.Value);
        }
    }

    private void PlayerConnected(ulong obj)
    {
        
        //ako ima vise od 4, disconnectaj
        if (playersConnected.Value >= 4)
        {
            Debug.Log("Previse igraca");
            networkManager.Shutdown();
        }

        //igrac se joinao, instanciraj objekt u lobiju i igraca na mapi

        HostJoin.SetActive(false);
        lobbyScreen.SetActive(true);
        if (IsOwner)
        {
            playersConnected.Value += 1;
        }
        Debug.Log("Player connected: " + obj);
        
        Transform playerPanelList = lobbyScreen.transform.Find("Panel").Find("Players");
        foreach (Transform playerPanel in playerPanelList)
        {
            if (!playerPanel.gameObject.activeSelf)
            {
                //ukljuci i ispuni tekstom i imenom i slikom
                playerPanel.gameObject.SetActive(true);
                playerPanel.Find("Status").GetComponent<TMP_Text>().text = "Connected";
                playerPanel.Find("Ime").GetComponent<TMP_Text>().text = "Gunster" + obj;
                //nesto sa slikom ali to kasnije
                break;
            }
        }

        
        



    }


    

    public void updateReadyNumber(int prevValue, int newValue)
    {
        Debug.Log("Number of players: " + playersConnected.Value);
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

    
    
    public void ExitButton()
    {
        //izadi iz igre
        EditorApplication.ExitPlaymode();
        Application.Quit();
        
    }
}
