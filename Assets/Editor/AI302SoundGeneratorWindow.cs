using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public sealed class AI302SoundGeneratorWindow : EditorWindow
{
    private const string ApiKeyEnvironmentVariable = "AI302_API_KEY";
    private const string Endpoint = "https://api.302.ai/elevenlabs/sound-generation";
    private const string DefaultModelId = "eleven_text_to_sound_v2";

    [Serializable]
    private sealed class SoundGenerationRequest
    {
        public string text;
        public bool loop;
        public float duration_seconds;
        public float prompt_influence;
        public string model_id;
    }

    [Serializable]
    private sealed class SoundGenerationResponse
    {
        public string url;
    }

    private string prompt =
        "A clean arcade game UI confirmation sound, short, punchy, no music, no voice";
    private bool loop;
    private float durationSeconds = 2f;
    private float promptInfluence = 0.3f;
    private string modelId = DefaultModelId;
    private string outputFolder = "Assets/Audio/Generated";
    private string outputFileName = "SFX_Generated";
    private string statusMessage = "Ready.";
    private MessageType statusType = MessageType.Info;
    private UnityWebRequest activeRequest;

    [MenuItem("Tools/Island Map/Generate Sound Effect (302.AI)")]
    public static void Open()
    {
        AI302SoundGeneratorWindow window = GetWindow<AI302SoundGeneratorWindow>();
        window.titleContent = new GUIContent("302.AI Sound");
        window.minSize = new Vector2(480f, 430f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("ElevenLabs Sound Generation via 302.AI", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Prompt");
        prompt = EditorGUILayout.TextArea(prompt, GUILayout.MinHeight(90f));
        loop = EditorGUILayout.Toggle("Seamless Loop", loop);
        durationSeconds = EditorGUILayout.Slider("Duration (seconds)", durationSeconds, 0.5f, 30f);
        promptInfluence = EditorGUILayout.Slider("Prompt Influence", promptInfluence, 0f, 1f);
        modelId = EditorGUILayout.TextField("Model ID", modelId);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        outputFolder = EditorGUILayout.TextField("Asset Folder", outputFolder);
        outputFileName = EditorGUILayout.TextField("File Name", outputFileName);

        EditorGUILayout.Space();
        bool hasApiKey = !string.IsNullOrWhiteSpace(GetApiKey());
        if (!hasApiKey)
        {
            EditorGUILayout.HelpBox(
                $"Set the {ApiKeyEnvironmentVariable} environment variable and restart Unity.",
                MessageType.Warning);
        }

        EditorGUILayout.HelpBox(statusMessage, statusType);

        using (new EditorGUI.DisabledScope(
                   activeRequest != null || !hasApiKey || string.IsNullOrWhiteSpace(prompt)))
        {
            if (GUILayout.Button(activeRequest == null ? "Generate and Import" : "Generating...", GUILayout.Height(34f)))
            {
                StartGeneration();
            }
        }
    }

    private void OnDisable()
    {
        if (activeRequest == null)
        {
            return;
        }

        activeRequest.Abort();
        activeRequest.Dispose();
        activeRequest = null;
    }

    private void StartGeneration()
    {
        if (!TryResolveOutputDirectory(out string normalizedFolder, out string absoluteFolder))
        {
            SetStatus("The output folder must be inside the project's Assets directory.", MessageType.Error);
            return;
        }

        string apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            SetStatus($"Missing {ApiKeyEnvironmentVariable} environment variable.", MessageType.Error);
            return;
        }

        Directory.CreateDirectory(absoluteFolder);
        AssetDatabase.Refresh();

        SoundGenerationRequest requestBody = new SoundGenerationRequest
        {
            text = prompt.Trim(),
            loop = loop,
            duration_seconds = durationSeconds,
            prompt_influence = promptInfluence,
            model_id = string.IsNullOrWhiteSpace(modelId) ? DefaultModelId : modelId.Trim()
        };

        byte[] jsonBytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(requestBody));
        activeRequest = new UnityWebRequest(Endpoint, UnityWebRequest.kHttpVerbPOST)
        {
            uploadHandler = new UploadHandlerRaw(jsonBytes),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout = 90
        };
        activeRequest.SetRequestHeader("Authorization", $"Bearer {apiKey}");
        activeRequest.SetRequestHeader("Content-Type", "application/json");
        activeRequest.SetRequestHeader("Accept", "audio/mpeg, audio/wav, audio/ogg, application/octet-stream");

        statusMessage = "Generating sound effect...";
        statusType = MessageType.Info;
        Repaint();

        UnityWebRequestAsyncOperation operation = activeRequest.SendWebRequest();
        operation.completed += _ => FinishGeneration(normalizedFolder);
    }

    private void FinishGeneration(string normalizedFolder)
    {
        if (activeRequest == null)
        {
            return;
        }

        UnityWebRequest completedRequest = activeRequest;
        activeRequest = null;

        try
        {
            if (completedRequest.result != UnityWebRequest.Result.Success)
            {
                string responseText = GetReadableResponse(completedRequest.downloadHandler.data);
                SetStatus(
                    $"302.AI request failed ({completedRequest.responseCode}): {responseText}",
                    MessageType.Error);
                return;
            }

            byte[] audioBytes = completedRequest.downloadHandler.data;
            if (TryDetectAudioExtension(
                    completedRequest.GetResponseHeader("Content-Type"),
                    audioBytes,
                    out string extension))
            {
                ImportAudio(normalizedFolder, extension, audioBytes);
                return;
            }

            if (TryGetDownloadUrl(audioBytes, out string downloadUrl))
            {
                StartAudioDownload(downloadUrl, normalizedFolder);
                return;
            }

            SetStatus(
                "302.AI returned a successful response, but it contained neither audio nor a " +
                "recognized download URL: " + GetReadableResponse(audioBytes),
                MessageType.Error);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SetStatus($"Could not import the generated audio: {exception.Message}", MessageType.Error);
        }
        finally
        {
            completedRequest.Dispose();
            Repaint();
        }
    }

    private void StartAudioDownload(string downloadUrl, string normalizedFolder)
    {
        activeRequest = UnityWebRequest.Get(downloadUrl);
        activeRequest.timeout = 120;
        activeRequest.SetRequestHeader(
            "Accept",
            "audio/mpeg, audio/wav, audio/ogg, application/octet-stream");

        statusMessage = "Sound generated. Downloading audio...";
        statusType = MessageType.Info;
        Repaint();

        UnityWebRequestAsyncOperation operation = activeRequest.SendWebRequest();
        operation.completed += _ => FinishAudioDownload(normalizedFolder);
    }

    private void FinishAudioDownload(string normalizedFolder)
    {
        if (activeRequest == null)
        {
            return;
        }

        UnityWebRequest completedRequest = activeRequest;
        activeRequest = null;

        try
        {
            if (completedRequest.result != UnityWebRequest.Result.Success)
            {
                SetStatus(
                    $"Generated audio download failed ({completedRequest.responseCode}): " +
                    GetReadableResponse(completedRequest.downloadHandler.data),
                    MessageType.Error);
                return;
            }

            byte[] audioBytes = completedRequest.downloadHandler.data;
            if (!TryDetectAudioExtension(
                    completedRequest.GetResponseHeader("Content-Type"),
                    audioBytes,
                    out string extension))
            {
                SetStatus("The downloaded file was not recognized as audio.", MessageType.Error);
                return;
            }

            ImportAudio(normalizedFolder, extension, audioBytes);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SetStatus($"Could not import the generated audio: {exception.Message}", MessageType.Error);
        }
        finally
        {
            completedRequest.Dispose();
            Repaint();
        }
    }

    private void ImportAudio(string normalizedFolder, string extension, byte[] audioBytes)
    {
        string safeFileName = SanitizeFileName(outputFileName);
        string requestedPath = $"{normalizedFolder}/{safeFileName}{extension}";
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(requestedPath);
        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        if (string.IsNullOrEmpty(projectRoot))
        {
            throw new InvalidOperationException("Could not resolve the Unity project directory.");
        }

        string absolutePath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        File.WriteAllBytes(absolutePath, audioBytes);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

        AudioClip generatedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        Selection.activeObject = generatedClip;
        EditorGUIUtility.PingObject(generatedClip);
        SetStatus($"Generated and imported {assetPath}", MessageType.Info);
    }

    private bool TryResolveOutputDirectory(out string normalizedFolder, out string absoluteFolder)
    {
        normalizedFolder = (outputFolder ?? string.Empty).Trim().Replace('\\', '/').TrimEnd('/');
        absoluteFolder = string.Empty;

        if (!normalizedFolder.Equals("Assets", StringComparison.Ordinal) &&
            !normalizedFolder.StartsWith("Assets/", StringComparison.Ordinal))
        {
            return false;
        }

        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        if (string.IsNullOrEmpty(projectRoot))
        {
            return false;
        }

        absoluteFolder = Path.GetFullPath(Path.Combine(projectRoot, normalizedFolder));
        string assetsRoot = Path.GetFullPath(Application.dataPath);
        return absoluteFolder.Equals(assetsRoot, StringComparison.OrdinalIgnoreCase) ||
               absoluteFolder.StartsWith(
                   assetsRoot + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string GetApiKey()
    {
        string apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
#if UNITY_EDITOR_WIN
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = Environment.GetEnvironmentVariable(
                ApiKeyEnvironmentVariable,
                EnvironmentVariableTarget.User);
        }
#endif
        return apiKey == null ? string.Empty : apiKey.Trim();
    }

    private static bool TryDetectAudioExtension(
        string contentType,
        byte[] data,
        out string extension)
    {
        extension = string.Empty;
        if (data == null || data.Length < 4)
        {
            return false;
        }

        if (StartsWithAscii(data, "RIFF"))
        {
            extension = ".wav";
            return true;
        }

        if (StartsWithAscii(data, "OggS"))
        {
            extension = ".ogg";
            return true;
        }

        bool hasId3Header = data.Length >= 3 && data[0] == 'I' && data[1] == 'D' && data[2] == '3';
        bool hasMpegFrameHeader = data[0] == 0xFF && (data[1] & 0xE0) == 0xE0;
        if (hasId3Header || hasMpegFrameHeader ||
            (!string.IsNullOrEmpty(contentType) &&
             contentType.IndexOf("audio/mpeg", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            extension = ".mp3";
            return true;
        }

        return false;
    }

    private static bool TryGetDownloadUrl(byte[] data, out string downloadUrl)
    {
        downloadUrl = string.Empty;
        if (data == null || data.Length == 0)
        {
            return false;
        }

        SoundGenerationResponse response;
        try
        {
            response = JsonUtility.FromJson<SoundGenerationResponse>(Encoding.UTF8.GetString(data));
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (response == null || string.IsNullOrWhiteSpace(response.url) ||
            !Uri.TryCreate(response.url, UriKind.Absolute, out Uri uri))
        {
            return false;
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !uri.Host.Equals("file.302ai.cn", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        downloadUrl = uri.AbsoluteUri;
        return true;
    }

    private static bool StartsWithAscii(byte[] data, string value)
    {
        if (data.Length < value.Length)
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (data[index] != value[index])
            {
                return false;
            }
        }

        return true;
    }

    private static string SanitizeFileName(string fileName)
    {
        string sanitized = string.IsNullOrWhiteSpace(fileName) ? "SFX_Generated" : fileName.Trim();
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalidCharacter, '_');
        }

        return sanitized;
    }

    private static string GetReadableResponse(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            return "No response body.";
        }

        string response = Encoding.UTF8.GetString(data).Trim();
        const int MaximumLength = 600;
        return response.Length <= MaximumLength
            ? response
            : response.Substring(0, MaximumLength) + "...";
    }

    private void SetStatus(string message, MessageType type)
    {
        statusMessage = message;
        statusType = type;
        Repaint();

        if (type == MessageType.Error)
        {
            Debug.LogError(message);
        }
        else
        {
            Debug.Log(message);
        }
    }
}
