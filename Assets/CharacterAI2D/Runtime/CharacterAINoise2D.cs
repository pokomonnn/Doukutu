using System;
using UnityEngine;

public enum CharacterAINoiseKind2D { Footstep, Gunshot }

/// <summary>音をAIへ通知する軽量イベント。AudioSourceの音量とは独立したゲーム内の聴覚範囲。</summary>
public static class CharacterAINoise2D
{
    public struct Noise
    {
        public readonly Vector2 Position;
        public readonly float Radius;
        public readonly Transform Source;
        public readonly CharacterAINoiseKind2D Kind;
        public Noise(Vector2 position, float radius, Transform source, CharacterAINoiseKind2D kind)
        { Position = position; Radius = radius; Source = source; Kind = kind; }
    }
    public static event Action<Noise> Emitted;

    public static void Emit(Vector2 position, float radius, Transform source, CharacterAINoiseKind2D kind)
    {
        if (radius <= 0f) return;
        Emitted?.Invoke(new Noise(position, radius, source, kind));
    }
}

/// <summary>同派閥が敵を発見した情報を配信。受信者は個別の敵対判定後に現地調査する。</summary>
public static class CharacterAIAllyAlert2D
{
    public struct Alert
    {
        public readonly Transform Sender;
        public readonly FactionMember2D SenderFaction;
        public readonly FactionMember2D TargetFaction;
        public readonly Vector2 SenderPosition;
        public readonly Vector2 LastKnownPosition;
        public readonly float Radius;
        public Alert(Transform sender, FactionMember2D senderFaction, FactionMember2D targetFaction,
            Vector2 senderPosition, Vector2 lastKnownPosition, float radius)
        {
            Sender = sender; SenderFaction = senderFaction; TargetFaction = targetFaction;
            SenderPosition = senderPosition; LastKnownPosition = lastKnownPosition; Radius = radius;
        }
    }
    public static event Action<Alert> Emitted;
    public static void Emit(Alert alert) { Emitted?.Invoke(alert); }
}

