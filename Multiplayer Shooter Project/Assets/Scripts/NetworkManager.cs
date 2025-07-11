using UnityEngine;
using NativeWebSocket;
using TMPro;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

public class NetwokManager : MonoBehaviour
{
    public static NetwokManager Instance;

    WebSocket ws;
    public GameObject playerPrefab;
    public GameObject bulletPrefab;
    public GameObject hitVFXPrefab;
    public TMP_Text roomDisplayText;
    public TMP_InputField roomInput;
    public TMP_InputField nameInput;
    public GameObject UICamera;
    public GameObject MenuUI;
    public GameObject GameplayUI;
    public Animator errorMessageAnimator;
    public Slider myHealthSlider;
    public TextMeshProUGUI nameText;

    [SerializeField] private GameEndHandler gameEndHandler;

    Dictionary<string, GameObject> players = new();
    string roomCode;
    string myPlayerId;

    class BulletData { public int id; public float x, y, z, rotationY; }
    private Dictionary<int, GameObject> bullets = new();
    Dictionary<int, GameObject> activeBullets = new();

    private PlayerAnimationHandler myPlayerAnimationhandler;

    int bulletIdCounter = 0;

    async void Awake()
    {
        Instance = this;
        ws = new WebSocket("ws://localhost:3000");

        ws.OnOpen += () => Debug.Log("Connected to server");
        ws.OnMessage += bytes =>
        {
            string msg = System.Text.Encoding.UTF8.GetString(bytes);
            HandleMessage(msg);
        };
        ws.OnError += e => Debug.LogError("WebSocket Error: " + e);
        ws.OnClose += e => Debug.Log("WebSocket Closed");

        await ws.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        ws?.DispatchMessageQueue();
#endif
    }

    public void CreateRoom()
    {
        var msg = new { type = "createRoom", name = nameInput.text };
        ws.SendText(JsonConvert.SerializeObject(msg));
    }

    public void JoinRoom()
    {
        var msg = new { type = "joinRoom", roomCode = roomInput.text, name = nameInput.text };
        ws.SendText(JsonConvert.SerializeObject(msg));
    }

    public void SendInput(bool forward, bool backward, bool left, bool right, float rotationDelta)
    {
        var msg = new
        {
            type = "move",
            input = new { forward, backward, left, right, rotationDelta }
        };
        ws.SendText(JsonConvert.SerializeObject(msg));
    }

    public void SendShoot()
    {
        var msg = new { type = "shoot" };
        ws.SendText(JsonConvert.SerializeObject(msg));
    }

