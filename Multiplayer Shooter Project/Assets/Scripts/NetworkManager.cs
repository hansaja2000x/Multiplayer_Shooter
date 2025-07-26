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
    [SerializeField] private TMP_InputField inputName;
    [SerializeField] private TMP_InputField inputRoomCode;
    [SerializeField] private TMP_Text txtRoomDisplay;
    [SerializeField] private TMP_Text txtErrorMessage;
    [SerializeField] private TMP_Text roundDisplay;
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

    [Header("Game Type")]
    [SerializeField] private bool isOnPC;

    // -------- Internal state --------
    private Socket socket;
    private string myPlayerId;
    private string currentRoomCode;
    private readonly Dictionary<string, GameObject> players = new();
    private readonly Dictionary<int, GameObject> bullets = new();
    private readonly Dictionary<int, GameObject> movingObstacles = new();
    private readonly Queue<Action> mainThreadCalls = new();

    #region Unity lifecycle
    private void Awake()
    {
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
    }

    private void Start()
    {
        Debug.Log("About to connect socket...");
        socket = SocketIo.establishSocketConnection("ws://localhost:3000");
        //socket = SocketIo.establishSocketConnection("wss://testb.xcodelab.xyz");

        RegisterEvents();
        socket.connect();
        Debug.Log("Socket connect() called");

        menuUI.SetActive(true);
        gameplayUI.SetActive(false);

        StartCoroutine(WaitForSocketConnection());
    }

    private IEnumerator WaitForSocketConnection()
    {
        while (!socket.connected)
        {
            yield return null;
        }

        Debug.Log("Socket connected!");
        JoinRoom();
    }

    private void Update()
    {
        lock (mainThreadCalls)
            while (mainThreadCalls.Count > 0)
                mainThreadCalls.Dequeue()?.Invoke();
    }

    private void OnDestroy() => socket?.disconnect();
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
        menuUI.SetActive(false);
        lobbyRobo.SetActive(false);
        gameplayUI.SetActive(true);
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

    private void OnInit(InitData d)
    {
        SpawnPlayer(d.players);
        SyncMovingObstacles(d.movingObstacles);
    }

    private void OnNewPlayerConnected(InitData d)
    {
        SpawnPlayer(d.players);
        SyncMovingObstacles(d.movingObstacles);
    }

    private void OnStateUpdate(StateUpdateData d)
    {
        UpdatePlayers(d.players);
        SyncBullets(d.bullets);
        SyncMovingObstacles(d.movingObstacles);
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
        }
        else
        {
            players[d.targetId].GetComponent<PlayerCanvasHandler>().SetHealth(d.newHealth / 100f);
        }
    }

    private void OnRoundOver(RoundOverResponse d)
    {
        if (players.TryGetValue(d.loserId, out var loser))
            loser.GetComponent<PlayerAnimationHandler>().DeathAnimation();

        if (players.TryGetValue(myPlayerId, out var me))
            me.GetComponent<PlayerInput>().EndGame();
        players[myPlayerId].GetComponent<PlayerInput>().EndGame();
        Debug.Log("Round winner: " + d.winnerName);
    }

    private void OnGameWon(GameWonResponse d)
    {
        gameEndHandler.GameEnd(d.winnerName);
        Debug.Log("Game winner: " + d.winnerName);
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
    }

    private void OnRoundStart(RoundStartData d)
    {
        roundDisplay.text = "Round " + d.currentRound;
        if (players.TryGetValue(myPlayerId, out var me))
        {
            var playerInput = me.GetComponent<PlayerInput>();
            playerInput.BeginRound(); // Assuming PlayerInput has a BeginRound method to re-enable input
        }
    }
    #endregion

    #region Outgoing events
    public void CreateRoom()
    {
        if (string.IsNullOrWhiteSpace(inputName.text))
        {
            txtErrorMessage.text = "Enter your name!";
            errorAnimator.SetTrigger("Start");
            return;
        }
        Emit("createRoom", new { name = inputName.text });
    }

    public void JoinRoom()
    {
        var urlParams = GetUrlParameters();

        if (urlParams.ContainsKey("gameSessionUuid") && urlParams.ContainsKey("uuid"))
        {
            string roomCode = urlParams["gameSessionUuid"];
            string playerUuId = urlParams["uuid"];

            JoinRoomWithParams(roomCode, playerUuId);
        }
        else
        {
            Debug.Log("No URL parameters found — falling back to manual join.");
        }
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

    public void SendInput(bool f, bool b, bool l, bool r, float rotDelta)
    {
        var json = JsonConvert.SerializeObject(new
        {
            input = new { forward = f, backward = b, left = l, right = r, rotationDelta = rotDelta }
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
            if (id != myPlayerId)
            {
                PlayerInput playerInput = pgo.GetComponent<PlayerInput>();
                playerInput.DeactivateCameraObject();
                playerInput.enabled = false;
                pgo.GetComponent<CanvasFacingCamera>().enabled = false;
                var canvas = pgo.GetComponent<PlayerCanvasHandler>();
                canvas.SetHealth(pd.health / 100f);
                canvas.SetName(pd.name);
                canvas.SetProfileImage(pd.profileImage);
            }
            else
            {
                healthSlider.value = pd.health / 100f;
                PlayerInput playerInput = pgo.GetComponent<PlayerInput>();
                playerInput.SetShootButton(shootButton);
                playerInput.SetVariableJoystick(variableJoystick);
                playerInput.SetIsOnPC(isOnPC);
            }

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
                canvas.SetHealth(pd.health / 100f);
                canvas.SetName(pd.name);
                canvas.SetProfileImage(pd.profileImage);
            }
            else
            {
                PlayerInput playerInput = pgo.GetComponent<PlayerInput>();
                playerInput.SetShootButton(shootButton);
                playerInput.SetVariableJoystick(variableJoystick);
                playerInput.SetIsOnPC(isOnPC);
                pgo.GetComponent<PlayerCanvasHandler>().DeactivateCanvas();
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
                    playerInput.SetIsOnPC(isOnPC);
                    go.GetComponent<PlayerCanvasHandler>().DeactivateCanvas();
                }
            }

            GameObject pgo = players[id];
            if (id != myPlayerId)
            {
                var canvas = pgo.GetComponent<PlayerCanvasHandler>();
                canvas.SetHealth(pd.health / 100f);
                canvas.SetName(pd.name);
                canvas.SetProfileImage(pd.profileImage);
            }
            else
            {
                healthSlider.value = pd.health / 100f;
            }

            pgo.transform.position = new Vector3(pd.x, pd.y, pd.z);
            pgo.transform.rotation = Quaternion.Euler(0, pd.rotationY, 0);
            pgo.GetComponent<PlayerAnimationHandler>().SetAnimState(pd.forward, pd.right);
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

    [Serializable]
    public class PlayerData
    {
        public float x, y, z;
        public float rotationY;
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