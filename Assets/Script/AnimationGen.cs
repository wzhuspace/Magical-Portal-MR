using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System;
using Amazon;
using Amazon.S3;
using Amazon.Runtime;
using Amazon.S3.Model;
using System.IO;
using UnityEngine.Events;
using TMPro;
using UnityEngine.Video;
using System.Net;

public class AnimationGen : MonoBehaviour
{

    public AudioController audioPlay;

    // LeiaPix API
    private const string LEIA_DEPTH_URL = "https://api.leiapix.com/api/v1/disparity";
    private const string LEIA_ANIME_URL = "https://api.leiapix.com/api/v1/animation";
    private const string LEIA_ID = "LEIA PIX ID";
    private const string LEIA_SECRET = "LEIA PIX SECRET KEY";
    private string TOKEN_URL = "https://auth.leialoft.com/auth/realms/leialoft/protocol/openid-connect/token";
    private string accessToken;

    // AWS S3 Service  
    private AmazonS3Client s3Client;
    private string s3BucketName = "wentianbucket";
    private string s3ObjectKey = "DepthImg.png"; 
    private string S3AnimationKey = "AnimationClip.mp4";
    private const string AWS_ID = "AWS ID";
    private const string AWS_SECRECT = "AWS SECRET";

    // Image Generation
    private string originalImageUrl = "";
    private string resultPresignedUrl = "";
    private string anime_resultPresignedUrl = "";

    [SerializeField] private TextMeshProUGUI debugText;

    public GameObject normalMat;
    public GameObject videoGO;


    // AWS Bucket File Object URL:
    private const string objectBasedURL = "https://wentianbucket.s3.ca-central-1.amazonaws.com/"; // NOTE: MAKE YOUR BUCKET PUBLIC
    private string objectURL => objectBasedURL + s3ObjectKey;
    private string animationURL => objectBasedURL + S3AnimationKey;

    public VideoPlayer animationPlayer;

    private string videoFilePath;

    void Awake()
    {
        normalMat.SetActive(true);
        videoGO.SetActive(false);
    }

    void Start()
    {
        videoFilePath = Path.Combine("_GenImages", "Animation.mp4");

        // Initialize the S3 client
        var credentials = new BasicAWSCredentials(AWS_ID, AWS_SECRECT);
        s3Client = new AmazonS3Client(credentials, RegionEndpoint.CACentral1);

        ImageGen imageGenScript = GetComponent<ImageGen>();

        if (imageGenScript != null)
        {
            imageGenScript.ImageURLGenerated += HandleImageGenerated;
        }
    }

    private void HandleImageGenerated(string imageUrl)
    {
        originalImageUrl = imageUrl;
        StartCoroutine(GetLeiaTokenDetail());

        debugText.text = "Leinardo Finshed, Leia triggered";
    }

    private IEnumerator GetLeiaTokenDetail()
    {
        resultPresignedUrl = GenerateDepthPresignedURL();

        UnityWebRequest webRequest = new UnityWebRequest(TOKEN_URL, "POST");

        byte[] bodyRaw = Encoding.UTF8.GetBytes($"client_id={LEIA_ID}&client_secret={LEIA_SECRET}&grant_type=client_credentials");
        webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/x-www-form-urlencoded");
        yield return webRequest.SendWebRequest();

        if (webRequest.result == UnityWebRequest.Result.ConnectionError || webRequest.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.Log(webRequest.error);
        }
        else
        {
            Debug.Log("Received: " + webRequest.downloadHandler.text);
            AccessTokenResponse response = JsonUtility.FromJson<AccessTokenResponse>(webRequest.downloadHandler.text);
            accessToken = response.access_token;

            StartCoroutine(SendLeiaRequest());  // Continue to the next request and get Disparity Map
        }
    }

    public string GenerateDepthPresignedURL()
    {
        Debug.Log("Generating presigned URL...");

        debugText.text = "Generating presigned URL...";

        var request = new GetPreSignedUrlRequest
        {
            BucketName = s3BucketName,
            Key = s3ObjectKey,
            Verb = HttpVerb.PUT, // This should be a PUT request for LeiaPix to upload the image
            Expires = DateTime.UtcNow.AddMinutes(60) // The URL will be valid for 60 minutes
        };

        string presignedUrl = s3Client.GetPreSignedURL(request);

        Debug.Log("Generated presigned URL: " + presignedUrl);
        debugText.text = "Generated presigned URL: " + presignedUrl;

        return presignedUrl;
    }

    private IEnumerator SendLeiaRequest()
    {
        string correlationId = Guid.NewGuid().ToString(); // Generate a unique ID for this request
        UnityWebRequest depthRequest = new UnityWebRequest(LEIA_DEPTH_URL, "POST");
        depthRequest.downloadHandler = new DownloadHandlerBuffer();
        depthRequest.SetRequestHeader("Authorization", "Bearer " + accessToken);
        depthRequest.SetRequestHeader("Content-Type", "application/json");

        string jsonBody = JsonUtility.ToJson(new DisparityRequest
        {
            correlationId = correlationId,
            inputImageUrl = originalImageUrl,
            resultPresignedUrl = resultPresignedUrl,
            dilation = 0.01f
        });

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        depthRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);

        yield return depthRequest.SendWebRequest();

