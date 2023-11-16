using UnityEngine;
using TMPro;

public class OpenPortalCheck : MonoBehaviour
{
    public Animator portalAnimation;
    public VelocityEstimator wandTipVelocityCheck;
    public ImageGen stopRecording;
    // public TextMeshProUGUI veloDebug;
    
    public AudioController audioController;

    public float velocityThreshold = 1.2f;
    public float keepOpenAfterXSeconds = 3.0f;
    private float keepOpenTimer = 0.0f;

    // private bool isWandTipMoving = false;   
    private bool isPortalOpen = false;

    private void Awake()
    {
        portalAnimation.SetBool("Open", false);

        // stopRecording = FindObjectOfType<ImageGen>();

        if (stopRecording == null)
        {
            Debug.LogError("OpenPortalCheck: stopRecording is null");
            // veloDebug.text = "Stop Recording is null";
        }
    }

    private void Update()
    {
        var tipVelocity = wandTipVelocityCheck.GetVelocityEstimate().magnitude;
        // veloDebug.text = "Velocity = " + tipVelocity.ToString();

        // if the wand tip is moving faster than the threshold, then open the portal (play the animation)
        // isWandTipMoving = tipVelocity > velocityThreshold;

        if (!isPortalOpen)
        {
            if(tipVelocity > velocityThreshold)
            {
                keepOpenTimer += Time.deltaTime;
            }

            else
            {
                keepOpenTimer = 0.0f;
            }

            // veloDebug.text = "Velocity = " + tipVelocity.ToString() + "Timer = " + keepOpenTimer.ToString();

            if (keepOpenTimer >= keepOpenAfterXSeconds)
            {
                
                portalAnimation.SetBool("Open", true);
                audioController.PlayPortalShow();
                isPortalOpen = true;
                stopRecording.StopRecording();
                // veloDebug.text = "Opening Portal and Stop recording...";
                keepOpenTimer = 0.0f;

            }

        }


    }

}