    void HandleMessage(string msg)
    {
        var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(msg);
        if (!json.TryGetValue("type", out var tType)) return;
        string type = tType.ToString();

        if (type == "yourId")
        {
            myPlayerId = json["id"].ToString();
            string myName = json["name"].ToString();
            Debug.Log("My Player ID: " + myPlayerId);
            nameText.text = myName;
            UICamera.SetActive(false);
            MenuUI.SetActive(false);
            GameplayUI.SetActive(true);
        }
        else if (type == "roomJoined")
        {
            roomCode = json["roomCode"].ToString();
            roomDisplayText.text = "Room: " + roomCode;
        }
        else if (type == "noRoom")
        {
            errorMessageAnimator.SetTrigger("Start");
        }
        else if (type == "init" || type == "newPlayerConnected")
        {
            var playerData = json["players"].ToString();
            var dict = JsonConvert.DeserializeObject<Dictionary<string, Position>>(playerData);
            foreach (var kv in dict)
            {
                if (!players.ContainsKey(kv.Key))
                {
                    var go = Instantiate(playerPrefab);
                    players[kv.Key] = go;
                    if (kv.Key != myPlayerId)
                    {
                        go.GetComponent<PlayerInput>().DeactivateCameraObject();
                        go.GetComponent<PlayerInput>().enabled = false;
                    }
                    else
                    {
                        myPlayerAnimationhandler = go.GetComponent<PlayerAnimationHandler>();
                    }
                }
                var p = players[kv.Key];
                p.transform.position = new Vector3(kv.Value.x, kv.Value.y, kv.Value.z);
                p.transform.rotation = Quaternion.Euler(0, kv.Value.rotationY, 0);

                /*var slider = p.GetComponentInChildren<Slider>();
                slider.value = kv.Value.health / 100f;*/
            }
        }
        else if (type == "stateUpdate")
        {
            var playerData = json["players"].ToString();
            var dict = JsonConvert.DeserializeObject<Dictionary<string, Position>>(playerData);
            foreach (var kv in dict)
            {
                if (!players.ContainsKey(kv.Key)) continue;
                var pgo = players[kv.Key];
                pgo.transform.position = new Vector3(kv.Value.x, kv.Value.y, kv.Value.z);
                pgo.transform.rotation = Quaternion.Euler(0, kv.Value.rotationY, 0);

                var anim = pgo.GetComponent<PlayerAnimationHandler>();
                anim.SetAnimState(kv.Value.forward, kv.Value.right);

                if (kv.Key == myPlayerId)
                    myHealthSlider.value = kv.Value.health / 100f;
                /*else
                    pgo.GetComponentInChildren<Slider>().value = kv.Value.health / 100f;*/
            }

            var bulletData = JsonConvert.SerializeObject(json["bullets"]);
            var bulletList = JsonConvert.DeserializeObject<List<BulletState>>(bulletData);

            SyncBullets(bulletList);
        }
        else if (type == "bulletRemove")
        {
            int id = int.Parse(json["bulletId"].ToString());
            if (activeBullets.ContainsKey(id))
            {
                Destroy(activeBullets[id]);
                activeBullets.Remove(id);
            }
        }

        else if (type == "bulletHitObstacle")
        {
            var pos = JsonConvert.DeserializeObject<Pos>(json["bulletPos"].ToString());
            Instantiate(hitVFXPrefab, new Vector3(pos.x, pos.y, pos.z), Quaternion.identity);
        }
        else if (type == "playerHit")
        {
            string targetId = json["targetId"].ToString();
            float newHealth = float.Parse(json["newHealth"].ToString());
            if (players.ContainsKey(targetId))
            {
                if (targetId == myPlayerId)
                    myHealthSlider.value = newHealth / 100f;
                /*else
                    players[targetId].GetComponentInChildren<Slider>().value = newHealth / 100f;*/
            }
        }
        else if (type == "playerWon")
        {
            string winnerName = json["winnerName"].ToString();
            string loserId = json["loserId"].ToString();
            if (players.ContainsKey(loserId))
            {

                players[loserId].GetComponent<PlayerAnimationHandler>().DeathAnimation();
                players[myPlayerId].GetComponent<PlayerInput>().EndGame();
                gameEndHandler.GameEnd(winnerName);
            }
        }
    }

    void SyncBullets(List<BulletState> serverBullets)
    {
        HashSet<int> serverIds = new();

        foreach (var b in serverBullets)
        {
            serverIds.Add(b.id);
            if (!bullets.ContainsKey(b.id))
            {
                GameObject go = Instantiate(bulletPrefab);
                bullets[b.id] = go;
                /*if(b.ownerId == myPlayerId)
                {
                    myPlayerAnimationhandler.EnableShootAnimation();
                }*/

                players[b.ownerId].GetComponent<PlayerAnimationHandler>().EnableShootAnimation();
            }

            GameObject bulletGO = bullets[b.id];
            bulletGO.transform.position = new Vector3(b.x, b.y, b.z);
            bulletGO.transform.rotation = Quaternion.Euler(0, b.rotationY, 0);
        }

        var idsToRemove = new List<int>();
        foreach (var id in bullets.Keys)
        {
            if (!serverIds.Contains(id))
                idsToRemove.Add(id);
        }

        foreach (var id in idsToRemove)
        {
            Destroy(bullets[id]);
            bullets.Remove(id);
        }
    }




    class Position { public float x, y, z, rotationY, forward, right, health; }
    class Pos { public float x, y, z; }

    class BulletState
    {
        public int id;
        public string ownerId;
        public float x, y, z, rotationY;
    }

    async void OnApplicationQuit()
    {
        await ws.Close();
    }
}
