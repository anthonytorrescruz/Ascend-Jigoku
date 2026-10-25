using UnityEngine;

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
