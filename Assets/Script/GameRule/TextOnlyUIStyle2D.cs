using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Readable text without a panel. Avoid changing shared Japanese atlas materials.</summary>
public static class TextOnlyUIStyle2D
{
    public static void Apply(TMP_Text text)
    {
        var shadow = text.GetComponent<Shadow>();
        if (shadow == null) shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .9f);
        shadow.effectDistance = new Vector2(1, -1);
        shadow.useGraphicAlpha = true;
    }
}
