using System;
using System.Collections.Generic;
using UnityEngine;

// GameSessionManagerのJSON変換・旧セーブ復元。Unityの参照は元ファイルのGUIDを維持する。
public partial class GameSessionManager
{
    // ---------------------------------------------------------------------
    // JSON本セーブ用の書き出し・読み込み
    // ---------------------------------------------------------------------

    /// <summary>
    /// 現在GameSessionManagerが保持している状態を、JSON保存可能なデータへ変換します。
    /// 保存直前にPlayerInventorySessionBridge / MissionSessionBridgeのCaptureToSessionを
    /// 呼んでおくと、現在シーンの最新状態が入ります。
    /// </summary>
    public SaveGameData CreateSaveGameData(string savedSceneName)
    {
        EnsureMoneyInitialized();

        SaveGameData saveData = new SaveGameData
        {
            SaveVersion = CurrentSaveVersion,
            SavedAtUtc = DateTime.UtcNow.ToString("O"),
            SavedSceneName = savedSceneName ?? string.Empty,
            Money = Mathf.Max(0, currentMoney),
            TotalDeadNpcCount = Mathf.Max(0, totalDeadNpcCount),
            TotalRescuedNpcCount = Mathf.Max(0, totalRescuedNpcCount),
            TrackedMissionId = trackedMissionId ?? string.Empty
        };

        EnsureSkillSessionData();
        saveData.SkillCards = CreateSavedSkillCardData();
        saveData.Tavern = CreateSavedTavernProgressData();

        PlayerInventorySessionData sourceInventory =
            inventorySessionData ?? new PlayerInventorySessionData();

        saveData.PlayerInventory = new SavedPlayerInventoryData
        {
            GridWidth = Mathf.Max(1, sourceInventory.GridWidth),
            GridHeight = Mathf.Max(1, sourceInventory.GridHeight),
            PrimaryWeapon = CreateSavedInventoryItem(
                sourceInventory.PrimaryWeaponItem
            ),
            Helmet = CreateSavedInventoryItem(
                sourceInventory.HelmetItem
            )
        };

        if (sourceInventory.InventoryItems != null)
        {
            foreach (SessionInventoryItemData item in
                     sourceInventory.InventoryItems)
            {
                SavedInventoryItemData savedItem =
                    CreateSavedInventoryItem(item);

                if (savedItem != null)
                {
                    saveData.PlayerInventory.InventoryItems.Add(savedItem);
                }
            }
        }

        saveData.PlayerItemBox = new SavedPlayerItemBoxData
        {
            HasData = hasPlayerItemBoxSessionData,
            GridWidth = playerItemBoxSessionData != null
                ? Mathf.Max(1, playerItemBoxSessionData.GridWidth)
                : 10,
            GridHeight = playerItemBoxSessionData != null
                ? Mathf.Max(1, playerItemBoxSessionData.GridHeight)
                : 12
        };

        if (hasPlayerItemBoxSessionData &&
            playerItemBoxSessionData != null &&
            playerItemBoxSessionData.Items != null)
        {
            foreach (SessionInventoryItemData item in
                     playerItemBoxSessionData.Items)
            {
                SavedInventoryItemData savedItem =
                    CreateSavedInventoryItem(item);

                if (savedItem != null)
                {
                    saveData.PlayerItemBox.Items.Add(savedItem);
                }
            }
        }

        foreach (MissionSessionData mission in missionSessionData)
        {
            if (mission == null ||
                string.IsNullOrWhiteSpace(mission.MissionId))
            {
                continue;
            }

            saveData.Missions.Add(new SavedMissionData
            {
                MissionId = mission.MissionId.Trim(),
                DisplayName = mission.DisplayName ?? string.Empty,
                State = mission.State,
                Progress = Mathf.Max(0, mission.Progress),
                RequiredAmount = Mathf.Max(1, mission.RequiredAmount),
                RewardClaimed = mission.RewardClaimed,
                AcceptSource = mission.AcceptSource
            });
        }

        foreach (StorySessionData story in storySessionData)
        {
            if (story == null ||
                string.IsNullOrWhiteSpace(story.StoryId))
            {
                continue;
            }

            saveData.Stories.Add(new SavedStoryQuestData
            {
                StoryId = story.StoryId.Trim(),
                DisplayName = story.DisplayName ?? string.Empty,
                State = story.State
            });
        }

        foreach (string flag in storyFlags)
        {
            if (!string.IsNullOrWhiteSpace(flag))
            {
                saveData.StoryFlags.Add(flag.Trim());
            }
        }

        foreach (string specialNpcId in
                 rescuedSpecialNpcIds)
        {
            if (!string.IsNullOrWhiteSpace(
                    specialNpcId))
            {
                saveData.RescuedSpecialNpcIds.Add(
                    specialNpcId.Trim()
                );
            }
        }

        foreach (TownFacilitySessionData facility in facilitySessionData)
        {
            if (facility == null ||
                string.IsNullOrWhiteSpace(facility.FacilityId))
            {
                continue;
            }

            saveData.FacilityLevels.Add(
                new SavedTownFacilityLevelData
                {
                    FacilityId = facility.FacilityId.Trim(),
                    Level = Mathf.Max(1, facility.Level)
                }
            );
        }

        LogTransfer(
            $"本セーブ用データ作成: Money={saveData.Money:N0} / " +
            $"DeadNpcTotal={saveData.TotalDeadNpcCount:N0}人 / " +
            $"RescuedNpcTotal={saveData.TotalRescuedNpcCount:N0}人 / " +
            $"SpecialRescue={saveData.RescuedSpecialNpcIds.Count}人 / " +
            $"Inventory={saveData.PlayerInventory.InventoryItems.Count}件 / " +
            $"PrimaryWeapon={(saveData.PlayerInventory.PrimaryWeapon != null ? "あり" : "なし")} / " +
            $"Helmet={(saveData.PlayerInventory.Helmet != null ? "あり" : "なし")} / " +
            $"PlayerItemBox={(saveData.PlayerItemBox.HasData ? saveData.PlayerItemBox.Items.Count : 0)}件 / " +
            $"Missions={saveData.Missions.Count}件 / " +
            $"Stories={saveData.Stories.Count}件 / " +
            $"StoryFlags={saveData.StoryFlags.Count}件 / " +
            $"TavernSolo={saveData.Tavern.PersonalDrinkTotal}回 / " +
            $"TavernTreat={saveData.Tavern.TreatEveryoneTotal}回 / " +
            $"Facilities={saveData.FacilityLevels.Count}件 / " +
            $"SkillCards={saveData.SkillCards.OwnedCardIds.Count}枚 / " +
            $"SkillSlots={saveData.SkillCards.UnlockedSlotCount}"
        );

        // KARMA_STEP1_SAVE
        saveData.KarmaReputation = PlayerKarmaReputation2D.GetOrCreate(this).CreateSaveData();

        return saveData;
    }

