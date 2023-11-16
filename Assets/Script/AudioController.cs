using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioController : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip introClip;
    public AudioClip pickUp;
    public AudioClip portalShow;
    public AudioClip vidShow;
    public AudioClip vidShowSpeech;

    private void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        audioSource.clip = introClip;
        StartCoroutine(PlayAudioAfterDelay(2.0f));
    }

    IEnumerator PlayAudioAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        audioSource.Play();
    }

    public void PlayPickUp()
    {
        audioSource.clip = pickUp;
        audioSource.Play();
    }

    public void PlayPortalShow()
    {
        audioSource.clip = portalShow;
        audioSource.Play();
    }

    public void PlayVideoShow()
    {
       audioSource.clip = vidShow;
        audioSource.Play();
    }

    public void PlayVideoSpeech()
    {
        audioSource.clip = vidShowSpeech;
        audioSource.Play();
    }

}
