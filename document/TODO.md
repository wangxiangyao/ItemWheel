# ItemWheel 寰呭姙浜嬮」娓呭崟

*鏇存柊鏃堕棿: 2025-11-07*

---

## 馃殌 褰撳墠杩涘害鎬昏

- 鉁?**宸插畬鎴?*: 鍩虹杞洏绯荤粺銆侀暱鎸?鐭寜銆?瀹牸甯冨眬
- 鈿狅笍 **杩涜涓?*: 杞洏鎷栨嫿鍚屾鍒拌儗鍖?
- 馃搵 **璁″垝涓?*: 浠ｇ爜浼樺寲銆佹枃妗ｅ畬鍠?

---

## 猸愨瓙猸?鏈€楂樹紭鍏堢骇

### TODO-001: 瀹炵幇杞洏鎷栨嫿鍚屾鍒拌儗鍖?

**鐘舵€?*: 馃敶 **鏈紑濮?*

**闇€姹傛弿杩?*:
褰撶帺瀹跺湪杞洏涓婃嫋鎷界墿鍝佷氦鎹綅缃椂锛屽悓姝ユ洿鏂拌儗鍖呬腑鐨勭墿鍝侀『搴忥紝淇濇寔杞洏甯冨眬涓庤儗鍖呴『搴忎竴鑷淬€?

**鑳屾櫙**:
- 鉁?褰撳墠宸插疄鐜帮細鑳屽寘鐗╁搧椤哄簭 鈫?杞洏甯冨眬
- 鉂?灏氭湭瀹炵幇锛氭嫋鎷借疆鐩樼墿鍝?鈫?鏀瑰彉鑳屽寘椤哄簭

**鍙傝€冨疄鐜?*:
- `../Backpack_QuickWheel/code/src/ShortcutSystem/MainBackpackWheelManager.cs`
  - `OnWheelSlotsSwapped()` (琛?247-1261)
  - `AdjustWheelPosition()` (琛?271-1374)

**鎶€鏈柟妗?*:

#### 鏂规姒傝堪
鍦?`ItemWheelSystem` 涓坊鍔犲弻鍚戞槧灏勬満鍒讹紝鐩戝惉 QuickWheel 鐨勬Ы浣嶄氦鎹簨浠讹紝鍚屾鍒拌儗鍖呫€?

#### 瀹炵幇姝ラ

**Step 1: 娣诲姞鏄犲皠鏁版嵁缁撴瀯** (浼拌: 30鍒嗛挓)

鍦?`ItemWheelSystem.CategoryWheel` 涓坊鍔?
```csharp
public class CategoryWheel
{
    // 鏂板瀛楁
    public int[] WheelToBackpackMapping;     // 杞洏浣嶇疆 鈫?鑳屽寘浣嶇疆
    public Dictionary<int, int> BackpackToWheelMapping; // 鑳屽寘浣嶇疆 鈫?杞洏浣嶇疆

    // 鏋勯€犳椂鍒濆鍖?
    public CategoryWheel()
    {
        WheelToBackpackMapping = new int[8];
        Array.Fill(WheelToBackpackMapping, -1);
        BackpackToWheelMapping = new Dictionary<int, int>();
    }
}
```

**鏂囦欢**: `ItemWheelSystem.cs:39-47`

---

**Step 2: 寤虹珛鍒濆鏄犲皠** (浼拌: 45鍒嗛挓)

鍦?`RefreshCategorySlots()` 鏂规硶涓缓绔嬫槧灏勫叧绯?

```csharp
private bool RefreshCategorySlots(CategoryWheel wheel)
{
    // ... 鐜版湁浠ｇ爜鏀堕泦鐗╁搧 ...

    List<Item> collected = CollectItemsForCategory(wheel.Category);

    // 娓呯┖鏃ф槧灏?
    Array.Fill(wheel.WheelToBackpackMapping, -1);
    wheel.BackpackToWheelMapping.Clear();

    // 寤虹珛鏂版槧灏?
    for (int i = 0; i < collected.Count && i < 8; i++)
    {
        Item item = collected[i];
        int backpackPos = _inventory.Content.IndexOf(item);

        wheel.WheelToBackpackMapping[i] = backpackPos;
        wheel.BackpackToWheelMapping[backpackPos] = i;

        Debug.Log($"[ItemWheel] Mapping: Wheel[{i}] <-> Backpack[{backpackPos}] ({item.DisplayName})");
    }

    // ... 鐜版湁浠ｇ爜璁剧疆妲戒綅 ...
}
```

