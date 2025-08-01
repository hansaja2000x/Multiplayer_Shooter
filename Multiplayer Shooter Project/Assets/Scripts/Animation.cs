using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationSequencer : MonoBehaviour
{
    // Reference to the Animator component
    public Animator animator;

    // List of animation state names to play in sequence (assuming states are named after clips in the Animator Controller)
    public List<string> clipNames = new List<string>();

    void Start()
    {
        // Automatically get the Animator if not assigned in Inspector
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        // Start the sequence if there are clips to play
        if (clipNames.Count > 0)
        {
            StartCoroutine(PlaySequentialAnimations());
        }
    }

    private IEnumerator PlaySequentialAnimations()
    {
        foreach (string clipName in clipNames)
        {
            // Play the current animation clip
            animator.Play(clipName);

            // Wait one frame for the state to update
            yield return null;

            // Wait until the current animation finishes
            while (!IsAnimationFinished(clipName))
            {
                yield return null;
            }
        }
    }

    private bool IsAnimationFinished(string clipName)
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        // Check if the current state matches the clip and has completed (normalizedTime >= 1)
        return stateInfo.IsName(clipName) && stateInfo.normalizedTime >= 1.0f;
    }
}