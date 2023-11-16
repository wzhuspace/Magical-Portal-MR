using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Collections;
using TMPro;
using System.IO;
using System;
using OpenAI;

public class ImageGen : MonoBehaviour
{

    private const string API_KEY = "LEONARDO.AI API";
    private const string MODEL_ID = "6bef9f1b-29cb-40c7-b9df-32b51c1f67d3";
    private const string API_URL = "https://cloud.leonardo.ai/api/rest/v1/generations";

    // Whisper
    private string OPENAI_KEY = "OPENAI API";
    private AudioClip clip;
    private byte[] bytes;
    private bool recording;
    private string transResult;
    private string fineTunedPrompt;

    private int MAX_WAIT_TIME = 10;

    public AudioController audioController;

    // Inspector References
    [SerializeField] private TextMeshProUGUI speechText;
    [SerializeField] private TextMeshProUGUI imgdebugText;

    //  Notify subscribed methods, providing them with a single string parameter, whenever the event is triggered.
    public event Action<string> ImageURLGenerated;

    public Material imgMat;
    public Texture2D black;

    private float fadeDuration = 5.0f;
    // private float smoothness = 1.0f;

    private string imgFilePath;

    void Awake()
    {

        string directoryPath = Path.Combine(Application.persistentDataPath, "_GenImages");

        // Ensure the "_GenImages" directory exists.
        if (Directory.Exists(directoryPath))
        {
            // Get all files in the directory.
            string[] files = Directory.GetFiles(directoryPath);

            // Loop through each file in the directory.
            foreach (string file in files)
            {
                // Delete each file.
                File.Delete(file);

                imgdebugText.text = "Exiting images and videos are all cleared.";
            }
        }

        else
        {
            // If the directory does not exist, create it.
            Directory.CreateDirectory(directoryPath);
        }

        imgFilePath = Path.Combine("_GenImages", "GeneratedImage.png");  // Set the imgFilePath after cleaning or creating the directory.

        imgMat.SetTexture("_BaseMap", black);
        SetMatSmooth(1.0f);

    }

    private void SetMatSmooth(float smoothness)
    {
        imgMat.SetFloat("_Smoothness", smoothness);
    }

    public void StartRecording()
    {
        audioController.PlayPickUp();

        clip = Microphone.Start(null, false, 20, 44100); // audio length max = 20 secs, 44100 Hz
        recording = true;

        imgdebugText.text = "Start Recording...";
    }

    public void StopRecording()
    {
        var position = Microphone.GetPosition(null);

        Microphone.End(null);

        var samples = new float[position * clip.channels];

        clip.GetData(samples, 0);

        bytes = EncodeAsWAV(samples, clip.frequency, clip.channels);

        recording = false;

        SendRecording();

        imgdebugText.text = "Stop Recording...";

        StartCoroutine(FadetoZero());

        // StartCoroutine(SendRecording());
    }

    private byte[] EncodeAsWAV(float[] samples, int frequency, int channels)
    {
        using (var memoryStream = new MemoryStream(44 + samples.Length * 2))
        {
            using (var writer = new BinaryWriter(memoryStream))
            {
                writer.Write("RIFF".ToCharArray());
                writer.Write(36 + samples.Length * 2);
                writer.Write("WAVE".ToCharArray());
                writer.Write("fmt ".ToCharArray());
                writer.Write(16);
                writer.Write((ushort)1);
                writer.Write((ushort)channels);
                writer.Write(frequency);
                writer.Write(frequency * channels * 2);
                writer.Write((ushort)(channels * 2));
                writer.Write((ushort)16);
                writer.Write("data".ToCharArray());
                writer.Write(samples.Length * 2);

                foreach (var sample in samples)
                {
                    writer.Write((short)(sample * short.MaxValue));
                }
            }

            return memoryStream.ToArray();
        }
    }
    private IEnumerator FadetoZero()
    {
        float currentTime = 0f;

        while (currentTime < fadeDuration)
        {
            float newSmoothness = Mathf.Lerp(1.0f, 0f, currentTime / fadeDuration);
            SetMatSmooth(newSmoothness);

            currentTime += Time.deltaTime;

            yield return null;

        }

        SetMatSmooth(0f);
    }

