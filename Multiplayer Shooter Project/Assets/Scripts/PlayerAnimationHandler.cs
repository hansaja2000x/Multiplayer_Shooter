using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimationHandler : MonoBehaviour
{
    [SerializeField] private Animator playerAnimator;

    [SerializeField] private AudioSource footAudio;
    [SerializeField] private AudioClip footAudioClip;

    [SerializeField] private AudioSource laserGunAudio;
    [SerializeField] private AudioClip laserGunAudioClip;

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
        playerAnimator.SetTrigger("Shooting");
    }

    public void DeathAnimation()
    {
        playerAnimator.SetTrigger("Death");
    }

    public void PlayFootAudio()
    {
        footAudio.PlayOneShot(footAudioClip);
    }

    public void PlayShootAudio()
    {
        laserGunAudio.PlayOneShot(laserGunAudioClip);
    }
}
