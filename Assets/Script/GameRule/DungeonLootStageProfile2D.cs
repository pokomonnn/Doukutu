using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>SceneごとのItemBoxランダム配置ルール。Assets/Resources/DungeonLootProfilesに配置します。</summary>
[CreateAssetMenu(fileName = "DungeonLoot_Stage", menuName = "2D Shooter/Dungeon Loot/Stage Profile")]
public sealed class DungeonLootStageProfile2D : ScriptableObject
{
    [Serializable]
    public sealed class ItemChance
    {
        public ItemData item;
        [Min(0)] public int weight = 10;
        [Min(1)] public int minAmount = 1;
        [Min(1)] public int maxAmount = 1;
    }

    [Serializable]
    public sealed class BoxRule
    {
        [Tooltip("管理するItemBoxプレハブ。実行時には下の識別IDで判定します。")]
        public GameObject itemBoxPrefab;
        [HideInInspector] public string prefabKey;
        public bool enabled = true;
        [Range(0, 100)] public int emptyChancePercent = 10;
        [Min(0)] public int minDraws = 2;
        [Min(0)] public int maxDraws = 5;
        [Tooltip("ONならステージ共通Itemリストを使わず、このBox専用のリストで抽選します。")]
        public bool useOwnItemTable;
        public List<ItemChance> ownItemTable = new List<ItemChance>();
    }

    [Header("対象Scene（ビルド時はこの名前で判定）")]
    public string sceneName = "Stage1";
    [Tooltip("このScene内に配置する可能性があるItemの共通候補。重みが大きいほど出現しやすいです。")]
    public List<ItemChance> stageItems = new List<ItemChance>();
    [Header("このステージでランダム生成するItemBox")]
    public List<BoxRule> boxes = new List<BoxRule>();

    public BoxRule FindBoxRule(string key)
    {
        if (string.IsNullOrEmpty(key) || boxes == null) return null;
        foreach (BoxRule rule in boxes)
            if (rule != null && rule.enabled && string.Equals(rule.prefabKey, key, StringComparison.Ordinal))
                return rule;
        return null;
    }
}
