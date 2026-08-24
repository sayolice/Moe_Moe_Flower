# Moe Moe Flower — 게임 설계/기술 문서 (AI 전달용)

> 이 문서는 GPT/Claude 등 다른 AI에게 이 프로젝트를 통째로 이해시켜서 향후 기획·개발 논의를 이어가기 위한 참고 자료입니다.
> 코드/씬을 직접 조사해서 작성했으며, 아직 구현되지 않은 것은 "미구현"이라고 명시했습니다. 추측이나 희망사항은 넣지 않았습니다.
> 최종 갱신 기준: Unity 6000.5.5f1, 프로젝트 루트 `D:\Development\Unity\projects\Moe_Moe_flower`.

---

## 1. 게임 개요

- **장르**: 방치형(idle/incremental) 육성 게임. 모바일 세로 화면(9:16)이 기본 타깃이고, PC 빌드도 지원한다.
- **핵심 판타지**: 씨앗을 심어 꽃을 키우고, 애정을 줘서 "꽃소녀"로 개화시킨다. 개화한 꽃은 이후 초당 골드를 자동 생산한다. 여러 꽃을 모으고 각각 레벨업시켜 생산량을 키우는 것이 목표.
- **플랫폼**: 모바일 + PC. PC에서는 화면 좌/우에 사이드바가 추가로 붙는 3열 레이아웃, 모바일에서는 중앙 영역만 보이고 사이드바는 자동으로 숨는다.
- **저장/오프라인 수익**: 아직 없음 (의도적으로 이번 개발 범위 밖). 앱을 완전히 종료하면 진행 상황이 전부 초기화된다. 단, PC 빌드에서 창이 백그라운드/최소화 상태여도 게임 루프는 계속 실행된다 (`ProjectSettings.asset`의 `runInBackground: 1`).

---

## 2. 핵심 게임 루프

```
화면 터치/클릭
  → 현재 표시 중인 꽃에 "터치 애정" 만큼 애정치 증가 + "터치 골드" 만큼 골드 획득 (조건 없이 항상)
  → 애정치가 requiredAffection에 도달하면 개화(Bloom)
      → 이후 그 꽃은 매 프레임 "초당 골드(G/s)"를 자동 생산 (화면에 안 보여도 계속)
  → 동시에, 보유한 모든 "미개화" 꽃에는 "자동 애정"이 매 프레임 자동으로 더해진다 (화면 표시 여부 무관, 전부 동시에)
  → 모은 골드로:
      - 새 꽃 씨앗 구매 (좌측 상점)
      - 보유한 꽃 레벨업 → G/s 증가 (우측 "꽃" 탭, 화면에 안 보이는 꽃도 직접 레벨업 가능)
      - 플레이어 전역 스탯 강화 → 터치 애정/터치 골드/자동 애정 자체의 수치가 오름 (우측 "플레이어" 탭)
  → 반복
```

중앙 화면에는 항상 "지금 표시 중인 꽃" 1개만 보인다. 하지만 **성장/생산은 표시 여부와 무관하게 보유한 모든 꽃에서 동시에 진행**된다는 것이 이 게임의 핵심 설계 포인트다 (아래 4장 참고).

---

## 3. 데이터 아키텍처 원칙

프로젝트 전체에 걸쳐 하나의 패턴이 일관되게 반복된다:

```
XxxData        (ScriptableObject) : 고정 밸런스 수치. 코드가 아니라 Inspector에서 채움. 여러 인스턴스(에셋) 가능.
XxxInstance    (순수 C# 클래스)    : 런타임에 변하는 상태만. MonoBehaviour 아님. 저장 시스템 붙을 때 그대로 직렬화 가능하게 설계.
XxxManager     (싱글턴 MonoBehaviour) : Xxx들을 전부 소유/관리. Awake에서 static Instance 등록 + DontDestroyOnLoad.
```

이 패턴이 **꽃 시스템**과 **플레이어 전역 강화 시스템** 양쪽에 동일하게 적용되어 있고, 두 시스템은 서로 독립적이다 (플레이어 강화 로직이 꽃 레벨업 코드와 섞이지 않음, 반대로 FlowerManager가 PlayerStatManager를 읽어가는 단방향 의존만 있음).

