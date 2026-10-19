using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class Dialogue600SoloEntry2D
{
    public string sourceDialogueId;
    public string title;
    [Range(0, 3)] public int requiredStage;
    [TextArea(1, 3)] public string text;
    public string speakerInfo;
    public string sourceSituation;
    [TextArea(1, 3)] public string sourcePrerequisites;
    public string sourcePersonalityHint;
    public string sourcePlaybackHint;
    [Min(1)] public int weight = 1;
}

[CreateAssetMenu(menuName = "2D Shooter/Dialogue Studio/600会話集・独り言", fileName = "Solo600_New")]
public sealed class Dialogue600SoloLibrary2D : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private List<Dialogue600SoloEntry2D> entries = new List<Dialogue600SoloEntry2D>();
    public string DisplayName => displayName;
    public IReadOnlyList<Dialogue600SoloEntry2D> Entries => entries;
}
