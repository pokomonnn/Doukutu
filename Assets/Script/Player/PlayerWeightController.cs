using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerWeightState
{
    Normal,
    SlightlySlow,
    Slow,
    VerySlow,
    Immobilized
}

[DisallowMultipleComponent]
public class PlayerWeightController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private InventoryController inventoryController;
    [SerializeField] private EquipmentController equipmentController;
    [SerializeField] private PlayerMove playerMove;

    [Header("重量ごとの段階")]
    [Tooltip("この重量までは通常速度")]
    [SerializeField, Min(0f)]
    private float normalWeightLimit = 20f;

    [Tooltip("この重量までは少し遅い")]
    [SerializeField, Min(0f)]
    private float slightlySlowWeightLimit = 40f;

    [Tooltip("この重量までは遅い")]
    [SerializeField, Min(0f)]
    private float slowWeightLimit = 60f;

    [Tooltip("この重量まではめっちゃ遅い。超えると移動不可")]
    [SerializeField, Min(0f)]
    private float verySlowWeightLimit = 75f;

    [Header("速度倍率")]
    [SerializeField, Range(0.05f, 1f)]
    private float slightlySlowSpeedMultiplier = 0.85f;

    [SerializeField, Range(0.05f, 1f)]
    private float slowSpeedMultiplier = 0.65f;

    [SerializeField, Range(0.05f, 1f)]
    private float verySlowSpeedMultiplier = 0.4f;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    private Rigidbody2D playerRigidbody;

    private float currentWeight;
    private PlayerWeightState currentState;

    private float baseMoveSpeed;
    private float baseJumpPower;
    private bool hasCachedBaseMovementValues;

    private bool isSubscribedToInventory;
    private bool isSubscribedToEquipment;
    private bool refreshRequested;

    public float CurrentWeight => currentWeight;

    // UI側では「75kgまで持てる」と表示するために使う
    public float ImmobilizedWeightLimit =>
        verySlowWeightLimit;

    public PlayerWeightState CurrentState => currentState;

    public bool IsImmobilized =>
        currentState == PlayerWeightState.Immobilized;

    public float CurrentMoveSpeedMultiplier
    {
        get
        {
            switch (currentState)
            {
                case PlayerWeightState.SlightlySlow:
                    return slightlySlowSpeedMultiplier;

                case PlayerWeightState.Slow:
                    return slowSpeedMultiplier;

                case PlayerWeightState.VerySlow:
                    return verySlowSpeedMultiplier;

                case PlayerWeightState.Immobilized:
                    return 0f;

                default:
                    return 1f;
            }
        }
    }

    public event Action<float> OnWeightChanged;
    public event Action<PlayerWeightState> OnWeightStateChanged;

    private void Awake()
    {
        FindReferences();
        CacheBaseMovementValues();
    }

    private void OnEnable()
    {
        FindReferences();
        CacheBaseMovementValues();
        SubscribeEvents();

        refreshRequested = true;
    }

    private void Start()
    {
        RecalculateWeight();
    }

    private void LateUpdate()
    {
        if (!refreshRequested)
        {
            return;
        }

        refreshRequested = false;
        RecalculateWeight();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        RestoreBaseMovementValues();
    }

    public void RecalculateWeight()
    {
        FindReferences();
        CacheBaseMovementValues();

        if (inventoryController == null)
        {
            return;
        }

        float previousWeight = currentWeight;
        PlayerWeightState previousState = currentState;

        currentWeight = CalculateTotalWeight();
        currentState = GetWeightState(currentWeight);

        ApplyMovementState();

        if (!Mathf.Approximately(
                previousWeight,
                currentWeight
            ))
        {
            OnWeightChanged?.Invoke(currentWeight);
        }

        if (previousState != currentState)
        {
            OnWeightStateChanged?.Invoke(currentState);

            if (showDebugLogs)
            {
                Debug.Log(
                    $"[PlayerWeightController] " +
                    $"{previousState} → {currentState} / " +
                    $"{currentWeight:0.0}kg",
                    this
                );
            }
        }
    }

    [ContextMenu("Recalculate Weight")]
    private void RecalculateWeightFromContextMenu()
    {
        RecalculateWeight();
    }

    private float CalculateTotalWeight()
    {
        float totalWeight = 0f;

        HashSet<InventoryItem> countedItems =
            new HashSet<InventoryItem>();

        foreach (InventoryItem item in inventoryController.Grid.Items)
        {
            totalWeight += AddItemWeightOnce(
                item,
                countedItems
            );
        }

        if (equipmentController != null)
        {
            totalWeight += AddItemWeightOnce(
                equipmentController.PrimaryWeaponItem,
                countedItems
            );

            totalWeight += AddItemWeightOnce(
                equipmentController.HelmetItem,
                countedItems
            );
        }

        return totalWeight;
    }

    private float AddItemWeightOnce(
        InventoryItem item,
        HashSet<InventoryItem> countedItems)
    {
        if (item == null ||
            item.ItemData == null ||
            countedItems.Contains(item))
        {
            return 0f;
        }

        countedItems.Add(item);

        float unitWeight = Mathf.Max(
            0f,
            item.ItemData.Weight
        );

        return unitWeight * Mathf.Max(0, item.Amount);
    }

    private PlayerWeightState GetWeightState(
        float weight)
    {
        if (weight > verySlowWeightLimit)
        {
            return PlayerWeightState.Immobilized;
        }

        if (weight > slowWeightLimit)
        {
            return PlayerWeightState.VerySlow;
        }

        if (weight > slightlySlowWeightLimit)
        {
            return PlayerWeightState.Slow;
        }

        if (weight > normalWeightLimit)
        {
            return PlayerWeightState.SlightlySlow;
        }

        return PlayerWeightState.Normal;
    }

    private void ApplyMovementState()
    {
        if (playerMove == null ||
            !hasCachedBaseMovementValues)
        {
            return;
        }

        playerMove.moveSpeed =
            baseMoveSpeed *
            CurrentMoveSpeedMultiplier;

        if (IsImmobilized)
        {
            playerMove.jumpPower = 0f;
            StopHorizontalMovement();
        }
        else
        {
            playerMove.jumpPower = baseJumpPower;
        }
    }

    private void StopHorizontalMovement()
    {
        if (playerRigidbody == null)
        {
            return;
        }

        Vector2 velocity = playerRigidbody.linearVelocity;
        velocity.x = 0f;

        playerRigidbody.linearVelocity = velocity;
    }

    private void HandleInventoryChanged()
    {
        refreshRequested = true;
    }

    private void HandleEquipmentChanged()
    {
        refreshRequested = true;
    }

    private void SubscribeEvents()
    {
        if (!isSubscribedToInventory &&
            inventoryController != null)
        {
            inventoryController.OnInventoryChanged +=
                HandleInventoryChanged;

            isSubscribedToInventory = true;
        }

        if (!isSubscribedToEquipment &&
            equipmentController != null)
        {
            equipmentController.OnEquipmentChanged +=
                HandleEquipmentChanged;

            isSubscribedToEquipment = true;
        }
    }

    private void UnsubscribeEvents()
    {
        if (isSubscribedToInventory &&
            inventoryController != null)
        {
            inventoryController.OnInventoryChanged -=
                HandleInventoryChanged;

            isSubscribedToInventory = false;
        }

        if (isSubscribedToEquipment &&
            equipmentController != null)
        {
            equipmentController.OnEquipmentChanged -=
                HandleEquipmentChanged;

            isSubscribedToEquipment = false;
        }
    }

    private void FindReferences()
    {
        if (inventoryController == null)
        {
            inventoryController =
                FindAnyObjectByType<InventoryController>();
        }

        if (equipmentController == null)
        {
            equipmentController =
                FindAnyObjectByType<EquipmentController>();
        }

        if (playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }

        if (playerRigidbody == null &&
            playerMove != null)
        {
            playerRigidbody =
                playerMove.GetComponent<Rigidbody2D>();
        }
    }

    private void CacheBaseMovementValues()
    {
        if (hasCachedBaseMovementValues ||
            playerMove == null)
        {
            return;
        }

        baseMoveSpeed = playerMove.moveSpeed;
        baseJumpPower = playerMove.jumpPower;

        hasCachedBaseMovementValues = true;
    }

    private void RestoreBaseMovementValues()
    {
        if (playerMove == null ||
            !hasCachedBaseMovementValues)
        {
            return;
        }

        playerMove.moveSpeed = baseMoveSpeed;
        playerMove.jumpPower = baseJumpPower;
    }

    private void OnValidate()
    {
        normalWeightLimit = Mathf.Max(
            0f,
            normalWeightLimit
        );

        slightlySlowWeightLimit = Mathf.Max(
            normalWeightLimit,
            slightlySlowWeightLimit
        );

        slowWeightLimit = Mathf.Max(
            slightlySlowWeightLimit,
            slowWeightLimit
        );

        verySlowWeightLimit = Mathf.Max(
            slowWeightLimit,
            verySlowWeightLimit
        );
    }
}