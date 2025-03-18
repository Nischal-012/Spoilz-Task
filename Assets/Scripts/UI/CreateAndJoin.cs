using UnityEngine;
using Photon.Pun;
using TMPro;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using UnityEngine.UI;
public class CreateAndJoin : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField joinRoomInput;
    [SerializeField] private Button joinButton;

    public int maxPlayers = 10;

    private RoomOptions roomOptions;
    private Hashtable playerProperties;


    void Awake()
    {
        joinButton.interactable = false;
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();

        }
        else if (PhotonNetwork.IsConnected && !PhotonNetwork.InLobby)
        {
            // If we're already connected (e.g. after reconnection) but not in lobby
            PhotonNetwork.JoinLobby();
        }

        playerNameInput.text = PlayerPrefs.HasKey("PlayerName")
            ? PlayerPrefs.GetString("PlayerName")
            : $"Guest{Random.Range(1000, 9999)}";

        joinRoomInput.text = "testRoom";

        roomOptions = new RoomOptions();
        roomOptions.CleanupCacheOnLeave = false;
        roomOptions.MaxPlayers = maxPlayers;
        roomOptions.PlayerTtl = -1;
        roomOptions.EmptyRoomTtl = 3000;

        playerProperties = new Hashtable();

    }

    public override void OnConnectedToMaster()
    {
        joinButton.interactable = true;
    }

    public override void OnJoinedLobby()
    {
        base.OnJoinedLobby();

        Debug.Log("Successfully joined lobby");
    }

    public void OnJoinButton()
    {
        if (!PhotonNetwork.IsConnected)
        {
            Debug.LogWarning("Not connected to Master Server. Please wait...");

            return;
        }

        PlayerPrefs.SetString("PlayerName", playerNameInput.text);

        PhotonNetwork.LocalPlayer.NickName = playerNameInput.text;
        PhotonNetwork.LocalPlayer.SetCustomProperties(playerProperties);

        PhotonNetwork.JoinOrCreateRoom(joinRoomInput.text, roomOptions, default);
    }

    public override void OnJoinedRoom()
    {
        PhotonNetwork.LoadLevel("GameScene");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        base.OnJoinRoomFailed(returnCode, message);
        joinButton.interactable = true;
        Debug.LogError("Join room failed: " + returnCode + " :: " + message);

    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        joinButton.interactable = true;
        Debug.Log($"Disconnected: {cause}");

    }

}