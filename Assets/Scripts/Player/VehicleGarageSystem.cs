using System.Collections.Generic;
using UnityEngine;

public sealed class VehicleGarageSystem : MonoBehaviour
{
    private sealed class VehicleDefinition
    {
        public string Id;
        public string DisplayName;
        public string ResourcePath;

        public bool IsDefault => string.IsNullOrEmpty(ResourcePath);
    }

    private const string SelectedVehicleKey = "Garage.SelectedVehicleId";
    private const string DefaultVehicleId = "default_car";
    private const int PreviewLayer = 31;
    private const float PreviewTargetSize = 5.5f;
    private const float AutomaticRotationSpeed = 20f;
    private const float DragSensitivity = 0.35f;

    private static readonly VehicleDefinition[] Vehicles =
    {
        new VehicleDefinition
        {
            Id = DefaultVehicleId,
            DisplayName = "经典座驾",
            ResourcePath = string.Empty
        },
        new VehicleDefinition
        {
            Id = "garage_car_02",
            DisplayName = "Porsche 911 GT3 RS",
            ResourcePath = "Vehicles/GarageCar02/Model"
        },
        new VehicleDefinition
        {
            Id = "garage_car_03",
            DisplayName = "BMW M4",
            ResourcePath = "Vehicles/GarageCar03/Model"
        },
        new VehicleDefinition
        {
            Id = "garage_car_04",
            DisplayName = "Nissan GTR",
            ResourcePath = "Vehicles/GarageCar04/Model"
        },
        new VehicleDefinition
        {
            Id = "garage_car_05",
            DisplayName = "Ferrari F40",
            ResourcePath = "Vehicles/GarageCar05/Model"
        }
    };

    private static readonly Dictionary<string, Material> RuntimeMaterials = new Dictionary<string, Material>();

    private GameObject defaultVisual;
    private GameObject appliedAlternateVisual;
    private GameObject previewRoot;
    private GameObject previewPivot;
    private GameObject previewVisual;
    private Camera previewCamera;
    private RenderTexture previewTexture;
    private bool isOpen;
    private bool wasAudioPaused;
    private bool draggingPreview;
    private float previousTimeScale;
    private float previewYaw = 25f;
    private float automaticRotationResumeTime;
    private int previewIndex;
    private string equippedVehicleId;
    private string statusMessage;
    private float statusMessageUntil;
    private float defaultHorizontalSize;
    private float defaultMinimumY;
    private GUIStyle titleStyle;
    private GUIStyle headingStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;

    public static VehicleGarageSystem Instance { get; private set; }
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        defaultVisual = FindDefaultVisual();
        if (defaultVisual == null)
        {
            Debug.LogWarning("Vehicle garage could not find the player's default visual. The default car will remain unchanged.", this);
            equippedVehicleId = DefaultVehicleId;
            return;
        }

