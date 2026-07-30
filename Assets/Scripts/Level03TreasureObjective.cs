using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class Level03TreasureObjective : MonoBehaviour
{
    [SerializeField, Min(1)] private int requiredChestCount = 4;
    [SerializeField] private Level03TreasureChest[] chests = Array.Empty<Level03TreasureChest>();

    private readonly HashSet<string> collectedChestIds = new HashSet<string>();
    private bool completionRaised;

    public event Action<int, int> ChestCollected;
    public event Action AllChestsCollected;

    public int CollectedCount => collectedChestIds.Count;
    public int RequiredChestCount => requiredChestCount;
    public bool IsComplete => CollectedCount >= requiredChestCount;

    private void Awake()
    {
        if (chests == null || chests.Length == 0)
        {
            chests = GetComponentsInChildren<Level03TreasureChest>(true);
        }

        foreach (Level03TreasureChest chest in chests)
        {
            if (chest != null)
            {
                chest.AssignObjective(this);
            }
        }
    }

    public bool TryCollect(Level03TreasureChest chest)
    {
        if (chest == null || string.IsNullOrWhiteSpace(chest.ChestId) ||
            !collectedChestIds.Add(chest.ChestId))
        {
            return false;
        }

        ChestCollected?.Invoke(CollectedCount, requiredChestCount);
        if (IsComplete && !completionRaised)
        {
            completionRaised = true;
            AllChestsCollected?.Invoke();
        }

        return true;
    }

    public void Configure(Level03TreasureChest[] placedChests, int requiredCount = 4)
    {
        chests = placedChests ?? Array.Empty<Level03TreasureChest>();
        requiredChestCount = Mathf.Max(1, requiredCount);
        foreach (Level03TreasureChest chest in chests)
        {
            if (chest != null)
            {
                chest.AssignObjective(this);
            }
        }
    }
}
