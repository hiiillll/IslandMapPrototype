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
    private const string PreviewCenterXKey = "Garage.PreviewCenterX";
    private const string PreviewCenterYKey = "Garage.PreviewCenterY";
    private const string PreviewWidthKey = "Garage.PreviewWidth";
    private const string PreviewHeightKey = "Garage.PreviewHeight";
    private const string DefaultVehicleId = "default_car";
    private const int PreviewLayer = 31;
    private const float PreviewTargetSize = 5.5f;
    private const float AutomaticRotationSpeed = 20f;
    private const float DragSensitivity = 0.35f;
    private const float DefaultPreviewCenterX = 0.38f;
    private const float DefaultPreviewCenterY = 0.3f;
    private const float DefaultPreviewWidth = 0.78f;
    private const float DefaultPreviewHeight = 0.78f;

    [Header("Garage Preview Layout")]
    [InspectorName("水平位置 X")]
    [Tooltip("预览区域中的水平位置，数值越小越靠左。")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float previewViewportCenterX = DefaultPreviewCenterX;
    [InspectorName("垂直位置 Y")]
    [Tooltip("预览区域中的垂直位置，数值越小越靠下。")]
    [Range(0.1f, 0.8f)]
    [SerializeField] private float previewViewportCenterY = DefaultPreviewCenterY;
    [InspectorName("车辆宽度")]
    [Tooltip("车辆目标宽度占预览区域的比例。")]
    [Range(0.2f, 1f)]
    [SerializeField] private float previewViewportWidth = DefaultPreviewWidth;
    [InspectorName("车辆高度")]
    [Tooltip("车辆目标高度占预览区域的比例。")]
    [Range(0.2f, 1f)]
    [SerializeField] private float previewViewportHeight = DefaultPreviewHeight;

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
    private GameObject previewShadow;
    private Camera previewCamera;
    private RenderTexture previewTexture;
    private Material previewShadowMaterial;
    private Texture2D previewShadowTexture;
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
    private Texture2D garageCoverTexture;
    private Texture2D garagePanelTexture;
    private Texture2D garageButtonTexture;
    private Texture2D garageButtonHoverTexture;
    private Texture2D garagePrimaryButtonTexture;
    private Texture2D garageAccentTexture;
    private Texture2D garageDividerTexture;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle headingStyle;
    private GUIStyle bodyStyle;
    private GUIStyle counterStyle;
    private GUIStyle buttonStyle;
    private GUIStyle primaryButtonStyle;
    private GUIStyle iconButtonStyle;
    private float lastFramedCenterX;
    private float lastFramedCenterY;
    private float lastFramedWidth;
    private float lastFramedHeight;
    private bool showLayoutEditor;
    private Rect layoutEditorRect;

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
        LoadPreviewLayout();
        garageCoverTexture = Resources.Load<Texture2D>("UI/GarageCover_MenuV2");
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
        if (isOpen && previewVisual != null && HasPreviewLayoutChanged())
        {
            FitPreviewToReferenceFrame();
            UpdatePreviewShadow(CalculateLocalBounds(previewVisual.transform, previewPivot.transform));
        }
    }

    public void SetPreviewLayout(float centerX, float centerY, float width, float height)
    {
        previewViewportCenterX = Mathf.Clamp(centerX, 0.1f, 0.9f);
        previewViewportCenterY = Mathf.Clamp(centerY, 0.1f, 0.8f);
        previewViewportWidth = Mathf.Clamp(width, 0.2f, 1f);
        previewViewportHeight = Mathf.Clamp(height, 0.2f, 1f);
        if (isOpen && previewVisual != null)
        {
            FitPreviewToReferenceFrame();
            UpdatePreviewShadow(CalculateLocalBounds(previewVisual.transform, previewPivot.transform));
        }
    }

    private void LoadPreviewLayout()
    {
        previewViewportCenterX = PlayerPrefs.GetFloat(PreviewCenterXKey, DefaultPreviewCenterX);
        previewViewportCenterY = PlayerPrefs.GetFloat(PreviewCenterYKey, DefaultPreviewCenterY);
        previewViewportWidth = PlayerPrefs.GetFloat(PreviewWidthKey, DefaultPreviewWidth);
        previewViewportHeight = PlayerPrefs.GetFloat(PreviewHeightKey, DefaultPreviewHeight);
    }

    private void SavePreviewLayout()
    {
        PlayerPrefs.SetFloat(PreviewCenterXKey, previewViewportCenterX);
        PlayerPrefs.SetFloat(PreviewCenterYKey, previewViewportCenterY);
        PlayerPrefs.SetFloat(PreviewWidthKey, previewViewportWidth);
        PlayerPrefs.SetFloat(PreviewHeightKey, previewViewportHeight);
        PlayerPrefs.Save();
        statusMessage = "构图参数已保存";
        statusMessageUntil = Time.unscaledTime + 2f;
    }

    private void ResetPreviewLayout()
    {
        SetPreviewLayout(
            DefaultPreviewCenterX,
            DefaultPreviewCenterY,
            DefaultPreviewWidth,
            DefaultPreviewHeight);
        PlayerPrefs.DeleteKey(PreviewCenterXKey);
        PlayerPrefs.DeleteKey(PreviewCenterYKey);
        PlayerPrefs.DeleteKey(PreviewWidthKey);
        PlayerPrefs.DeleteKey(PreviewHeightKey);
        PlayerPrefs.Save();
        statusMessage = "构图参数已重置";
        statusMessageUntil = Time.unscaledTime + 2f;
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
        showLayoutEditor = false;
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
        cameraObject.transform.localPosition = new Vector3(4.25f, 2.75f, -7.4f);
        cameraObject.transform.LookAt(previewRoot.transform.position + Vector3.up * 1.05f);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = Color.clear;
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.fieldOfView = 27f;
        previewCamera.allowHDR = false;

        GameObject lightObject = new GameObject("GARAGE_KeyLight");
        lightObject.transform.SetParent(previewRoot.transform, false);
        lightObject.transform.localRotation = Quaternion.LookRotation(
            new Vector3(0.34f, -0.56f, -0.76f),
            Vector3.up);
        Light keyLight = lightObject.AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.intensity = 0.68f;
        keyLight.color = new Color(1f, 0.68f, 0.42f);
        keyLight.cullingMask = 1 << PreviewLayer;

        GameObject fillLightObject = new GameObject("GARAGE_FillLight");
        fillLightObject.transform.SetParent(previewRoot.transform, false);
        fillLightObject.transform.localRotation = Quaternion.LookRotation(
            new Vector3(-0.42f, -0.34f, 0.84f),
            Vector3.up);
        Light fillLight = fillLightObject.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.intensity = 0.18f;
        fillLight.color = new Color(0.48f, 0.64f, 0.82f);
        fillLight.cullingMask = 1 << PreviewLayer;

        GameObject topLightObject = new GameObject("GARAGE_TopLight");
        topLightObject.transform.SetParent(previewRoot.transform, false);
        topLightObject.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        Light topLight = topLightObject.AddComponent<Light>();
        topLight.type = LightType.Directional;
        topLight.intensity = 0.12f;
        topLight.color = new Color(1f, 0.86f, 0.7f);
        topLight.cullingMask = 1 << PreviewLayer;

        CreatePreviewShadow();

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

        previewPivot.transform.localPosition = Vector3.zero;

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
        previewPivot.transform.localRotation = Quaternion.Euler(0f, previewYaw, 0f);
        FitPreviewToReferenceFrame();
        UpdatePreviewShadow(CalculateLocalBounds(previewVisual.transform, previewPivot.transform));
        automaticRotationResumeTime = Time.unscaledTime + 1f;
    }

    private void FitPreviewToReferenceFrame()
    {
        if (previewVisual == null || previewPivot == null || previewCamera == null)
        {
            return;
        }

        if (!TryGetPreviewViewportBounds(out Rect viewportBounds, out float cameraDepth))
        {
            return;
        }

        float widthScale = previewViewportWidth / Mathf.Max(viewportBounds.width, 0.001f);
        float heightScale = previewViewportHeight / Mathf.Max(viewportBounds.height, 0.001f);
        float framingScale = Mathf.Clamp(Mathf.Min(widthScale, heightScale), 0.45f, 1.8f);
        previewVisual.transform.localScale *= framingScale;

        Bounds rotationBounds = CalculateLocalBounds(previewVisual.transform, previewPivot.transform);
        previewVisual.transform.localPosition += new Vector3(
            -rotationBounds.center.x,
            -rotationBounds.min.y,
            -rotationBounds.center.z);

        if (!TryGetPreviewViewportBounds(out viewportBounds, out cameraDepth))
        {
            return;
        }

        Vector2 currentCenter = viewportBounds.center;
        float verticalWorldSize = 2f * cameraDepth
            * Mathf.Tan(previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float horizontalWorldSize = verticalWorldSize * previewCamera.aspect;
        Vector3 worldOffset = previewCamera.transform.right
            * ((previewViewportCenterX - currentCenter.x) * horizontalWorldSize)
            + previewCamera.transform.up
            * ((previewViewportCenterY - currentCenter.y) * verticalWorldSize);
        previewPivot.transform.position += worldOffset;
        lastFramedCenterX = previewViewportCenterX;
        lastFramedCenterY = previewViewportCenterY;
        lastFramedWidth = previewViewportWidth;
        lastFramedHeight = previewViewportHeight;
    }

    private bool HasPreviewLayoutChanged()
    {
        return !Mathf.Approximately(lastFramedCenterX, previewViewportCenterX)
            || !Mathf.Approximately(lastFramedCenterY, previewViewportCenterY)
            || !Mathf.Approximately(lastFramedWidth, previewViewportWidth)
            || !Mathf.Approximately(lastFramedHeight, previewViewportHeight);
    }

    private bool TryGetPreviewViewportBounds(out Rect viewportBounds, out float cameraDepth)
    {
        viewportBounds = default;
        cameraDepth = 0f;
        if (previewVisual == null || previewCamera == null)
        {
            return false;
        }

        bool initialized = false;
        Vector2 minimum = Vector2.zero;
        Vector2 maximum = Vector2.zero;
        float depthTotal = 0f;
        int depthCount = 0;
        foreach (Renderer visualRenderer in previewVisual.GetComponentsInChildren<Renderer>(true))
        {
            if (!visualRenderer.enabled || !visualRenderer.gameObject.activeInHierarchy
                || visualRenderer.forceRenderingOff)
            {
                continue;
            }

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
                Vector3 viewportPoint = previewCamera.WorldToViewportPoint(corner);
                if (viewportPoint.z <= 0f)
                {
                    continue;
                }

                Vector2 point = new Vector2(viewportPoint.x, viewportPoint.y);
                if (!initialized)
                {
                    minimum = point;
                    maximum = point;
                    initialized = true;
                }
                else
                {
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                }
                depthTotal += viewportPoint.z;
                depthCount++;
            }
        }

        if (!initialized || depthCount == 0)
        {
            return false;
        }

        viewportBounds = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        cameraDepth = depthTotal / depthCount;
        return true;
    }

    private void CreatePreviewShadow()
    {
        Shader shadowShader = Shader.Find("Unlit/Transparent");
        if (shadowShader == null)
        {
            return;
        }

        previewShadowTexture = CreateSoftShadowTexture(128);
        previewShadowMaterial = new Material(shadowShader)
        {
            name = "MAT_GarageContactShadow",
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = previewShadowTexture
        };

        previewShadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        previewShadow.name = "GARAGE_ContactShadow";
        previewShadow.transform.SetParent(previewPivot.transform, false);
        previewShadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        previewShadow.layer = PreviewLayer;

        Collider shadowCollider = previewShadow.GetComponent<Collider>();
        if (shadowCollider != null)
        {
            Destroy(shadowCollider);
        }

        MeshRenderer shadowRenderer = previewShadow.GetComponent<MeshRenderer>();
        shadowRenderer.sharedMaterial = previewShadowMaterial;
        shadowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shadowRenderer.receiveShadows = false;
    }

    private void UpdatePreviewShadow(Bounds vehicleBounds)
    {
        if (previewShadow == null)
        {
            return;
        }

        previewShadow.transform.localPosition = new Vector3(
            vehicleBounds.center.x,
            vehicleBounds.min.y - 0.035f,
            vehicleBounds.center.z);
        previewShadow.transform.localScale = new Vector3(
            vehicleBounds.size.x * 0.94f,
            vehicleBounds.size.z * 0.82f,
            1f);
    }

    private static Texture2D CreateSoftShadowTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "TEX_GarageContactShadow",
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int y = 0; y < size; y++)
        {
            float normalizedY = y / (size - 1f) * 2f - 1f;
            for (int x = 0; x < size; x++)
            {
                float normalizedX = x / (size - 1f) * 2f - 1f;
                float distanceSquared = normalizedX * normalizedX + normalizedY * normalizedY;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - distanceSquared), 2.4f) * 0.42f;
                texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }

        texture.Apply(false, true);
        return texture;
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
            material.color = new Color(0.72f, 0.72f, 0.72f, 1f);
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
                material.SetFloat("_Metallic", 0.45f);
                material.SetFloat("_GlossMapScale", 0.3f);
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
        Rect screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
        GUI.DrawTexture(
            screenRect,
            garageCoverTexture != null ? garageCoverTexture : Texture2D.blackTexture,
            ScaleMode.ScaleAndCrop,
            true);
        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.12f);
        GUI.DrawTexture(screenRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = previousColor;

        float scale = Mathf.Clamp(Screen.height / 1080f, 0.67f, 1.2f);
        UpdateStyleScale(scale);
        float previewWidth = Mathf.Min(Screen.width * 0.76f, 1100f * scale);
        float previewHeight = Mathf.Min(Screen.height * 0.54f, 580f * scale);
        Rect previewRect = new Rect(
            (Screen.width - previewWidth) * 0.5f,
            Screen.height * 0.32f,
            previewWidth,
            previewHeight);
        layoutEditorRect = new Rect(
            Screen.width - 324f * scale,
            88f * scale,
            296f * scale,
            290f * scale);

        GUI.Label(new Rect(0f, 24f * scale, Screen.width, 48f * scale), "车库", titleStyle);
        GUI.DrawTexture(
            new Rect(Screen.width * 0.5f - 30f * scale, 74f * scale, 60f * scale, 2f * scale),
            garageAccentTexture);
        GUI.Label(
            new Rect(0f, 78f * scale, Screen.width, 24f * scale),
            "V E H I C L E   G A R A G E",
            subtitleStyle);
        if (previewTexture != null)
        {
            Color previewColor = GUI.color;
            GUI.color = new Color(0.88f, 0.84f, 0.8f, 1f);
            GUI.DrawTexture(previewRect, previewTexture, ScaleMode.ScaleToFit, true);
            GUI.color = previewColor;
        }
        HandlePreviewDrag(previewRect, showLayoutEditor ? layoutEditorRect : default);

        VehicleDefinition definition = Vehicles[previewIndex];
        bool isEquipped = definition.Id == equippedVehicleId;
        float infoY = Screen.height - 176f * scale;
        float infoHeight = 70f * scale;
        Rect infoRect = new Rect(previewRect.x, infoY, previewRect.width, infoHeight);
        GUI.DrawTexture(infoRect, garagePanelTexture);
        GUI.DrawTexture(new Rect(infoRect.x, infoRect.y, infoRect.width, 1f), garageDividerTexture);
        GUI.Label(
            new Rect(infoRect.x + 22f * scale, infoRect.y + 7f * scale, infoRect.width * 0.65f, 36f * scale),
            definition.DisplayName,
            headingStyle);
        GUI.Label(
            new Rect(infoRect.xMax - 170f * scale, infoRect.y + 9f * scale, 148f * scale, 32f * scale),
            $"{previewIndex + 1:00} / {Vehicles.Length:00}",
            counterStyle);
        GUI.Label(
            new Rect(infoRect.x + 24f * scale, infoRect.y + 39f * scale, infoRect.width - 48f * scale, 24f * scale),
            isEquipped ? "当前车辆" : "预览车辆",
            bodyStyle);

        float iconSize = 54f * scale;
        float equipWidth = 230f * scale;
        float buttonGap = 12f * scale;
        float groupWidth = iconSize * 2f + equipWidth + buttonGap * 2f;
        float buttonX = (Screen.width - groupWidth) * 0.5f;
        float buttonY = infoRect.yMax + 18f * scale;
        if (GUI.Button(
            new Rect(buttonX, buttonY, iconSize, iconSize),
            new GUIContent("‹", "上一辆 [A]"),
            iconButtonStyle))
        {
            ChangePreview(-1);
        }

        bool previousEnabled = GUI.enabled;
        GUI.enabled = !isEquipped;
        if (GUI.Button(
            new Rect(buttonX + iconSize + buttonGap, buttonY, equipWidth, iconSize),
            isEquipped ? "当前车辆" : "设为当前车辆",
            primaryButtonStyle))
        {
            EquipPreviewVehicle();
        }
        GUI.enabled = previousEnabled;

        if (GUI.Button(
            new Rect(buttonX + iconSize + buttonGap * 2f + equipWidth, buttonY, iconSize, iconSize),
            new GUIContent("›", "下一辆 [D]"),
            iconButtonStyle))
        {
            ChangePreview(1);
        }
        if (GUI.Button(
            new Rect(28f * scale, 28f * scale, 48f * scale, 48f * scale),
            new GUIContent("←", "返回 [Esc]"),
            iconButtonStyle))
        {
            CloseGarage();
        }
        if (GUI.Button(
            new Rect(Screen.width - 76f * scale, 28f * scale, 48f * scale, 48f * scale),
            new GUIContent("⚙", "调整车辆构图"),
            iconButtonStyle))
        {
            showLayoutEditor = !showLayoutEditor;
        }
        if (showLayoutEditor)
        {
            DrawLayoutEditor(scale);
        }
        if (!string.IsNullOrEmpty(statusMessage) && Time.unscaledTime < statusMessageUntil)
        {
            GUI.Label(
                new Rect(0f, buttonY + iconSize + 8f * scale, Screen.width, 28f * scale),
                statusMessage,
                subtitleStyle);
        }
    }

    private void DrawLayoutEditor(float scale)
    {
        GUI.DrawTexture(layoutEditorRect, garagePanelTexture);
        GUI.DrawTexture(
            new Rect(layoutEditorRect.x, layoutEditorRect.y, layoutEditorRect.width, 1f),
            garageAccentTexture);
        GUI.Label(
            new Rect(layoutEditorRect.x + 16f * scale, layoutEditorRect.y + 10f * scale,
                layoutEditorRect.width - 32f * scale, 28f * scale),
            "车辆构图",
            headingStyle);

        float rowX = layoutEditorRect.x + 18f * scale;
        float rowWidth = layoutEditorRect.width - 36f * scale;
        float rowY = layoutEditorRect.y + 48f * scale;
        float nextCenterX = DrawLayoutSlider(
            new Rect(rowX, rowY, rowWidth, 42f * scale),
            "水平位置 X",
            previewViewportCenterX,
            0.1f,
            0.9f,
            scale);
        rowY += 48f * scale;
        float nextCenterY = DrawLayoutSlider(
            new Rect(rowX, rowY, rowWidth, 42f * scale),
            "垂直位置 Y",
            previewViewportCenterY,
            0.1f,
            0.8f,
            scale);
        rowY += 48f * scale;
        float nextWidth = DrawLayoutSlider(
            new Rect(rowX, rowY, rowWidth, 42f * scale),
            "车辆宽度",
            previewViewportWidth,
            0.2f,
            1f,
            scale);
        rowY += 48f * scale;
        float nextHeight = DrawLayoutSlider(
            new Rect(rowX, rowY, rowWidth, 42f * scale),
            "车辆高度",
            previewViewportHeight,
            0.2f,
            1f,
            scale);

        if (!Mathf.Approximately(nextCenterX, previewViewportCenterX)
            || !Mathf.Approximately(nextCenterY, previewViewportCenterY)
            || !Mathf.Approximately(nextWidth, previewViewportWidth)
            || !Mathf.Approximately(nextHeight, previewViewportHeight))
        {
            SetPreviewLayout(nextCenterX, nextCenterY, nextWidth, nextHeight);
        }

        float buttonY = layoutEditorRect.yMax - 48f * scale;
        float buttonGap = 10f * scale;
        float buttonWidth = (rowWidth - buttonGap) * 0.5f;
        if (GUI.Button(
            new Rect(rowX, buttonY, buttonWidth, 34f * scale),
            "保存",
            primaryButtonStyle))
        {
            SavePreviewLayout();
        }
        if (GUI.Button(
            new Rect(rowX + buttonWidth + buttonGap, buttonY, buttonWidth, 34f * scale),
            "重置",
            buttonStyle))
        {
            ResetPreviewLayout();
        }
    }

    private float DrawLayoutSlider(
        Rect rowRect,
        string label,
        float value,
        float minimum,
        float maximum,
        float scale)
    {
        GUI.Label(
            new Rect(rowRect.x, rowRect.y, rowRect.width, 20f * scale),
            $"{label}    {value:0.00}",
            bodyStyle);
        return GUI.HorizontalSlider(
            new Rect(rowRect.x, rowRect.y + 25f * scale, rowRect.width, 14f * scale),
            value,
            minimum,
            maximum);
    }

    private void HandlePreviewDrag(Rect previewRect, Rect excludedRect)
    {
        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0
            && previewRect.Contains(currentEvent.mousePosition)
            && !excludedRect.Contains(currentEvent.mousePosition))
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

        garagePanelTexture = CreateSolidTexture(new Color(0.015f, 0.02f, 0.022f, 0.76f));
        garageButtonTexture = CreateSolidTexture(new Color(0.025f, 0.035f, 0.038f, 0.9f));
        garageButtonHoverTexture = CreateSolidTexture(new Color(0.28f, 0.11f, 0.035f, 0.96f));
        garagePrimaryButtonTexture = CreateSolidTexture(new Color(0.52f, 0.18f, 0.045f, 0.96f));
        garageAccentTexture = CreateSolidTexture(new Color(1f, 0.38f, 0.08f, 1f));
        garageDividerTexture = CreateSolidTexture(new Color(0.78f, 0.84f, 0.84f, 0.34f));

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Normal
        };
        titleStyle.normal.textColor = Color.white;
        subtitleStyle = new GUIStyle(titleStyle) { fontStyle = FontStyle.Bold };
        subtitleStyle.normal.textColor = new Color(0.78f, 0.84f, 0.83f, 0.9f);
        headingStyle = new GUIStyle(titleStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Bold
        };
        headingStyle.normal.textColor = new Color(0.95f, 0.97f, 0.96f, 1f);
        bodyStyle = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleLeft };
        bodyStyle.normal.textColor = new Color(0.72f, 0.78f, 0.78f, 1f);
        counterStyle = new GUIStyle(titleStyle)
        {
            alignment = TextAnchor.MiddleRight,
            fontStyle = FontStyle.Bold
        };
        counterStyle.normal.textColor = new Color(1f, 0.42f, 0.1f, 1f);
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };
        buttonStyle.normal.background = garageButtonTexture;
        buttonStyle.hover.background = garageButtonHoverTexture;
        buttonStyle.active.background = garagePrimaryButtonTexture;
        buttonStyle.normal.textColor = new Color(0.9f, 0.94f, 0.93f, 1f);
        buttonStyle.hover.textColor = Color.white;
        buttonStyle.active.textColor = Color.white;
        primaryButtonStyle = new GUIStyle(buttonStyle);
        primaryButtonStyle.normal.background = garagePrimaryButtonTexture;
        iconButtonStyle = new GUIStyle(buttonStyle);
    }

    private void UpdateStyleScale(float scale)
    {
        titleStyle.fontSize = Mathf.RoundToInt(42f * scale);
        subtitleStyle.fontSize = Mathf.RoundToInt(13f * scale);
        headingStyle.fontSize = Mathf.RoundToInt(29f * scale);
        bodyStyle.fontSize = Mathf.RoundToInt(16f * scale);
        counterStyle.fontSize = Mathf.RoundToInt(18f * scale);
        buttonStyle.fontSize = Mathf.RoundToInt(18f * scale);
        primaryButtonStyle.fontSize = buttonStyle.fontSize;
        iconButtonStyle.fontSize = Mathf.RoundToInt(30f * scale);
    }

    private static Texture2D CreateSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private void CleanupPreviewResources()
    {
        previewVisual = null;
        previewShadow = null;
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
        if (previewShadowMaterial != null)
        {
            Destroy(previewShadowMaterial);
            previewShadowMaterial = null;
        }
        DestroyRuntimeTexture(previewShadowTexture);
        previewShadowTexture = null;
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
        DestroyRuntimeTexture(garagePanelTexture);
        DestroyRuntimeTexture(garageButtonTexture);
        DestroyRuntimeTexture(garageButtonHoverTexture);
        DestroyRuntimeTexture(garagePrimaryButtonTexture);
        DestroyRuntimeTexture(garageAccentTexture);
        DestroyRuntimeTexture(garageDividerTexture);
    }

    private static void DestroyRuntimeTexture(Texture2D texture)
    {
        if (texture != null)
        {
            Destroy(texture);
        }
    }
}
