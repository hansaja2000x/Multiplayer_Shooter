using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using KyleDulce.SocketIo;   
using Newtonsoft.Json;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance;

    // -------- Inspector references --------
    public TMP_InputField inputName;
    public TMP_InputField inputRoomCode;
    public TMP_Text txtRoomDisplay;
    public TMP_Text txtErrorMessage;
    public GameObject menuUI;
    public GameObject gameplayUI;
    public GameObject UICamera;
    public Slider healthSlider;
    public TextMeshProUGUI playerNameText;
    public GameEndHandler gameEndHandler;
    public Animator errorAnimator;

    [Header("Prefabs")]
    public GameObject playerPrefab;
    public GameObject bulletPrefab;
    public GameObject hitVFXPrefab;

    // -------- Internal state --------
    private Socket socket;
    private string myPlayerId;
    private string currentRoomCode;
    private readonly Dictionary<string, GameObject> players = new();
    private readonly Dictionary<int, GameObject> bullets = new();
    private readonly Queue<Action> mainThreadCalls = new();

    // -------------------------------------------------------------------------
    #region Unity lifecycle
    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Debug.Log("About to connect socket...");
        socket = SocketIo.establishSocketConnection("ws://localhost:3000");

        RegisterEvents();
        socket.connect();  
        Debug.Log("Socket connect() called");

        menuUI.SetActive(true);
        gameplayUI.SetActive(false);
    }

    private void Update()
    {
        lock (mainThreadCalls)
            while (mainThreadCalls.Count > 0)
                mainThreadCalls.Dequeue()?.Invoke();
    }

    private void OnDestroy() => socket?.disconnect();
    #endregion
    // -------------------------------------------------------------------------
    #region Socket event registration
    private void RegisterEvents()
    {
        // Lifecycle events
        socket.on("open", _ => Debug.Log("Connected"));
        socket.on("close", _ => Debug.Log("Disconnected"));
        socket.on("error", e => Debug.LogWarning("Socket error: " + e));
        socket.on("reconnect", _ => Debug.Log("Reconnected"));

        socket.on("mirror", d => Queue(() => Debug.Log("[MIRROR] " + d)));

        // Game messages
        socket.on("yourId", d => Queue(() => OnYourId(Parse<YourIdResponse>(d))));
        socket.on("roomJoined", d => Queue(() => OnRoomJoined(Parse<RoomJoinedResponse>(d))));
        socket.on("errorRoom", d => Queue(() => OnErrorRoom(Parse<ErrorRoomResponse>(d))));
        socket.on("init", d => Queue(() => OnInitPlayers(Parse<PlayersWrapper>(d))));
        socket.on("newPlayerConnected", d => Queue(() => OnNewPlayers(Parse<PlayersWrapper>(d))));
        socket.on("stateUpdate", d => Queue(() => OnStateUpdate(Parse<StateUpdateData>(d))));
        socket.on("bulletRemove", d => Queue(() => OnBulletRemove(Parse<BulletRemoveResponse>(d))));
        socket.on("bulletHitObstacle", d => Queue(() => OnBulletHitObstacle(Parse<BulletHitObstacleResponse>(d))));
        socket.on("playerHit", d => Queue(() => OnPlayerHit(Parse<PlayerHitResponse>(d))));
        socket.on("playerWon", d => Queue(() => OnPlayerWon(Parse<PlayerWonResponse>(d))));
        socket.on("playerDisconnected", d => Queue(() => OnPlayerDisconnected(Parse<PlayerDisconnectedResponse>(d))));
    }
    #endregion

    // -------------------------------------------------------------------------
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

    // -------------------------------------------------------------------------
    #region Incoming events -> game logic
    private void OnYourId(YourIdResponse d)
    {
        myPlayerId = d.id;
        playerNameText.text = d.name;
        menuUI.SetActive(false);
        gameplayUI.SetActive(true);
        UICamera.SetActive(false);
    }

    private void OnRoomJoined(RoomJoinedResponse d)
    {
        currentRoomCode = d.roomCode;
        txtRoomDisplay.text = "Room: " + currentRoomCode;
    }

    private void OnErrorRoom(ErrorRoomResponse d) {
        txtErrorMessage.text = d.msg;
        errorAnimator.SetTrigger("Start");
    }

    private void OnInitPlayers(PlayersWrapper d) => SpawnPlayer(d.players);
    private void OnNewPlayers(PlayersWrapper d) => SpawnPlayer(d.players);
    private void OnStateUpdate(StateUpdateData d)
    {
        UpdatePlayers(d.players);
        SyncBullets(d.bullets);
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
        if (d.targetId == myPlayerId) { 
            healthSlider.value = d.newHealth / 100f; 
        }
        else
        {
            players[d.targetId].GetComponent<PlayerCanvasHandler>().SetHealth(d.newHealth / 100f);
        }
    }

    private void OnPlayerWon(PlayerWonResponse d)
    {
        if (players.TryGetValue(d.loserId, out var loser))
            loser.GetComponent<PlayerAnimationHandler>().DeathAnimation();

        if (players.TryGetValue(myPlayerId, out var me))
            me.GetComponent<PlayerInput>().EndGame();
        gameEndHandler.GameEnd(d.winnerName);
        players[myPlayerId].GetComponent<PlayerInput>().EndGame();
        Debug.Log("Game  winner: " + d.winnerName);
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
    #endregion

    // -------------------------------------------------------------------------
    #region Outgoing events
    public  void CreateRoom()
    {
        if (string.IsNullOrWhiteSpace(inputName.text))
        {
            txtErrorMessage.text = "Enter your name!";
            errorAnimator.SetTrigger("Start");
            return;
        }
         Emit("createRoom", new { name = inputName.text });
    }

    public  void JoinRoom()
    {
        if (string.IsNullOrWhiteSpace(inputName.text) ||
            string.IsNullOrWhiteSpace(inputRoomCode.text))
        {
            txtErrorMessage.text = "Enter name & room code!";
            errorAnimator.SetTrigger("Start");
            return;
        }
         Emit("joinRoom", new { roomCode = inputRoomCode.text, name = inputName.text });
    }

    public void SendInput(bool f, bool b, bool l, bool r, float rotDelta)
    {
        var json = JsonConvert.SerializeObject(new
        {
            input = new { forward = f, backward = b, left = l, right = r, rotationDelta = rotDelta }
        });
        socket.emit("move", json);
    }

    public  void SendShoot() => Emit("shoot");
    #endregion

    // -------------------------------------------------------------------------
    #region World sync
    private void SpawnPlayer(Dictionary<string, PlayerData> pdata)
    {
        foreach (KeyValuePair<string, PlayerData> kv in pdata)
        {
            string id = kv.Key;
            PlayerData pd = kv.Value;

            if (!players.ContainsKey(id))
            {
                GameObject go = Instantiate(playerPrefab);
                players[id] = go;

                if (id != myPlayerId)
                {
                    PlayerInput playerInput = go.GetComponent<PlayerInput>();
                    playerInput.DeactivateCameraObject();
                    playerInput.enabled = false;
                    var canvas = go.GetComponent<PlayerCanvasHandler>();
                    canvas.SetHealth(pd.health / 100f);
                    canvas.SetName(pd.name);
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
            }
            else
            {
                pgo.GetComponent<PlayerCanvasHandler>().DeactivateCanvas();
                healthSlider.value = pd.health / 100f;
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
            }
            else
            {
                pgo.GetComponent<PlayerCanvasHandler>().DeactivateCanvas();
                healthSlider.value = pd.health / 100f;
            }
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
                GameObject go = Instantiate(playerPrefab);
                players[id] = go;

                if (id != myPlayerId)
                {
                    PlayerInput playerInput = go.GetComponent<PlayerInput>();
                    playerInput.DeactivateCameraObject();
                    playerInput.enabled = false;
                    var canvas = go.GetComponent<PlayerCanvasHandler>();
                    canvas.SetHealth(pd.health / 100f);
                    canvas.SetName(pd.name);
                    go.GetComponent<CanvasFacingCamera>().enabled = false;
                    players[myPlayerId].GetComponent<CanvasFacingCamera>().AddCanvas(canvas.GetCanvasgameObject());
                }
                else
                {
                    go.GetComponent<PlayerCanvasHandler>().DeactivateCanvas();
                }
            }

            GameObject pgo = players[id];
            if (id != myPlayerId)
            {
                var canvas = pgo.GetComponent<PlayerCanvasHandler>();
                canvas.SetHealth(pd.health / 100f);
                canvas.SetName(pd.name);
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
    #endregion

    // -------------------------------------------------------------------------
    #region DTOs
    [Serializable] public class YourIdResponse { public string id; public string name; }
    [Serializable] public class RoomJoinedResponse { public string roomCode; }
    [Serializable] public class ErrorRoomResponse { public string msg; }
    [Serializable] public class PlayerDisconnectedResponse { public string playerId; }
    [Serializable] public class PlayerHitResponse { public string targetId; public float newHealth; }
    [Serializable] public class PlayerWonResponse { public string winnerName; public string loserId; }
    [Serializable] public class BulletRemoveResponse { public int bulletId; }
    [Serializable] public class BulletHitObstacleResponse { public Position bulletPos; }

    [Serializable]
    public class PlayerData
    {
        public float x, y, z;
        public float rotationY;
        public float forward, right;
        public float health;
        public string name;
    }

    [Serializable]
    public class BulletData
    {
        public int id;
        public string ownerId;
        public float x, y, z;
        public float rotationY;
    }

    [Serializable] public class Position { public float x, y, z; }

    [Serializable]
    public class StateUpdateData
    {
        public Dictionary<string, PlayerData> players;
        public List<BulletData> bullets;
    }

    [Serializable]
    public class PlayersWrapper
    {
        public Dictionary<string, PlayerData> players;
    }
    #endregion
}
