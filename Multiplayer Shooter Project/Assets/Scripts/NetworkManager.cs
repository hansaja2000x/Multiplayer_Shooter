using UnityEngine;
using NativeWebSocket;
using TMPro;
using Newtonsoft.Json;
using System.Collections.Generic;

public class NetwokManager : MonoBehaviour
{
    public static NetwokManager Instance;

    WebSocket ws;
    public GameObject playerPrefab;
    public TMP_Text roomDisplayText;
    public TMP_InputField roomInput;
    public GameObject UICamera;

    Dictionary<string, GameObject> players = new();
    string roomCode;
    string myPlayerId;

    async void Awake()
    {
        Instance = this;
        ws = new WebSocket("ws://localhost:3000");

        ws.OnOpen += () => Debug.Log("Connected to server");

        ws.OnMessage += (bytes) =>
        {
            string message = System.Text.Encoding.UTF8.GetString(bytes);
            HandleMessage(message);
        };

        ws.OnError += (e) => Debug.Log("WebSocket Error: " + e);
        ws.OnClose += (e) => Debug.Log("WebSocket Closed");

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
        var msg = new { type = "createRoom" };
        ws.SendText(JsonConvert.SerializeObject(msg));
    }

    public void JoinRoom()
    {
        var msg = new { type = "joinRoom", roomCode = roomInput.text };
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

    void HandleMessage(string msg)
    {
        var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(msg);
        if (!json.ContainsKey("type")) return;

        string type = json["type"].ToString();

        if (type == "yourId")
        {
            myPlayerId = json["playerId"].ToString();
            Debug.Log("My Player ID: " + myPlayerId);
            UICamera.SetActive(false);
        }
        else if (type == "roomCreated")
        {
            roomCode = json["roomCode"].ToString();
            roomDisplayText.text = "Room: " + roomCode;
        }
        else if (type == "init")
        {
            string playerData = json["players"].ToString();
            var dict = JsonConvert.DeserializeObject<Dictionary<string, Position>>(playerData);

            foreach (var kv in dict)
            {
                // Instantiate new players
                if (!players.ContainsKey(kv.Key))
                    players[kv.Key] = Instantiate(playerPrefab);

                // Set initial transform
                players[kv.Key].transform.position = new Vector3(kv.Value.x, kv.Value.y, kv.Value.z);
                players[kv.Key].transform.rotation = Quaternion.Euler(0, kv.Value.rotationY, 0);

                // Disable camera for non-local players
                if (kv.Key != myPlayerId)
                {
                    PlayerInput input = players[kv.Key].GetComponent<PlayerInput>();
                    input.DeactivateCameraObject();
                }
            }
        }
        else if (type == "stateUpdate")
        {
            string playerData = json["players"].ToString();
            var dict = JsonConvert.DeserializeObject<Dictionary<string, Position>>(playerData);

            foreach (var kv in dict)
            {
                if (players.ContainsKey(kv.Key))
                {
                    GameObject player = players[kv.Key];
                    player.transform.position = new Vector3(kv.Value.x, kv.Value.y, kv.Value.z);
                    player.transform.rotation = Quaternion.Euler(0, kv.Value.rotationY, 0);
                }
            }
        }
        else if (type == "mirror")
        {
            Debug.Log("Mirror: " + msg);
        }
        else if (type == "error")
        {
            Debug.LogError(json["msg"].ToString());
        }
    }


    class Position
    {
        public float x, y, z, rotationY;
    }

    async void OnApplicationQuit()
    {
        await ws.Close();
    }
}