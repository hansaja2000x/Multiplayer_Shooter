using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class RoundWinDisplay : MonoBehaviour
{
    [SerializeField] private List<RawImage> playerWinIndicators;   // 3 RawImages for YOU, representing 3 rounds
    [SerializeField] private List<RawImage> opponentWinIndicators; // 3 RawImages for OPPONENT, representing 3 rounds
    [SerializeField] private NetworkManager networkManager;

    [Header("Player Name Texts")]
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI opponentNameText;

    private readonly Color winColorYou = new Color32(0x00, 0xAA, 0x2D, 0xFF);     // #00AA2D
    private readonly Color winColorOpponent = new Color32(0xFF, 0x40, 0x55, 0xFF); // #FF4055
    private readonly Color defaultColor = new Color32(0x66, 0x66, 0x66, 0xFF);     // #666666

    private void Start()
    {
        if (networkManager == null)
            networkManager = FindObjectOfType<NetworkManager>();

        if (playerWinIndicators.Count != 3 || opponentWinIndicators.Count != 3)
        {
            Debug.LogError("You must assign exactly 3 RawImages each for player and opponent.");
            return;
        }

        // Only reset if no game data is available (e.g., at initial load)
        if (networkManager.MyPlayerId == null)
        {
            ResetDisplay();
        }

        if (networkManager != null)
            networkManager.OnStateUpdated += UpdateRoundWins;
    }

    private void OnDestroy()
    {
        if (networkManager != null)
            networkManager.OnStateUpdated -= UpdateRoundWins;
    }

    private void UpdateRoundWins(NetworkManager.StateUpdateData data)
    {
        if (data.roundWins == null)
        {
            Debug.LogWarning("RoundWins data is null in UpdateRoundWins");
            return;
        }

        string myUuid = networkManager.GetPlayerUuid(networkManager.MyPlayerId);
        string opponentId = networkManager.GetOpponentId();
        string opponentUuid = opponentId != null ? networkManager.GetPlayerUuid(opponentId) : null;

        Debug.Log($"MyPlayerId: {networkManager.MyPlayerId}, MyUuid: {myUuid}, OpponentId: {opponentId}, OpponentUuid: {opponentUuid}");

        if (myUuid == null || opponentUuid == null)
        {
            Debug.LogWarning($"Invalid UUIDs - myUuid: {myUuid}, opponentUuid: {opponentUuid}");
            return;
        }

        int myWins = data.roundWins.TryGetValue(myUuid, out int mWins) ? Mathf.Min(mWins, 3) : 0; // Cap at 3
        int opponentWins = data.roundWins.TryGetValue(opponentUuid, out int oWins) ? Mathf.Min(oWins, 3) : 0; // Cap at 3

        Debug.Log($"Updating UI - MyWins: {myWins}, OpponentWins: {opponentWins}");

        UpdateIndicators(playerWinIndicators, myWins, winColorYou);
        UpdateIndicators(opponentWinIndicators, opponentWins, winColorOpponent);

        string myName = networkManager.GetPlayerName(networkManager.MyPlayerId) ?? "Player";
        if (playerNameText != null)
            playerNameText.text = myName;

        string opponentName = opponentId != null ? (networkManager.GetPlayerName(opponentId) ?? "Opponent") : "Opponent";
        if (opponentNameText != null)
            opponentNameText.text = opponentName;
    }

    private void UpdateIndicators(List<RawImage> indicators, int wins, Color winColor)
    {
        for (int i = 0; i < indicators.Count; i++)
        {
            if (indicators[i] == null)
            {
                Debug.LogError($"Null RawImage at index {i}");
                continue;
            }

            // Show the image if the round is won (i < wins), set to default otherwise
            if (i < wins)
            {
                indicators[i].gameObject.SetActive(true);
                indicators[i].color = winColor;
                Debug.Log($"Indicator {i} set to winColor (Round {i + 1} won)");
            }
            else
            {
                indicators[i].gameObject.SetActive(true);
                indicators[i].color = defaultColor;
                Debug.Log($"Indicator {i} set to defaultColor (Round {i + 1} not won)");
            }
        }
    }

    public void ResetDisplay()
    {
        UpdateIndicators(playerWinIndicators, 0, winColorYou);
        UpdateIndicators(opponentWinIndicators, 0, winColorOpponent);

        if (playerNameText != null) playerNameText.text = "";
        if (opponentNameText != null) opponentNameText.text = "";
    }
}