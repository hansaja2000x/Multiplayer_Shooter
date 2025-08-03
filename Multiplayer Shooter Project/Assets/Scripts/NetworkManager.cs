using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using KyleDulce.SocketIo;
using Newtonsoft.Json;
using System.Collections;
using UnityEngine.Networking;
using System.IO;
using System.Runtime.InteropServices;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;

    // -------- Inspector references for UI --------
    [SerializeField] private TMP_Text txtRoomDisplay;
    [SerializeField] private TMP_Text txtErrorMessage;
    [SerializeField] private TMP_Text roundDisplay;
    [SerializeField] private TMP_Text roundProgressText; // NEW: Additional text for "Round X/5" display
    [SerializeField] private TMP_Text countdownText; // NEW: For countdown and messages
    [SerializeField] private GameObject countdownPanel; // NEW: Separate panel for countdown UI
    [SerializeField] private GameObject menuUI;
    [SerializeField] private GameObject gameplayUI;
    [SerializeField] private GameObject UICamera;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private RawImage playerProfileImage;
    [SerializeField] private GameEndHandler gameEndHandler;
    [SerializeField] private Animator errorAnimator;
    [SerializeField] private Button shootButton;
    [SerializeField] private VariableJoystick variableJoystick;
    [SerializeField] private GameObject lobbyRobo;
    [SerializeField] private TMP_Text timerText; // NEW: Text for displaying remaining time (e.g., "05:00")
    [SerializeField] private GameObject cinematicCamera; // NEW: Reference to a separate cinematic camera

    [Header("URL Configuration")]
    [SerializeField] private string url = "ws://192.168.1.12:3000"; // Default URL, can be overridden in inspector

    [Header("Prefabs")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private GameObject hitVFXPrefab;
    [SerializeField] private GameObject[] obstaclePrefabs;

    [Header("Character Prefabs")]
    [SerializeField] private List<CharacterPrefabMapping> characterPrefabMappings;

    [System.Serializable]
    private class CharacterPrefabMapping
    {
        public string characterKey;
        public GameObject prefab;
    }

    private Dictionary<string, GameObject> characterPrefabs = new Dictionary<string, GameObject>();

    // -------- Internal state --------
    private Socket socket;
    private string myPlayerId;
    private string currentRoomCode;
    private readonly Dictionary<string, GameObject> players = new();
    private readonly Dictionary<string, string> playerUuids = new();
    private readonly Dictionary<int, GameObject> bullets = new();
    private readonly Dictionary<int, GameObject> movingObstacles = new();
    private readonly Queue<Action> mainThreadCalls = new();
    private readonly Dictionary<string, string> playerNames = new();
    private bool isFirstRound = true;
    public event System.Action<StateUpdateData> OnStateUpdated;

    [Header("Game Settings")]
    [SerializeField] private int totalRounds = 5; // NEW: Configurable total rounds (default 5)
    [SerializeField] private float gameDuration = 300f; // NEW: Game duration in seconds (5 minutes)

    private int expectedRound = 1; // NEW: Track expected next round for detecting restarts
    private Coroutine timerCoroutine; // NEW: Reference to the timer coroutine
    private int currentRemaining;
    private Coroutine timeoutCoroutine; // NEW: Reference to the timeout coroutine

    #region Unity lifecycle
    private void Awake()
    {
        Time.timeScale = 1f;
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var mapping in characterPrefabMappings)
        {
            if (!characterPrefabs.ContainsKey(mapping.characterKey))
            {
                characterPrefabs.Add(mapping.characterKey, mapping.prefab);
            }
        }
        currentRemaining = (int)gameDuration;
    }

    public string GetPlayerUuid(string playerId)
    {
        return playerUuids.TryGetValue(playerId, out string uuid) ? uuid : null;
    }

    // Add this property to NetworkManager to expose myPlayerId
    public string MyPlayerId => myPlayerId;

    // Add a getter method for playerNames
    public string GetPlayerName(string playerId)
    {
        return playerNames.TryGetValue(playerId, out string name) ? name : null;
    }

    private void Start()
    {
        Debug.Log("About to connect socket " + url);
        socket = SocketIo.establishSocketConnection(url);

        RegisterEvents();
        socket.connect();
        Debug.Log("Socket connect() called" + url);

        // Enable UI camera and disable cinematic camera
        UICamera.SetActive(true);
        cinematicCamera.SetActive(false);

        menuUI.SetActive(true);
        gameplayUI.SetActive(false);

        StartCoroutine(WaitForSocketConnection());
        timerCoroutine = StartCoroutine(TimeTicker());
    }

    private IEnumerator TimeTicker()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            if (gameplayUI.activeSelf)
            {
                currentRemaining = Mathf.Max(0, currentRemaining - 1);
                UpdateTimerText();
            }
        }
    }

    private void UpdateTimerText()
    {
        if (timerText != null)
        {
            int minutes = currentRemaining / 60;
            int seconds = currentRemaining % 60;
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    private IEnumerator WaitForSocketConnection()
    {
        while (!socket.connected)
        {
            yield return null;
        }

        Debug.Log("Socket connected!");
        yield return new WaitForSeconds(3f);
        JoinRoom();
    }

    private void Update()
    {
        lock (mainThreadCalls)
            while (mainThreadCalls.Count > 0)
                mainThreadCalls.Dequeue()?.Invoke();
    }

    private void OnDestroy()
    {
        socket?.disconnect();
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }
    }
    #endregion

    #region Socket event registration
    private void RegisterEvents()
    {
        socket.on("open", _ => Debug.Log("Connected"));
        socket.on("disconnect", _ => Queue(() => { Debug.Log("Disconnected"); SendGameOver(); }));
        socket.on("error", e => Debug.LogWarning("Socket error: " + e));
        socket.on("reconnect", _ => Debug.Log("Reconnected"));

        socket.on("mirror", d => Queue(() => Debug.Log("[MIRROR] " + d)));

        socket.on("yourId", d => Queue(() => OnYourId(Parse<YourIdResponse>(d))));
        socket.on("roomJoined", d => Queue(() => OnRoomJoined(Parse<RoomJoinedResponse>(d))));
        socket.on("errorRoom", d => Queue(() => OnErrorRoom(Parse<ErrorRoomResponse>(d))));
        socket.on("waitingForOpponent", d => Queue(() => OnWaitingForOpponent(Parse<WaitingForOpponentResponse>(d))));
        socket.on("init", d => Queue(() => OnInit(Parse<InitData>(d))));
        socket.on("newPlayerConnected", d => Queue(() => OnNewPlayerConnected(Parse<InitData>(d))));
        socket.on("stateUpdate", d => Queue(() => OnStateUpdate(Parse<StateUpdateData>(d))));
        socket.on("bulletRemove", d => Queue(() => OnBulletRemove(Parse<BulletRemoveResponse>(d))));
        socket.on("bulletHitObstacle", d => Queue(() => OnBulletHitObstacle(Parse<BulletHitObstacleResponse>(d))));
        socket.on("playerHit", d => Queue(() => OnPlayerHit(Parse<PlayerHitResponse>(d))));
        socket.on("roundOver", d => Queue(() => OnRoundOver(Parse<RoundOverResponse>(d))));
        socket.on("gameWon", d => Queue(() => OnGameWon(Parse<GameWonResponse>(d))));
        socket.on("playerDisconnected", d => Queue(() => OnPlayerDisconnected(Parse<PlayerDisconnectedResponse>(d))));
        socket.on("playerDropped", d => Queue(() => OnPlayerDropped(Parse<PlayerDisconnectedResponse>(d))));
        socket.on("gameOver", d => Queue(() => { Debug.Log("Received gameOver"); SendGameOver(); }));
        socket.on("roundStart", d => Queue(() => OnRoundStart(Parse<RoundStartData>(d))));
        socket.on("countdown", d => Queue(() => OnCountdown(Parse<CountdownData>(d))));
        socket.on("roundBegin", _ => Queue(() => OnRoundBegin()));
        socket.on("timerSync", d => Queue(() => OnTimerSync(Parse<TimerSyncData>(d))));
    }
    #endregion

    #region Helpers / Emit
    private void Queue(Action a) { lock (mainThreadCalls) mainThreadCalls.Enqueue(a); }

    private static T Parse<T>(string json) =>
        JsonConvert.DeserializeObject<T>(json);

    private void Emit(string evt, object payload = null)
    {
        string json = payload == null ? "" : JsonConvert.SerializeObject(payload);
        socket.emit(evt, json);
    }
    #endregion

    #region Incoming events -> game logic
    private void OnYourId(YourIdResponse d)
    {
        myPlayerId = d.id;
        playerNameText.text = d.name;
        if (!string.IsNullOrEmpty(d.profileImage))
        {
            StartCoroutine(LoadProfileImage(d.profileImage));
        }
        lobbyRobo.SetActive(false);
        gameEndHandler.localPlayerName = d.name;
        UICamera.SetActive(false);
    }

    private IEnumerator LoadProfileImage(string url)
    {
        using (UnityWebRequest uwr = UnityWebRequestTexture.GetTexture(url))
        {
            yield return uwr.SendWebRequest();

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Failed to load profile image: " + uwr.error);
            }
            else
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
                playerProfileImage.texture = texture;
            }
        }
    }

    private void OnRoomJoined(RoomJoinedResponse d)
    {
        currentRoomCode = d.roomCode;
        txtRoomDisplay.text = "Room: " + currentRoomCode;
    }

    private void OnErrorRoom(ErrorRoomResponse d)
    {
        txtErrorMessage.text = d.msg;
        errorAnimator.SetTrigger("Start");
    }

    private void OnWaitingForOpponent(WaitingForOpponentResponse d)
    {
        // Start timeout coroutine
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
        }
        timeoutCoroutine = StartCoroutine(TimeoutCoroutine(d.timeout));
    }

    private IEnumerator TimeoutCoroutine(int timeoutSeconds)
    {
        Debug.Log($"Waiting for opponent, timeout in {timeoutSeconds} seconds");
        yield return new WaitForSecondsRealtime(timeoutSeconds);
        Debug.Log("Timeout occurred, sending game over");
        SendGameOver();
        timeoutCoroutine = null;
    }

    private void OnInit(InitData d)
    {
        // Stop timeout coroutine if running
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }

        menuUI.SetActive(false); // Hide waiting panel when game initializes
        cinematicCamera.SetActive(true); // Ensure cinematic camera is disabled at start
        SpawnPlayer(d.players);
        SyncMovingObstacles(d.movingObstacles);

        SetupCamera();
    }

    private void OnNewPlayerConnected(InitData d)
    {
        // Stop timeout coroutine if running
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }
        if (!cinematicCamera.activeSelf)
            cinematicCamera.SetActive(true);
    
        menuUI.SetActive(false);
        SpawnPlayer(d.players);
        SyncMovingObstacles(d.movingObstacles);

        // Ensure both players are present before starting cinematic
        if (players.Count >= 2 && !countdownPanel.activeSelf)
        {
            StartCoroutine(StartRoundCoroutine());
        }
    }

    private void SetupCamera()
    {
        ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
        if (cam != null && players.ContainsKey(myPlayerId))
        {
            cam.SetTarget(players[myPlayerId].transform);
            cam.SetMode(ThirdPersonCamera.CameraMode.Behind);
        }
    }

    private void OnStateUpdate(StateUpdateData d)
    {
        UpdatePlayers(d.players);
        SyncBullets(d.bullets);
        SyncMovingObstacles(d.movingObstacles);

        // Add debug logging to inspect roundWins
        if (d.roundWins != null)
        {
            foreach (var kvp in d.roundWins)
            {
                Debug.Log($"RoundWins - UUID: {kvp.Key}, Wins: {kvp.Value}");
            }
        }
        else
        {
            Debug.LogWarning("RoundWins data is null in OnStateUpdate");
        }

        OnStateUpdated?.Invoke(d); // Invoke the event
    }

    private void OnBulletRemove(BulletRemoveResponse d)
    {
        if (bullets.TryGetValue(d.bulletId, out var go))
        {
            Destroy(go);
            bullets.Remove(d.bulletId);
        }
    }

    private void OnBulletHitObstacle(BulletHitObstacleResponse d)
    {
        Position p = d.bulletPos;
        Instantiate(hitVFXPrefab, new Vector3(p.x, p.y, p.z), Quaternion.identity);
    }

    private void OnPlayerHit(PlayerHitResponse d)
    {
        if (!players.TryGetValue(d.targetId, out var target)) return;
        if (d.targetId == myPlayerId)
        {
            healthSlider.value = d.newHealth / 100f;
            if (d.newHealth <= 0)
            {
                target.GetComponent<PlayerAnimationHandler>().DeathAnimation();
            }
        }
        else
        {
            players[d.targetId].GetComponent<PlayerCanvasHandler>().SetHealth(d.newHealth / 100f);
            if (d.newHealth <= 0)
            {
                target.GetComponent<PlayerAnimationHandler>().DeathAnimation();
            }
        }

        if (d.newHealth <= 0)
        {
            string killerId = GetOpponentId();
            if (killerId == null) return;
            ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
            if (cam == null) return;
            if (d.targetId == myPlayerId)
            {
                cam.SetTarget(players[killerId].transform);
                cam.SetMode(ThirdPersonCamera.CameraMode.FrontBadass);
            }
            else
            {
                cam.SetTarget(players[myPlayerId].transform);
                cam.SetMode(ThirdPersonCamera.CameraMode.FrontBadass);
            }
        }
    }

    private void OnRoundOver(RoundOverResponse d)
    {
        if (players.TryGetValue(d.loserId, out var loser))
            loser.GetComponent<PlayerAnimationHandler>().DeathAnimation();

        if (players.TryGetValue(myPlayerId, out var me))
            me.GetComponent<PlayerInput>().EndGame(); // disable input during the 5s pause

        // NEW: show "won the round." text and auto-hide after 5s
        gameEndHandler.RoundEnd(d.winnerName);
        Debug.Log("Round winner: " + d.winnerName);

        string winnerId = GetPlayerIdByName(d.winnerName);
        if (winnerId == null) return;
        ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
        if (cam == null) return;
        cam.SetTarget(players[winnerId].transform);
        cam.SetMode(ThirdPersonCamera.CameraMode.FrontBadass);

        // Hide gameplay UI when round finishes to prepare for countdown
        gameplayUI.SetActive(false);
    }

    private void OnGameWon(GameWonResponse d)
    {
        gameEndHandler.GameEnd(d.winnerName);
        Debug.Log("Game winner: " + d.winnerName);

        string winnerId = GetPlayerIdByName(d.winnerName);
        if (winnerId == null) return;
        ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
        if (cam == null) return;
        cam.SetTarget(players[winnerId].transform);
        cam.SetMode(ThirdPersonCamera.CameraMode.FrontBadass);

        // NEW: Stop the timer on game won
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
            currentRemaining = 0;
            UpdateTimerText();
        }
    }

    private void OnPlayerDisconnected(PlayerDisconnectedResponse d)
    {
        if (players.TryGetValue(d.playerId, out var go))
        {
            var canvas = go.GetComponent<PlayerCanvasHandler>();
            players[myPlayerId].GetComponent<CanvasFacingCamera>().RemoveCanvas(canvas.GetCanvasgameObject());
            go.SetActive(false);
            players.Remove(d.playerId);
        }
    }

    private void OnPlayerDropped(PlayerDisconnectedResponse d)
    {
        if (players.TryGetValue(d.playerId, out var go))
        {
            var canvas = go.GetComponent<PlayerCanvasHandler>();
            players[myPlayerId].GetComponent<CanvasFacingCamera>().RemoveCanvas(canvas.GetCanvasgameObject());
            go.SetActive(false);
            players.Remove(d.playerId);
        }
        gameEndHandler.PlayerDropped();
        if (players.TryGetValue(myPlayerId, out var me))
            me.GetComponent<PlayerInput>().EndGame();

        ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
        if (cam != null)
        {
            cam.SetTarget(players[myPlayerId].transform);
            cam.SetMode(ThirdPersonCamera.CameraMode.FrontBadass);
        }

        // NEW: Stop the timer on player dropped
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
            currentRemaining = 0;
            UpdateTimerText();
        }
    }

    private void OnRoundStart(RoundStartData d)
    {
        // Log the current round for debugging
        Debug.Log($"RoundStart: Current Round {d.currentRound}");

        // NEW: Clear all bullets at the start of a new round
        foreach (var bullet in bullets)
        {
            Destroy(bullet.Value);
        }
        bullets.Clear();
        Debug.Log($"Cleared {bullets.Count} bullets at round start");

        // NEW: Detect game restart on tie (currentRound reset to 1 when expected higher)
        if (d.currentRound == 1 && expectedRound > 1)
        {
            Debug.Log("Game restarted due to tie. Resetting timer and round wins.");
            currentRemaining = (int)gameDuration;
            UpdateTimerText();
            isFirstRound = true; // Optional: Replay cinematic on restart
        }

        roundDisplay.text = "Round " + d.currentRound;
        if (roundProgressText != null)
        {
            roundProgressText.text = "Round " + d.currentRound + "/" + totalRounds;
        }

        // NEW: Update expected next round
        expectedRound = d.currentRound + 1;

        StartCoroutine(StartRoundCoroutine());
    }

    private IEnumerator StartRoundCoroutine()
    {
        // Disable input at the start
        if (players.TryGetValue(myPlayerId, out var me))
            me.GetComponent<PlayerInput>().EndGame();

        ThirdPersonCamera cam = Camera.main.GetComponent<ThirdPersonCamera>();
        if (cam != null)
        {
            cam.SetTarget(players[myPlayerId].transform);
            cam.SetMode(ThirdPersonCamera.CameraMode.Behind);
        }

        gameplayUI.SetActive(false);
        countdownPanel.SetActive(true);
        countdownText.text = "";

        if (isFirstRound)
        {
            yield return StartCoroutine(CinematicIntro());
        }
        else
        {
            countdownText.text = "Switching positions";
            yield return new WaitForSeconds(2f);
            countdownText.text = "";
        }
        Emit("readyForCountdown");

        foreach (var playerKV in players)
        {
            playerKV.Value.GetComponent<PlayerAnimationHandler>().Revive();
        }

        isFirstRound = false;
    }

    private IEnumerator CinematicIntro()
    {
        // Disable main gameplay camera and its script
        Camera mainCamera = Camera.main;
        ThirdPersonCamera thirdPersonCam = mainCamera?.GetComponent<ThirdPersonCamera>();
        if (mainCamera != null) mainCamera.enabled = false;
        if (thirdPersonCam != null) thirdPersonCam.enabled = false;

        // Disable UI camera if active
        if (UICamera != null) UICamera.SetActive(false);

        // Enable cinematic camera
        cinematicCamera.SetActive(true);

        yield return new WaitForSeconds(3f); // ⏱️ Duration of cinematic

        cinematicCamera.SetActive(false);
        // Switch back to main camera
        if (mainCamera != null) mainCamera.enabled = true;
        if (thirdPersonCam != null)
        {
            thirdPersonCam.enabled = true;
            thirdPersonCam.SetTarget(players[myPlayerId].transform);
            thirdPersonCam.SetMode(ThirdPersonCamera.CameraMode.Behind);
        }
    }


    private void OnCountdown(CountdownData d)
    {
        countdownText.text = d.text;
    }

    private void OnRoundBegin()
    {
        countdownPanel.SetActive(false);
        gameplayUI.SetActive(true);
        if (players.TryGetValue(myPlayerId, out var meAgain))
        {
            var playerInput = meAgain.GetComponent<PlayerInput>();
            playerInput.BeginRound(); // Re-enable input after countdown
        }

        // NEW: Request a state update from the server to ensure roundWins is refreshed
        Emit("requestStateUpdate");
    }

    private void OnTimerSync(TimerSyncData d)
    {
        currentRemaining = d.remaining;
        UpdateTimerText();
    }
    #endregion

    #region Outgoing events
    public void JoinRoom()
    {
        var urlParams = GetUrlParameters();

        string roomCode = urlParams["gameSessionUuid"];
        string playerUuId = urlParams["uuid"];

        JoinRoomWithParams(roomCode, playerUuId);

    }

    private Dictionary<string, string> GetUrlParameters()
    {
        string url = Application.absoluteURL;
        var result = new Dictionary<string, string>();

        if (string.IsNullOrEmpty(url) || !url.Contains("?"))
            return result;

        string[] parts = url.Split('?');
        if (parts.Length < 2) return result;

        string[] parameters = parts[1].Split('&');
        foreach (string param in parameters)
        {
            string[] keyValue = param.Split('=');
            if (keyValue.Length == 2)
            {
                result[Uri.UnescapeDataString(keyValue[0])] = Uri.UnescapeDataString(keyValue[1]);
            }
        }

        return result;
    }

    public void JoinRoomWithParams(string roomCode, string playerUuId)
    {
        Emit("joinRoom", new { roomCode, uuId = playerUuId });
    }

    public void SendInput(bool f, bool b, bool l, bool r, float rotDelta, float rotVertical)
    {
        var json = JsonConvert.SerializeObject(new
        {
            input = new { forward = f, backward = b, left = l, right = r, rotationDelta = rotDelta, rotationVerticalDelta = rotVertical }
        });
        socket.emit("move", json);
    }

    public void SendShoot() => Emit("shoot");
    #endregion

    #region World sync
    private void SpawnPlayer(Dictionary<string, PlayerData> pdata)
    {
        foreach (KeyValuePair<string, PlayerData> kv in pdata)
        {
            string id = kv.Key;
            PlayerData pd = kv.Value;

            if (!players.ContainsKey(id))
            {
                string characterKey = GetCharacterKeyFromUrl(pd.profileImage);
                GameObject prefab;
                if (characterPrefabs.TryGetValue(characterKey, out var charPrefab))
                {
                    prefab = charPrefab;
                }
                else if (characterPrefabMappings.Count > 0)
                {
                    prefab = characterPrefabMappings[0].prefab;
                }
                else
                {
                    prefab = playerPrefab;
                }
                GameObject go = Instantiate(prefab);
                players[id] = go;
                playerNames[id] = pd.name;
                playerUuids[id] = pd.uuId;

                if (id != myPlayerId)
                {
                    PlayerInput playerInput = go.GetComponent<PlayerInput>();
                    playerInput.DeactivateCameraObject();
                    playerInput.enabled = false;
                    var canvas = go.GetComponent<PlayerCanvasHandler>();
                    canvas.SetHealth(pd.health / 100f);
                    canvas.SetName(pd.name);
                    canvas.SetProfileImage(pd.profileImage);
                    go.GetComponent<CanvasFacingCamera>().enabled = false;
                }
                else
                {
                    go.GetComponent<PlayerCanvasHandler>().DeactivateCanvas();
                }
            }

            GameObject pgo = players[id];
            pgo.transform.position = new Vector3(pd.x, pd.y, pd.z);
            pgo.transform.rotation = Quaternion.Euler(0, pd.rotationY, 0);
            pgo.GetComponent<PlayerAnimationHandler>().SetAnimState(pd.forward, pd.right);
        }

        foreach (KeyValuePair<string, PlayerData> kv in pdata)
        {
            string id = kv.Key;
            PlayerData pd = kv.Value;
            GameObject pgo = players[id];
            if (id != myPlayerId)
            {
                PlayerInput playerInput = pgo.GetComponent<PlayerInput>();
                playerInput.DeactivateCameraObject();
                playerInput.enabled = false;
                pgo.GetComponent<CanvasFacingCamera>().enabled = false;
                var canvas = pgo.GetComponent<PlayerCanvasHandler>();
                players[myPlayerId].GetComponent<CanvasFacingCamera>().AddCanvas(canvas.GetCanvasgameObject());
            }
            else
            {
                PlayerInput playerInput = pgo.GetComponent<PlayerInput>();
                playerInput.SetShootButton(shootButton);
                playerInput.SetVariableJoystick(variableJoystick);
                healthSlider.value = pd.health / 100f;
            }
        }
    }

    private string GetCharacterKeyFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;

        try
        {
            Uri uri = new Uri(url);
            string fileName = Path.GetFileName(uri.AbsolutePath);
            return Path.GetFileNameWithoutExtension(fileName);
        }
        catch
        {
            Debug.LogWarning("Invalid avatar URL: " + url);
            return string.Empty;
        }
    }

    private void UpdatePlayers(Dictionary<string, PlayerData> pdata)
    {
        foreach (KeyValuePair<string, PlayerData> kv in pdata)
        {
            string id = kv.Key;
            PlayerData pd = kv.Value;

            if (!players.ContainsKey(id))
            {
                string characterKey = GetCharacterKeyFromUrl(pd.profileImage);
                GameObject prefab;
                if (characterPrefabs.TryGetValue(characterKey, out var charPrefab))
                {
                    prefab = charPrefab;
                }
                else if (characterPrefabMappings.Count > 0)
                {
                    prefab = characterPrefabMappings[0].prefab;
                }
                else
                {
                    prefab = playerPrefab;
                }
                GameObject go = Instantiate(prefab);
                players[id] = go;
                playerNames[id] = pd.name;
                playerUuids[id] = pd.uuId;

                if (id != myPlayerId)
                {
                    PlayerInput playerInput = go.GetComponent<PlayerInput>();
                    playerInput.DeactivateCameraObject();
                    playerInput.enabled = false;
                    var canvas = go.GetComponent<PlayerCanvasHandler>();
                    canvas.SetHealth(pd.health / 100f);
                    canvas.SetName(pd.name);
                    canvas.SetProfileImage(pd.profileImage);
                    go.GetComponent<CanvasFacingCamera>().enabled = false;
                    players[myPlayerId].GetComponent<CanvasFacingCamera>().AddCanvas(canvas.GetCanvasgameObject());
                }
                else
                {
                    PlayerInput playerInput = go.GetComponent<PlayerInput>();
                    playerInput.SetShootButton(shootButton);
                    playerInput.SetVariableJoystick(variableJoystick);
                    go.GetComponent<PlayerCanvasHandler>().DeactivateCanvas();
                }
            }

            GameObject pgo = players[id];
            if (id != myPlayerId)
            {
                var canvas = pgo.GetComponent<PlayerCanvasHandler>();
                canvas.SetHealth(pd.health / 100f);
            }
            else
            {
                healthSlider.value = pd.health / 100f;
            }

            pgo.transform.position = new Vector3(pd.x, pd.y, pd.z);
            pgo.transform.rotation = Quaternion.Euler(0, pd.rotationY, 0);
            PlayerAnimationHandler playerAnim = pgo.GetComponent<PlayerAnimationHandler>();
            playerAnim.SetVerticleAngle(pd.rotationX);
            playerAnim.SetAnimState(pd.forward, pd.right);
        }
    }

    private void SyncBullets(List<BulletData> bdata)
    {
        HashSet<int> serverIds = new();
        foreach (BulletData b in bdata)
        {
            serverIds.Add(b.id);

            if (!bullets.ContainsKey(b.id))
            {
                GameObject go = Instantiate(bulletPrefab);
                bullets[b.id] = go;

                if (players.ContainsKey(b.ownerId))
                    players[b.ownerId]
                        .GetComponent<PlayerAnimationHandler>()
                        .EnableShootAnimation();
            }

            GameObject bulletGO = bullets[b.id];
            bulletGO.transform.position = new Vector3(b.x, b.y, b.z);
            bulletGO.transform.rotation = Quaternion.Euler(0, b.rotationY, 0);
        }

        List<int> toRemove = new();
        foreach (int id in bullets.Keys)
            if (!serverIds.Contains(id)) toRemove.Add(id);

        foreach (int id in toRemove)
        {
            Destroy(bullets[id]);
            bullets.Remove(id);
        }
    }

    private void SyncMovingObstacles(List<MovingObstacleData> mdata)
    {
        HashSet<int> serverIds = new();
        foreach (MovingObstacleData m in mdata)
        {
            serverIds.Add(m.id);

            if (!movingObstacles.ContainsKey(m.id))
            {
                int prefabIndex = Mathf.Clamp(m.prefabType, 0, obstaclePrefabs.Length - 1);
                GameObject go = Instantiate(obstaclePrefabs[prefabIndex]);
                go.transform.localScale = new Vector3(m.size.x, m.size.y, m.size.z);
                go.transform.rotation = Quaternion.Euler(0, m.rotationY, 0);
                movingObstacles[m.id] = go;
            }

            GameObject obs = movingObstacles[m.id];
            obs.transform.position = new Vector3(m.x, m.y, m.z);
        }

        List<int> toRemove = new();
        foreach (int id in movingObstacles.Keys)
            if (!serverIds.Contains(id)) toRemove.Add(id);

        foreach (int id in toRemove)
        {
            Destroy(movingObstacles[id]);
            movingObstacles.Remove(id);
        }
    }
    #endregion

    public string GetPlayerIdByName(string name)
    {
        foreach (var kv in playerNames)
        {
            if (kv.Value == name) return kv.Key;
        }
        return null;
    }

    public string GetOpponentId()
    {
        foreach (var key in players.Keys)
        {
            if (key != myPlayerId) return key;
        }
        return null;
    }

    #region DTOs
    [Serializable] public class YourIdResponse { public string id; public string name; public string profileImage; }
    [Serializable] public class RoomJoinedResponse { public string roomCode; }
    [Serializable] public class ErrorRoomResponse { public string msg; }
    [Serializable] public class PlayerDisconnectedResponse { public string playerId; }
    [Serializable] public class PlayerHitResponse { public string targetId; public float newHealth; }
    [Serializable] public class RoundOverResponse { public string winnerName; public string loserId; }
    [Serializable] public class GameWonResponse { public string winnerName; }
    [Serializable] public class BulletRemoveResponse { public int bulletId; }
    [Serializable] public class BulletHitObstacleResponse { public Position bulletPos; }
    [Serializable] public class RoundStartData { public int currentRound; }
    [Serializable] public class CountdownData { public string text; }
    [Serializable] public class TimerSyncData { public int remaining; }
    [Serializable] public class WaitingForOpponentResponse { public int timeout; } // NEW

    [Serializable]
    public class PlayerData
    {
        public float x, y, z;
        public float rotationY;
        public float rotationX;
        public float forward, right;
        public float health;
        public string uuId;
        public string name;
        public string profileImage;
    }

    [Serializable]
    public class BulletData
    {
        public int id;
        public string ownerId;
        public float x, y, z;
        public float rotationY;
    }

    [Serializable]
    public class ObstacleData
    {
        public int id;
        public float x, y, z;
        public float rotationY;
        public Size size;
        public int prefabType;
    }

    [Serializable]
    public class MovingObstacleData : ObstacleData { }

    [Serializable] public class Size { public float x, y, z; }

    [Serializable] public class Position { public float x, y, z; }

    [Serializable]
    public class StateUpdateData
    {
        public Dictionary<string, PlayerData> players;
        public List<BulletData> bullets;
        public List<MovingObstacleData> movingObstacles;
        public Dictionary<string, int> roundWins; // NEW: Added to store round win counts for each player (by UUID)
    }
    [Serializable]
    public class InitData
    {
        public Dictionary<string, PlayerData> players;
        public List<ObstacleData> obstacles;
        public List<MovingObstacleData> movingObstacles;
    }
    #endregion

    #region WebGL specific
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SendGameOver();
#else
    private static void SendGameOver() { Debug.Log("SendGameOver called (non-WebGL)"); }
#endif
    #endregion
}