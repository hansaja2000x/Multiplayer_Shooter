using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimationHandler : MonoBehaviour
{
    [SerializeField] private Animator playerAnimator;

    public void SetAnimState(float forward, float right)
    {
        playerAnimator.SetFloat("MoveX", right );
        playerAnimator.SetFloat("MoveZ", forward );

    }

    public void DisableShootAnimation()
    {
        //playerAnimator.SetBool("isShooting", false);
    }

    public void EnableShootAnimation()
    {
        playerAnimator.SetTrigger("isShooting");
    }
}