    private async void SendRecording()
    {
        var open_AI = new OpenAIApi(OPENAI_KEY);

        imgdebugText.text = "Sending Recording...";

        var requestCN = new CreateAudioTranslationRequest
        {
            FileData = new FileData() { Data = bytes, Name = "audio.wav" },
            Model = "whisper-1",
        };

        var result = await open_AI.CreateAudioTranslation(requestCN);

        transResult = result.Text;

        StartCoroutine(GenerateImage());
    }


    private IEnumerator GenerateImage()
    {
        speechText.text = transResult;

        fineTunedPrompt = transResult +
                          "craft a mesmerizing image depicting a foreboding, apocalyptic scene where a dangerous, " +
                          "cloudy, and spooky environment engulfs the earth on its final day, embrace the juxtaposition of chaos and beauty as shimmering, " +
                          "the brilliance of octane render to create a surreal composition that captivates the viewer's imagination, elevate the visual impact with stunning photography elements";

        imgdebugText.text = "Generating Image...";

        using (UnityWebRequest webRequest = new UnityWebRequest(API_URL, "POST"))
        {

            // Set headers
            webRequest.SetRequestHeader("accept", "application/json");
            webRequest.SetRequestHeader("Authorization", "Bearer " + API_KEY);

            // Create a new instance of ImageRequest to specify the parameters for the image generation.
            ImageRequest requestData = new ImageRequest
            {
                height = 1024,
                modelId = MODEL_ID,
                prompt = fineTunedPrompt,
                width = 1024,
                presetStyle = "LEONARDO",
                promptMagic = true,
                promptMagicVersion = "v2",
                num_images = 1,
                highResolution = true,
                guidance_scale = 5,
                negative_prompt =
                "Out of frame, error, cropped, low quality, deformed, blurred, ugly, " +
                "duplicate, mutilated, mutation, disfigured, gross proportions, bad anatomy, " +
                "bad proportions, bad posture, malformed limbs, extra limbs, extra body parts, long neck , " +
                "poorly drawn face, cloned face, open mouth, animal ears, double ears on one side, extra ear, extra nose, " +
                "extra tongue, extra eyes, closed eyes, deformed eyes, more than two legs, abnormal leg, missing legs, extra legs, " +
                "more than two arms, missing arms, extra arms, more than two hands, mutated hands, poorly drawn hands, abnormal hand, extra fingers, " +
                "fused fingers, too many fingers, abnormal fingers, more than five fingers on one hand, feet deformed, more than two feet, username, " +
                "watermark, signature, text, purse, wallet, bag."
            };

            // Convert the ImageRequest instance to a JSON string.
            string jsonBody = JsonUtility.ToJson(requestData);

            // Convert the JSON string into bytes for transmission.
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

            // Set the request body using an upload handler.
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);

            // Set a download handler to retrieve data from the server's response.
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            // Specify the content type of the request to be JSON.
            webRequest.SetRequestHeader("Content-Type", "application/json");

            // Send the web request and pause the coroutine until the request completes.
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.ConnectionError)
            {
                Debug.LogError("Error: " + webRequest.error);
            }

