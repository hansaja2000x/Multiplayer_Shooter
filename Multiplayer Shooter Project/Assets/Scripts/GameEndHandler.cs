using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GameEndHandler : MonoBehaviour
{
    [SerializeField] private string endMessage = " won the game.";
    public TextMeshProUGUI fullEndMessage;

    [SerializeField] private GameObject gameplayUI;
    [SerializeField] private GameObject endGameUI;

    public void GameEnd(string winnerName)
    {
        gameplayUI.SetActive(false);
        endGameUI.SetActive(true);
        endMessage = winnerName + endMessage;
        fullEndMessage.text = endMessage;
    }
    
}
