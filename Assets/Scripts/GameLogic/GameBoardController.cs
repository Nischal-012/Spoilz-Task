using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Collections.Generic;
using System;
using TMPro;
using System.Collections;

/// <summary>
/// Represents a move in the tic-tac-toe game
/// </summary>
public struct TicTacToeMove
{
    public int Position;
    public int PlayerSymbol;

    public TicTacToeMove(int position, int playerSymbol)
    {
        Position = position;
        PlayerSymbol = playerSymbol;
    }
}

/// <summary>
/// Represents the current state of the game board
/// </summary>
[Serializable]
public class GameState
{
    public int[] BoardCells = new int[9];
    public bool IsXTurn = true;
    public bool GameEnded = false;
    public int WinnerSymbol = 0; // 0 = none, 1 = X, 2 = O

    public void Reset()
    {
        for (int i = 0; i < BoardCells.Length; i++)
        {
            BoardCells[i] = 0;
        }
        IsXTurn = true;
        GameEnded = false;
        WinnerSymbol = 0;
    }

    public bool IsMoveValid(int position)
    {
        return position >= 0 && position < 9 && BoardCells[position] == 0;
    }

    public bool IsBoardFull()
    {
        foreach (int cell in BoardCells)
        {
            if (cell == 0)
                return false;
        }
        return true;
    }
}

/// <summary>
/// Controls the tic-tac-toe game board and handles network synchronization.
/// Implements the MVC pattern to separate game logic from presentation.
/// </summary>
public class GameBoardController : MonoBehaviourPunCallbacks, IOnEventCallback
{
    [Header("UI References")]
    [SerializeField] private Button[] boardButtons;
    [SerializeField] private TMP_Text turnText;
    [SerializeField] private GameObject winPanel;
    [SerializeField] private TMP_Text winText;
    [SerializeField] private Button playAgainButton;
    [SerializeField] private TMP_Text roomCodeText;
    [SerializeField] private TMP_Text player1NameText;
    [SerializeField] private TMP_Text player2NameText;
    [SerializeField] private Image player1Panel;
    [SerializeField] private Image player2Panel;
    [SerializeField] private TMP_Text player1SymbolText;
    [SerializeField] private TMP_Text player2SymbolText;

    [Header("Network Configuration")]
    [SerializeField] private float syncTimeout = 5.0f;

    [Header("Navigation")]
    [SerializeField] private Button returnToMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Reconnection UI")]
    [SerializeField] private TMP_Text reconnectingTMP;
    [SerializeField] private GameObject reconnectionPanel;

    // Network event codes
    private const byte MAKE_MOVE_EVENT = 1;
    private const byte SYNC_BOARD_EVENT = 2;
    private const byte RESTART_GAME_EVENT = 3;
    private const byte RANDOM_FIRST_TURN_EVENT = 4;

    // Game state
    private GameState gameState = new GameState();
    private bool isSyncRequested = false;
    private float syncRequestTime = 0f;
    private bool gameCanStart = false;

    // Reconnection
    private bool isAttemptingReconnect = false;
    private float reconnectTimer = 0f;
    private float reconnectInterval = 5f;
    private int maxReconnectAttempt = 5;
    private int reconnectAttempt = 0;

    // Events
    public event Action<GameState> OnGameStateChanged;
    public event Action<TicTacToeMove> OnMoveMade;

    #region Unity Lifecycle

    void Start()
    {
        ResetBoard();
        UpdateUI();

        // Set up board buttons with their click events
        for (int i = 0; i < boardButtons.Length; i++)
        {
            int index = i;
            boardButtons[i].onClick.AddListener(() => OnBoardButtonClicked(index));
        }

        if (playAgainButton != null)
            playAgainButton.onClick.AddListener(RequestRestartGame);

        if (returnToMenuButton != null)
            returnToMenuButton.onClick.AddListener(ReturnToMainMenu);

        // If rejoining an ongoing game, request current board state
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient)
        {
            RequestBoardState();
        }