**鏂囦欢**: `ItemWheelSystem.cs:445-498`

---

**Step 3: 鍦?QuickWheel 涓坊鍔犱簨浠?* (浼拌: 1灏忔椂)

淇敼 `Wheel.cs` 娣诲姞妲戒綅浜ゆ崲浜嬩欢:

```csharp
// QuickWheel/src/Core/Wheel.cs
public class Wheel<T>
{
    // 鏂板浜嬩欢
    public event Action<int, int> OnSlotsSwapped;

    // 鍦ㄦЫ浣嶄氦鎹㈡椂瑙﹀彂锛堥渶瑕佹壘鍒颁氦鎹㈢殑瑙﹀彂鐐癸級
    protected virtual void NotifySlotSwapped(int fromIndex, int toIndex)
    {
        OnSlotsSwapped?.Invoke(fromIndex, toIndex);
    }
}
```

**鎴栬€?*鍦?`WheelSlotDisplay.cs` 涓Е鍙?

```csharp
// QuickWheel/src/UI/WheelSlotDisplay.cs
private void OnDragEnd(int targetIndex)
{
    // 鐜版湁浜ゆ崲閫昏緫...

    // 鏂板锛氶€氱煡鐖剁骇 Wheel
    _parentWheel?.NotifySlotSwapped(_myIndex, targetIndex);
}
```

**鏂囦欢**:
- `QuickWheel/src/Core/Wheel.cs`
- `QuickWheel/src/UI/WheelSlotDisplay.cs`

---

**Step 4: 鐩戝惉浜嬩欢骞跺悓姝ヨ儗鍖?* (浼拌: 1.5灏忔椂)

鍦?`ItemWheelSystem.EnsureWheel()` 涓闃呬簨浠?

```csharp
private CategoryWheel EnsureWheel(ItemWheelCategory category)
{
    // ... 鐜版湁浠ｇ爜鍒涘缓杞洏 ...

    Wheel<Item> wheel = new WheelBuilder<Item>()
        // ... 鐜版湁閰嶇疆 ...
        .Build();

    // 鏂板锛氳闃呮Ы浣嶄氦鎹簨浠?
    wheel.OnSlotsSwapped += (from, to) => OnWheelSlotsSwapped(context, from, to);

    context.Wheel = wheel;
    // ...
}
```

瀹炵幇浜ゆ崲澶勭悊鏂规硶:

```csharp
private bool _isPerformingSwap = false; // 闃叉閫掑綊

private void OnWheelSlotsSwapped(CategoryWheel wheel, int fromWheelPos, int toWheelPos)
{
    Debug.Log($"[ItemWheel] Slots swapped: {fromWheelPos} <-> {toWheelPos}");

    // 鑾峰彇鑳屽寘浣嶇疆
    int fromBackpackPos = wheel.WheelToBackpackMapping[fromWheelPos];
    int toBackpackPos = wheel.WheelToBackpackMapping[toWheelPos];

    if (fromBackpackPos == -1)
    {
        Debug.LogWarning($"[ItemWheel] Source position {fromWheelPos} is empty");
        return;
    }

    // 璁剧疆鏍囧織锛岄槻姝?onContentChanged 閫掑綊瑙﹀彂
    _isPerformingSwap = true;

    try
    {
        if (toBackpackPos != -1)
        {
            // 鎯呭喌1锛氱洰鏍囦綅缃湁鐗╁搧 - 浜ゆ崲鑳屽寘浣嶇疆
            var item1 = _inventory.GetItemAt(fromBackpackPos);
            var item2 = _inventory.GetItemAt(toBackpackPos);

            Debug.Log($"[ItemWheel] Swapping backpack positions: {fromBackpackPos} <-> {toBackpackPos}");

            item1.Detach();
            item2.Detach();
            _inventory.AddAt(item2, fromBackpackPos);
            _inventory.AddAt(item1, toBackpackPos);

            // 鏇存柊鏄犲皠锛堝弻鍚戜氦鎹級
            wheel.WheelToBackpackMapping[fromWheelPos] = toBackpackPos;
            wheel.WheelToBackpackMapping[toWheelPos] = fromBackpackPos;
            wheel.BackpackToWheelMapping[toBackpackPos] = fromWheelPos;
            wheel.BackpackToWheelMapping[fromBackpackPos] = toWheelPos;
        }
        else
        {
            // 鎯呭喌2锛氱洰鏍囦綅缃负绌?- 鍙洿鏂版槧灏勶紝涓嶆搷浣滆儗鍖?
            Debug.Log($"[ItemWheel] Target position is empty, updating mapping only");

            wheel.WheelToBackpackMapping[fromWheelPos] = -1;
            wheel.WheelToBackpackMapping[toWheelPos] = fromBackpackPos;
            wheel.BackpackToWheelMapping[fromBackpackPos] = toWheelPos;
        }
    }
    catch (Exception ex)
    {
        Debug.LogError($"[ItemWheel] Failed to sync backpack: {ex.Message}");
    }
    finally
    {
        _isPerformingSwap = false;
    }
}
```

