using UnityEngine;

public class PlayerAnimationHandler : MonoBehaviour
{
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Transform assaultRifle;

    [SerializeField] private AudioSource footAudio;
    [SerializeField] private AudioClip footAudioClip;

    [SerializeField] private AudioSource laserGunAudio;
    [SerializeField] private AudioClip laserGunAudioClip;

    private Transform upperBody;
   [SerializeField] private float verticalAngle = 35f;

    void Start()
    {
        upperBody = playerAnimator.GetBoneTransform(HumanBodyBones.Spine); 
    }

    void LateUpdate()
    {
        if (upperBody != null)
        {
            Vector3 currentEuler = upperBody.localRotation.eulerAngles;
            upperBody.localRotation = Quaternion.Euler(currentEuler.x, currentEuler.y, verticalAngle);
            assaultRifle.localRotation = upperBody.localRotation;
        }
    }

    public void SetVerticleAngle(float angle)
    {
        verticalAngle = angle;
    }

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