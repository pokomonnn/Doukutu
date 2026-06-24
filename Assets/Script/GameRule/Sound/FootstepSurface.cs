using UnityEngine;

[DisallowMultipleComponent]
public class FootstepSurface : MonoBehaviour
{
    [Header("1歩目の足音")]
    [SerializeField] private AudioClip[] firstStepClips;

    [Header("2歩目の足音")]
    [SerializeField] private AudioClip[] secondStepClips;

    [Header("音量・ピッチ")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    [SerializeField, Range(0.5f, 1.5f)] private float pitchMin = 0.95f;
    [SerializeField, Range(0.5f, 1.5f)] private float pitchMax = 1.05f;

    public bool TryGetFootstep(
        bool isFirstStep,
        out AudioClip clip,
        out float clipVolume,
        out float pitch)
    {
        AudioClip[] clips = isFirstStep
            ? firstStepClips
            : secondStepClips;

        clip = GetRandomClip(clips);
        clipVolume = volume;
        pitch = Random.Range(pitchMin, pitchMax);

        return clip != null;
    }

    private AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        int validClipCount = 0;

        foreach (AudioClip footstepClip in clips)
        {
            if (footstepClip != null)
            {
                validClipCount++;
            }
        }

        if (validClipCount == 0)
        {
            return null;
        }

        int selectedIndex = Random.Range(0, validClipCount);

        foreach (AudioClip footstepClip in clips)
        {
            if (footstepClip == null)
            {
                continue;
            }

            if (selectedIndex == 0)
            {
                return footstepClip;
            }

            selectedIndex--;
        }

        return null;
    }

    private void OnValidate()
    {
        volume = Mathf.Clamp01(volume);

        pitchMin = Mathf.Clamp(pitchMin, 0.5f, 1.5f);
        pitchMax = Mathf.Clamp(pitchMax, 0.5f, 1.5f);

        if (pitchMax < pitchMin)
        {
            pitchMax = pitchMin;
        }
    }
}