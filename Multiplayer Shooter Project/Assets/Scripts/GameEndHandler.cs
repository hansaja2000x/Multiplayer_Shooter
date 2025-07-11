using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class GameEndHandler : MonoBehaviour
{
    [SerializeField] private string endMessage = " won the game.";
    public TextMeshProUGUI fullEndMessage;

    [SerializeField] private UnityEvent endGameEvents;

    public void GameEnd(string winnerName)
    {
        endGameEvents.Invoke();
        endMessage = winnerName + endMessage;
        fullEndMessage.text = endMessage;
    }
    
}
