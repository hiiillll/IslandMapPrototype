using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SphereCollider))]
public sealed class Level03TreasureChest : MonoBehaviour
{
    [SerializeField] private string chestId;
    [SerializeField] private Level03TreasureObjective objective;
    [SerializeField] private Transform lidPivot;
    [SerializeField, Min(0.1f)] private float openDuration = 0.35f;
    [SerializeField, Min(0f)] private float visibleAfterOpening = 0.8f;

    private bool collected;

    public string ChestId => chestId;

    private void Awake()
    {
        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        if (objective == null)
        {
            objective = GetComponentInParent<Level03TreasureObjective>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected ||
            other.GetComponentInParent<SimpleAutoDriveController>() == null)
        {
            return;
        }

        if (objective != null && objective.TryCollect(this))
        {
            collected = true;
            GetComponent<SphereCollider>().enabled = false;
            StartCoroutine(OpenAndHide());
        }
    }

    private IEnumerator OpenAndHide()
    {
        if (lidPivot != null)
        {
            Quaternion closedRotation = lidPivot.localRotation;
            Quaternion openRotation = closedRotation * Quaternion.Euler(-105f, 0f, 0f);
            float elapsed = 0f;
            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / openDuration);
                lidPivot.localRotation = Quaternion.Slerp(
                    closedRotation,
                    openRotation,
                    Mathf.SmoothStep(0f, 1f, progress));
                yield return null;
            }
        }

        if (visibleAfterOpening > 0f)
        {
            yield return new WaitForSeconds(visibleAfterOpening);
        }

        gameObject.SetActive(false);
    }

    public void Configure(
        string id,
        Level03TreasureObjective owningObjective,
        Transform configuredLidPivot = null)
    {
        chestId = id;
        objective = owningObjective;
        if (configuredLidPivot != null)
        {
            lidPivot = configuredLidPivot;
        }
    }

    public void AssignObjective(Level03TreasureObjective owningObjective)
    {
        objective = owningObjective;
    }
}