    /// <summary>
    /// JSONから読み取ったデータをGameSessionManagerへ戻します。
    /// ItemDataはItemDataDatabaseを使ってItemIdから再取得します。
    /// </summary>
    public bool ApplySaveGameData(
        SaveGameData saveData,
        ItemDataDatabase itemDataDatabase,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (saveData == null)
        {
            resultMessage = "セーブデータがnullです。";
            return false;
        }

        if (saveData.SaveVersion <= 0 ||
            saveData.SaveVersion > CurrentSaveVersion)
        {
            resultMessage =
                $"対応していないセーブバージョンです。" +
                $" 保存={saveData.SaveVersion} / 対応={CurrentSaveVersion}";
            return false;
        }

        if (itemDataDatabase == null)
        {
            resultMessage = "ItemDataDatabaseが設定されていません。";
            return false;
        }

        SaveRestoreRevision++;
        currentMoney = Mathf.Max(0, saveData.Money);
        hasInitializedMoney = true;
        totalDeadNpcCount = Mathf.Max(0, saveData.TotalDeadNpcCount);
        totalRescuedNpcCount =
            Mathf.Max(
                0,
                saveData.TotalRescuedNpcCount
            );

        rescuedSpecialNpcIds.Clear();

        if (saveData.RescuedSpecialNpcIds != null)
        {
            foreach (string rawSpecialNpcId in
                     saveData.RescuedSpecialNpcIds)
            {
                string specialNpcId =
                    rawSpecialNpcId?.Trim() ??
                    string.Empty;

                if (string.IsNullOrWhiteSpace(
                        specialNpcId) ||
                    HasRescuedSpecialNpc(
                        specialNpcId))
                {
                    continue;
                }

                rescuedSpecialNpcIds.Add(
                    specialNpcId
                );
            }
        }

        RescueProgressExpeditionSession.Clear();

        RestoreTavernProgressData(saveData.Tavern);

        SavedPlayerInventoryData savedInventory =
            saveData.PlayerInventory ?? new SavedPlayerInventoryData();

        PlayerInventorySessionData nextInventory =
            new PlayerInventorySessionData
            {
                GridWidth = Mathf.Max(1, savedInventory.GridWidth),
                GridHeight = Mathf.Max(1, savedInventory.GridHeight)
            };

        int restoredInventoryCount = 0;
        int missingItemCount = 0;

        if (savedInventory.InventoryItems != null)
        {
            foreach (SavedInventoryItemData savedItem in
                     savedInventory.InventoryItems)
            {
                SessionInventoryItemData sessionItem =
                    CreateSessionInventoryItem(
                        savedItem,
                        itemDataDatabase,
                        out string itemError
                    );

                if (sessionItem != null)
                {
                    nextInventory.InventoryItems.Add(sessionItem);
                    restoredInventoryCount++;
                }
                else
                {
                    missingItemCount++;
                    LogTransferWarning(
                        $"ロード時にアイテムをスキップしました: {itemError}"
                    );
                }
            }
        }

        nextInventory.PrimaryWeaponItem = CreateSessionInventoryItem(
            savedInventory.PrimaryWeapon,
            itemDataDatabase,
            out string primaryWeaponError
        );

        if (savedInventory.PrimaryWeapon != null &&
            nextInventory.PrimaryWeaponItem == null)
        {
            missingItemCount++;
            LogTransferWarning(
                $"装備武器をロードできませんでした: {primaryWeaponError}"
            );
        }

        nextInventory.HelmetItem = CreateSessionInventoryItem(
            savedInventory.Helmet,
            itemDataDatabase,
            out string helmetError
        );

        if (savedInventory.Helmet != null &&
            nextInventory.HelmetItem == null)
        {
            missingItemCount++;
            LogTransferWarning(
                $"装備ヘルメットをロードできませんでした: {helmetError}"
            );
        }

        inventorySessionData = nextInventory;
        hasInventorySessionData = true;

        playerItemBoxSessionData = new PlayerItemBoxSessionData();
        hasPlayerItemBoxSessionData = false;
        int restoredPlayerItemBoxCount = 0;

        SavedPlayerItemBoxData savedPlayerItemBox = saveData.PlayerItemBox;

        if (savedPlayerItemBox != null && savedPlayerItemBox.HasData)
        {
            playerItemBoxSessionData.GridWidth =
                Mathf.Max(1, savedPlayerItemBox.GridWidth);
            playerItemBoxSessionData.GridHeight =
                Mathf.Max(1, savedPlayerItemBox.GridHeight);

            if (savedPlayerItemBox.Items != null)
            {
                foreach (SavedInventoryItemData savedItem in
                         savedPlayerItemBox.Items)
                {
                    SessionInventoryItemData sessionItem =
                        CreateSessionInventoryItem(
                            savedItem,
                            itemDataDatabase,
                            out string itemBoxItemError
                        );

                    if (sessionItem != null)
                    {
                        playerItemBoxSessionData.Items.Add(sessionItem);
                        restoredPlayerItemBoxCount++;
                    }
                    else
                    {
                        missingItemCount++;
                        LogTransferWarning(
                            $"プレイヤー保管箱のItemをロード時にスキップしました: {itemBoxItemError}"
                        );
                    }
                }
            }

            hasPlayerItemBoxSessionData = true;
        }

        facilitySessionData.Clear();
        int restoredFacilityCount = 0;

        if (saveData.FacilityLevels != null)
        {
            foreach (SavedTownFacilityLevelData savedFacility in
                     saveData.FacilityLevels)
            {
                if (savedFacility == null ||
                    string.IsNullOrWhiteSpace(savedFacility.FacilityId))
                {
                    continue;
                }

                string facilityId = savedFacility.FacilityId.Trim();

                if (FindFacilitySessionData(facilityId) != null)
                {
                    continue;
                }

                facilitySessionData.Add(
                    new TownFacilitySessionData
                    {
                        FacilityId = facilityId,
                        Level = Mathf.Max(1, savedFacility.Level)
                    }
                );

                restoredFacilityCount++;
            }
        }

        missionSessionData.Clear();
        int restoredMissionCount = 0;
        int skippedMissionCount = 0;

        if (saveData.Missions != null)
        {
            foreach (SavedMissionData savedMission in saveData.Missions)
            {
                if (savedMission == null ||
                    string.IsNullOrWhiteSpace(savedMission.MissionId))
                {
                    skippedMissionCount++;
                    continue;
                }

                MissionSessionData mission = new MissionSessionData
                {
                    Mission = null,
                    MissionId = savedMission.MissionId.Trim(),
                    DisplayName = string.IsNullOrWhiteSpace(savedMission.DisplayName)
                        ? savedMission.MissionId.Trim()
                        : savedMission.DisplayName,
                    State = savedMission.State,
                    Progress = Mathf.Max(0, savedMission.Progress),
                    RequiredAmount = Mathf.Max(1, savedMission.RequiredAmount),
                    RewardClaimed = savedMission.RewardClaimed,
                    AcceptSource = savedMission.AcceptSource
                };

                mission.Progress = Mathf.Clamp(
                    mission.Progress,
                    0,
                    mission.RequiredAmount
                );

                // 報酬受取済みなら、状態も達成済みに統一します。
                if (mission.RewardClaimed)
                {
                    mission.State = MissionSessionState.Completed;
                    mission.Progress = mission.RequiredAmount;
                }

                missionSessionData.Add(mission);
                restoredMissionCount++;
            }
        }

        storySessionData.Clear();
        int restoredStoryCount = 0;

        if (saveData.Stories != null)
        {
            foreach (SavedStoryQuestData savedStory in saveData.Stories)
            {
                if (savedStory == null ||
                    string.IsNullOrWhiteSpace(savedStory.StoryId))
                {
                    continue;
                }

                string storyId = savedStory.StoryId.Trim();

                if (FindStorySessionData(storyId) != null)
                {
                    continue;
                }

                storySessionData.Add(new StorySessionData
                {
                    Story = null,
                    StoryId = storyId,
                    DisplayName = string.IsNullOrWhiteSpace(savedStory.DisplayName)
                        ? storyId
                        : savedStory.DisplayName,
                    State = savedStory.State
                });

                restoredStoryCount++;
            }
        }

        storyFlags.Clear();

        if (saveData.StoryFlags != null)
        {
            foreach (string savedFlag in saveData.StoryFlags)
            {
                if (string.IsNullOrWhiteSpace(savedFlag) ||
                    HasStoryFlag(savedFlag))
                {
                    continue;
                }

                storyFlags.Add(savedFlag.Trim());
            }
        }

        trackedMissionId = saveData.TrackedMissionId ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(trackedMissionId))
        {
            MissionSessionData trackedData =
                FindMissionSessionData(trackedMissionId);

            if (trackedData == null ||
                (trackedData.State != MissionSessionState.InProgress &&
                 (trackedData.State != MissionSessionState.Completed || trackedData.RewardClaimed)))
            {
                trackedMissionId = string.Empty;
            }
        }

