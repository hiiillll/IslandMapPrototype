using UnityEngine;

public class EnvironmentCollisionRefiner : MonoBehaviour
{
    private const float PalmTrunkWidth = 2.2f;
    private const float PalmTrunkHeight = 4f;
    private const float BuildingFootprintInset = 0.8f;

    private void Awake()
    {
        RefineAll(false);
    }

    public static void RefineAll()
    {
        RefineAll(true);
    }

    private static void RefineAll(bool refineBuildingFootprints)
    {
        foreach (BoxCollider collider in Object.FindObjectsOfType<BoxCollider>())
        {
            if (collider == null || !collider.enabled || collider.isTrigger || collider.gameObject.name.StartsWith("COL_"))
            {
                continue;
            }

            if (HasNamedAncestor(collider.transform, "PROP_PalmTree"))
            {
                RefitPalmTrunk(collider);
            }
            else if (refineBuildingFootprints && HasBuildingAncestor(collider.transform))
            {
                RefitBuildingFootprint(collider);
            }
        }

        foreach (Transform transform in Object.FindObjectsOfType<Transform>())
        {
            if (!transform.name.StartsWith("PROP_PalmTree_"))
            {
                continue;
            }

            BoxCollider collider = transform.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = transform.gameObject.AddComponent<BoxCollider>();
            }
            collider.enabled = true;
            collider.isTrigger = false;
            RefitPalmTrunk(collider, CalculateVisualBounds(transform));
        }
    }

    private static void RefitPalmTrunk(BoxCollider collider)
    {
        RefitPalmTrunk(collider, collider.bounds);
    }

    private static void RefitPalmTrunk(BoxCollider collider, Bounds bounds)
    {
        float trunkHeight = Mathf.Min(PalmTrunkHeight, bounds.size.y);
        Bounds trunkBounds = new Bounds(
            new Vector3(bounds.center.x, bounds.min.y + trunkHeight * 0.5f, bounds.center.z),
            new Vector3(PalmTrunkWidth, trunkHeight, PalmTrunkWidth));
        SetWorldBounds(collider, trunkBounds);
    }

    private static Bounds CalculateVisualBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(root.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }
        return bounds;
    }

    private static void RefitBuildingFootprint(BoxCollider collider)
    {
        Bounds bounds = collider.bounds;
        float width = Mathf.Max(1f, bounds.size.x - BuildingFootprintInset * 2f);
        float depth = Mathf.Max(1f, bounds.size.z - BuildingFootprintInset * 2f);
        Bounds footprintBounds = new Bounds(
            new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.3f, bounds.center.z),
            new Vector3(width, bounds.size.y * 0.6f, depth));
        SetWorldBounds(collider, footprintBounds);
    }

    private static void SetWorldBounds(BoxCollider collider, Bounds worldBounds)
    {
        Transform transform = collider.transform;
        Vector3 scale = transform.lossyScale;
        collider.center = transform.InverseTransformPoint(worldBounds.center);
        collider.size = new Vector3(
            worldBounds.size.x / Mathf.Max(Mathf.Abs(scale.x), 0.001f),
            worldBounds.size.y / Mathf.Max(Mathf.Abs(scale.y), 0.001f),
            worldBounds.size.z / Mathf.Max(Mathf.Abs(scale.z), 0.001f));
    }

    private static bool HasNamedAncestor(Transform transform, string namePrefix)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            if (current.name.StartsWith(namePrefix))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasBuildingAncestor(Transform transform)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            if (current.name.StartsWith("BLD_"))
            {
                return true;
            }
        }
        return false;
    }
}