        if (depthRequest.result == UnityWebRequest.Result.ConnectionError || depthRequest.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError(depthRequest.error);
        }
        else
        {
            Debug.Log("Received disparity response: " + depthRequest.downloadHandler.text);
            Debug.Log("START GENERATING ANIMATION");
            StartCoroutine(SendAnimationRequest());
        }
    }

    private IEnumerator SendAnimationRequest()
    {
        string new_correlationId = Guid.NewGuid().ToString(); // Generate a unique ID for this request
        UnityWebRequest customDepthRequest = new UnityWebRequest(LEIA_ANIME_URL, "POST");
        customDepthRequest.downloadHandler = new DownloadHandlerBuffer();
        customDepthRequest.SetRequestHeader("Authorization", "Bearer " + accessToken);
        customDepthRequest.SetRequestHeader("Content-Type", "application/json");
        anime_resultPresignedUrl = GenerateAnimePresignedURL();

        string jsonBody = JsonUtility.ToJson(new CustomDisparityRequest
        {
            correlationId = new_correlationId,
            inputImageUrl = originalImageUrl,           // origianl Image Url
            inputDisparityUrl = objectURL,              // Depth Url
            resultPresignedUrl = anime_resultPresignedUrl,    // Amazon S3 Url
            convergence = -1,
            animationLength = 5,
            phaseX = 0f,
            phaseY = 0.25f,
            phaseZ = 0.2f,
            amplitudeX = 0.65f,
            amplitudeY = 0.11f,
            amplitudeZ = 0.62f
        });

        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        customDepthRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);

        yield return customDepthRequest.SendWebRequest();

        if (customDepthRequest.result == UnityWebRequest.Result.ConnectionError || customDepthRequest.result == UnityWebRequest.Result.ProtocolError)
        {
            Debug.LogError(customDepthRequest.error);
            debugText.text = "Error: " + customDepthRequest.error;
        }
        else
        {
            Debug.Log("Animation: Received custom disparity response: " + customDepthRequest.downloadHandler.text);
            debugText.text = "Animation: Received custom disparity response: " + customDepthRequest.downloadHandler.text;

            StartCoroutine(DownloadAnimation());
        }

    }

    public string GenerateAnimePresignedURL()
    {
        Debug.Log("Generating Animation presigned URL...");
        debugText.text = "Behold! You've arrived at the realm of your dreams.";

        var request = new GetPreSignedUrlRequest
        {
            BucketName = s3BucketName,
            Key = S3AnimationKey,
            Verb = HttpVerb.PUT, // This should be a PUT request for LeiaPix to upload the image
            Expires = DateTime.UtcNow.AddMinutes(60) // The URL will be valid for 60 minutes
        };

        string presignedUrl = s3Client.GetPreSignedURL(request);

        Debug.Log("Generated Animation presigned URL: " + presignedUrl);
        debugText.text = "Generated Animation presigned URL: " + presignedUrl;

        audioPlay.PlayVideoSpeech();

        return presignedUrl;
    }

    private IEnumerator DownloadAnimation()
    {
        using (UnityWebRequest uwr = new UnityWebRequest(animationURL))
        {
            uwr.downloadHandler = new DownloadHandlerBuffer(); // Set up the download handler

            yield return uwr.SendWebRequest(); // Send the request

            if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.Log(uwr.error);
                debugText.text = "Error: " + uwr.error;
            }
            else
            {
                SaveVideo(uwr.downloadHandler.data, videoFilePath);
                debugText.text = "Animation Downloaded";
            }
        }
    }

    private void SaveVideo(byte[] videoData, string videoFilePath)
    {
        // Combine the persistent data path with the relative file path
        string fullPath = Path.Combine(Application.persistentDataPath, videoFilePath);

        // Ensure the directory exists before trying to write the file
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

        try
        {
            File.WriteAllBytes(fullPath, videoData);

            Debug.Log("Animation Video saved at: " + fullPath);
            debugText.text = "Animation Video saved at: " + fullPath;

            PlayVideo(fullPath);

        }
        catch (Exception ex)
        {
            Debug.LogError("Error saving video: " + ex.Message);
            debugText.text = "Error saving video: " + ex.Message;
        }
    }

    private void PlayVideo(string videoPath)
    {
        animationPlayer.url = videoPath;
        animationPlayer.Prepare();
        
        audioPlay.PlayVideoShow();

        Debug.Log("Video is ready to play, please wait...");

        // When the video is prepared, play it.
        animationPlayer.prepareCompleted += (source) => {
            source.Play();
        };

        normalMat.SetActive(false);
        videoGO.SetActive(true);
    }

}


[System.Serializable]
public class AccessTokenResponse
{
    public string access_token;
    public int expires_in;
}

[System.Serializable]
public class DisparityRequest
{
    public string correlationId;
    public string inputImageUrl;
    public string resultPresignedUrl;
    public float dilation;
}

[System.Serializable]
public class CustomDisparityRequest
{
    public string correlationId;
    public string inputImageUrl;
    public string inputDisparityUrl;
    public string resultPresignedUrl;
    public int convergence;
    public int animationLength;
    public float phaseX;
    public float phaseY;
    public float phaseZ;
    public float amplitudeX;
    public float amplitudeY;
    public float amplitudeZ;
}
