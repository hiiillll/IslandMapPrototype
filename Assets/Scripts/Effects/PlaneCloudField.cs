using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class PlaneCloudField : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Material deckCloudMaterial;
    [SerializeField] private Material horizonCloudMaterial;
    [SerializeField, Min(1)] private int deckCardCount = 18;
    [SerializeField, Min(0)] private int horizonCardCount = 8;
    [SerializeField, Min(10f)] private float fieldRadius = 280f;
    [SerializeField, Min(0f)] private float minimumInitialDistance = 42f;
    [SerializeField, Min(10f)] private float horizonRadius = 235f;
    [SerializeField] private int randomSeed = 4042026;

    private readonly List<Transform> deckCards = new List<Transform>();
    private readonly List<Transform> horizonCards = new List<Transform>();
    private readonly List<float> horizonAngles = new List<float>();
    private readonly List<float> horizonHeights = new List<float>();
    private System.Random random;

    public void Configure(
        Transform followTarget,
        Material configuredDeckCloudMaterial,
        Material configuredHorizonCloudMaterial)
    {
        target = followTarget;
        deckCloudMaterial = configuredDeckCloudMaterial;
        horizonCloudMaterial = configuredHorizonCloudMaterial;
    }

    public void BuildPreviewClouds()
    {
        InitializeClouds();
    }

    public void ClearPreviewClouds()
    {
        if (Application.isPlaying)
        {
            return;
        }

        for (int index = transform.childCount - 1; index >= 0; index--)
        {
            DestroyImmediate(transform.GetChild(index).gameObject);
        }
        deckCards.Clear();
        horizonCards.Clear();
        horizonAngles.Clear();
        horizonHeights.Clear();
    }

    private void Start()
    {
        InitializeClouds();
    }

    private void InitializeClouds()
    {
        if (deckCards.Count > 0 || horizonCards.Count > 0)
        {
            return;
        }
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            target = player != null ? player.transform : null;
        }
        if (target == null || deckCloudMaterial == null)
        {
            enabled = false;
            return;
        }

        if (horizonCloudMaterial == null)
        {
            horizonCloudMaterial = deckCloudMaterial;
        }

        random = new System.Random(randomSeed);
        for (int index = 0; index < deckCardCount; index++)
        {
            Transform card = CreateCard($"CloudDeck_{index + 1:00}", index, false);
            deckCards.Add(card);
            PlaceDeckCard(card, true);
        }
        for (int index = 0; index < horizonCardCount; index++)
        {
            Transform card = CreateCard($"CloudHorizon_{index + 1:00}", index, true);
            horizonCards.Add(card);
            horizonAngles.Add(index * 360f / horizonCardCount + NextFloat(-10f, 10f));
            horizonHeights.Add(NextFloat(7f, 14f));
            PositionHorizonCard(index);
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        float recycleDistanceSquared = fieldRadius * fieldRadius * 1.69f;
        foreach (Transform card in deckCards)
        {
            Vector3 delta = card.position - target.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > recycleDistanceSquared)
            {
                PlaceDeckCard(card, false);
            }
        }
        for (int index = 0; index < horizonCards.Count; index++)
        {
            PositionHorizonCard(index);
        }
    }

    private Transform CreateCard(string objectName, int index, bool isHorizon)
    {
        GameObject cardObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        cardObject.name = objectName;
        cardObject.transform.SetParent(transform, false);
        Collider collider = cardObject.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }
        }

        Renderer renderer = cardObject.GetComponent<Renderer>();
        renderer.sharedMaterial = isHorizon ? horizonCloudMaterial : deckCloudMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = isHorizon ? -2 : -1;
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        float alpha = isHorizon ? NextFloat(0.62f, 0.82f) : NextFloat(0.22f, 0.38f);
        float tone = NextFloat(0.9f, 1.06f);
        Color baseTint = isHorizon
            ? new Color(0.82f, 0.75f, 0.72f, alpha)
            : new Color(0.76f, 0.76f, 0.78f, alpha);
        properties.SetColor(
            "_Tint",
            new Color(baseTint.r * tone, baseTint.g * tone, baseTint.b * tone, alpha));
        float textureScaleX = isHorizon ? NextFloat(0.84f, 1.16f) : NextFloat(0.72f, 1.34f);
        float textureScaleY = isHorizon ? NextFloat(0.9f, 1.08f) : NextFloat(0.72f, 1.28f);
        properties.SetVector(
            "_MainTex_ST",
            new Vector4(textureScaleX, textureScaleY, NextFloat(0f, 1f), NextFloat(0f, 1f)));
        properties.SetFloat("_Phase", NextFloat(0f, 100f));
        renderer.SetPropertyBlock(properties);

        if (isHorizon)
        {
            cardObject.transform.localScale = new Vector3(
                NextFloat(160f, 225f),
                NextFloat(58f, 94f),
                1f);
        }
        else
        {
            cardObject.transform.localScale = new Vector3(
                NextFloat(58f, 115f),
                NextFloat(38f, 82f),
                1f);
        }
        return cardObject.transform;
    }

    private void PlaceDeckCard(Transform card, bool initialPlacement)
    {
        Vector3 forward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        float angle = initialPlacement ? NextFloat(0f, 360f) : NextFloat(-112f, 112f);
        float minimumDistance = initialPlacement ? minimumInitialDistance : fieldRadius * 0.7f;
        float distance = NextFloat(minimumDistance, fieldRadius);
        Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
        card.position = new Vector3(
            target.position.x + direction.x * distance,
            target.position.y - NextFloat(7f, 19f),
            target.position.z + direction.z * distance);
        card.rotation = Quaternion.Euler(90f, NextFloat(0f, 360f), 0f);
    }

    private void PositionHorizonCard(int index)
    {
        float radians = horizonAngles[index] * Mathf.Deg2Rad;
        Vector3 radial = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
        float staggeredRadius = horizonRadius + (index % 3 - 1) * 42f;
        Vector3 position = new Vector3(
            target.position.x + radial.x * staggeredRadius,
            target.position.y + horizonHeights[index],
            target.position.z + radial.z * staggeredRadius);
        horizonCards[index].position = position;
        Vector3 inward = new Vector3(target.position.x, position.y, target.position.z) - position;
        horizonCards[index].rotation = Quaternion.LookRotation(inward.normalized, Vector3.up);
    }

    private float NextFloat(float minimum, float maximum)
    {
        return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
    }
}