---

## 4. 꽃 시스템

### 4.1 FlowerData (ScriptableObject) — `Scripts/Data/FlowerData.cs`

```
flowerId, displayName, description
seedSprite, sproutSprite, growingSprite, bloomSprite   (성장 4단계 스프라이트)
requiredAffection   (개화에 필요한 총 애정량)
seedPrice           (씨앗 구매 가격, 골드)
baseGoldPerSecond    (Lv.1 기준 초당 골드 생산량)
levelUpBaseCost      (Lv.1→2 레벨업 비용)
levelUpGrowthRate    (레벨업 비용 성장률, 기본 1.20)
goldPerSecondGrowthRate (레벨업당 G/s 성장률, 기본 1.15)
hasPassive, passiveType, passiveValue   (패시브 — 필드만 있고 로직 미구현)
```

계산 함수:
- `GetLevelUpCost(currentLevel) = levelUpBaseCost * levelUpGrowthRate^(currentLevel-1)`
- `GetGoldPerSecond(level) = baseGoldPerSecond * goldPerSecondGrowthRate^(level-1)`
- `GetLevelUpCostForLevels(fromLevel, levels)` — fromLevel에서 levels번 연속 레벨업 시 총 비용 (미리보기용)
- `GetMaxAffordableLevels(fromLevel, gold)` — 주어진 골드로 최대 몇 레벨까지 오를 수 있는지 (안전 상한 100,000회 루프)

**현재 등록된 꽃 데이터는 1종뿐** (`Assets/Project/Scripts/ScriptableObjects/FlowerGirl/Flower Data/Flower_Dandelion.asset`, 민들레):
```
flowerId: dandelion, displayName: 민들레
requiredAffection: 100, seedPrice: 0 (첫 꽃, 무료)
baseGoldPerSecond: 1, levelUpBaseCost: 10
levelUpGrowthRate: 1.2, goldPerSecondGrowthRate: 1.15
hasPassive: false
```

### 4.2 FlowerInstance (순수 C#) — `Scripts/Data/FlowerInstance.cs`

```
flowerId, currentAffection (float), currentLevel (int, 개화 전엔 0), isBloomed (bool)
```
`GetGrowthPercent(requiredAffection)`, `GetGrowthStage(requiredAffection)` → `GrowthStage{Seed, Sprout, Growing, Bloomed}` (25%/50%/100% 구간).

### 4.3 FlowerManager (싱글턴) — `Scripts/Core/Flowermanager.cs`

- `allFlowers: List<FlowerData>` — 도감 순서 = 이 리스트 순서. 현재 1종만 들어있음.
- `ownedFlowers: Dictionary<string, FlowerInstance>` — **실제로 구매한 꽃만** 존재. 최초 실행 시 `allFlowers[0]`(민들레)을 자동 무료 지급 (튜토리얼).
- `currentDisplayedFlowerId` — 중앙 화면에 지금 보이는 꽃. 도감순 스와이프(`SwipeNext/Previous`)나 도감 이동(`TryJumpTo`)으로 바뀜.
- 이벤트: `OnDisplayedFlowerChanged(id)`, `OnFlowerBloomed(id)`, `OnOwnedFlowersChanged()`
- **`Update()`이 이 게임의 핵심 로직**:
  1. `ownedFlowers` 전체 순회 → `isBloomed`인 꽃마다 `GetGoldPerSecond(level) * dt`만큼 골드 생산 (화면 표시 무관)
  2. `ownedFlowers` 전체 순회 → `!isBloomed`인 꽃마다 `PlayerStatManager.GetCurrentValue(AutoAffection) * dt`만큼 애정 증가 (화면 표시 무관, **보유한 모든 미개화 꽃에 동시에** 적용됨 — 이번 세션에서 확정한 설계)