**鏂囦欢**: `ItemWheelSystem.cs` (鏂板鏂规硶)

---

**Step 5: 闃叉閫掑綊浜嬩欢** (浼拌: 30鍒嗛挓)

淇敼 `RefreshCategorySlots()` 妫€鏌ユ爣蹇?

```csharp
private bool RefreshCategorySlots(CategoryWheel wheel)
{
    // 鍦ㄤ氦鎹㈣繃绋嬩腑璺宠繃鍒锋柊
    if (_isPerformingSwap)
    {
        Debug.Log($"[ItemWheel] Swap in progress, skip refresh");
        return true; // 杩斿洖true閬垮厤閿欒
    }

    // ... 鐜版湁鍒锋柊閫昏緫 ...
}
```

**鏂囦欢**: `ItemWheelSystem.cs:445`

---

#### 楠屾敹鏍囧噯

- [ ] 鎷栨嫿杞洏鐗╁搧鍚庯紝鎵撳紑鑳屽寘锛岀墿鍝侀『搴忓凡鏀瑰彉
- [ ] 鍏抽棴杞洏锛岄噸鏂版墦寮€锛屾柊椤哄簭淇濇寔
- [ ] 鍦ㄨ儗鍖呬腑鎵嬪姩璋冩暣椤哄簭锛岃疆鐩樺悓姝ユ洿鏂?
- [ ] 娌℃湁宕╂簝鎴栧紓甯告棩蹇?
- [ ] 绌轰綅浜ゆ崲姝ｅ父宸ヤ綔
- [ ] 涓嶅悓绫诲埆杞洏浜掍笉骞叉壈

#### 娴嬭瘯鐢ㄤ緥

**娴嬭瘯1: 鍩烘湰浜ゆ崲**
1. 鎵撳紑鍖荤枟杞洏锛堝亣璁炬湁3涓墿鍝侊細缁峰甫銆佹€ユ晳鍖呫€佸尰鐤楅拡锛?
2. 鎷栨嫿缁峰甫鍒版€ユ晳鍖呬綅缃?
3. 鍏抽棴杞洏
4. 鎵撳紑鑳屽寘锛岄獙璇佺墿鍝侀『搴忥細鎬ユ晳鍖呫€佺环甯︺€佸尰鐤楅拡

**娴嬭瘯2: 绌轰綅浜ゆ崲**
1. 鎵撳紑杞洏锛?涓墿鍝侊級
2. 鎷栨嫿浣嶇疆1鐨勭墿鍝佸埌绌轰綅缃?
3. 楠岃瘉浣嶇疆1鍙樼┖锛屼綅缃?鏈夌墿鍝?

**娴嬭瘯3: 璺ㄧ被鍒笉骞叉壈**
1. 璋冩暣鍖荤枟杞洏鐗╁搧椤哄簭
2. 鎵撳紑鍒烘縺鍓傝疆鐩橈紝楠岃瘉椤哄簭鏈彈褰卞搷
3. 鍦ㄨ儗鍖呬腑璋冩暣鍒烘縺鍓傜墿鍝?
4. 閲嶆柊鎵撳紑鍖荤枟杞洏锛岄獙璇佸尰鐤楃墿鍝侀『搴忎繚鎸?

**娴嬭瘯4: 閫掑綊闃叉姢**
1. 鍚敤璇︾粏鏃ュ織
2. 鎵ц澶氭蹇€熸嫋鎷戒氦鎹?
3. 妫€鏌ユ棩蹇楋紝纭娌℃湁閫掑綊璋冪敤璀﹀憡