        int restoredSkillCardCount = RestoreSkillCardSession(
            saveData.SkillCards,
            itemDataDatabase
        );

        // KARMA_STEP1_LOAD
        PlayerKarmaReputation2D.GetOrCreate(this).RestoreFromSaveData(saveData.KarmaReputation);

        NotifyMoneyChanged();
        NotifyDeadNpcCountChanged();
        NotifyRescueProgressChanged();
        InventorySessionChanged?.Invoke();
        PlayerItemBoxSessionChanged?.Invoke();
        MissionSessionChanged?.Invoke();
        StorySessionChanged?.Invoke();
        TavernProgressChanged?.Invoke();
        SkillSessionChanged?.Invoke();

        resultMessage =
            $"所持金={currentMoney:N0} / " +
            $"墓場の死亡NPC={totalDeadNpcCount:N0}人 / " +
            $"累計救出NPC={totalRescuedNpcCount:N0}人 / " +
            $"特殊NPC救出={rescuedSpecialNpcIds.Count}人 / " +
            $"通常アイテム={restoredInventoryCount}件 / " +
            $"保管箱={restoredPlayerItemBoxCount}件 / " +
            $"ミッション={restoredMissionCount}件 / " +
            $"ストーリー={restoredStoryCount}件 / " +
            $"StoryFlag={storyFlags.Count}件 / " +
            $"酒場一人飲み={TavernPersonalDrinkTotal}回 / " +
            $"酒場おごり={TavernTreatEveryoneTotal}回 / " +
            $"施設={restoredFacilityCount}件 / " +
            $"スキルカード={restoredSkillCardCount}枚 / " +
            $"スキル枠={UnlockedSkillSlotCount}";