        CheckGameCanStart();
    }

    public void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    public void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    void Update()
    {
        // Handle sync timeout: if no board state is received in time, reset locally.
        if (isSyncRequested && Time.time - syncRequestTime > syncTimeout)
        {
            Debug.LogWarning("Board sync request timed out. Using default board state.");
            isSyncRequested = false;
            ResetBoard();
            UpdateUI();
        }

        // Reconnection logic when disconnected
        if (isAttemptingReconnect)
        {
            if (reconnectAttempt <= maxReconnectAttempt)
            {
                reconnectTimer += Time.deltaTime;
                if (reconnectTimer >= reconnectInterval)
                {
                    reconnectTimer = 0f;
                    bool willTryReconnect = PhotonNetwork.ReconnectAndRejoin();
                    Debug.Log("Attempting to reconnect and rejoin: " + willTryReconnect);
                    reconnectAttempt++;
                    if (!willTryReconnect)
                    {
                        isAttemptingReconnect = false;
                        Debug.Log("Reconnection failed. Returning to main menu.");
                        LoadMainMenu();
                    }
                }
            }
            else
            {
                isAttemptingReconnect = false;
                Debug.Log("Reconnection timed out. Returning to main menu.");
                LoadMainMenu();
            }
        }
    }

    // Check if game can start based on the current player count.
    private void CheckGameCanStart()
    {
        gameCanStart = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.PlayerCount >= 2;
        UpdateUI();
    }

    #endregion

    #region Game Logic

    private void ResetBoard()
    {
        gameState.Reset();
        ResetButtonColors();
        UpdateBoardUI();
        UpdateTurnDisplay();
        winPanel.SetActive(false);
    }

    private void ResetButtonColors()
    {
        Color defaultColor = Color.white;
        foreach (Button button in boardButtons)
        {
            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = defaultColor;
            }
        }
    }

    // Makes a move if valid, updates the game state, checks win conditions, and notifies listeners.
    private void MakeMove(int index)
    {
        if (!gameState.IsMoveValid(index) || gameState.GameEnded)
            return;

        int playerSymbol = gameState.IsXTurn ? 1 : 2;
        gameState.BoardCells[index] = playerSymbol;
        TicTacToeMove move = new TicTacToeMove(index, playerSymbol);
        OnMoveMade?.Invoke(move);

        if (CheckWinCondition(playerSymbol))
        {
            gameState.GameEnded = true;
            gameState.WinnerSymbol = playerSymbol;
            HighlightWinningCombination(playerSymbol);
        }
        else if (gameState.IsBoardFull())
        {
            gameState.GameEnded = true;
        }
        else
        {
            gameState.IsXTurn = !gameState.IsXTurn;
        }

        OnGameStateChanged?.Invoke(gameState);
        UpdateUI();
    }

    private bool CheckWinCondition(int playerSymbol)
    {
        int[] cells = gameState.BoardCells;

        // Check rows
        for (int i = 0; i < 3; i++)
        {
            if (cells[i * 3] == playerSymbol &&
                cells[i * 3 + 1] == playerSymbol &&
                cells[i * 3 + 2] == playerSymbol)
                return true;
        }

        // Check columns
        for (int i = 0; i < 3; i++)
        {
            if (cells[i] == playerSymbol &&
                cells[i + 3] == playerSymbol &&
                cells[i + 6] == playerSymbol)
                return true;
        }

        // Check diagonals
        if ((cells[0] == playerSymbol && cells[4] == playerSymbol && cells[8] == playerSymbol) ||
            (cells[2] == playerSymbol && cells[4] == playerSymbol && cells[6] == playerSymbol))
            return true;

        return false;
    }

    // Determines if it's the current player's turn.
    private bool IsMyTurn()
    {
        bool isPlayerX = PhotonNetwork.IsMasterClient;
        return (gameState.IsXTurn && isPlayerX) || (!gameState.IsXTurn && !isPlayerX);
    }

    #endregion

    #region UI Updates

    private void UpdateUI()
    {
        UpdateBoardUI();
        UpdateTurnDisplay();
        UpdateGameEndUI();
        UpdatePlayerPanels();
    }

    private void UpdateBoardUI()
    {
        for (int i = 0; i < gameState.BoardCells.Length; i++)
        {
            TMP_Text buttonText = boardButtons[i].GetComponentInChildren<TMP_Text>();
            if (gameState.BoardCells[i] == 1)
            {
                buttonText.text = "X";
                boardButtons[i].interactable = false;
            }
            else if (gameState.BoardCells[i] == 2)
            {
                buttonText.text = "O";
                boardButtons[i].interactable = false;
            }
            else
            {
                buttonText.text = "";
                boardButtons[i].interactable = gameCanStart && !gameState.GameEnded && IsMyTurn();
            }
        }
    }

    private void UpdateTurnDisplay()
    {
        if (!gameCanStart)
        {
            turnText.text = "Waiting for player...";
            return;
        }
        turnText.text = gameState.GameEnded ? "Game Over" :
            (gameState.IsXTurn ? "Current Turn: X" + (PhotonNetwork.IsMasterClient ? " (You)" : " (Opponent)") :
                                 "Current Turn: O" + (!PhotonNetwork.IsMasterClient ? " (You)" : " (Opponent)"));
    }

    private void UpdateGameEndUI()
    {
        if (gameState.GameEnded)
        {
            winText.text = gameState.WinnerSymbol > 0 ?
                (gameState.WinnerSymbol == 1 ? "X Wins!" : "O Wins!") : "It's a Draw!";
            winPanel.SetActive(true);

            if (!PhotonNetwork.IsMasterClient && playAgainButton != null)
            {
                playAgainButton.interactable = false;
                winText.text += "\nWaiting for host to restart...";
            }
            else if (playAgainButton != null)
            {
                playAgainButton.interactable = true;
            }

            foreach (Button button in boardButtons)
                button.interactable = false;
        }
        else
        {
            winPanel.SetActive(false);
        }
    }

    private void UpdatePlayerPanels()
    {
        if (PhotonNetwork.InRoom)
        {
            roomCodeText.text = "Room Code: " + PhotonNetwork.CurrentRoom.Name;
            Player masterPlayer = PhotonNetwork.MasterClient;
            string masterPlayerName = masterPlayer != null ? masterPlayer.NickName : "Player 1";
            player1NameText.text = "P1: " + masterPlayerName;
            player1SymbolText.text = "X";

            if (PhotonNetwork.CurrentRoom.PlayerCount > 1)
            {
                foreach (Player p in PhotonNetwork.CurrentRoom.Players.Values)
                {
                    if (p.ActorNumber != masterPlayer.ActorNumber)
                    {
                        player2NameText.text = "P2: " + p.NickName;
                        break;
                    }
                }
            }
            else
            {
                player2NameText.text = "P2: Waiting...";
            }
            player2SymbolText.text = "O";

            Color activeColor = new Color(1f, 1f, 0.8f);
            Color inactiveColor = new Color(0.8f, 0.8f, 0.8f);

            if (gameState.GameEnded)
            {
                player1Panel.color = inactiveColor;
                player2Panel.color = inactiveColor;
            }
            else if (gameState.IsXTurn)
            {
                player1Panel.color = activeColor;
                player2Panel.color = inactiveColor;
            }
            else
            {
                player1Panel.color = inactiveColor;
                player2Panel.color = activeColor;
            }
        }
    }

    #endregion

    #region User Input Handlers

    private void OnBoardButtonClicked(int index)
    {
        if (!gameCanStart)
        {
            Debug.Log("Game cannot start yet, waiting for players!");
            return;
        }
        if (!IsMyTurn())
        {
            Debug.Log("Not your turn!");
            return;
        }
        if (!gameState.IsMoveValid(index))
        {
            Debug.Log("Invalid move - space already taken!");
            return;
        }

        // Process move locally and send to other players
        MakeMove(index);
        object[] content = new object[] { index };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(MAKE_MOVE_EVENT, content, raiseEventOptions, SendOptions.SendReliable);
    }

    private void RequestRestartGame()
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.Log("Only the host can restart the game");
            if (winText != null && !winText.text.Contains("Waiting for host"))
                winText.text += "\nWaiting for host to restart...";
            return;
        }

        ResetBoard();
        RandomizeFirstTurn();
        object[] content = new object[] { true };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(RESTART_GAME_EVENT, content, raiseEventOptions, SendOptions.SendReliable);
        Debug.Log("Game restarted by host");
    }

    /// <summary>
    /// Leaves the current game room and returns to the main menu.
    /// </summary>
    public void ReturnToMainMenu()
    {
        if (returnToMenuButton != null)
            returnToMenuButton.interactable = false;

        if (turnText != null)
            turnText.text = "Leaving game...";

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }
        else
        {
            LoadMainMenu();
        }
    }

    // Disconnects from Photon and loads the main menu scene.
    private void LoadMainMenu()
    {
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();
        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
    }

    #endregion

    #region Network Sync

    // Requests the current board state from the master client.
    private void RequestBoardState()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("Cannot request board state: not in a room");
            return;
        }

        object[] content = new object[] { true };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
        PhotonNetwork.RaiseEvent(SYNC_BOARD_EVENT, content, raiseEventOptions, SendOptions.SendReliable);
        isSyncRequested = true;
        syncRequestTime = Time.time;
        turnText.text = "Synchronizing game...";
        Debug.Log("Requested board state from host");
    }

    // Sends the current board state to a specific player.
    private void SendBoardState(Player targetPlayer)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("Only master client can send board state");
            return;
        }

        object[] content = new object[] {
            gameState.BoardCells,
            gameState.IsXTurn,
            gameState.GameEnded,
            gameState.WinnerSymbol
        };

        RaiseEventOptions raiseEventOptions = new RaiseEventOptions
        {
            TargetActors = new int[] { targetPlayer.ActorNumber }
        };

        PhotonNetwork.RaiseEvent(SYNC_BOARD_EVENT, content, raiseEventOptions, SendOptions.SendReliable);
        Debug.Log($"Sent board state to player {targetPlayer.NickName}");
    }

    // Handles Photon events for moves, board sync, game restart, and first turn randomization.
    public void OnEvent(EventData photonEvent)
    {
        byte eventCode = photonEvent.Code;

        if (eventCode == MAKE_MOVE_EVENT)
        {
            object[] data = (object[])photonEvent.CustomData;
            int index = (int)data[0];
            MakeMove(index);
        }
        else if (eventCode == SYNC_BOARD_EVENT)
        {
            object[] data = (object[])photonEvent.CustomData;
            if (data.Length == 1 && PhotonNetwork.IsMasterClient)
            {
                Player requestingPlayer = PhotonNetwork.CurrentRoom.GetPlayer(photonEvent.Sender);
                SendBoardState(requestingPlayer);
            }
            else if (data.Length > 1)
            {
                isSyncRequested = false;
                gameState.BoardCells = (int[])data[0];
                gameState.IsXTurn = (bool)data[1];
                if (data.Length > 2)
                {
                    gameState.GameEnded = (bool)data[2];
                    gameState.WinnerSymbol = (int)data[3];
                    if (gameState.GameEnded && gameState.WinnerSymbol > 0)
                        HighlightWinningCombination(gameState.WinnerSymbol);
                }
                UpdateUI();
                Debug.Log("Received and applied board state from host");
            }
        }
        else if (eventCode == RESTART_GAME_EVENT)
        {
            ResetBoard();
            UpdateUI();
            Debug.Log("Game has been restarted by the host");
        }
        else if (eventCode == RANDOM_FIRST_TURN_EVENT)
        {
            object[] data = (object[])photonEvent.CustomData;
            gameState.IsXTurn = (bool)data[0];
            UpdateUI();
            Debug.Log($"Received random first turn: {(gameState.IsXTurn ? "X" : "O")}");
        }
    }

    #endregion

    #region Photon Callbacks

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"Player {newPlayer.NickName} entered room");
        bool gameStartingNow = !gameCanStart && PhotonNetwork.CurrentRoom.PlayerCount >= 2;
        CheckGameCanStart();

        if (PhotonNetwork.IsMasterClient)
        {
            if (gameStartingNow)
            {
                ResetBoard();
                RandomizeFirstTurn();
            }
            SendBoardState(newPlayer);
        }
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (isAttemptingReconnect)
        {
            isAttemptingReconnect = false;
            Debug.Log($"Failed to rejoin room: {message}");
            LoadMainMenu();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"Player {otherPlayer.NickName} left the room");
        CheckGameCanStart();
    }

    public override void OnLeftRoom()
    {
        Debug.Log("Left the game room");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (cause != DisconnectCause.DisconnectByClientLogic)
        {
            isAttemptingReconnect = true;
            reconnectTimer = 0f;
            reconnectionPanel.SetActive(true);
            reconnectingTMP.text = $"Connection lost. Attempting to reconnect... {reconnectAttempt}/{maxReconnectAttempt}";
            foreach (Button button in boardButtons)
            {
                button.interactable = false;
            }
            if (returnToMenuButton != null)
                returnToMenuButton.interactable = true;
        }
        else
        {
            LoadMainMenu();
        }
    }

    public override void OnJoinedRoom()
    {
        if (isAttemptingReconnect)
        {
            isAttemptingReconnect = false;
            reconnectAttempt = 0;
            Debug.Log("Successfully reconnected and rejoined the room!");
            reconnectionPanel.SetActive(false);
            RequestBoardState();
            if (turnText != null)
                turnText.text = "Reconnected! Synchronizing game...";
        }
    }

    #endregion

    // Highlights the winning combination on the board.
    private void HighlightWinningCombination(int playerSymbol)
    {
        int[] cells = gameState.BoardCells;
        Color winColor = playerSymbol == 1 ? Color.red : Color.blue;

        // Check rows
        for (int i = 0; i < 3; i++)
        {
            if (cells[i * 3] == playerSymbol && cells[i * 3 + 1] == playerSymbol && cells[i * 3 + 2] == playerSymbol)
            {
                HighlightButtons(new int[] { i * 3, i * 3 + 1, i * 3 + 2 }, winColor);
                return;
            }
        }

        // Check columns
        for (int i = 0; i < 3; i++)
        {
            if (cells[i] == playerSymbol && cells[i + 3] == playerSymbol && cells[i + 6] == playerSymbol)
            {
                HighlightButtons(new int[] { i, i + 3, i + 6 }, winColor);
                return;
            }
        }

        // Check diagonals
        if (cells[0] == playerSymbol && cells[4] == playerSymbol && cells[8] == playerSymbol)
        {
            HighlightButtons(new int[] { 0, 4, 8 }, winColor);
            return;
        }
        if (cells[2] == playerSymbol && cells[4] == playerSymbol && cells[6] == playerSymbol)
        {
            HighlightButtons(new int[] { 2, 4, 6 }, winColor);
        }
    }

    private void HighlightButtons(int[] indices, Color color)
    {
        foreach (int index in indices)
        {
            Image buttonImage = boardButtons[index].GetComponent<Image>();
            buttonImage.color = color;
        }
    }

    // Randomly selects which player goes first and synchronizes the decision.
    private void RandomizeFirstTurn()
    {
        if (!PhotonNetwork.IsMasterClient)
            return;

        bool isXTurn = UnityEngine.Random.value > 0.5f;
        gameState.IsXTurn = isXTurn;
        object[] content = new object[] { isXTurn };
        RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(RANDOM_FIRST_TURN_EVENT, content, raiseEventOptions, SendOptions.SendReliable);
        UpdateUI();
        Debug.Log($"First turn randomly set to: {(isXTurn ? "X" : "O")}");
    }
}