- `ClickCurrentFlower()` — 화면 터치 시 호출. 현재 표시 중인 꽃에게만: 골드는 무조건 증가(`PlayerStatManager.GetCurrentValue(TouchGold)`), 미개화면 애정도 증가(`TouchAffection`).
- `TryPurchaseSeed(id)` — 구매 성공 시 그 꽃이 자동으로 중앙에 표시됨.
- `TryLevelUpFlowerBy(id, levels)` / `TryLevelUpFlowerToMax(id)` — **화면에 안 보이는 꽃도 id만 알면 레벨업 가능**. 개화한 꽃만 가능, 골드 부족 지점에서 멈추고 실제로 오른 레벨 수를 반환. 오프스크린 레벨업이어도 다음 프레임부터 G/s에 즉시 반영됨 (딕셔너리 안의 `FlowerInstance` 객체를 직접 수정하기 때문).
- `TryLevelUpFlower(id)` — 구버전 단일 API, 내부적으로 `TryLevelUpFlowerBy(id,1)`에 위임 (하위 호환용으로 남겨둠).

### 4.4 화면 표시/입력

- **FlowerDisplayController** (`Scripts/Core/FlowerDisplayController.cs`) — 중앙 화면에 "현재 표시 중인 꽃" 1개만 그림. 이벤트 구독이 아니라 **매 프레임 폴링**으로 갱신 (Awake/Start 실행 순서 보장 안 되는 문제 회피). 스프라이트가 없으면 성장 단계별 색상 사각형(placeholder)을 대신 그림.
- **ScreenTouchController** (`Scripts/Input/ScreenTouchController.cs`) — 화면 전체 클릭(PC 마우스)/터치(모바일) 감지 → `FlowerManager.ClickCurrentFlower()` 호출. 새 Input System의 `Mouse.current`/`Touchscreen.current`를 직접 폴링 (Input Action Asset은 안 씀).
- **FlowerController.cs** — ⚠️ **더 이상 존재하지 않음.** 화분 1개당 자체 로직을 갖던 구버전 설계였고, `FlowerManager`+`FlowerDisplayController` 조합으로 완전히 대체되어 미사용 상태였다가, 프로젝트 어디서도 참조되지 않음을 확인 후 삭제했다. 백업본이 `Assets/Project/Legacy/FlowerController.cs.txt`에 남아있음 (확장자를 `.txt`로 바꿔서 Unity가 컴파일하지 않도록 처리).

---

## 5. 플레이어 전역 강화 시스템 (꽃과 완전히 독립)

터치 클릭 효율과 자동 성장 속도를 올리는 시스템. 꽃 시스템과 데이터/로직이 섞이지 않는다.

### 5.1 PlayerStatData (ScriptableObject) — `Scripts/Data/PlayerStatData.cs`
```
statId, displayName, valueSuffix (표시용 단위, 예: "/s")
startingLevel   (터치 스탯은 1=항상 작동, 자동 애정은 0=강화 전엔 꺼짐)
baseValue, valueGrowthRate     (Lv.1 기준 값과 성장률)
upgradeBaseCost, upgradeCostGrowthRate   (강화 비용과 성장률)
```
- `GetValue(level)` = `level<=0`이면 0, 아니면 `baseValue * valueGrowthRate^(level-1)`
- `GetUpgradeCost(currentLevel)` = `upgradeBaseCost * upgradeCostGrowthRate^(currentLevel - startingLevel)` — `startingLevel`을 지수의 기준점으로 잡아서, 시작 레벨이 다른 스탯끼리도 "upgradeBaseCost = 첫 강화 비용"이라는 의미가 동일하게 유지됨
- `CreateAssetMenu`: `Create > FlowerGirl > Player Stat Data`, 기본 파일명 `NewPlayerStatData`

**확정된 에셋 3종** (`Assets/Project/ScriptableObjects/`):

| 파일 | statId | displayName | startingLevel | baseValue | upgradeBaseCost | valueGrowthRate | upgradeCostGrowthRate |
|---|---|---|---|---|---|---|---|
| TouchAffection.asset | touchAffection | 터치 애정 | 1 | 1 | 50 | 1.15 | 1.20 |
| TouchGold.asset | touchGold | 터치 골드 | 1 | 1 | 50 | 1.15 | 1.20 |
| AutoAffection.asset | autoAffection | 자동 애정 | 0 | 1 | 5000 | 1.90 | 1.20 |

### 5.2 PlayerStatInstance (순수 C#) — `Scripts/Data/PlayerStatInstance.cs`
`currentLevel` 하나만 보관.

