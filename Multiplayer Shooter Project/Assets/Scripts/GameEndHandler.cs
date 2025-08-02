using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

public class GameEndHandler : MonoBehaviour
{
    [SerializeField] public string localPlayerName = "Player"; // Set this to the local player's name, e.g., via script or Inspector
    [SerializeField] private string winMessage = "won the game.";
    [SerializeField] private string loseMessage = "lost the game.";
    [SerializeField] private string roundWinMessage = "won the round.";
    [SerializeField] private string roundLoseMessage = "lost the round.";
    [SerializeField] private string dropMessage = "Opponent dropped.\n You won";

    // Separate texts for player name and status message
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI statusText;

    [SerializeField] private UnityEvent endGameEvents;
    [SerializeField] private UnityEvent roundEndEvents;
    [SerializeField] private UnityEvent roundRevertEvents;

    // Animator reference for resetting animation
    [SerializeField] private Animator animator;

    public void GameEnd(string winnerName)
    {
        endGameEvents.Invoke();
        playerNameText.text = localPlayerName;
        statusText.text = (winnerName == localPlayerName) ? winMessage : loseMessage;
    }

    public void RoundEnd(string winnerName)
    {
        roundEndEvents.Invoke();
        playerNameText.text = localPlayerName;
        statusText.text = (winnerName == localPlayerName) ? roundWinMessage : roundLoseMessage;
        StartCoroutine(HideMessageAfterDelay(3f));
    }

    private IEnumerator HideMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        playerNameText.text = "";
        statusText.text = "";
        roundRevertEvents.Invoke();

        // Reset the animator after revert by disabling and re-enabling it (resets to default state for simple animations without specific states)
        if (animator != null)
        {
            animator.enabled = false;
            animator.enabled = true;
        }
    }

    public void PlayerDropped()
    {
        endGameEvents.Invoke();
        playerNameText.text = "";
        statusText.text = dropMessage; // Treat as a win for the local player
    }
}