#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class MedicalKitUIFlowTests
{
    const string Key = "MedicalKit.UIFlow";
    static IEnumerator flow;
    static int frame = -1;
    static double started;
    static readonly List<string> warnings = new List<string>();

    static MedicalKitUIFlowTests()
    {
        if (!SessionState.GetBool(Key, false)) return;
        started = EditorApplication.timeSinceStartup;
        Application.logMessageReceived += OnLog;
        EditorApplication.update += Tick;
    }

    static void OnLog(string message, string trace, LogType type)
    {
        if (type == LogType.Warning && message.Contains("[MedicalKitUI]"))
            warnings.Add(message);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Isolate()
    {
        if (SessionState.GetBool(Key, false))
            SaveManager.EditorTestSaveFolder = Path.GetFullPath("Logs/MedicalUIFlowSaves");
    }

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/stage/stage1.unity");
        var ui = UnityEngine.Object.FindFirstObjectByType<MedicalKitInventoryUI2D>(FindObjectsInactive.Include);
        var toggle = UnityEngine.Object.FindFirstObjectByType<InventoryPanelToggle>(FindObjectsInactive.Include);
        Check(ui.DiagnosticPanelRoot.name == "MedicalKitPanel", "saved kit panel");
        Check(ui.DiagnosticMainInventoryPanel == Field<GameObject>(toggle, "inventoryPanel"), "saved inventory panel");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("MEDICAL UI FLOW: " + message);
    }
    static void Click(Button button)
    {
        Check(button != null && button.isActiveAndEnabled && button.interactable, "button visible and enabled");
        Check(button.GetComponent<Graphic>().raycastTarget, "button accepts pointer");
        button.onClick.Invoke();
    }
    static void Use(MedicalKitInventoryUI2D ui, int slot)
    {
        Click(Field<RectTransform>(ui, "kitSlotsRoot").Find("FixedSlot_" + slot).GetComponent<Button>());
        Click(Field<Button>(Field<MedicalKitItemContextMenu2D>(ui, "contextMenu"), "useButton"));
    }

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("UI flow timeout");
            if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (flow == null) flow = Flow();
            if (flow.MoveNext()) return;
            Check(warnings.Count == 0, "unexpected UI warnings: " + string.Join("\n", warnings));
            SessionState.SetBool(Key, false);
            Debug.Log("MEDICAL UI FLOW PASS: saved references; inventory/kit toggle; three player and NPC uses through buttons; cancel without consumption; NPC target reset; zero MedicalKitUI warnings");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            SessionState.SetBool(Key, false);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    static IEnumerator Flow()
    {
        for (int n = 0; n < 60; n++) yield return null;
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyChaser2D>(FindObjectsInactive.Include))
            enemy.enabled = false;
        var ui = UnityEngine.Object.FindFirstObjectByType<MedicalKitInventoryUI2D>(FindObjectsInactive.Include);
        var toggle = UnityEngine.Object.FindFirstObjectByType<InventoryPanelToggle>(FindObjectsInactive.Include);
        var kit = ui.DiagnosticMedicalKit;
        kit.RestoreSavedAmounts(3, 3, 3);
        Check(!toggle.IsOpen && !ui.IsMedicalPanelOpen, "initially closed");
        for (int n = 0; n < 3; n++)
        {
            toggle.ToggleInventory(); yield return null;
            Click(ui.DiagnosticOpenButton); yield return null;
            Check(ui.IsMedicalPanelOpen && !ui.IsTreatingNpc, "open player kit");
            Click(ui.DiagnosticCloseButton); yield return null;
            Check(toggle.IsOpen && !ui.IsMedicalPanelOpen, "kit close keeps inventory");
            Click(ui.DiagnosticOpenButton); yield return null;
            toggle.ToggleInventory(); yield return null; yield return null;
            Check(!toggle.IsOpen && !ui.IsMedicalPanelOpen && !ui.DiagnosticPanelRoot.activeSelf, "inventory closes kit");
        }
        toggle.OpenInventory(); yield return null;
        Check(!ui.IsMedicalPanelOpen, "reopen inventory starts with kit closed");
        Click(ui.DiagnosticOpenButton); yield return null;
        var conditions = kit.GetComponent<PlayerStatusConditionController>();
        var symptoms = new[] { StatusConditionType.Poison, StatusConditionType.Fracture, StatusConditionType.Bleeding };
        for (int i = 0; i < 3; i++)
        {
            conditions.AddConditions(symptoms[i]);
            Use(ui, i); yield return null; yield return null;
            Check(!conditions.HasCondition(symptoms[i]) && kit.GetAmountAtSlot(i) == 2, "player use " + i);
        }
        toggle.CloseInventory(); yield return null; yield return null;
        var npc = UnityEngine.Object.FindObjectsByType<RescuePersonSymptoms2D>(FindObjectsInactive.Exclude)
            .First(n => !n.IsDead && n.GetComponent<StoryFinnDelivery>() == null);
        foreach (var body in npc.GetComponentsInChildren<Rigidbody2D>()) body.simulated = false;
        var playerBody = kit.GetComponent<Rigidbody2D>();
        playerBody.simulated = false;
        kit.transform.position = npc.transform.position + Vector3.right * 0.8f;
        npc.AddSymptoms(StatusConditionType.Poison);
        Check(ui.OpenForNpc(npc), "NPC inventory open"); yield return null;
        Check(toggle.IsOpen && !ui.IsMedicalPanelOpen && ui.CurrentNpcTarget == npc, "NPC opens inventory first");
        Click(ui.DiagnosticOpenButton); yield return null;
        Use(ui, 0);
        var progress = Field<MedicalKitNPCTreatmentProgress2D>(ui, "npcTreatmentProgress");
        Check(progress.IsTreating && kit.GetAmountAtSlot(0) == 2, "NPC starts without consumption");
        toggle.CloseInventory(); yield return null; yield return null;
        Check(!progress.IsTreating && kit.GetAmountAtSlot(0) == 2, "closing inventory cancels");
        Check(!ui.IsTreatingNpc && ui.CurrentNpcTarget == null, "closing clears NPC");
        Check(ui.OpenForNpc(npc), "NPC reopen"); yield return null;
        Click(ui.DiagnosticOpenButton); yield return null;
        Use(ui, 0);
        Click(ui.DiagnosticCloseButton); yield return null;
        Check(!progress.IsTreating && kit.GetAmountAtSlot(0) == 2, "closing kit cancels");
        toggle.CloseInventory(); yield return null; yield return null;
        for (int i = 0; i < 3; i++)
        {
            npc.AddSymptoms(symptoms[i]);
            Check(ui.OpenForNpc(npc), "NPC target " + i); yield return null;
            Click(ui.DiagnosticOpenButton); yield return null;
            Use(ui, i);
            Check(progress.IsTreating, "NPC progress starts " + i);
            float deadline = Time.realtimeSinceStartup + 10f;
            while (progress.IsTreating && Time.realtimeSinceStartup < deadline) yield return null;
            Check(npc.IsStabilized(symptoms[i]) && kit.GetAmountAtSlot(i) == 1, "NPC completes once " + i);
            toggle.CloseInventory(); yield return null; yield return null;
        }
        toggle.OpenInventory(); yield return null;
        Click(ui.DiagnosticOpenButton); yield return null;
        Check(!ui.IsTreatingNpc && ui.CurrentNpcTarget == null, "player reopen after NPC resets target");
        toggle.CloseInventory(); yield return null; yield return null;
    }
}
#endif