        if (missingItemCount > 0)
        {
            resultMessage +=
                $" / ItemData未登録などで{missingItemCount}件スキップ";
        }

        if (skippedMissionCount > 0)
        {
            resultMessage +=
                $" / 無効ミッション{skippedMissionCount}件スキップ";
        }

        LogTransfer($"本セーブデータ読込完了: {resultMessage}");
        return true;
    }

    private SavedSkillCardData CreateSavedSkillCardData()
    {
        EnsureSkillSessionData();

        SavedSkillCardData result = new SavedSkillCardData
        {
            UnlockedSlotCount = UnlockedSkillSlotCount
        };

        foreach (SkillCardData card in skillCardSessionData.OwnedCards)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.ItemId))
            {
                continue;
            }

            result.OwnedCardIds.Add(card.ItemId.Trim());
        }

        for (int i = 0; i < MaxSkillSlots; i++)
        {
            SkillCardData card = skillCardSessionData.EquippedCards[i];
            result.EquippedCardIds.Add(
                card != null && !string.IsNullOrWhiteSpace(card.ItemId)
                    ? card.ItemId.Trim()
                    : string.Empty
            );
        }

        return result;
    }

    private int RestoreSkillCardSession(
        SavedSkillCardData saved,
        ItemDataDatabase itemDataDatabase)
    {
        skillCardSessionData = new SkillCardSessionData();
        EnsureSkillSessionData();

        if (saved == null)
        {
            return 0;
        }

        skillCardSessionData.UnlockedSlotCount = Mathf.Clamp(
            saved.UnlockedSlotCount <= 0
                ? MinSkillSlots
                : saved.UnlockedSlotCount,
            MinSkillSlots,
            MaxSkillSlots
        );

        Dictionary<string, SkillCardData> restoredById =
            new Dictionary<string, SkillCardData>(
                StringComparer.OrdinalIgnoreCase
            );

        if (saved.OwnedCardIds != null)
        {
            foreach (string rawId in saved.OwnedCardIds)
            {
                string id = rawId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id) ||
                    restoredById.ContainsKey(id))
                {
                    continue;
                }

                if (itemDataDatabase.TryGetItemData(
                        id,
                        out ItemData itemData) &&
                    itemData is SkillCardData card)
                {
                    skillCardSessionData.OwnedCards.Add(card);
                    restoredById.Add(id, card);
                }
                else
                {
                    LogTransferWarning(
                        $"SkillCard ItemId={id} がItemDataDatabaseに見つからないか、SkillCardDataではありません。"
                    );
                }
            }
        }

        if (saved.EquippedCardIds != null)
        {
            int count = Mathf.Min(
                MaxSkillSlots,
                saved.EquippedCardIds.Count
            );

            for (int i = 0; i < count; i++)
            {
                if (i >= skillCardSessionData.UnlockedSlotCount)
                {
                    break;
                }

                string id = saved.EquippedCardIds[i]?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                if (!restoredById.TryGetValue(id, out SkillCardData card))
                {
                    if (itemDataDatabase.TryGetItemData(
                            id,
                            out ItemData itemData) &&
                        itemData is SkillCardData resolvedCard)
                    {
                        card = resolvedCard;
                        restoredById[id] = card;
                        skillCardSessionData.OwnedCards.Add(card);
                    }
                }

                if (card != null && FindEquippedSkillCardSlot(card) < 0)
                {
                    skillCardSessionData.EquippedCards[i] = card;
                }
            }
        }

        return skillCardSessionData.OwnedCards.Count;
    }

    private static SavedInventoryItemData CreateSavedInventoryItem(
        SessionInventoryItemData source)
    {
        if (source == null ||
            source.ItemData == null ||
            string.IsNullOrWhiteSpace(source.ItemData.ItemId) ||
            source.Amount <= 0)
        {
            return null;
        }

        return new SavedInventoryItemData
        {
            ItemId = source.ItemData.ItemId.Trim(),
            GridX = source.GridX,
            GridY = source.GridY,
            IsRotated = source.IsRotated,
            HasFourWayRotation = source.HasFourWayRotation,
            RotationSteps = source.ResolvedRotationSteps,
            Amount = Mathf.Max(1, source.Amount),
            HasStoredMagazineAmmo = source.HasStoredMagazineAmmo,
            StoredMagazineAmmo = Mathf.Max(0, source.StoredMagazineAmmo),
            StoredMagazineAmmoItemId =
                source.StoredMagazineAmmoType != null &&
                !string.IsNullOrWhiteSpace(source.StoredMagazineAmmoType.ItemId)
                    ? source.StoredMagazineAmmoType.ItemId.Trim()
                    : string.Empty,
            HasStoredWeaponDurability =
                source.HasStoredWeaponDurability,
            StoredWeaponDurability =
                Mathf.Max(0f, source.StoredWeaponDurability),
            StoredWeaponJammed = source.StoredWeaponJammed
        };
    }

    private static SessionInventoryItemData CreateSessionInventoryItem(
        SavedInventoryItemData source,
        ItemDataDatabase itemDataDatabase,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        if (source == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(source.ItemId))
        {
            errorMessage = "ItemIdが空です。";
            return null;
        }

        if (itemDataDatabase == null ||
            !itemDataDatabase.TryGetItemData(
                source.ItemId.Trim(),
                out ItemData itemData) ||
            itemData == null)
        {
            errorMessage =
                $"ItemId={source.ItemId} がItemDataDatabaseに登録されていません。";
            return null;
        }

        AmmoItemData storedMagazineAmmoType = null;

        if (!string.IsNullOrWhiteSpace(source.StoredMagazineAmmoItemId) &&
            itemDataDatabase.TryGetItemData(
                source.StoredMagazineAmmoItemId.Trim(),
                out ItemData storedAmmoItemData))
        {
            storedMagazineAmmoType =
                storedAmmoItemData as AmmoItemData;
        }

        return new SessionInventoryItemData
        {
            ItemData = itemData,
            GridX = Mathf.Max(0, source.GridX),
            GridY = Mathf.Max(0, source.GridY),
            IsRotated = source.IsRotated,
            HasFourWayRotation = source.HasFourWayRotation,
            RotationSteps = source.ResolvedRotationSteps,
            Amount = Mathf.Clamp(
                source.Amount,
                1,
                Mathf.Max(1, itemData.MaxStack)
            ),
            HasStoredMagazineAmmo = source.HasStoredMagazineAmmo,
            StoredMagazineAmmo = Mathf.Max(0, source.StoredMagazineAmmo),
            StoredMagazineAmmoType = storedMagazineAmmoType,
            HasStoredWeaponDurability =
                source.HasStoredWeaponDurability,
            StoredWeaponDurability =
                Mathf.Max(0f, source.StoredWeaponDurability),
            StoredWeaponJammed = source.StoredWeaponJammed
        };
    }

}
