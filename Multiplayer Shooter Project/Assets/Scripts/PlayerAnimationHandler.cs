using UnityEngine;

public class PlayerAnimationHandler : MonoBehaviour
{
    [SerializeField] private Animator playerAnimator;

    [SerializeField] private AudioSource footAudio;
    [SerializeField] private AudioClip footAudioClip;

    [SerializeField] private AudioSource laserGunAudio;
    [SerializeField] private AudioClip laserGunAudioClip;

    // Set movement animation parameters (expects values between -1 and 1 for smooth blending)
    public void SetAnimState(float forward, float right)
    {
        playerAnimator.SetFloat("MoveX", right);
        playerAnimator.SetFloat("MoveZ", forward);
    }

    // Trigger shooting animation
    public void EnableShootAnimation()
    {
        playerAnimator.SetTrigger("Shooting");
    }

    // Trigger death animation
    public void DeathAnimation()
    {
        playerAnimator.SetTrigger("Death");
    }

    // Play footstep audio (call from animation events)
    public void PlayFootAudio()
    {
        if (footAudio != null && footAudioClip != null)
            footAudio.PlayOneShot(footAudioClip);
    }

    // Play shooting audio (call when shooting)
    public void PlayShootAudio()
    {
        if (laserGunAudio != null && laserGunAudioClip != null)
            laserGunAudio.PlayOneShot(laserGunAudioClip);
    }
}