---

#### 棰勮宸ヤ綔閲?

- **鎬绘椂闂?*: 4-5灏忔椂
- **闅惧害**: 馃敟馃敟馃敟 涓珮
- **椋庨櫓**:
  - 鈿狅笍 QuickWheel 鍙兘娌℃湁鏆撮湶鎷栨嫿浜嬩欢锛岄渶瑕佷慨鏀规簮鐮?
  - 鈿狅笍 閫掑綊浜嬩欢鍙兘瀵艰嚧姝诲惊鐜紝闇€瑕佷粩缁嗘祴璇?

---

## 猸愨瓙 涓瓑浼樺厛绾?

### TODO-002: 瀹屽杽 QuickWheel 浜嬩欢绯荤粺

**鐘舵€?*: 馃煛 **渚濊禆 TODO-001**

**闇€姹傛弿杩?*:
QuickWheel 褰撳墠鍙兘娌℃湁瀹屾暣鏆撮湶妲戒綅浜ゆ崲浜嬩欢锛岄渶瑕佸湪妗嗘灦灞傞潰瀹屽杽浜嬩欢绯荤粺銆?

**瀹炵幇寤鸿**:

鍦?`Wheel.cs` 涓坊鍔?
```csharp
public event Action<int, int> OnSlotsSwapped;
public event Action<int> OnSlotRemoved;
public event Action<int, T> OnSlotUpdated;
```

鍦?`WheelSlotDisplay.cs` 涓Е鍙?
```csharp
protected void TriggerSlotSwapped(int fromIndex, int toIndex)
{
    _parentWheel.OnSlotsSwapped?.Invoke(fromIndex, toIndex);
}
```

**鏂囦欢**:
- `QuickWheel/src/Core/Wheel.cs`
- `QuickWheel/src/UI/WheelSlotDisplay.cs`

**棰勮宸ヤ綔閲?*: 1-2灏忔椂

---

### TODO-003: 鏀寔鑳屽寘鐗╁搧澧炲垹鏃惰嚜鍔ㄦ洿鏂拌疆鐩?

**鐘舵€?*: 馃敶 **鏈紑濮?*

**闇€姹傛弿杩?*:
褰撶帺瀹跺湪鑳屽寘涓坊鍔犳垨鍒犻櫎鐗╁搧鏃讹紝濡傛灉杞洏姝ｅ湪鏄剧ず锛岃嚜鍔ㄥ埛鏂拌疆鐩樺唴瀹广€?

**褰撳墠闂**:
- 杞洏鏄剧ず鍚庯紝淇敼鑳屽寘鐗╁搧
- 鍏抽棴杞洏鍐嶆鎵撳紑锛屽彲鑳芥樉绀烘棫鏁版嵁

**瀹炵幇寤鸿**:

鐩戝惉 `Inventory.onContentChanged` 浜嬩欢:
```csharp
private void OnInventoryChanged(Inventory inventory, int changedSlot)
{
    if (_isPerformingSwap) return; // 璺宠繃鑷繁瑙﹀彂鐨勫彉鍖?

    // 妫€鏌ュ摢涓被鍒彈褰卞搷
    ItemWheelCategory affectedCategory = DetermineCategory(changedSlot);

    if (affectedCategory != ItemWheelCategory.None)
    {
        // 濡傛灉璇ョ被鍒疆鐩樻鍦ㄦ樉绀猴紝鍒锋柊
        if (_wheels.TryGetValue(affectedCategory, out var wheel))
        {
            if (wheel.Wheel.IsVisible)
            {
                RefreshCategorySlots(wheel);
                // 閫氱煡UI鍒锋柊
                wheel.View?.Refresh();
            }
        }
    }
}
```

**鏂囦欢**: `ItemWheelSystem.cs` (鏂板鏂规硶)

**棰勮宸ヤ綔閲?*: 2-3灏忔椂

---

### TODO-004: 娣诲姞杞洏鐗╁搧鏁伴噺鏄剧ず

**鐘舵€?*: 馃敶 **鏈紑濮?*

**闇€姹傛弿杩?*:
鍦ㄨ疆鐩樻牸瀛愪笂鏄剧ず鐗╁搧鍫嗗彔鏁伴噺锛堜緥濡傦細缁峰甫 x3锛?

