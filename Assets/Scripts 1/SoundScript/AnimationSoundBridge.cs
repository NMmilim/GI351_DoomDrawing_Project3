using UnityEngine;

public class AnimationAudioBridge : MonoBehaviour
{
    // For 2D Sounds (UI, First Person Weapon sounds, or global SFX)
    public void PlayAnimationSound2D(string soundName)
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySound2D(soundName);
        }
    }

    // For 3D Sounds (Third Person Weapons, NPC footsteps, or localized SFX)
    public void PlayAnimationSound3D(string soundName)
    {
        if (SoundManager.Instance != null)
        {
            // Uses the current position of the GameObject animating
            SoundManager.Instance.PlaySound3D(soundName, transform.position);
        }
    }
}