        Bounds defaultBounds = CalculateLocalBounds(defaultVisual.transform, transform);
        defaultHorizontalSize = Mathf.Max(defaultBounds.size.x, defaultBounds.size.z);
        defaultMinimumY = defaultBounds.min.y;
        equippedVehicleId = ReadValidSavedVehicleId();
        ApplyEquippedVehicle();
    }

    private void Update()
    {
        if (!isOpen)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseGarage();
            return;
        }
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            ChangePreview(-1);
        }
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            ChangePreview(1);
        }
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
        {
            EquipPreviewVehicle();
        }

        if (!draggingPreview && Time.unscaledTime >= automaticRotationResumeTime)
        {
            previewYaw = Mathf.Repeat(previewYaw + AutomaticRotationSpeed * Time.unscaledDeltaTime, 360f);
        }
        if (previewPivot != null)
        {
            previewPivot.transform.localRotation = Quaternion.Euler(0f, previewYaw, 0f);
        }
    }

    public void OpenGarage()
    {
        if (isOpen || defaultVisual == null)
        {
            return;
        }

        previousTimeScale = Time.timeScale;
        wasAudioPaused = AudioListener.pause;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        isOpen = true;
        previewIndex = GetVehicleIndex(equippedVehicleId);
        CreatePreviewResources();
        RefreshPreviewVehicle();
    }

    public void CloseGarage()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        draggingPreview = false;
        CleanupPreviewResources();
        Time.timeScale = previousTimeScale;
        AudioListener.pause = wasAudioPaused;
    }

    private string ReadValidSavedVehicleId()
    {
        string savedId = PlayerPrefs.GetString(SelectedVehicleKey, DefaultVehicleId);
        int savedIndex = GetVehicleIndex(savedId);
        if (savedIndex < 0 || !CanLoadVehicle(Vehicles[savedIndex]))
        {
            SaveVehicleId(DefaultVehicleId);
            return DefaultVehicleId;
        }

        if (!PlayerPrefs.HasKey(SelectedVehicleKey))
        {
            SaveVehicleId(DefaultVehicleId);
        }
        return savedId;
    }

    private static int GetVehicleIndex(string vehicleId)
    {
        for (int index = 0; index < Vehicles.Length; index++)
        {
            if (Vehicles[index].Id == vehicleId)
            {
                return index;
            }
        }
        return -1;
    }

    private static bool CanLoadVehicle(VehicleDefinition definition)
    {
        return definition.IsDefault || Resources.Load<GameObject>(definition.ResourcePath) != null;
    }

    private static void SaveVehicleId(string vehicleId)
    {
        PlayerPrefs.SetString(SelectedVehicleKey, vehicleId);
        PlayerPrefs.Save();
    }

    private void ApplyEquippedVehicle()
    {
        if (defaultVisual == null)
        {
            return;
        }

        if (appliedAlternateVisual != null)
        {
            appliedAlternateVisual.SetActive(false);
            Destroy(appliedAlternateVisual);
            appliedAlternateVisual = null;
        }

        int definitionIndex = GetVehicleIndex(equippedVehicleId);
        VehicleDefinition definition = definitionIndex >= 0 ? Vehicles[definitionIndex] : Vehicles[0];
        if (definition.IsDefault)
        {
            defaultVisual.SetActive(true);
            return;
        }

        GameObject source = Resources.Load<GameObject>(definition.ResourcePath);
        if (source == null)
        {
            equippedVehicleId = DefaultVehicleId;
            SaveVehicleId(equippedVehicleId);
            defaultVisual.SetActive(true);
            return;
        }

        defaultVisual.SetActive(false);
        appliedAlternateVisual = Instantiate(source, transform);
        appliedAlternateVisual.name = "GarageVehicleVisual_" + definition.Id;
        PrepareVisual(appliedAlternateVisual, definition, transform, defaultHorizontalSize, defaultMinimumY);
    }

    private GameObject FindDefaultVisual()
    {
        Transform namedVisual = transform.Find("Visual_Sedan");
        if (namedVisual != null)
        {
            return namedVisual.gameObject;
        }

        foreach (Transform child in transform)
        {
            if (child.GetComponentInChildren<Renderer>(true) != null)
            {
                return child.gameObject;
            }
        }
        return null;
    }

    private void CreatePreviewResources()
    {
        CleanupPreviewResources();
        previewRoot = new GameObject("GARAGE_PreviewRoot");
        previewRoot.transform.position = new Vector3(10000f, 10000f, 10000f);

        previewPivot = new GameObject("GARAGE_PreviewPivot");
        previewPivot.transform.SetParent(previewRoot.transform, false);

        GameObject cameraObject = new GameObject("GARAGE_PreviewCamera");
        cameraObject.transform.SetParent(previewRoot.transform, false);
        cameraObject.transform.localPosition = new Vector3(4.8f, 3.2f, -8.2f);
        cameraObject.transform.LookAt(previewRoot.transform.position + Vector3.up * 1.15f);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.018f, 0.035f, 0.065f, 1f);
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.fieldOfView = 34f;

        GameObject lightObject = new GameObject("GARAGE_KeyLight");
        lightObject.transform.SetParent(previewRoot.transform, false);
        lightObject.transform.localRotation = Quaternion.Euler(38f, -32f, 0f);
        Light keyLight = lightObject.AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.intensity = 1.45f;
        keyLight.color = new Color(1f, 0.93f, 0.82f);
        keyLight.cullingMask = 1 << PreviewLayer;

        GameObject fillLightObject = new GameObject("GARAGE_FillLight");
        fillLightObject.transform.SetParent(previewRoot.transform, false);
        fillLightObject.transform.localRotation = Quaternion.Euler(25f, 150f, 0f);
        Light fillLight = fillLightObject.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.intensity = 0.75f;
        fillLight.color = new Color(0.28f, 0.62f, 1f);
        fillLight.cullingMask = 1 << PreviewLayer;

        previewTexture = new RenderTexture(1024, 600, 24, RenderTextureFormat.ARGB32)
        {
            name = "GARAGE_VehiclePreview",
            antiAliasing = 4
        };
        previewTexture.Create();
        previewCamera.targetTexture = previewTexture;
    }

    private void RefreshPreviewVehicle()
    {
        if (previewPivot == null)
        {
            return;
        }

        if (previewVisual != null)
        {
            previewVisual.SetActive(false);
            Destroy(previewVisual);
        }

        VehicleDefinition definition = Vehicles[previewIndex];
        GameObject source = definition.IsDefault
            ? defaultVisual
            : Resources.Load<GameObject>(definition.ResourcePath);
        if (source == null)
        {
            previewIndex = 0;
            definition = Vehicles[0];
            source = defaultVisual;
        }

        previewVisual = Instantiate(source, previewPivot.transform);
        previewVisual.name = "GARAGE_Preview_" + definition.Id;
        previewVisual.SetActive(true);
        PrepareVisual(previewVisual, definition, previewPivot.transform, PreviewTargetSize, 0f);
        SetLayerRecursively(previewVisual.transform, PreviewLayer);
        previewYaw = 25f;
        automaticRotationResumeTime = Time.unscaledTime + 1f;
    }

    private static void PrepareVisual(
        GameObject visual,
        VehicleDefinition definition,
        Transform relativeTo,
        float targetHorizontalSize,
        float targetMinimumY)
    {
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        DisableGameplayComponents(visual);

        Bounds initialBounds = CalculateLocalBounds(visual.transform, relativeTo);
        float initialHorizontalSize = Mathf.Max(initialBounds.size.x, initialBounds.size.z);
        float scaleFactor = targetHorizontalSize / Mathf.Max(initialHorizontalSize, 0.01f);
        visual.transform.localScale *= scaleFactor;

        Bounds fittedBounds = CalculateLocalBounds(visual.transform, relativeTo);
        visual.transform.localPosition += new Vector3(
            -fittedBounds.center.x,
            targetMinimumY - fittedBounds.min.y,
            -fittedBounds.center.z);
        AssignRuntimeMaterial(visual, definition);
    }

    private static void DisableGameplayComponents(GameObject visual)
    {
        foreach (Collider visualCollider in visual.GetComponentsInChildren<Collider>(true))
        {
            visualCollider.enabled = false;
            Destroy(visualCollider);
        }
        foreach (Rigidbody visualBody in visual.GetComponentsInChildren<Rigidbody>(true))
        {
            visualBody.isKinematic = true;
            Destroy(visualBody);
        }
        foreach (MonoBehaviour behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
            Destroy(behaviour);
        }
    }

    private static void AssignRuntimeMaterial(GameObject visual, VehicleDefinition definition)
    {
        if (definition.IsDefault)
        {
            return;
        }

        if (!RuntimeMaterials.TryGetValue(definition.Id, out Material material) || material == null)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                return;
            }

            string directory = definition.ResourcePath.Substring(0, definition.ResourcePath.LastIndexOf('/'));
            material = new Material(shader) { name = "MAT_Garage_" + definition.Id };
            Texture2D albedo = Resources.Load<Texture2D>(directory + "/texture_pbr_20250901");
            Texture2D metallic = Resources.Load<Texture2D>(directory + "/texture_pbr_20250901_metallic");
            Texture2D normal = Resources.Load<Texture2D>(directory + "/texture_pbr_20250901_normal");
            if (albedo != null)
            {
                material.mainTexture = albedo;
            }
            if (metallic != null)
            {
                material.EnableKeyword("_METALLICGLOSSMAP");
                material.SetTexture("_MetallicGlossMap", metallic);
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_GlossMapScale", 0.48f);
            }
            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
            }
            RuntimeMaterials[definition.Id] = material;
        }

        foreach (Renderer visualRenderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            visualRenderer.sharedMaterial = material;
        }
    }

    private static Bounds CalculateLocalBounds(Transform visual, Transform relativeTo)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one);
        }

        bool initialized = false;
        Bounds localBounds = new Bounds();
        foreach (Renderer visualRenderer in renderers)
        {
            Bounds worldBounds = visualRenderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
            };
            foreach (Vector3 corner in corners)
            {
                Vector3 localCorner = relativeTo.InverseTransformPoint(corner);
                if (!initialized)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    localBounds.Encapsulate(localCorner);
                }
            }
        }
        return localBounds;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        foreach (Transform child in root)
        {
            SetLayerRecursively(child, layer);
        }
    }

    private void ChangePreview(int direction)
    {
        previewIndex = (previewIndex + direction + Vehicles.Length) % Vehicles.Length;
        statusMessage = string.Empty;
        RefreshPreviewVehicle();
    }

    private void EquipPreviewVehicle()
    {
        VehicleDefinition definition = Vehicles[previewIndex];
        if (definition.Id == equippedVehicleId)
        {
            return;
        }

        if (!CanLoadVehicle(definition))
        {
            previewIndex = 0;
            definition = Vehicles[0];
        }

        equippedVehicleId = definition.Id;
        SaveVehicleId(equippedVehicleId);
        ApplyEquippedVehicle();
        statusMessage = "已设为当前车辆";
        statusMessageUntil = Time.unscaledTime + 2f;
    }

    private void OnGUI()
    {
        if (!isOpen)
        {
            return;
        }

        EnsureStyles();
        GUI.depth = -1300;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.blackTexture, ScaleMode.StretchToFill);

        float scale = Mathf.Clamp(Screen.height / 1080f, 0.67f, 1.2f);
        float previewWidth = Mathf.Min(Screen.width * 0.72f, 1040f * scale);
        float previewHeight = Mathf.Min(Screen.height * 0.57f, 600f * scale);
        Rect previewRect = new Rect(
            (Screen.width - previewWidth) * 0.5f,
            112f * scale,
            previewWidth,
            previewHeight);

        GUI.Label(new Rect(0f, 24f * scale, Screen.width, 62f * scale), "车库", titleStyle);
        if (previewTexture != null)
        {
            GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit, false);
        }
        GUI.Box(previewRect, GUIContent.none);
        HandlePreviewDrag(previewRect);

        VehicleDefinition definition = Vehicles[previewIndex];
        bool isEquipped = definition.Id == equippedVehicleId;
        float controlsY = previewRect.yMax + 16f * scale;
        GUI.Label(new Rect(0f, controlsY, Screen.width, 40f * scale),
            $"{definition.DisplayName}    {previewIndex + 1}/{Vehicles.Length}", headingStyle);
        GUI.Label(new Rect(0f, controlsY + 38f * scale, Screen.width, 32f * scale),
            isEquipped ? "当前使用" : "正在预览", bodyStyle);

        float buttonWidth = 190f * scale;
        float buttonHeight = 54f * scale;
        float buttonY = controlsY + 82f * scale;
        if (GUI.Button(new Rect(Screen.width * 0.5f - buttonWidth * 1.65f, buttonY, buttonWidth, buttonHeight), "上一辆  [A]", buttonStyle))
        {
            ChangePreview(-1);
        }

        bool previousEnabled = GUI.enabled;
        GUI.enabled = !isEquipped;
        if (GUI.Button(new Rect(Screen.width * 0.5f - buttonWidth * 0.5f, buttonY, buttonWidth, buttonHeight),
            isEquipped ? "已装备" : "设为当前车辆", buttonStyle))
        {
            EquipPreviewVehicle();
        }
        GUI.enabled = previousEnabled;

        if (GUI.Button(new Rect(Screen.width * 0.5f + buttonWidth * 0.65f, buttonY, buttonWidth, buttonHeight), "下一辆  [D]", buttonStyle))
        {
            ChangePreview(1);
        }
        if (GUI.Button(new Rect(28f * scale, 26f * scale, 120f * scale, 46f * scale), "返回  [Esc]", buttonStyle))
        {
            CloseGarage();
        }
        if (!string.IsNullOrEmpty(statusMessage) && Time.unscaledTime < statusMessageUntil)
        {
            GUI.Label(new Rect(0f, buttonY + 64f * scale, Screen.width, 36f * scale), statusMessage, bodyStyle);
        }
    }

    private void HandlePreviewDrag(Rect previewRect)
    {
        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0
            && previewRect.Contains(currentEvent.mousePosition))
        {
            draggingPreview = true;
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseDrag && draggingPreview)
        {
            previewYaw = Mathf.Repeat(previewYaw - currentEvent.delta.x * DragSensitivity, 360f);
            automaticRotationResumeTime = Time.unscaledTime + 1f;
            if (!previewRect.Contains(currentEvent.mousePosition))
            {
                draggingPreview = false;
            }
            currentEvent.Use();
            return;
        }

        if ((currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
            || currentEvent.type == EventType.MouseLeaveWindow)
        {
            draggingPreview = false;
            automaticRotationResumeTime = Time.unscaledTime + 1f;
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 42,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = Color.white;
        headingStyle = new GUIStyle(titleStyle) { fontSize = 28 };
        headingStyle.normal.textColor = new Color(0.08f, 0.72f, 1f);
        bodyStyle = new GUIStyle(titleStyle) { fontSize = 20 };
        bodyStyle.normal.textColor = new Color(0.86f, 0.92f, 1f);
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
    }

    private void CleanupPreviewResources()
    {
        previewVisual = null;
        previewPivot = null;
        previewCamera = null;
        if (previewRoot != null)
        {
            Destroy(previewRoot);
            previewRoot = null;
        }
        if (previewTexture != null)
        {
            previewTexture.Release();
            Destroy(previewTexture);
            previewTexture = null;
        }
    }

    private void OnDisable()
    {
        if (isOpen)
        {
            CloseGarage();
        }
    }

    private void OnDestroy()
    {
        if (isOpen)
        {
            CloseGarage();
        }
        else
        {
            CleanupPreviewResources();
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