### 5.3 PlayerStatManager (싱글턴) — `Scripts/Core/PlayerStatManager.cs`
- `statEntries: List<StatEntry>` — `{PlayerStatType type, PlayerStatData data}` 쌍의 리스트. Inspector에서 3종 연결.
- `PlayerStatType` enum: `TouchAffection, TouchGold, AutoAffection`
- `GetData/GetLevel/GetCurrentValue/GetUpgradeCost/TryUpgrade(type)` — `TryUpgrade`는 `GameManager.TrySpendGold`로 골드 차감 시도 후 성공 시 `currentLevel++`.
- ⚠️ **씬에 아직 GameObject로 배치되지 않은 상태** (현재 진행 중인 작업). 배치 전에는 `PlayerStatManager.Instance == null`이라 `FlowerManager`/`GameManager` 쪽에서 전부 안전하게 0값으로 폴백함 (클릭해도 골드/애정이 안 오름 — 의도된 안전장치이지 버그 아님).

### 5.4 GameManager (싱글턴) — `Scripts/Core/GameManager.cs`
- 이제 **`totalGold`(double) + `AddGold`/`TrySpendGold`만** 담당. 예전엔 `touchAffectionAmount`/`touchGoldAmount` 필드가 여기 있었지만 PlayerStatManager로 완전히 이관하면서 제거됨.

---

## 6. UI 아키텍처

### 6.1 PC 3열 레이아웃 (`Tools > 🌸 Build PC Layout` 에디터 메뉴가 이 전체를 자동 생성)

```
GameCanvas (CanvasScaler 1920x1080 ScaleWithScreenSize)
└─ PCLayout (PCLayoutController: 사이드바 on/off, 모바일이면 자동 숨김)
   ├─ LeftSidebar → LeftSlot
   │   ├─ ShopPanel        (좌측 = 탐색/수집 계열)
   │   └─ DexPanel (비활성 placeholder)
   ├─ MobileGameArea (607.5×1080, 9:16 고정)
   │   ├─ TopBar (골드, 골드/s)
   │   └─ FlowerStatusUI (애정 게이지, 애정/s, 애정 수치 — 레벨 텍스트는 여기서 제거됨, 아래 참고)
   └─ RightSidebar → RightSlot
       └─ GrowthPanel      (우측 = 성장 계열)
```
`FlowerDisplay`(GameObject, `FlowerDisplayController`+`SpriteRenderer`)는 Canvas가 아니라 씬 최상위(World Space)에 별도로 존재.

**`PCLayoutController`와 `PanelSwitcher`는 서로 다른 층위**: `PCLayoutController`는 "LeftSlot/RightSlot에 어떤 큰 패널이 들어가는지"(하이어라키 드래그로 교체, 런타임 이동 없음)만 관리하고, 그 안의 탭 전환은 `PanelSwitcher`가 별도로 담당한다.

### 6.2 GrowthPanel (우측) — 이 프로젝트의 "범용 패널 전환 시스템" 적용 사례

```
GrowthPanel (PanelSwitcher: activeTabColor/inactiveTabColor를 Inspector에서 지정)
├─ TabBar
│   ├─ FlowerTabButton  → FlowerUpgradePanel
│   └─ PlayerTabButton  → PlayerUpgradePanel
└─ PanelContainer
    ├─ FlowerUpgradePanel  (기본 활성)
    │   ├─ ListHeader
    │   │   ├─ SortSelectorButton   (FlowerSortSelector: 도감순→가격↑→가격↓→레벨↑→레벨↓ 순환)
    │   │   └─ AmountSelectorButton (LevelUpAmountSelector: +1→+10→MAX 순환)
    │   └─ Scroll View → Content ← FlowerUpgradeItem(prefab) 런타임 생성, 보유 꽃 수만큼
    └─ PlayerUpgradePanel
        ├─ TouchAffectionRow  (PlayerStatRow)
        ├─ TouchGoldRow       (PlayerStatRow)
        └─ AutoAffectionRow   (PlayerStatRow)
```

