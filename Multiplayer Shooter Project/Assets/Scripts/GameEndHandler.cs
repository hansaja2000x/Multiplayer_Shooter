using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class GameEndHandler : MonoBehaviour
{
    [SerializeField] private string winMessage = " won the game.";
    [SerializeField] private string roundWinMessage = " won the round.";
    [SerializeField] private string dropMessage = "Opponent dropped.\n You won";
    public TextMeshProUGUI fullEndMessage;

    [SerializeField] private UnityEvent endGameEvents;
    [SerializeField] private UnityEvent roundEndEvents;

    public void GameEnd(string winnerName)
    {
        endGameEvents.Invoke();
        fullEndMessage.text = winnerName + winMessage;
    }

    public void RoundEnd(string winnerName)
    {
        roundEndEvents.Invoke();
        fullEndMessage.text = winnerName + roundWinMessage;
        StartCoroutine(HideMessageAfterDelay(3f));
    }

    private IEnumerator HideMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        fullEndMessage.text = "";
    }

    public void PlayerDropped()
    {
        endGameEvents.Invoke();
        fullEndMessage.text = dropMessage;
    }
}