            else
            {
                Debug.Log("Received: " + webRequest.downloadHandler.text);

                GenerationJobResponse generationJobResponse = JsonUtility.FromJson<GenerationJobResponse>(webRequest.downloadHandler.text);

                string generationId = generationJobResponse.sdGenerationJob.generationId;

                imgdebugText.text = "Image starting to Generate...";

                yield return StartCoroutine(CheckGenerationStatus(generationId));
            }
        }
    }


    // The GenerateImage Coroutine will post a generation task and then we need to get the id and download it to local storage.
    private IEnumerator CheckGenerationStatus(string generationID)
    {
        int elapsedTime = 0;
        bool isComplete = false;

        // Wait for 10 seconds first, then check if the image generation is complete every 2 seconds.
        while (elapsedTime < MAX_WAIT_TIME && !isComplete)
        {

            Debug.Log("Checking image generation status...");
            // speechText.text = "Please be patient... The portal is taking you to the place...";

            yield return new WaitForSeconds(2);  // Wait for 2 secs before checking again.
            elapsedTime += 2;

            string detailsURL = API_URL + "/" + generationID;

            using (UnityWebRequest detailRequest = UnityWebRequest.Get(detailsURL))
            {
                detailRequest.SetRequestHeader("accept", "application/json");
                detailRequest.SetRequestHeader("Authorization", "Bearer " + API_KEY);

                yield return detailRequest.SendWebRequest();

                Debug.Log("Image generation initiated. Please wait...");

                imgdebugText.text = "Image generation initiated. Please wait....";


                if (detailRequest.result == UnityWebRequest.Result.ConnectionError)
                {
                    Debug.LogError("Error: " + detailRequest.error);
                }
                else
                {
                    GenerationDetailResponse detailResponse = JsonUtility.FromJson<GenerationDetailResponse>(detailRequest.downloadHandler.text);

                    if (detailResponse.generations_by_pk.generated_images.Length > 0)
                    {
                        isComplete = true;
                        string imageUrl = detailResponse.generations_by_pk.generated_images[0].url;
                        Debug.Log("Image generated: " + imageUrl);

                        imgdebugText.text = "Image generated: " + imageUrl;

                        StartCoroutine(LoadAndDisplayImage(imageUrl));  // Load and display the image

                        yield break;
                    }
                }
            }
        }

        if (!isComplete)
        {
            Debug.Log("Image generation took too long or there was an error.");
            imgdebugText.text = "Image generation took too long or there was an error.";
        }
    }

    private IEnumerator LoadAndDisplayImage(string imageUrl)
    {
        using (UnityWebRequest imageRequest = UnityWebRequestTexture.GetTexture(imageUrl))
        {
            Debug.Log("Attempting to load image from: " + imageUrl);

            yield return imageRequest.SendWebRequest();

            if (imageRequest.result == UnityWebRequest.Result.ConnectionError || imageRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError("Error loading image: " + imageRequest.error);
            }
            else
            {
                Debug.Log("Image loaded successfully.");
                imgdebugText.text = "Here is the final Image";

                Texture2D texture = ((DownloadHandlerTexture)imageRequest.downloadHandler).texture;
                SaveTextureToFile(texture);
                // AssignTextureToMaterial(texture);

                ImageURLGenerated?.Invoke(imageUrl);

            }
        }
    }

    private void SaveTextureToFile(Texture2D texture)
    {
        byte[] imageData = texture.EncodeToPNG();

        // Combine the persistent data path with the relative file path
        string fullPath = Path.Combine(Application.persistentDataPath, imgFilePath);

        // Ensure the directory exists before trying to write the file
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

        File.WriteAllBytes(fullPath, imageData);

        Debug.Log("Original Image saved to: " + fullPath);
        imgdebugText.text = "Original Image saved to: " + fullPath;
    }


    /*private void AssignTextureToMaterial(Texture2D downloadedTexture)
    {
        imgMat.SetTexture("_BaseMap", downloadedTexture);
        imgMat.SetTexture("_EmissionMap", downloadedTexture);

        imgdebugText.text = "Texture assigned to _BaseMap.";
    }*/


    [System.Serializable]
    public class ImageRequest
    {
        public int height;
        public string modelId;
        public string prompt;
        public int width;
        public string presetStyle;
        public bool promptMagic;
        public string promptMagicVersion;
        public int num_images;
        public bool highResolution;
        public int guidance_scale;
        public string negative_prompt;
    }

    [System.Serializable]
    public class GenerationJobResponse
    {
        public SdGenerationJob sdGenerationJob;
    }

    [System.Serializable]
    public class SdGenerationJob
    {
        public string generationId;
    }

    [System.Serializable]
    public class GenerationDetailResponse
    {
        public GenerationsByPk generations_by_pk;
    }

    [System.Serializable]
    public class GenerationsByPk
    {
        public GeneratedImage[] generated_images;
        public string status;
    }

    [System.Serializable]
    public class GeneratedImage
    {
        public string url;
    }
}