**鍙傝€?*: 娓告垙鍘熺敓 `ItemDisplay` 鏈夋暟閲忔樉绀?

**瀹炵幇浣嶇疆**: `WheelSlotDisplay.cs`

**棰勮宸ヤ綔閲?*: 1灏忔椂

---

## 猸?浣庝紭鍏堢骇

### TODO-005: 鍑忓皯Debug鏃ュ織杈撳嚭

**鐘舵€?*: 馃敶 **鏈紑濮?*

**闇€姹傛弿杩?*:
褰撳墠鏈夊ぇ閲?Debug.Log锛屽奖鍝嶆€ц兘鍜屽彲璇绘€э紝鐢熶骇鐜涓嶉渶瑕併€?

**瀹炵幇鏂规**:

鏂规1: 鏉′欢缂栬瘧
```csharp
#if DEBUG
    Debug.Log("[ItemWheel] ...");
#endif
```

鏂规2: 鏃ュ織绛夌骇
```csharp
public static class WheelLog
{
    public static LogLevel Level = LogLevel.Warning;

    public static void Info(string msg)
    {
        if (Level <= LogLevel.Info) Debug.Log(msg);
    }
}
```

**棰勮宸ヤ綔閲?*: 1灏忔椂

---

### TODO-006: 鎻愬彇閰嶇疆甯搁噺

**鐘舵€?*: 馃敶 **鏈紑濮?*

**闇€姹傛弿杩?*:
灏嗛瓟娉曟暟瀛楁彁鍙栧埌閰嶇疆绫汇€?

**褰撳墠闂**:
- 闀挎寜闃堝€?`0.15f` 纭紪鐮佸湪浠ｇ爜涓?
- 鏍煎瓙澶у皬 `90f` 纭紪鐮?
- 闂磋窛 `12f` 纭紪鐮?

**瀹炵幇鏂规**:

鍒涘缓 `WheelConfig.cs`:
```csharp
public static class WheelConfig
{
    public const int SLOT_COUNT = 9;
    public const float LONG_PRESS_THRESHOLD = 0.15f;
    public const float GRID_CELL_SIZE = 90f;
    public const float GRID_SPACING = 12f;

    // 杞洏浣嶇疆鏄犲皠
    public static readonly Vector2Int[] GRID_POSITIONS = ...;
}
```

**鏂囦欢**: 鏂板缓 `WheelConfig.cs`

**棰勮宸ヤ綔閲?*: 30鍒嗛挓

---

### TODO-007: 缂栧啓鍗曞厓娴嬭瘯

**鐘舵€?*: 馃敶 **鏈紑濮?*

**闇€姹傛弿杩?*:
涓哄叧閿€昏緫娣诲姞鍗曞厓娴嬭瘯銆?

**娴嬭瘯鑼冨洿**:
- 鍙屽悜鏄犲皠姝ｇ‘鎬?
- 闀挎寜/鐭寜妫€娴?
- 鐗╁搧鍒嗙被閫昏緫

**妗嗘灦**: Unity Test Framework 鎴?NUnit

**棰勮宸ヤ綔閲?*: 3-4灏忔椂

---

### TODO-008: 娣诲姞XML鏂囨。娉ㄩ噴

**鐘舵€?*: 馃敶 **鏈紑濮?*

**闇€姹傛弿杩?*:
涓哄叕鍏盇PI娣诲姞XML娉ㄩ噴锛屾柟渚縄DE鏅鸿兘鎻愮ず銆?

**绀轰緥**:
```csharp
/// <summary>
/// 鏄剧ず鎸囧畾绫诲埆鐨勮疆鐩?
/// </summary>
/// <param name="category">鐗╁搧绫诲埆</param>
/// <param name="wheelCenter">杞洏涓績浣嶇疆锛堝彲閫夛級</param>
/// <returns>鏄惁鎴愬姛鏄剧ず</returns>
public bool ShowWheel(ItemWheelCategory category, Vector2? wheelCenter = null)
```

**棰勮宸ヤ綔閲?*: 2灏忔椂

---

### TODO-009: 缁樺埗瀹屾暣鏋舵瀯鍥?

**鐘舵€?*: 馃敶 **鏈紑濮?*

