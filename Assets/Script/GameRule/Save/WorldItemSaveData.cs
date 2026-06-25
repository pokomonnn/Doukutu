using System;
using System.Collections.Generic;

[Serializable]
public class WorldItemSaveData
{
    // どのシーンに置かれていたアイテムか
    public string sceneName;

    // ItemData.ItemId
    public string itemId;

    // スタック数
    public int amount;

    // インベントリ内の回転状態
    public bool isRotated;

    // 武器などの個別データ
    public bool hasStoredMagazineAmmo;
    public int storedMagazineAmmo;

    // ワールド座標
    public float positionX;
    public float positionY;
    public float positionZ;

    // 落下・投擲途中でも自然に復元するための速度
    public float velocityX;
    public float velocityY;
}

[Serializable]
public class WorldItemSaveCollection
{
    public List<WorldItemSaveData> items =
        new List<WorldItemSaveData>();
}