**`PanelSwitcher`** (`Scripts/UI/Common/PanelSwitcher.cs`) — 완전히 범용. `List<Tab>{Button, GameObject}` + `defaultTabIndex`만 알고 Flower/Player/Shop 등 구체 기능을 전혀 모른다. `activeTabColor`/`inactiveTabColor`는 public 필드라 색을 코드에 고정하지 않고 Inspector에서 자유롭게 바꿀 수 있다. **좌측 사이드바나 다른 화면에 그대로 재사용 가능하도록 설계됨** (예: 나중에 `[상점][도감][메모리얼]` 탭을 왼쪽에 붙일 때 같은 컴포넌트 재사용).

**`HoldRepeatButton`** (`Scripts/UI/Common/HoldRepeatButton.cs`) — 범용 "누르면 즉시 1회 + 길게 누르면 반복" 입력 컴포넌트. `Button.interactable`을 감시해서 골드 부족 시 자동으로 반복을 멈춘다. `allowRepeat=false`로 설정하면 반복 없이 1회만 (MAX 모드에 사용).

**`FlowerUpgradePanel`/`FlowerUpgradeItem`** — 보유한 **모든** 꽃을 나열하고, **중앙 화면에 어떤 꽃이 떠 있는지와 완전히 무관하게** 각 행에서 바로 레벨업 가능 (스와이프 불필요). `+1/+10/MAX` 단위는 전역 선택기(`LevelUpAmountSelector`)를 공유. 비용/증가 G/s 미리보기는 **매 프레임 재계산하지 않고, 골드(정수부)·레벨·단위가 실제로 바뀐 프레임에만** 재계산 (성능 최적화, 꽃이 많아져도 MAX 계산 루프가 불필요하게 반복되지 않도록).

**`PlayerUpgradePanel`/`PlayerStatRow`** — 항상 고정 3행(터치애정/터치골드/자동애정). `FlowerUpgradeItem`과 동일한 캐시 원칙 재사용. `+1/+10/MAX` 개념 없이 항상 1레벨씩 강화, `HoldRepeatButton`으로 길게 누르기 반복 지원.

### 6.3 좌측(탐색/수집 계열)
- **ShopPanel** (`ShopManager`+`ShopItem`) — `FlowerManager.allFlowers`를 순회해 구매 카드 동적 생성. 이미 보유한 꽃은 구매 버튼 비활성화. **의도적으로 꽃 레벨업 카드와 분리된 별개 컴포넌트** (`ShopItem` ≠ `FlowerUpgradeItem`).
- **DexPanel** — 현재 비활성 placeholder, 실제 도감 기능 없음.

### 6.4 UIManager (`Scripts/UI/Uimanager.cs`)
`GameManager`/`FlowerManager` 값을 TopBar·FlowerStatusUI에 매 프레임 반영. 골드/애정 속도는 지수평활(smoothing) 처리. **개화한 꽃의 레벨 텍스트는 중앙 화면에서 제거됨** — 꽃 레벨은 이제 우측 "꽃" 탭에서만 확인 가능하다 (이번 세션에서 의도적으로 옮김).

### 6.5 PCLayoutBuilder (에디터 전용, `Scripts/Editor/PCLayoutBuilder.cs`)
`Tools > 🌸 Build PC Layout` 메뉴로 위 6.1~6.3 전체를 코드로 씬에 생성/재생성한다. 재실행하면 `PCLayout` 전체가 통째로 재생성됨 (TopBar/FlowerStatusUI 위치는 보존). **UI 생성/배선만 담당하고, `PlayerStatData`/`PlayerStatManager` 같은 게임 데이터·매니저는 만들지 않는다** (책임 분리 원칙, 이번 세션에서 명시적으로 확정). `FlowerUpgradeItem.prefab`은 없으면 최초 1회 자동 생성(`PrefabUtility.SaveAsPrefabAsset`), 있으면 그대로 재사용.

---

## 7. 폴더 구조 (실제 경로)