**闇€姹傛弿杩?*:
浣跨敤 draw.io 缁樺埗瀹屾暣鏋舵瀯鍥撅紝鍖呮嫭锛?
- 绫诲叧绯诲浘
- 鏁版嵁娴佸浘
- 浜嬩欢娴佸浘

**鍙傝€?*: `document/QuickWheel鏋舵瀯鍥?drawio`

**棰勮宸ヤ綔閲?*: 2灏忔椂

---

## 馃悰 宸茬煡闂

### BUG-001: Y杞村潗鏍囩郴鍙嶈浆

**鐘舵€?*: 鉁?**宸蹭慨澶?*

**闂**: 榧犳爣绉诲姩鍒颁笂杈归€夋嫨浜嗕笅杈?

**鏍瑰洜**: Unity鍧愭爣绯籝鍚戜笂涓烘锛屽睆骞曞潗鏍囩郴Y鍚戜笅涓烘

**淇**: `Vector2 correctedDirection = new Vector2(direction.x, -direction.y);`

**鏂囦欢**: `QuickWheel/src/Selection/GridSelectionStrategy.cs`

---

### BUG-002: 鐗╁搧鍥炬爣涓嶆樉绀?

**鐘舵€?*: 鉁?**宸蹭慨澶?*

**闂**: WheelSlotDisplay 鍙湪鍒濆鍖栨椂鏈夋暟鎹墠鍒涘缓鍥炬爣瀵硅薄

**淇**: 鎬绘槸鍒涘缓鍥炬爣鍜屾爣绛惧璞★紝鐢?SetActive() 鎺у埗鏄剧ず

**鏂囦欢**: `QuickWheel/src/UI/WheelSlotDisplay.cs`

---

## 馃搳 杩涘害杩借釜

### 鍔熻兘瀹屾垚搴?

| 鍔熻兘妯″潡 | 鐘舵€?| 瀹屾垚搴?|
|---------|------|--------|
| 蹇嵎閿嫤鎴?| 鉁?瀹屾垚 | 100% |
| 闀挎寜/鐭寜妫€娴?| 鉁?瀹屾垚 | 100% |
| 9瀹牸甯冨眬 | 鉁?瀹屾垚 | 100% |
| 鑷畾涔塖prite | 鉁?瀹屾垚 | 100% |
| 鐗╁搧鍥炬爣鏄剧ず | 鉁?瀹屾垚 | 100% |
| 鑳屽寘鈫掕疆鐩樺悓姝?| 鉁?瀹屾垚 | 100% |
| **杞洏鈫掕儗鍖呭悓姝?* | 馃敶 **鏈紑濮?* | **0%** |
| 瀹炴椂鍒锋柊 | 鈿狅笍 閮ㄥ垎瀹屾垚 | 70% |
| 閿欒澶勭悊 | 鈿狅笍 閮ㄥ垎瀹屾垚 | 60% |
| 鍗曞厓娴嬭瘯 | 馃敶 鏈紑濮?| 0% |
| 鏂囨。瀹屽杽 | 鈿狅笍 杩涜涓?| 80% |

---

## 馃幆 閲岀▼纰?

### Milestone 1: 鍩虹鍔熻兘 鉁?

- [x] 蹇嵎閿嫤鎴?
- [x] 闀挎寜/鐭寜妫€娴?
- [x] 9瀹牸甯冨眬
- [x] 鐗╁搧鏄剧ず
- [x] 鐗╁搧浣跨敤

**瀹屾垚鏃ユ湡**: 2025-11-06

---

### Milestone 2: 鏍稿績鍔熻兘 鈿狅笍 (杩涜涓?

- [x] 鑳屽寘鐗╁搧鈫掕疆鐩樺竷灞€
- [ ] **杞洏鎷栨嫿鈫掕儗鍖呭悓姝?* 鈫?褰撳墠鍏抽敭浠诲姟
- [ ] 瀹炴椂鍒锋柊鏈哄埗
- [ ] 杈圭晫鎯呭喌澶勭悊

**鐩爣鏃ユ湡**: 2025-11-08

---

### Milestone 3: 浼樺寲涓庢祴璇?馃敎

- [ ] 鍗曞厓娴嬭瘯瑕嗙洊
- [ ] 鎬ц兘浼樺寲
- [ ] 鏃ュ織浼樺寲
- [ ] 浠ｇ爜閲嶆瀯

**鐩爣鏃ユ湡**: 2025-11-10

---

### Milestone 4: 鍙戝竷鍑嗗 馃敎

