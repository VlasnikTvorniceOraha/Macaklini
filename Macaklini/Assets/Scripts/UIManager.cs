using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.UIElements;
using Unity.VisualScripting;
using UnityEditor;

public class UIManager : MonoBehaviour
{
    // Start is called before the first frame update
    NetworkManager networkManager;
    UnityTransport unityTransport;

    public GameObject HostJoin;

    public GameObject serverBrowser;

    public GameObject lobbyScreen;

    void Start()
    {
        networkManager = GameObject.Find("NetworkManager").GetComponent<NetworkManager>();
        unityTransport = networkManager.gameObject.GetComponent<UnityTransport>();
        
    }

    // Update is called once per frame
    void Update()
    {

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

        if (networkManager.StartHost())
        {
            //uspjesno pokrenut host
            HostJoin.SetActive(false);
            lobbyScreen.SetActive(true);
            //dodaj playera i takva sranja etc.
        }
        else
        {
            //neuspjeh!
            Error.SetActive(true);
            return;
        }
        
        

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

        if (networkManager.StartClient())
        {
            //uspjesno pokrenut host
            HostJoin.SetActive(false);
            lobbyScreen.SetActive(true);
            //dodaj playera i takva sranja etc.
        }
        else
        {
            //neuspjeh!
            Error.SetActive(true);
            return;
        }
        
    }

    public void ExitButton()
    {
        //izadi iz igre
        EditorApplication.ExitPlaymode();
        Application.Quit();
        
    }
}