```
Assets/
├─ Project/
│  ├─ Scripts/
│  │  ├─ Core/      FlowerDisplayController, Flowermanager, GameManager, PlayerStatManager
│  │  ├─ Data/      FlowerData, FlowerInstance, PlayerStatData, PlayerStatInstance
│  │  ├─ UI/        Uimanager, ShopItem, ShopManager, PCLayoutController,
│  │  │              FlowerUpgradePanel, FlowerUpgradeItem, FlowerSortSelector,
│  │  │              LevelUpAmountSelector, PlayerUpgradePanel, PlayerStatRow,
│  │  │              FlowerManagementPanelController(⚠️미사용 legacy, 아래 참고)
│  │  │  └─ Common/ PanelSwitcher, HoldRepeatButton   (범용 컴포넌트만 모아둠)
│  │  ├─ Input/     ScreenTouchController
│  │  ├─ Editor/    PCLayoutBuilder
│  │  ├─ Save/      (비어있음 — 세이브 시스템 없음)
│  │  └─ ScriptableObjects/FlowerGirl/Flower Data/  Flower_Dandelion.asset
│  │      ⚠️ 꽃 데이터는 아직 여기(Scripts 하위)에 있음 — 아래 8장 "정리 필요" 참고
│  ├─ ScriptableObjects/   TouchAffection.asset, TouchGold.asset, AutoAffection.asset
│  │      (플레이어 스탯은 Scripts 밖, 프로젝트 최상위 ScriptableObjects 폴더에 위치)
│  ├─ Prefabs/      ShopItem.prefab, FlowerUpgradeItem.prefab(자동생성)
│  ├─ Legacy/       FlowerController.cs.txt (삭제된 구버전 코드 백업, 컴파일 제외)
│  ├─ Sprites/Flowers/Dandelion/  Seed.png, Sprout.png, Growing.png, Bloom.png
│  ├─ Scenes/       (비어있음)
│  └─ UI_Layout/    (비어있음)
└─ Scenes/
   └─ SampleScene.unity   ← 실제로 쓰는 유일한 씬 (Project/Scenes 아님, Unity 기본 위치)
```

---

## 8. 현재 구현 상태

### 정상 동작
- 클릭 → 애정 증가 → 개화 → 초당 골드 자동 생산 → 클릭 시 골드 획득 (기본 루프)
- 화면 밖 꽃도 계속 G/s 생산 + 계속 미개화 꽃 자동 애정 성장
- 꽃 레벨업 +1/+10/MAX (홀드 반복 포함), 정렬 5종
- 상점에서 씨앗 구매
- PC/모바일 레이아웃 전환, PC 백그라운드 실행(창 비활성/최소화에도 진행)
- 탭 전환 시스템(PanelSwitcher) + 활성 탭 하이라이트

### 진행 중 (마지막 작업 세션 기준 미완료)
- **PlayerStatManager를 씬에 GameObject로 배치하고 3개 에셋을 연결하는 작업** — 아직 완료 확인 안 됨. 이게 안 되어 있으면 터치/자동 애정 값이 전부 0으로 폴백해서 클릭해도 아무 효과가 없어 보임 (에러는 아님, 의도된 안전장치).

### 미구현
- 저장/불러오기, 오프라인 수익 계산
- 도감(DexPanel) 실제 기능
- 메모리얼, 조합 보너스
- 꽃 패시브 시스템 (`FlowerData.hasPassive` 필드는 있지만 로직 없음)
- 꽃 2종 이상 (현재 민들레 1종)
- 레벨 상한 (꽃/플레이어 스탯 모두 무제한 레벨업 가능)
- 골드 UI 갱신을 이벤트 기반으로 바꾸는 것 (`GameManager.AddGold`에 TODO 주석으로만 남아있음, 현재는 매 프레임 폴링으로 충분히 동작 중)

---

## 9. 이 프로젝트의 설계 원칙 (향후 기획 시 유지해야 할 것)