- [ ] 鏂囨。瀹屽杽
- [ ] 浣跨敤鎵嬪唽
- [ ] 鍙戝竷娴嬭瘯
- [ ] 鐢ㄦ埛鍙嶉鏀堕泦

**鐩爣鏃ユ湡**: 2025-11-12

---

## 馃摓 鑱旂郴涓庡崗浣?

濡傞渶璁ㄨ浠讳綍TODO椤癸紝璇凤細
1. 鍦ㄦ鏂囨。涓坊鍔犺瘎璁?
2. 鍒涘缓 GitHub Issue锛堝鏈夛級
3. 鏇存柊杩涘害鐘舵€?

---

*鏈€鍚庢洿鏂? 2025-11-07 by Claude*


### TODO-NEXT: 拖拽时取消不应使用物品（防误触）

状态: 🟡 待实现

需求：当轮盘上发生过拖拽操作后，玩家松开快捷键关闭轮盘时，应取消选择（不触发使用），避免误用物品。

要点：
- 在触发确认（ManualConfirm/Hide(executeSelection=true)）前，检测 UI 是否处于拖拽态；若在拖拽中，则改为取消（ManualCancel）。
- 检测拖拽态可通过 DefaultWheelView 内部 UIManager 的 IsDragging 属性（反射）获取。
- 关闭轮盘时，仍保留“点击选择并关闭”与“Hover选择并关闭”的既有语义。

文件参考：
- QuickWheel 核心：..\\QuickWheel\\src\\Core\\Wheel.cs:206（Hide 流程）
- UI 管理：..\\QuickWheel\\src\\UI\\WheelUIManager.cs（IsDragging）
- 调用侧：ItemWheelSystem.cs:167（ConfirmWheelSelection）

---

## ⭐ 优先级（短期）

### 1) 近战武器轮盘（V）

状态: 🟡 待实现

需求：
- 快捷键：沿用官方 V。
- 收集物品：背包中 Tag= MeleeWeapon 的物品。
- 交互：类似手雷，选择后先将武器切到手上（装备）。

要点：
- 新增一个 ItemWheelCategory：Melee（或单独 Wheel 实例）。
- 选择策略、UI 复用现有网格轮盘。
- 成功装备后，重置“不可使用”情绪计数。

依赖：物品到手的持有接口（参考 EquipItemToHand）。

### 2) 子弹切换轮盘（长按 R）

状态: 🟡 待实现

需求：
- 快捷键：由官方 T 改为长按 R 呼出。
- 收集物品：当前武器的所有可用弹药类型（从背包中可用的弹药里筛选）。
- 交互：Hover 并关闭、或点击并关闭，均直接切换到该类子弹。
- UI：武器 UI 顶部的子弹类型高亮颜色做明显区分（记录创意，稍后实现）。

要点：
- 需要获取“当前武器”“可用弹药类型”的数据接口。
- 切换弹药的底层调用入口（参考官方实现）。
- 轮盘只展示与当前武器匹配且在背包中有存量的弹药类型。

### 3) 语音轮盘（长按 F1）

状态: 🟡 待实现（功能记录）

需求：
- 快捷键：长按 F1。
- 功能：播放语音、显示气泡文字（可直接复用 BubbleNotifier）。

要点：
- 文案/语音表可配置（后续）。
- 与条件化提示系统共用一套展示（文本/语音可选）。

---

## 🌿 功能优化/视觉

### 轮盘物品信息显示优化

状态: 🟡 待实现

需求：
- 在物品名字上方显示：堆叠数量、耐久度。
- 物品名字靠右对齐显示。

要点：
- 修改 QuickWheel.UI.WheelSlotDisplay 的 UI 结构，增加数量/耐久度文本节点。
- 右对齐名称：调整 Text 对齐或 RectTransform 锚点。
- 注意与自定义 Slot Sprite 的适配（不遮挡）。

---

## 💡 创意/后续记录

- 为不同“不可使用原因”配置不同的气泡文案（与 ConditionHintManager 的条件扩展对齐）：
  - 冷却中、无目标、被阻挡、弹药不足、姿态不对等。
  - 每个原因支持多条文案与情绪升级、轮换。
- 语音轮盘与提示系统打通：文本+语音双轨输出。
- 高亮当前武器弹药类型的 UI（颜色与可用性状态明显区分）。
