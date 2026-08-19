using System.Collections.Generic;
using UnityEngine;

public sealed class Level02Whirlpool : MonoBehaviour
{
    private const int MaximumShaderWhirlpools = 4;
    private const float PullRadius = 11.5f;
    private const float MaximumCombinedCurrent = 38f;

    private static readonly List<Level02Whirlpool> ActiveWhirlpools =
        new List<Level02Whirlpool>();
    private static readonly Vector4[] ShaderPositions =
        new Vector4[MaximumShaderWhirlpools];
    private static readonly Vector4[] ShaderParameters =
        new Vector4[MaximumShaderWhirlpools];

    private float lifetime;
    private float age;
    private float visualIntensity;
    private float phaseOffset;
    private float rotationSpeed;

    public void Initialize(float activeLifetime)
    {
        lifetime = Mathf.Max(4f, activeLifetime);
        transform.position = new Vector3(transform.position.x, 0.02f, transform.position.z);
        phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        rotationSpeed = Random.Range(1.05f, 1.22f);
        UpdateShaderGlobals();
    }

    public static Vector3 SampleWaterCurrent(Vector3 worldPosition)
    {
        Vector3 combinedCurrent = Vector3.zero;
        for (int index = ActiveWhirlpools.Count - 1; index >= 0; index--)
        {
            Level02Whirlpool whirlpool = ActiveWhirlpools[index];
            if (whirlpool == null || !whirlpool.isActiveAndEnabled)
            {
                ActiveWhirlpools.RemoveAt(index);
                continue;
            }

            combinedCurrent += whirlpool.CalculateCurrent(worldPosition);
        }

        return Vector3.ClampMagnitude(combinedCurrent, MaximumCombinedCurrent);
    }

    public static float SamplePropulsionScale(Vector3 worldPosition)
    {
        float propulsionScale = 1f;
        for (int index = ActiveWhirlpools.Count - 1; index >= 0; index--)
        {
            Level02Whirlpool whirlpool = ActiveWhirlpools[index];
            if (whirlpool == null || !whirlpool.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 offset = worldPosition - whirlpool.transform.position;
            offset.y = 0f;
            float normalizedDistance = Mathf.Clamp01(offset.magnitude / PullRadius);
            float coreRecovery = Mathf.SmoothStep(0f, 1f, normalizedDistance / 0.48f);
            propulsionScale = Mathf.Min(propulsionScale, Mathf.Lerp(0.08f, 1f, coreRecovery));
        }

        return propulsionScale;
    }

    private void OnEnable()
    {
        if (!ActiveWhirlpools.Contains(this))
        {
            ActiveWhirlpools.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveWhirlpools.Remove(this);
        UpdateShaderGlobals();
    }

    private void Update()
    {
        age += Time.deltaTime;
        float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age / 0.65f));
        float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((lifetime - age) / 1.5f));
        visualIntensity = fadeIn * fadeOut;
        UpdateShaderGlobals();

        if (age >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private Vector3 CalculateCurrent(Vector3 worldPosition)
    {
        Vector3 toCenter = transform.position - worldPosition;
        toCenter.y = 0f;
        float distance = toCenter.magnitude;
        if (distance >= PullRadius || distance <= 0.001f)
        {
            return Vector3.zero;
        }

        float strength = 1f - distance / PullRadius;
        float easedStrength = strength * strength;
        Vector3 inwardDirection = toCenter / distance;
        Vector3 tangentDirection = Vector3.Cross(Vector3.up, inwardDirection);
        float inwardSpeed = strength * 3.5f + easedStrength * 32f;
        float tangentialSpeed = Mathf.Sin(strength * Mathf.PI) * 7.5f
            + easedStrength * 4f;
        return inwardDirection * inwardSpeed + tangentDirection * tangentialSpeed;
    }

    private static void UpdateShaderGlobals()
    {
        int activeCount = 0;
        for (int index = ActiveWhirlpools.Count - 1; index >= 0; index--)
        {
            if (ActiveWhirlpools[index] == null)
            {
                ActiveWhirlpools.RemoveAt(index);
            }
        }

        for (int index = 0;
             index < ActiveWhirlpools.Count && activeCount < MaximumShaderWhirlpools;
             index++)
        {
            Level02Whirlpool whirlpool = ActiveWhirlpools[index];
            Vector3 position = whirlpool.transform.position;
            ShaderPositions[activeCount] = new Vector4(
                position.x,
                position.z,
                PullRadius,
                whirlpool.visualIntensity);
            ShaderParameters[activeCount] = new Vector4(
                whirlpool.phaseOffset,
                whirlpool.rotationSpeed,
                0f,
                0f);
            activeCount++;
        }

        for (int index = activeCount; index < MaximumShaderWhirlpools; index++)
        {
            ShaderPositions[index] = Vector4.zero;
            ShaderParameters[index] = Vector4.zero;
        }

        Shader.SetGlobalFloat("_Level02WhirlpoolCount", activeCount);
        Shader.SetGlobalFloat("_Level02WhirlpoolTime", Time.unscaledTime);
        Shader.SetGlobalVectorArray("_Level02WhirlpoolPositions", ShaderPositions);
        Shader.SetGlobalVectorArray("_Level02WhirlpoolParameters", ShaderParameters);
    }
}