1. **데이터(SO) / 상태(순수 클래스) / 매니저(싱글턴) 3분리**를 새 시스템에도 동일하게 적용한다.
2. **UI 생성(Editor Builder)과 게임 데이터/매니저 생성 책임을 분리**한다 — `PCLayoutBuilder`는 씬에 UI만 만들고, 게임 매니저나 SO 에셋은 만들지 않는다.
3. **특정 위치에 종속된 하드코딩을 피하고 범용 컴포넌트를 우선**한다 (`PanelSwitcher`, `HoldRepeatButton`처럼 "오른쪽 전용" 같은 이름/가정을 넣지 않음).
4. 밸런스 성장 공식은 항상 `base * rate^(level - 기준레벨)` 형태의 명시적 지수식으로 통일하고, **확정된 수치 없이 임의의 테스트 값을 넣지 않는다** (실제 값이 필요하면 먼저 물어봄).
5. 매 프레임 무조건 재계산하지 않고, **실제로 값이 바뀐 프레임에만 재계산하는 캐시 패턴**을 UI 미리보기에 일관되게 적용한다 (골드 정수부/레벨/모드 비교).
6. 화면에 표시되는 꽃과 실제로 성장/생산 중인 꽃은 별개 개념이다 — "표시 중"이라는 이유로 다른 꽃의 진행을 막지 않는다.
7. 더 이상 쓰지 않는 코드는 삭제하거나(참조 없음 확인 후) `.cs.txt`로 비활성화해서 `Assets/Project/Legacy/`에 보관한다 — 씬/다른 스크립트 어디서도 참조하지 않는지 먼저 전체 검색으로 확인한다.

---

## 10. 알려진 정리 필요 사항 (기술 부채)

- 꽃 데이터(`Flower_Dandelion.asset`)는 `Scripts/ScriptableObjects/...` 아래에 있고, 플레이어 스탯 데이터는 `Assets/Project/ScriptableObjects/`(최상위)에 있음 — **위치 규칙이 통일되어 있지 않다.** 향후 꽃 데이터도 최상위 `ScriptableObjects` 폴더로 옮기는 정리가 필요할 수 있음.
- `Assets/Project/Scenes/`, `Assets/Project/UI_Layout/` 폴더가 존재하지만 비어있음 — 실제 씬은 Unity 기본 위치(`Assets/Scenes/SampleScene.unity`)를 그대로 쓰고 있음.
- `Scripts/UI/FlowerManagementPanelController.cs` — 꽃 레벨업 UI의 구버전(단일 꽃, 탭 없음) 구현. `FlowerUpgradePanel`/`FlowerUpgradeItem`으로 완전히 대체되어 현재 어디서도 참조되지 않는 미사용 상태. `FlowerController.cs`와 같은 절차(전체 참조 검색 후 백업+삭제)로 정리할 수 있음.
- `Scripts/Save/` 폴더가 비어있음 — 세이브 시스템 붙일 자리로 미리 만들어져 있으나 아직 아무 코드도 없음.

---

## 11. 용어 사전 (기획 용어 ↔ 코드 식별자)

| 기획 용어 | 코드 식별자 | 비고 |
|---|---|---|
| 애정 | `currentAffection`, `AddAffection()` | 개화 전 꽃에만 존재하는 개념 |
| 개화 | `isBloomed`, `Bloom` 관련 로직 | 애정 100% 도달 시 1회 발생 |
| G/s (초당 골드) | `GetGoldPerSecond()` | 개화한 꽃만 생산 |
| 터치 애정 / 터치 골드 | `PlayerStatType.TouchAffection` / `TouchGold` | 클릭 1회당 획득량, 전역 강화 대상 |
| 자동 애정 | `PlayerStatType.AutoAffection` | 보유한 모든 미개화 꽃에 동시 적용되는 초당 애정 |
| 도감순 | `FlowerManager.allFlowers` 리스트 순서 | 스와이프 순서, 정렬 기본값과 동일 |
| 꽃 관리/레벨업 탭 | `FlowerUpgradePanel` | 우측 "꽃" 탭 |
| 플레이어 강화 탭 | `PlayerUpgradePanel` | 우측 "플레이어" 탭 |
| 레벨업 단위 | `LevelUpAmount{One, Ten, Max}` | +1/+10/MAX |
| 성장 단계 | `GrowthStage{Seed, Sprout, Growing, Bloomed}` | 0/25/50/100% 구간 |

---

## 12. 이 문서를 다른 AI에게 줄 때 함께 전달하면 좋은 것

- 이 문서 자체(현재 파일)
- 논의하려는 기획 주제가 특정 스크립트와 관련 있다면 해당 `.cs` 파일 원문 (예: 상점 시스템 논의 시 `ShopManager.cs`/`ShopItem.cs`)
- 밸런스 논의 시 5.1 표(플레이어 스탯) 또는 4.1의 `Flower_Dandelion.asset` 값
