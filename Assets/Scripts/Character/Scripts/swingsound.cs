using UnityEngine;

// Plays an attack clip through the AudioSource on this object. PlayAttackSound only sounds if the Attack button
// was pressed this frame, so it is meant to be called from the player's attack handling or an animation event.
public class AttackSound : MonoBehaviour
{
    public AudioClip attackClip;
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public void PlayAttackSound()
    {
        if (Input.GetButtonDown("Attack"))
            audioSource.PlayOneShot(attackClip);
    }
}
