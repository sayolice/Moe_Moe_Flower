using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class MiniArenaGame : MonoBehaviour
{
    private sealed class Enemy
    {
        public RectTransform rect;
        public float hp;
        public float speed;
        public float radius;
        public float xp;
        public bool boss;
        public Color baseColor;
        public float hitFlashTimer;
    }

    private sealed class ExperienceOrb
    {
        public RectTransform rect;
        public float value;
    }

    private sealed class Shot
    {
        public RectTransform rect;
        public Vector2 direction;
        public float speed;
        public float lifetime;
        public float damage;
    }

    private sealed class ArenaEffect
    {
        public Image image;
        public float age;
        public float duration;
        public float startScale;
        public float endScale;
        public float startAlpha;
    }

    private static MiniArenaGame activeGame;
    private readonly List<Enemy> enemies = new List<Enemy>();
    private readonly List<Shot> shots = new List<Shot>();
    private readonly List<ExperienceOrb> experienceOrbs = new List<ExperienceOrb>();
    private readonly List<ArenaEffect> effects = new List<ArenaEffect>();
    private readonly List<Button> levelChoiceButtons = new List<Button>();
    private Sprite effectRingSprite;
    private RectTransform playfield;
    private RectTransform playerRect;
    private MiniArenaCharacterView playerView;
    private RectTransform overlayRoot;
    private TMP_Text statusText;
    private TMP_Text helpText;
    private TMP_Text xpText;
    private TMP_Text creditsText;
    private TMP_Text bossText;
    private TMP_Text passiveText;
    private Image xpBarBackgroundImage;
    private Image bossBarBackgroundImage;
    private Image bossBarFill;
    private RectTransform startPanel;
    private RectTransform levelPanel;
    private RectTransform resultPanel;
    private RectTransform xpBar;
    private MiniArenaDifficulty difficulty = MiniArenaDifficulty.Standard;
    private MiniArenaWeaponStyle weaponStyle = MiniArenaWeaponStyle.Balanced;
    private string flowerId = "Dandelion";
    private readonly Dictionary<string, int> runUpgrades = new Dictionary<string, int>();
    private Vector2 moveInput;
    private Vector2 pointerMoveInput;
    private Vector2 keyboardMoveInput;
    private Vector2 aimDirection = Vector2.up;
    private float elapsed;
    private float spawnTimer;
    private float fireTimer;
    private float bossTimer = 150f;
    private float xp;
    private float xpToNext = 8f;
    private float bossMaxHp;
    private float playerHp = 5f;
    private float playerMaxHp = 5f;
    private float hitFlashTimer;
    private float pulseCooldown;
    private float healCooldown;
    private float overdriveTime;
    private float orbitDamageTimer;
    private float bossContactTimer;
    private float passiveSeeds;
    private float tailwindTimer;
    private float bloomTimer;
    private int defeated;
    private int bossesDefeated;
    private int playerLevel = 1;
    private int levelUpChoicesPending;
    private bool pausedForChoice;
    private bool runActive;
    private bool runFinished;
    private bool paused;
    private bool isClosed;
    private bool gameOver;
    private float previousTimeScale;

    public static void Open()
    {
        if (activeGame != null) return;
        GameObject host = new GameObject("MiniArenaGame", typeof(RectTransform));
        activeGame = host.AddComponent<MiniArenaGame>();
        activeGame.BuildUI();
    }

    public static void ShowDemoNotice()
    {
        if (activeGame != null || GameObject.Find("MiniArenaDemoNotice") != null) return;
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("미니 아레나를 열 수 없습니다. 씬에 Canvas가 없습니다.");
            return;
        }
        GameObject notice = new GameObject("MiniArenaDemoNotice", typeof(RectTransform));
        notice.transform.SetParent(canvas.rootCanvas.transform, false);
        RectTransform root = notice.GetComponent<RectTransform>();
        Stretch(root);
        Image dim = notice.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.72f);
        dim.raycastTarget = true;

        RectTransform panel = CreatePanel(root, "DemoNoticePanel", new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.72f), new Color(0.12f, 0.18f, 0.15f, 1f));
        TMP_Text title = CreateText(panel, "Title", 27f, TextAlignmentOptions.Center);
        title.text = "미니 아레나 데모 안내";
        SetAnchors(title.rectTransform, new Vector2(0.05f, 0.72f), new Vector2(0.95f, 0.94f));
        TMP_Text message = CreateText(panel, "Message", 18f, TextAlignmentOptions.Center);
        message.text = "현재 미니 아레나는 개발 중인 데모 버전입니다.\n콘텐츠와 밸런스가 변경될 수 있으며, 오류가 발생하거나 진행 데이터가 초기화될 수 있습니다.";
        SetAnchors(message.rectTransform, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.70f));
        CreateButton(panel, "Cancel", "돌아가기", new Vector2(0.12f, 0.08f), new Vector2(0.45f, 0.25f), () => Object.Destroy(notice));
        CreateButton(panel, "Continue", "데모 시작", new Vector2(0.55f, 0.08f), new Vector2(0.88f, 0.25f), () =>
        {
            Object.Destroy(notice);
            Open();
        });
    }

    private void BuildUI()
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("Mini Arena requires an existing Canvas.");
            Close();
            return;
        }

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject eventSystemObject = new GameObject("MiniArenaEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            DontDestroyOnLoad(eventSystemObject);
        }

        transform.SetParent(canvas.rootCanvas.transform, false);
        transform.SetAsLastSibling();
        CreateEffectRingSprite();
        GameObject root = gameObject;
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);
        Image rootImage = root.AddComponent<Image>();
        rootImage.color = new Color(0.035f, 0.055f, 0.08f, 1f);
        rootImage.raycastTarget = true;
        overlayRoot = rootRect;

        GameObject field = new GameObject("Playfield", typeof(RectTransform), typeof(Image), typeof(MiniArenaPointerInput));
        field.transform.SetParent(root.transform, false);
        playfield = field.GetComponent<RectTransform>();
        playfield.anchorMin = new Vector2(0.04f, 0.12f);
        playfield.anchorMax = new Vector2(0.96f, 0.88f);
        playfield.offsetMin = playfield.offsetMax = Vector2.zero;
        field.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.15f, 1f);
        field.GetComponent<Image>().raycastTarget = true;
        field.GetComponent<MiniArenaPointerInput>().Initialize(this);

        statusText = CreateText(root.transform, "Status", 22f, TextAlignmentOptions.Left);
        SetAnchors(statusText.rectTransform, new Vector2(0.04f, 0.91f), new Vector2(0.72f, 0.98f));
        xpText = CreateText(root.transform, "Experience", 15f, TextAlignmentOptions.Right);
        SetAnchors(xpText.rectTransform, new Vector2(0.73f, 0.91f), new Vector2(0.88f, 0.98f));
        xpBarBackgroundImage = CreateImage(root.transform, "ExperienceBarBackground", new Color(0.18f, 0.22f, 0.28f), Vector2.zero);
        SetAnchors(xpBarBackgroundImage.rectTransform, new Vector2(0.04f, 0.89f), new Vector2(0.96f, 0.905f));
        Image xpBarFill = CreateImage(xpBarBackgroundImage.transform, "Fill", new Color(0.25f, 0.75f, 1f), Vector2.zero);
        xpBar = xpBarFill.rectTransform;
        xpBar.anchorMin = new Vector2(0f, 0f);
        xpBar.anchorMax = new Vector2(0f, 1f);
        xpBar.pivot = new Vector2(0f, 0.5f);
        xpBar.sizeDelta = Vector2.zero;
        bossText = CreateText(root.transform, "BossStatus", 16f, TextAlignmentOptions.Center);
        bossText.gameObject.SetActive(false);
        SetAnchors(bossText.rectTransform, new Vector2(0.25f, 0.84f), new Vector2(0.75f, 0.88f));
        bossBarBackgroundImage = CreateImage(root.transform, "BossBarBackground", new Color(0.2f, 0.1f, 0.14f), Vector2.zero);
        SetAnchors(bossBarBackgroundImage.rectTransform, new Vector2(0.25f, 0.82f), new Vector2(0.75f, 0.835f));
        Image bossBarFillImage = CreateImage(bossBarBackgroundImage.transform, "Fill", new Color(1f, 0.25f, 0.35f), Vector2.zero);
        bossBarFill = bossBarFillImage;
        bossBarFillImage.rectTransform.anchorMin = new Vector2(0f, 0f);
        bossBarFillImage.rectTransform.anchorMax = new Vector2(1f, 1f);
        bossBarFillImage.rectTransform.pivot = new Vector2(0f, 0.5f);
        bossBarBackgroundImage.gameObject.SetActive(false);
        helpText = CreateText(root.transform, "Help", 15f, TextAlignmentOptions.Center);
        helpText.text = "드래그/WASD: 이동 · 자동 조준 공격 · 1: 파동 · 2: 회복 · ESC: 나가기";
        SetAnchors(helpText.rectTransform, new Vector2(0.02f, 0.02f), new Vector2(0.60f, 0.09f));

        CreateButton(root.transform, "Pulse", "파동 (1)", new Vector2(0.61f, 0.02f), new Vector2(0.72f, 0.10f), UsePulse);
        CreateButton(root.transform, "Heal", "회복 (2)", new Vector2(0.73f, 0.02f), new Vector2(0.84f, 0.10f), UseHeal);
        CreateButton(root.transform, "Pause", "일시정지 (P)", new Vector2(0.85f, 0.02f), new Vector2(0.97f, 0.10f), TogglePause);
        startPanel = CreatePanel(root.transform, "StartPanel", new Vector2(0.12f, 0.15f), new Vector2(0.88f, 0.86f), new Color(0.04f, 0.06f, 0.10f, 0.98f));
        BuildStartPanel();
        levelPanel = CreatePanel(root.transform, "LevelUpPanel", new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.74f), new Color(0.04f, 0.06f, 0.10f, 0.98f));
        levelPanel.gameObject.SetActive(false);
        resultPanel = CreatePanel(root.transform, "ResultPanel", new Vector2(0.18f, 0.25f), new Vector2(0.82f, 0.75f), new Color(0.04f, 0.06f, 0.10f, 0.98f));
        resultPanel.gameObject.SetActive(false);

        MiniArenaShapeCharacterView shapeView = new GameObject("PlayerView", typeof(RectTransform), typeof(MiniArenaShapeCharacterView)).GetComponent<MiniArenaShapeCharacterView>();
        shapeView.Initialize(playfield, new Color(0.35f, 0.9f, 0.75f), 34f);
        playerView = shapeView;
        playerRect = playerView.RectTransform;
        playerRect.anchoredPosition = Vector2.zero;
        playerView.gameObject.SetActive(false);
        statusText.gameObject.SetActive(false);
        xpText.gameObject.SetActive(false);
        xpBarBackgroundImage.gameObject.SetActive(false);
        helpText.gameObject.SetActive(false);
        SetGameplayButtonsActive(false);
        UpdateHubCredits();
        UpdateFlowerAvailability();
    }

    private void BuildStartPanel()
    {
        TMP_Text title = CreateText(startPanel, "Title", 32f, TextAlignmentOptions.Center);
        title.text = "미니 아레나";
        SetAnchors(title.rectTransform, new Vector2(0.05f, 0.82f), new Vector2(0.95f, 0.96f));

        TMP_Text info = CreateText(startPanel, "Info", 17f, TextAlignmentOptions.Center);
        SetAnchors(info.rectTransform, new Vector2(0.05f, 0.66f), new Vector2(0.95f, 0.82f));
        info.text = "난이도와 공격 방식을 선택해 전투에 도전하세요. 처치와 생존으로 전용 크레딧을 얻습니다.";
        TMP_Text passiveInfo = CreateText(startPanel, "FlowerPassive", 14f, TextAlignmentOptions.Center);
        SetAnchors(passiveInfo.rectTransform, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.13f));
        passiveInfo.text = "민들레 고유 패시브: 홀씨의 축복 — 5회 처치마다 주변을 밀쳐냅니다.";

        creditsText = CreateText(startPanel, "Credits", 20f, TextAlignmentOptions.Center);
        SetAnchors(creditsText.rectTransform, new Vector2(0.05f, 0.56f), new Vector2(0.95f, 0.65f));
        // Use the previously created passiveInfo text for runtime updates to avoid duplicate objects.
        passiveText = passiveInfo;

        CreateButton(startPanel, "DifficultyRelaxed", "여유", new Vector2(0.10f, 0.43f), new Vector2(0.28f, 0.53f), () => SetDifficulty(MiniArenaDifficulty.Relaxed));
        CreateButton(startPanel, "DifficultyStandard", "표준", new Vector2(0.30f, 0.43f), new Vector2(0.48f, 0.53f), () => SetDifficulty(MiniArenaDifficulty.Standard));
        CreateButton(startPanel, "DifficultyExpert", "도전", new Vector2(0.50f, 0.43f), new Vector2(0.68f, 0.53f), () => SetDifficulty(MiniArenaDifficulty.Expert));
        CreateButton(startPanel, "DifficultyEndless", "무한", new Vector2(0.70f, 0.43f), new Vector2(0.88f, 0.53f), () => SetDifficulty(MiniArenaDifficulty.Endless));

        CreateButton(startPanel, "StyleBalanced", "균형 공격", new Vector2(0.18f, 0.29f), new Vector2(0.38f, 0.39f), () => SetWeaponStyle(MiniArenaWeaponStyle.Balanced));
        CreateButton(startPanel, "StyleRapid", "빠른 공격", new Vector2(0.40f, 0.29f), new Vector2(0.60f, 0.39f), () => SetWeaponStyle(MiniArenaWeaponStyle.Rapid));
        CreateButton(startPanel, "StyleHeavy", "강한 공격", new Vector2(0.62f, 0.29f), new Vector2(0.82f, 0.39f), () => SetWeaponStyle(MiniArenaWeaponStyle.Heavy));

        CreateButton(startPanel, "StartRun", "전투 시작", new Vector2(0.18f, 0.14f), new Vector2(0.43f, 0.25f), StartRun);
        CreateButton(startPanel, "MetaShop", "영구 강화", new Vector2(0.46f, 0.14f), new Vector2(0.71f, 0.25f), ToggleMetaShop);
        CreateButton(startPanel, "Exit", "닫기", new Vector2(0.74f, 0.14f), new Vector2(0.89f, 0.25f), Close);
        CreateButton(startPanel, "MetaBack", "돌아가기", new Vector2(0.74f, 0.08f), new Vector2(0.89f, 0.17f), () => ToggleMetaShop(false)).gameObject.SetActive(false);

        CreateMetaUpgradeButtons();
        UpdateSelectionButtons();
    }

    private void CreateMetaUpgradeButtons()
    {
        for (int i = 0; i < MiniArenaProgression.UpgradeIds.Length; i++)
        {
            int index = i;
            float yMax = 0.53f - i * 0.075f;
            float yMin = yMax - 0.065f;
            CreateButton(startPanel, "MetaUpgrade" + i, "", new Vector2(0.12f, yMin), new Vector2(0.88f, yMax), () => BuyMetaUpgrade(index));
        }
        for (int i = 0; i < startPanel.childCount; i++)
        {
            Transform child = startPanel.GetChild(i);
            if (child.name.StartsWith("MetaUpgrade")) child.gameObject.SetActive(false);
        }
    }

    private void BuildLevelUpPanel()
    {
        levelChoiceButtons.Clear();
        foreach (Transform child in levelPanel) Destroy(child.gameObject);

        TMP_Text title = CreateText(levelPanel, "Title", 28f, TextAlignmentOptions.Center);
        title.text = "레벨 상승 — 강화 선택";
        SetAnchors(title.rectTransform, new Vector2(0.04f, 0.80f), new Vector2(0.96f, 0.96f));

        List<MiniArenaUpgradeDefinition> available = MiniArenaUpgradeCatalog.GetAvailable(runUpgrades, flowerId);
        List<MiniArenaUpgradeDefinition> choices = PickUpgradeChoices(available, 4);
        float cardWidth = 0.88f / Mathf.Max(1, choices.Count);
        for (int i = 0; i < choices.Count; i++)
        {
            MiniArenaUpgradeDefinition definition = choices[i];
            int level = runUpgrades.TryGetValue(definition.id, out int current) ? current : 0;
            float minX = 0.06f + i * cardWidth;
            Button button = CreateButton(levelPanel, "Choice_" + definition.id,
                $"{definition.name}\nLv.{level}/{definition.maxLevel}\n{definition.description}",
                new Vector2(minX, 0.20f), new Vector2(minX + cardWidth - 0.02f, 0.75f), () => SelectUpgrade(definition));
            levelChoiceButtons.Add(button);
        }
    }

    private List<MiniArenaUpgradeDefinition> PickUpgradeChoices(List<MiniArenaUpgradeDefinition> available, int count)
    {
        List<MiniArenaUpgradeDefinition> pool = new List<MiniArenaUpgradeDefinition>();
        List<MiniArenaUpgradeDefinition> fusions = new List<MiniArenaUpgradeDefinition>();
        foreach (MiniArenaUpgradeDefinition definition in available)
        {
            if (definition.kind == MiniArenaUpgradeKind.Fusion) fusions.Add(definition);
            else pool.Add(definition);
        }
        List<MiniArenaUpgradeDefinition> result = new List<MiniArenaUpgradeDefinition>();
        if (pool.Count == 0 && fusions.Count > 0)
        {
            while (result.Count < count && fusions.Count > 0)
            {
                int index = Random.Range(0, fusions.Count);
                result.Add(fusions[index]);
                fusions.RemoveAt(index);
            }
            return result;
        }

        MiniArenaUpgradeDefinition fusion = null;
        List<MiniArenaUpgradeDefinition> offeredFusions = new List<MiniArenaUpgradeDefinition>();
        foreach (MiniArenaUpgradeDefinition candidate in fusions)
        {
            if (Random.value <= MiniArenaUpgradeCatalog.GetFusionOfferChance(candidate, runUpgrades))
                offeredFusions.Add(candidate);
        }
        if (offeredFusions.Count > 0)
            fusion = offeredFusions[Random.Range(0, offeredFusions.Count)];

        int normalSlots = fusion != null ? count - 1 : count;
        List<MiniArenaUpgradeDefinition> cores = pool.FindAll(x => x.tier == 1);
        MiniArenaUpgradeDefinition guaranteedCore = cores.Count > 0 ? cores[Random.Range(0, cores.Count)] : null;
        if (guaranteedCore != null)
        {
            result.Add(guaranteedCore);
            pool.Remove(guaranteedCore);
        }
        while (result.Count < normalSlots && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }
        if (fusion != null) result.Add(fusion);
        if (result.Count == 0)
            result.Add(new MiniArenaUpgradeDefinition("repair", "긴급 회복", "체력을 1 회복합니다.", MiniArenaUpgradeKind.Universal, 999, 0f, null, true));
        return result;
    }

    private void SelectUpgrade(MiniArenaUpgradeDefinition definition)
    {
        if (definition.id == "repair")
            playerHp = Mathf.Min(playerMaxHp, playerHp + 1f);
        else
        {
            int current = runUpgrades.TryGetValue(definition.id, out int value) ? value : 0;
            runUpgrades[definition.id] = current + 1;
            if (definition.id == "vitality")
            {
                playerMaxHp += 1f;
                playerHp = Mathf.Min(playerMaxHp, playerHp + 1f);
            }
        }

        if (definition.kind == MiniArenaUpgradeKind.Fusion)
            ApplyFusionUpgrade(definition.id);

        levelUpChoicesPending--;
        if (levelUpChoicesPending > 0)
            BuildLevelUpPanel();
        else
        {
            pausedForChoice = false;
            levelPanel.gameObject.SetActive(false);
        }
        UpdateStatus();
    }

    private void ApplyFusionUpgrade(string fusionId)
    {
        if (fusionId == "fusionSeedWind")
            tailwindTimer = Mathf.Min(tailwindTimer, 0.25f);
        else if (fusionId == "fusionSeedBloom")
        {
            passiveSeeds = Mathf.Min(passiveSeeds, 1f);
            bloomTimer = 0f;
        }
        else if (fusionId == "fusionWindBloom")
            bloomTimer = Mathf.Min(bloomTimer, 0.35f);
    }

    private void BuildResultPanel(bool victory)
    {
        foreach (Transform child in resultPanel) Destroy(child.gameObject);
        TMP_Text title = CreateText(resultPanel, "Title", 30f, TextAlignmentOptions.Center);
        title.text = victory ? "도전 성공" : "도전 종료";
        SetAnchors(title.rectTransform, new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.93f));
        TMP_Text result = CreateText(resultPanel, "Result", 21f, TextAlignmentOptions.Center);
        result.text = $"생존 {Mathf.FloorToInt(elapsed)}초 · 처치 {defeated}\n보상 크레딧 +{CalculateRunReward(victory)}";
        SetAnchors(result.rectTransform, new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.72f));
        CreateButton(resultPanel, "Return", "기지로", new Vector2(0.20f, 0.15f), new Vector2(0.48f, 0.34f), ReturnToHub);
        CreateButton(resultPanel, "Again", "다시 도전", new Vector2(0.52f, 0.15f), new Vector2(0.80f, 0.34f), StartRun);
    }

    private int CalculateRunReward(bool victory)
    {
        return Mathf.Max(1, Mathf.FloorToInt(elapsed / 8f) + defeated / 3 + (victory ? 50 : 0));
    }

    private void SetDifficulty(MiniArenaDifficulty value) { difficulty = value; }
    private void SetWeaponStyle(MiniArenaWeaponStyle value) { weaponStyle = value; UpdateSelectionButtons(); }

    private void UpdateSelectionButtons()
    {
        SetButtonSelected("DifficultyRelaxed", difficulty == MiniArenaDifficulty.Relaxed);
        SetButtonSelected("DifficultyStandard", difficulty == MiniArenaDifficulty.Standard);
        SetButtonSelected("DifficultyExpert", difficulty == MiniArenaDifficulty.Expert);
        SetButtonSelected("DifficultyEndless", difficulty == MiniArenaDifficulty.Endless);
        SetButtonSelected("StyleBalanced", weaponStyle == MiniArenaWeaponStyle.Balanced);
        SetButtonSelected("StyleRapid", weaponStyle == MiniArenaWeaponStyle.Rapid);
        SetButtonSelected("StyleHeavy", weaponStyle == MiniArenaWeaponStyle.Heavy);
    }

    private void SetButtonSelected(string buttonName, bool selected)
    {
        Transform buttonTransform = startPanel != null ? startPanel.Find(buttonName) : null;
        Image image = buttonTransform != null ? buttonTransform.GetComponent<Image>() : null;
        if (image != null) image.color = selected ? new Color(0.25f, 0.56f, 0.61f, 1f) : new Color(0.2f, 0.38f, 0.5f, 1f);
    }

    private void StartRun()
    {
        if (!IsFlowerAvailable())
        {
            UpdateFlowerAvailability();
            return;
        }
        ClearRunObjects();
        runUpgrades.Clear();
        elapsed = 0f;
        spawnTimer = 0f;
        fireTimer = 0f;
        bossTimer = 150f;
        xp = 0f;
        xpToNext = 8f;
        playerLevel = 1;
        playerMaxHp = 5f + MiniArenaProgression.Data.maxHealthLevel;
        playerHp = playerMaxHp;
        pulseCooldown = 0f;
        healCooldown = 0f;
        overdriveTime = 0f;
        passiveSeeds = 0f;
        tailwindTimer = 0f;
        bloomTimer = 0f;
        orbitDamageTimer = 0f;
        bossContactTimer = 0f;
        defeated = 0;
        bossesDefeated = 0;
        gameOver = false;
        runFinished = false;
        paused = false;
        pausedForChoice = false;
        levelUpChoicesPending = 0;
        startPanel.gameObject.SetActive(false);
        resultPanel.gameObject.SetActive(false);
        levelPanel.gameObject.SetActive(false);
        playerView.gameObject.SetActive(true);
        statusText.gameObject.SetActive(true);
        xpText.gameObject.SetActive(true);
        xpBarBackgroundImage.gameObject.SetActive(true);
        helpText.gameObject.SetActive(true);
        SetGameplayButtonsActive(true);
        runActive = true;
        paused = false;
        Time.timeScale = 0f;
        UpdateStatus();
    }

    private void ReturnToHub()
    {
        ClearRunObjects();
        runActive = false;
        runFinished = false;
        playerView.gameObject.SetActive(false);
        statusText.gameObject.SetActive(false);
        xpText.gameObject.SetActive(false);
        xpBarBackgroundImage.gameObject.SetActive(false);
        bossBarBackgroundImage.gameObject.SetActive(false);
        helpText.gameObject.SetActive(false);
        SetGameplayButtonsActive(false);
        resultPanel.gameObject.SetActive(false);
        startPanel.gameObject.SetActive(true);
        ToggleMetaShop(false);
        UpdateHubCredits();
        UpdateFlowerAvailability();
    }

    private void ToggleMetaShop() => ToggleMetaShop(true);

    private void ToggleMetaShop(bool toggle)
    {
        bool show = toggle && !startPanel.Find("MetaUpgrade0").gameObject.activeSelf;
        for (int i = 0; i < startPanel.childCount; i++)
        {
            Transform child = startPanel.GetChild(i);
            if (child.name.StartsWith("MetaUpgrade")) child.gameObject.SetActive(show);
            if (child.name.StartsWith("Difficulty") || child.name.StartsWith("Style")) child.gameObject.SetActive(!show);
        }
        startPanel.Find("FlowerPassive").gameObject.SetActive(!show);
        startPanel.Find("StartRun").gameObject.SetActive(!show);
        startPanel.Find("MetaShop").gameObject.SetActive(!show);
        startPanel.Find("Exit").gameObject.SetActive(!show);
        startPanel.Find("MetaBack").gameObject.SetActive(show);
        UpdateMetaButtons();
    }

    private void UpdateMetaButtons()
    {
        for (int i = 0; i < MiniArenaProgression.UpgradeIds.Length; i++)
        {
            Transform item = startPanel.Find("MetaUpgrade" + i);
            if (item == null) continue;
            TMP_Text label = item.GetComponentInChildren<TMP_Text>();
            int level = MiniArenaProgression.GetUpgradeLevel(i);
            label.text = $"{MiniArenaProgression.UpgradeNames[i]}  Lv.{level}/{MiniArenaProgression.UpgradeMaxLevels[i]}  ·  {MiniArenaProgression.GetUpgradeCost(i)} 크레딧";
        }
        UpdateHubCredits();
    }

    private void BuyMetaUpgrade(int index)
    {
        MiniArenaProgression.BuyUpgrade(index);
        UpdateMetaButtons();
        UpdateHubCredits();
    }

    private void UpdateHubCredits()
    {
        if (creditsText != null)
        {
            MiniArenaSaveData save = MiniArenaProgression.Data;
            creditsText.text = $"크레딧 {save.credits}   최고 생존 {save.bestSurvivalSeconds}초   최고 처치 {save.bestKills}   성공 {save.wins}";
        }
    }

    private void SetGameplayButtonsActive(bool value)
    {
        foreach (Transform child in overlayRoot)
            if (child.name == "Pulse" || child.name == "Heal" || child.name == "Pause") child.gameObject.SetActive(value);
    }

    private void Update()
    {
        if (isClosed) return;
        UpdateEffects(Time.unscaledDeltaTime);
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (runActive) TogglePause();
            else Close();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame && runActive && !pausedForChoice && !runFinished)
            TogglePause();
        if (Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame && runActive && !pausedForChoice)
            UseOrbitBurst();

        if (!runActive || paused || pausedForChoice || runFinished || gameOver)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        if (!enemies.Exists(enemy => enemy.boss)) elapsed += dt;
        pulseCooldown = Mathf.Max(0f, pulseCooldown - dt);
        healCooldown = Mathf.Max(0f, healCooldown - dt);
        overdriveTime = Mathf.Max(0f, overdriveTime - dt);
        orbitDamageTimer -= dt;
        bossContactTimer -= dt;
        tailwindTimer -= dt;
        bloomTimer -= dt;
        hitFlashTimer = Mathf.Max(0f, hitFlashTimer - dt);
        playerView.SetHitFlash(hitFlashTimer * 5f);
        UpdatePlayer(dt);
        UpdateEnemies(dt);
        UpdateFlowerSkills(dt);
        UpdateShots(dt);
        UpdateExperienceOrbs(dt);
        SpawnEnemies(dt);
        UpdateBossTimer(dt);
        UpdateStatus();
    }

    private void UpdatePlayer(float dt)
    {
        if (Keyboard.current != null)
        {
            keyboardMoveInput = Vector2.zero;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) keyboardMoveInput.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) keyboardMoveInput.y -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) keyboardMoveInput.x += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) keyboardMoveInput.x -= 1f;
            if (Keyboard.current.digit1Key.wasPressedThisFrame) UsePulse();
            if (Keyboard.current.digit2Key.wasPressedThisFrame) UseHeal();
        }

        moveInput = keyboardMoveInput.sqrMagnitude > 0f ? keyboardMoveInput : pointerMoveInput;
        if (moveInput.sqrMagnitude > 1f) moveInput.Normalize();
        int windLevel = MiniArenaUpgradeCatalog.GetLevel(runUpgrades, "wind");
        int galeLevel = MiniArenaUpgradeCatalog.GetLevel(runUpgrades, "gale");
        Vector2 position = playerRect.anchoredPosition + moveInput * (250f * (1f + windLevel * 0.08f + galeLevel * 0.04f)) * dt;
        Rect bounds = playfield.rect;
        position.x = Mathf.Clamp(position.x, bounds.xMin + 22f, bounds.xMax - 22f);
        position.y = Mathf.Clamp(position.y, bounds.yMin + 22f, bounds.yMax - 22f);
        playerRect.anchoredPosition = position;
        playerView.SetMoving(moveInput.sqrMagnitude > 0.01f);

        Enemy nearest = FindNearestEnemy();
        if (nearest != null)
        {
            Vector2 toEnemy = nearest.rect.anchoredPosition - position;
            if (toEnemy.sqrMagnitude > 0.01f) aimDirection = toEnemy.normalized;
            playerView.SetFacing(aimDirection);
            fireTimer -= dt;
            if (fireTimer <= 0f)
            {
                FireShot(aimDirection);
                float rateBonus = runUpgrades.TryGetValue("cadence", out int cadenceLevel) ? 1f + cadenceLevel * 0.1f : 1f;
                rateBonus *= 1f + MiniArenaProgression.Data.fireRateLevel * 0.05f;
                if (overdriveTime > 0f) rateBonus *= 1.5f;
                fireTimer = Mathf.Max(0.12f, GetBaseFireInterval() / rateBonus);
            }
        }
    }

    private void SpawnEnemies(float dt)
    {
        spawnTimer -= dt;
        if (spawnTimer > 0f) return;
        spawnTimer = Mathf.Max(0.35f, GetBaseSpawnInterval() - elapsed * 0.003f);

        Rect bounds = playfield.rect;
        int edge = Random.Range(0, 4);
        Vector2 position = edge switch
        {
            0 => new Vector2(bounds.xMin, Random.Range(bounds.yMin, bounds.yMax)),
            1 => new Vector2(bounds.xMax, Random.Range(bounds.yMin, bounds.yMax)),
            2 => new Vector2(Random.Range(bounds.xMin, bounds.xMax), bounds.yMin),
            _ => new Vector2(Random.Range(bounds.xMin, bounds.xMax), bounds.yMax)
        };

        Image image = CreateImage(playfield, "Enemy", new Color(0.95f, 0.35f, 0.42f), new Vector2(26f, 26f));
        RectTransform rect = image.rectTransform;
        rect.anchoredPosition = position;
        float difficultyHp = GetDifficultyHpMultiplier();
        float baseHp = (1f + elapsed / 45f) * difficultyHp;
        float enemySpeed = 42f + Mathf.Min(elapsed * 0.45f, 42f);
        enemies.Add(new Enemy { rect = rect, hp = baseHp, speed = enemySpeed, radius = 16f, xp = 1f + elapsed / 90f, baseColor = image.color });
    }

    private void UpdateEnemies(float dt)
    {
        Vector2 playerPosition = playerRect.anchoredPosition;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemies[i];
            if (enemy.hitFlashTimer > 0f)
            {
                enemy.hitFlashTimer = Mathf.Max(0f, enemy.hitFlashTimer - dt);
                if (enemy.rect != null)
                {
                    Image enemyImage = enemy.rect.GetComponent<Image>();
                    if (enemyImage != null) enemyImage.color = Color.Lerp(enemy.baseColor, Color.white, enemy.hitFlashTimer / 0.09f);
                }
            }
            Vector2 delta = playerPosition - enemy.rect.anchoredPosition;
            if (delta.sqrMagnitude > 0.01f) enemy.rect.anchoredPosition += delta.normalized * enemy.speed * dt;
            if (enemy.boss)
            {
                bossBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(enemy.hp / bossMaxHp), 1f);
                if (delta.magnitude < enemy.radius + 17f && bossContactTimer <= 0f)
                {
                    playerHp -= 1f;
                    bossContactTimer = 1f;
                    hitFlashTimer = 0.2f;
                    ShowEffect(playerPosition, new Color(1f, 0.3f, 0.34f, 0.8f), 82f, 0.24f, 0.4f, 1f);
                    if (playerHp <= 0f) EndGame();
                }
                continue;
            }
            if (delta.magnitude < enemy.radius + 17f)
            {
                enemies.RemoveAt(i);
                Destroy(enemy.rect.gameObject);
                playerHp -= 1f;
                hitFlashTimer = 0.2f;
                ShowEffect(playerPosition, new Color(1f, 0.3f, 0.34f, 0.8f), 82f, 0.24f, 0.4f, 1f);
                if (playerHp <= 0f) EndGame();
            }
        }
    }

    private void UpdateShots(float dt)
    {
        for (int i = shots.Count - 1; i >= 0; i--)
        {
            Shot shot = shots[i];
            shot.lifetime -= dt;
            shot.rect.anchoredPosition += shot.direction * shot.speed * dt;
            bool hit = false;
            for (int e = enemies.Count - 1; e >= 0; e--)
            {
                Enemy enemy = enemies[e];
                if (Vector2.Distance(shot.rect.anchoredPosition, enemy.rect.anchoredPosition) > enemy.radius + 7f) continue;
                enemy.hp -= shot.damage;
                enemy.hitFlashTimer = 0.09f;
                Image enemyImage = enemy.rect.GetComponent<Image>();
                if (enemyImage != null) enemyImage.color = Color.white;
                hit = true;
                if (enemy.hp <= 0f)
                {
                    Destroy(enemy.rect.gameObject);
                    enemies.RemoveAt(e);
                    ResolveEnemyDeath(enemy, true);
                }
                break;
            }

            Rect bounds = playfield.rect;
            Vector2 p = shot.rect.anchoredPosition;
            if (hit || shot.lifetime <= 0f || p.x < bounds.xMin || p.x > bounds.xMax || p.y < bounds.yMin || p.y > bounds.yMax)
            {
                Destroy(shot.rect.gameObject);
                shots.RemoveAt(i);
            }
        }
    }

    private void FireShot(Vector2 direction)
    {
        float size = weaponStyle == MiniArenaWeaponStyle.Heavy ? 16f : 10f;
        Color shotColor = weaponStyle == MiniArenaWeaponStyle.Rapid ? new Color(0.45f, 0.85f, 1f) : new Color(1f, 0.88f, 0.35f);
        Image image = CreateImage(playfield, "Shot", shotColor, Vector2.one * size);
        RectTransform rect = image.rectTransform;
        rect.anchoredPosition = playerRect.anchoredPosition + direction * 22f;
        float speed = weaponStyle == MiniArenaWeaponStyle.Heavy ? 340f : weaponStyle == MiniArenaWeaponStyle.Rapid ? 510f : 430f;
        shots.Add(new Shot { rect = rect, direction = direction, speed = speed, lifetime = 2.2f, damage = GetShotDamage() });
    }

    private void UsePulse()
    {
        if (!runActive || gameOver || isClosed || pausedForChoice || pulseCooldown > 0f) return;
        pulseCooldown = 8f;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemies[i];
            if (Vector2.Distance(playerRect.anchoredPosition, enemy.rect.anchoredPosition) > 150f) continue;
            enemy.hp -= 3f;
            if (enemy.hp <= 0f)
            {
                Destroy(enemy.rect.gameObject);
                enemies.RemoveAt(i);
                ResolveEnemyDeath(enemy, true);
            }
        }
        ShowPulseEffect();
    }

    private void UseHeal()
    {
        if (!runActive || gameOver || isClosed || healCooldown > 0f) return;
        healCooldown = 12f;
        playerHp = Mathf.Min(playerMaxHp, playerHp + 1f);
    }

    private void UseOrbitBurst()
    {
        if (!runActive || pausedForChoice || gameOver || isClosed) return;
        int level = runUpgrades.TryGetValue("bloom", out int value) ? value : 0;
        if (level <= 0) return;
        float reach = 100f + (runUpgrades.TryGetValue("petalShield", out int reachLevel) ? reachLevel * 20f : 0f);
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemies[i];
            if (Vector2.Distance(enemy.rect.anchoredPosition, playerRect.anchoredPosition) > reach) continue;
            enemy.hp -= level * 2f;
            if (enemy.hp <= 0f)
            {
                Destroy(enemy.rect.gameObject);
                enemies.RemoveAt(i);
                ResolveEnemyDeath(enemy, true);
            }
        }
        ShowPulseEffect();
    }

    private void ShowPulseEffect()
    {
        ShowEffect(playerRect.anchoredPosition, new Color(0.5f, 0.85f, 1f, 0.8f), 300f, 0.28f, 0.35f, 1.1f);
    }

    private void ShowEffect(Vector2 position, Color color, float size, float duration, float startScale, float endScale)
    {
        Image ring = CreateImage(playfield, "ArenaEffect", color, new Vector2(size, size));
        ring.sprite = effectRingSprite;
        ring.type = Image.Type.Simple;
        ring.preserveAspect = true;
        ring.rectTransform.anchoredPosition = position;
        ring.rectTransform.localScale = Vector3.one * startScale;
        effects.Add(new ArenaEffect { image = ring, duration = duration, startScale = startScale, endScale = endScale, startAlpha = color.a });
    }

    private void UpdateEffects(float dt)
    {
        for (int i = effects.Count - 1; i >= 0; i--)
        {
            ArenaEffect effect = effects[i];
            if (effect.image == null)
            {
                effects.RemoveAt(i);
                continue;
            }

            effect.age += dt;
            float progress = Mathf.Clamp01(effect.age / effect.duration);
            effect.image.rectTransform.localScale = Vector3.one * Mathf.Lerp(effect.startScale, effect.endScale, progress);
            Color color = effect.image.color;
            color.a = effect.startAlpha * (1f - progress);
            effect.image.color = color;
            if (progress >= 1f)
            {
                Destroy(effect.image.gameObject);
                effects.RemoveAt(i);
            }
        }
    }

    private void CreateEffectRingSprite()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "MiniArenaEffectRingTexture";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float radius = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                float alpha = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.44f) / 0.075f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        effectRingSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        effectRingSprite.name = "MiniArenaEffectRing";
    }

    private Enemy FindNearestEnemy()
    {
        Enemy closest = null;
        float best = float.MaxValue;
        Vector2 position = playerRect.anchoredPosition;
        foreach (Enemy enemy in enemies)
        {
            float distance = (enemy.rect.anchoredPosition - position).sqrMagnitude;
            if (distance < best) { best = distance; closest = enemy; }
        }
        return closest;
    }

    private void EndGame()
    {
        FinishRun(false);
    }

    private void UpdateStatus()
    {
        if (statusText != null && !gameOver)
        {
            statusText.text = $"{GetDifficultyName()}  {FormatTime(elapsed)}  Lv.{playerLevel}  체력 {Mathf.CeilToInt(playerHp)}/{Mathf.CeilToInt(playerMaxHp)}  처치 {defeated}  보스 {bossesDefeated}";
            xpText.text = $"XP {Mathf.FloorToInt(xp)}/{Mathf.CeilToInt(xpToNext)}";
            xpBar.anchorMax = new Vector2(Mathf.Clamp01(xp / xpToNext), 1f);
            if (bossText.gameObject.activeSelf)
                bossBarFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(bossBarFill.rectTransform.anchorMax.x), 1f);
        }
    }
    private void SpawnExperience(Enemy enemy)
    {
        if (enemy == null || enemy.boss) return;
        Image orb = CreateImage(playfield, "ExperienceOrb", new Color(0.4f, 0.85f, 1f), new Vector2(13f, 13f));
        orb.rectTransform.anchoredPosition = enemy.rect.anchoredPosition;
        float tierBonus = elapsed >= 480f ? 2.5f : elapsed >= 180f ? 1.5f : 0f;
        experienceOrbs.Add(new ExperienceOrb { rect = orb.rectTransform, value = enemy.xp + tierBonus });
    }

    private void UpdateExperienceOrbs(float dt)
    {
        float pickupRadius = 36f + MiniArenaProgression.Data.pickupLevel * 10f;
        Vector2 playerPosition = playerRect.anchoredPosition;
        for (int i = experienceOrbs.Count - 1; i >= 0; i--)
        {
            ExperienceOrb orb = experienceOrbs[i];
            if (orb.rect == null) { experienceOrbs.RemoveAt(i); continue; }
            Vector2 delta = playerPosition - orb.rect.anchoredPosition;
            if (delta.sqrMagnitude <= pickupRadius * pickupRadius)
            {
                int insight = runUpgrades.TryGetValue("insight", out int insightLevel) ? insightLevel : 0;
                AddExperience(orb.value * (1f + MiniArenaProgression.Data.experienceLevel * 0.06f + insight * 0.15f));
                Destroy(orb.rect.gameObject);
                experienceOrbs.RemoveAt(i);
            }
            else if (delta.sqrMagnitude < 180f * 180f)
                orb.rect.anchoredPosition += delta.normalized * 240f * dt;
        }
    }

    private void AddExperience(float amount)
    {
        xp += amount;
        while (xp >= xpToNext && runActive && !runFinished)
        {
            xp -= xpToNext;
            xpToNext = Mathf.Ceil(xpToNext * 1.18f + 2f);
            playerLevel++;
            levelUpChoicesPending++;
            ShowEffect(playerRect.anchoredPosition, new Color(0.5f, 1f, 0.72f, 0.9f), 125f, 0.52f, 0.3f, 1.2f);
        }
        if (levelUpChoicesPending > 0 && !pausedForChoice)
        {
            pausedForChoice = true;
            BuildLevelUpPanel();
            levelPanel.gameObject.SetActive(true);
            levelPanel.SetAsLastSibling();
        }
    }

    private void UpdateBossTimer(float dt)
    {
        if (enemies.Exists(enemy => enemy.boss)) return;
        bossTimer -= dt;
        if (bossTimer > 0f) return;
        SpawnBoss();
    }

    private void SpawnBoss()
    {
        if (enemies.Exists(enemy => enemy.boss)) return;
        float difficultyMultiplier = GetDifficultyHpMultiplier();
        bossMaxHp = (30f + bossesDefeated * 22f) * difficultyMultiplier;
        Image image = CreateImage(playfield, "ArenaBoss", new Color(0.7f, 0.25f, 0.95f), new Vector2(60f, 60f));
        Rect bounds = playfield.rect;
        image.rectTransform.anchoredPosition = new Vector2(bounds.xMax - 70f, bounds.yMax - 70f);
        enemies.Add(new Enemy { rect = image.rectTransform, hp = bossMaxHp, speed = 38f + bossesDefeated * 3f, radius = 34f, xp = 0f, boss = true, baseColor = image.color });
        ShowEffect(image.rectTransform.anchoredPosition, new Color(0.8f, 0.45f, 1f, 0.9f), 240f, 0.65f, 0.25f, 1.35f);
        bossText.text = $"도전의 수호자 {bossesDefeated + 1}";
        bossText.gameObject.SetActive(true);
        bossBarBackgroundImage.gameObject.SetActive(true);
        bossBarFill.rectTransform.anchorMax = Vector2.one;
    }

    private void OnBossDefeated()
    {
        bossText.gameObject.SetActive(false);
        bossBarBackgroundImage.gameObject.SetActive(false);
        bossTimer = 150f;
        int requiredBosses = difficulty == MiniArenaDifficulty.Relaxed ? 2 : difficulty == MiniArenaDifficulty.Standard ? 4 : 8;
        if (difficulty != MiniArenaDifficulty.Endless && bossesDefeated >= requiredBosses)
            FinishRun(true);
    }

    private void ResolveEnemyDeath(Enemy enemy, bool canTriggerPassive)
    {
        if (enemy == null) return;
        if (enemy.boss)
        {
            if (enemy.rect != null)
                ShowEffect(enemy.rect.anchoredPosition, new Color(1f, 0.7f, 0.35f, 0.9f), 220f, 0.55f, 0.25f, 1.2f);
            bossesDefeated++;
            OnBossDefeated();
            return;
        }

        SpawnExperience(enemy);
        RegisterEnemyKill(enemy, canTriggerPassive);
    }

    private void RegisterEnemyKill(Enemy enemy, bool canTriggerPassive)
    {
        defeated++;
        if (!canTriggerPassive || MiniArenaFlowerProfiles.Find(flowerId) == null) return;

        int seedLevel = MiniArenaUpgradeCatalog.GetLevel(runUpgrades, "seed");
        passiveSeeds++;
        float threshold = Mathf.Max(2f, 5f - seedLevel * 0.5f);
        while (passiveSeeds >= threshold)
        {
            passiveSeeds -= threshold;
            TriggerDandelionBurst();
        }
    }

    private void TriggerDandelionBurst()
    {
        int seedLevel = runUpgrades.TryGetValue("seed", out int seed) ? seed : 0;
        int burstLevel = runUpgrades.TryGetValue("seedBurst", out int burst) ? burst : 0;
        int germination = runUpgrades.TryGetValue("seedGermination", out int germinationLevel) ? germinationLevel : 0;
        int seedRain = runUpgrades.TryGetValue("seedRain", out int seedRainLevel) ? seedRainLevel : 0;
        int windLevel = runUpgrades.TryGetValue("wind", out int wind) ? wind : 0;
        float radius = 145f + burstLevel * 14f;
        float damage = (1.5f + seedLevel * 0.45f + burstLevel * 0.35f) * (1f + germination * 0.08f);
        Vector2 center = playerRect.anchoredPosition;
        List<Vector2> burstDirections = new List<Vector2>();
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy enemy = enemies[i];
            Vector2 away = enemy.rect.anchoredPosition - center;
            if (away.magnitude > radius) continue;
            enemy.hp -= damage;
            if (away.sqrMagnitude > 0.01f)
                enemy.rect.anchoredPosition += away.normalized * (65f + windLevel * 18f);
            if (enemy.hp > 0f) continue;
            burstDirections.Add(away.sqrMagnitude > 0.01f ? away.normalized : Vector2.up);
            Destroy(enemy.rect.gameObject);
            enemies.RemoveAt(i);
            ResolveEnemyDeath(enemy, false);
        }
        int fusionSeedWind = runUpgrades.TryGetValue("fusionSeedWind", out int seedWindLevel) ? seedWindLevel : 0;
        int fusionSeedBloom = runUpgrades.TryGetValue("fusionSeedBloom", out int seedBloomLevel) ? seedBloomLevel : 0;
        int projectileCount = Mathf.Min(3, seedRain + fusionSeedWind + (fusionSeedBloom > 0 ? 1 : 0));
        for (int i = 0; i < projectileCount; i++)
        {
            Vector2 direction = burstDirections.Count > 0
                ? burstDirections[i % burstDirections.Count]
                : Quaternion.Euler(0f, 0f, i * 120f) * aimDirection;
            FireSkillShot(direction, "Seed", new Color(0.85f, 1f, 0.55f), 1.1f + seedRain * 0.3f + fusionSeedWind * 0.35f, 440f);
        }
        if (fusionSeedBloom > 0)
            bloomTimer = Mathf.Min(bloomTimer, 0.2f);
        ShowPulseEffect();
    }

    private void UpdateFlowerSkills(float dt)
    {
        if (MiniArenaFlowerProfiles.Find(flowerId) == null) return;

        int tailwind = runUpgrades.TryGetValue("tailwind", out int tailwindLevel) ? tailwindLevel : 0;
        int crosswind = runUpgrades.TryGetValue("crosswind", out int crosswindLevel) ? crosswindLevel : 0;
        int gale = runUpgrades.TryGetValue("gale", out int galeLevel) ? galeLevel : 0;
        if (tailwind > 0 && moveInput.sqrMagnitude > 0.01f && tailwindTimer <= 0f)
        {
            tailwindTimer = Mathf.Max(0.35f, 2.5f - tailwind * 0.25f - gale * 0.3f);
            Enemy nearest = FindNearestEnemy();
            if (nearest != null)
            {
                Vector2 direction = (nearest.rect.anchoredPosition - playerRect.anchoredPosition).normalized;
                FireSkillShot(direction, "Tailwind", new Color(0.55f, 0.9f, 1f), 1f + tailwind * 0.25f + crosswind * 0.2f, 520f);
            }
        }

        int bloom = runUpgrades.TryGetValue("bloom", out int bloomLevel) ? bloomLevel : 0;
        if (bloom > 0 && bloomTimer <= 0f)
        {
            int pollen = runUpgrades.TryGetValue("pollen", out int pollenLevel) ? pollenLevel : 0;
            int everbloom = runUpgrades.TryGetValue("everbloom", out int everbloomLevel) ? everbloomLevel : 0;
            bloomTimer = Mathf.Max(0.35f, 2.2f - bloom * 0.2f - everbloom * 0.25f);
            int shield = runUpgrades.TryGetValue("petalShield", out int shieldLevel) ? shieldLevel : 0;
            int fusionWindBloom = runUpgrades.TryGetValue("fusionWindBloom", out int windBloomLevel) ? windBloomLevel : 0;
            float radius = 75f + shield * 12f + everbloom * 10f + fusionWindBloom * 18f;
            List<Vector2> pollenKillPositions = new List<Vector2>();
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = enemies[i];
                if (Vector2.Distance(enemy.rect.anchoredPosition, playerRect.anchoredPosition) > radius) continue;
                enemy.hp -= 0.8f + bloom * 0.35f + shield * 0.15f + fusionWindBloom * 0.4f;
                if (enemy.hp <= 0f && !enemy.boss)
                {
                    if (pollen > 0) pollenKillPositions.Add(enemy.rect.anchoredPosition);
                    Destroy(enemy.rect.gameObject);
                    enemies.RemoveAt(i);
                    ResolveEnemyDeath(enemy, true);
                }
            }
            foreach (Vector2 killPosition in pollenKillPositions)
                ApplyPollenBurst(killPosition, pollen, everbloom);
            if (fusionWindBloom > 0)
                FireSkillShot(aimDirection, "FlowerWind", new Color(1f, 0.72f, 0.85f), 1.2f + fusionWindBloom * 0.35f, 420f);
            ShowPulseEffect();
        }
    }

    private void ApplyPollenBurst(Vector2 center, int pollenLevel, int everbloomLevel)
    {
        float radius = 45f + pollenLevel * 12f + everbloomLevel * 10f;
        float damage = 0.5f + pollenLevel * 0.25f;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            Enemy target = enemies[i];
            if (Vector2.Distance(target.rect.anchoredPosition, center) > radius) continue;
            target.hp -= damage;
            if (target.hp > 0f || target.boss) continue;
            Destroy(target.rect.gameObject);
            enemies.RemoveAt(i);
            ResolveEnemyDeath(target, false);
        }
    }

    private void FireSkillShot(Vector2 direction, string name, Color color, float damage, float speed)
    {
        Image image = CreateImage(playfield, name, color, new Vector2(12f, 12f));
        RectTransform rect = image.rectTransform;
        rect.anchoredPosition = playerRect.anchoredPosition;
        shots.Add(new Shot { rect = rect, direction = direction, speed = speed, lifetime = 1.8f, damage = damage });
    }

    private bool IsFlowerAvailable()
    {
        FlowerManager manager = FlowerManager.Instance;
        FlowerInstance instance = manager != null ? manager.GetInstance(flowerId) : null;
        return instance != null && instance.isBloomed;
    }

    private void UpdateFlowerAvailability()
    {
        bool available = IsFlowerAvailable();
        if (passiveText != null)
        {
            MiniArenaFlowerProfile profile = MiniArenaFlowerProfiles.Find(flowerId);
            passiveText.text = available && profile != null
                ? $"{profile.displayName} 고유 패시브: {profile.passiveName} — {profile.passiveDescription}"
                : "민들레를 개화하면 미니 아레나에 입장할 수 있습니다.";
        }
        Button start = startPanel != null ? startPanel.Find("StartRun")?.GetComponent<Button>() : null;
        if (start != null) start.interactable = available;
    }

    private float GetShotDamage()
    {
        float damage = weaponStyle == MiniArenaWeaponStyle.Heavy ? 3f : weaponStyle == MiniArenaWeaponStyle.Rapid ? 0.65f : 1f;
        if (runUpgrades.TryGetValue("power", out int power)) damage *= 1f + power * 0.10f;
        return damage * (1f + MiniArenaProgression.Data.damageLevel * 0.06f);
    }

    private float GetBaseFireInterval()
    {
        return weaponStyle == MiniArenaWeaponStyle.Rapid ? 0.28f : weaponStyle == MiniArenaWeaponStyle.Heavy ? 1f : 0.65f;
    }

    private float GetBaseSpawnInterval()
    {
        return difficulty == MiniArenaDifficulty.Relaxed ? 1.9f
            : difficulty == MiniArenaDifficulty.Expert ? 0.95f
            : difficulty == MiniArenaDifficulty.Endless ? 1.05f : 1.4f;
    }

    private float GetDifficultyHpMultiplier()
    {
        return difficulty == MiniArenaDifficulty.Relaxed ? 0.75f
            : difficulty == MiniArenaDifficulty.Expert ? 1.6f
            : difficulty == MiniArenaDifficulty.Endless ? 1.35f : 1f;
    }

    private string GetDifficultyName()
    {
        switch (difficulty)
        {
            case MiniArenaDifficulty.Relaxed: return "여유";
            case MiniArenaDifficulty.Expert: return "도전";
            case MiniArenaDifficulty.Endless: return "무한";
            default: return "표준";
        }
    }

    private static string FormatTime(float seconds) => $"{Mathf.FloorToInt(seconds / 60f):00}:{Mathf.FloorToInt(seconds % 60f):00}";

    private void FinishRun(bool victory)
    {
        if (runFinished) return;
        runFinished = true;
        runActive = false;
        gameOver = !victory;
        int seconds = Mathf.FloorToInt(elapsed);
        MiniArenaProgression.CompleteRun(seconds, defeated, victory);
        Time.timeScale = 0f;
        BuildResultPanel(victory);
        resultPanel.gameObject.SetActive(true);
        resultPanel.SetAsLastSibling();
        UpdateStatus();
    }

    private void TogglePause()
    {
        if (!runActive || pausedForChoice || runFinished) return;
        paused = !paused;
        if (paused)
        {
            Time.timeScale = 0f;
            levelPanel.gameObject.SetActive(true);
            foreach (Transform child in levelPanel) Destroy(child.gameObject);
            TMP_Text label = CreateText(levelPanel, "PauseLabel", 26f, TextAlignmentOptions.Center);
            label.text = "일시정지";
            SetAnchors(label.rectTransform, new Vector2(0.1f, 0.65f), new Vector2(0.9f, 0.9f));
            CreateButton(levelPanel, "Resume", "계속", new Vector2(0.12f, 0.24f), new Vector2(0.42f, 0.52f), () => { paused = false; Time.timeScale = 0f; levelPanel.gameObject.SetActive(false); });
            CreateButton(levelPanel, "Retreat", "도전 종료", new Vector2(0.58f, 0.24f), new Vector2(0.88f, 0.52f), () => FinishRun(false));
        }
        else
        {
            Time.timeScale = 0f;
            levelPanel.gameObject.SetActive(false);
        }
    }

    private void ClearRunObjects()
    {
        foreach (Enemy enemy in enemies) if (enemy.rect != null) Destroy(enemy.rect.gameObject);
        foreach (Shot shot in shots) if (shot.rect != null) Destroy(shot.rect.gameObject);
        foreach (ExperienceOrb orb in experienceOrbs) if (orb.rect != null) Destroy(orb.rect.gameObject);
        foreach (ArenaEffect effect in effects) if (effect.image != null) Destroy(effect.image.gameObject);
        enemies.Clear();
        shots.Clear();
        experienceOrbs.Clear();
        effects.Clear();
    }

    private void Restart()
    {
        StartRun();
    }

    private static TMP_Text CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(10f, size * 0.68f);
        text.fontSizeMax = size;
        text.color = Color.white;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        UIManager uiManager = Object.FindAnyObjectByType<UIManager>();
        if (uiManager != null && uiManager.affectionValueText != null && uiManager.affectionValueText.font != null)
            text.font = uiManager.affectionValueText.font;
        else if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        return text;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        SetAnchors(rect, min, max);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.2f, 0.38f, 0.5f, 1f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.78f, 0.86f, 0.92f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.65f);
        button.colors = colors;
        button.onClick.AddListener(action);
        TMP_Text text = CreateText(go.transform, "Label", 17f, TextAlignmentOptions.Center);
        SetAnchors(text.rectTransform, Vector2.zero, Vector2.one);
        text.text = label;
        return button;
    }

    private static Image CreateImage(Transform parent, string name, Color color, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        SetAnchors(rect, min, max);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return rect;
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void Close()
    {
        if (isClosed) return;
        isClosed = true;
        Time.timeScale = previousTimeScale;
        activeGame = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (activeGame == this)
        {
            activeGame = null;
            Time.timeScale = previousTimeScale;
        }
        if (effectRingSprite != null)
        {
            Destroy(effectRingSprite.texture);
            Destroy(effectRingSprite);
            effectRingSprite = null;
        }
    }

    public void SetMoveInput(Vector2 input) => pointerMoveInput = Vector2.ClampMagnitude(input, 1f);
    public void TriggerRestart() { if (runFinished) StartRun(); }
}

public sealed class MiniArenaPointerInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private MiniArenaGame game;
    private RectTransform rect;
    private bool dragging;
    private Vector2 lastPointer;

    public void Initialize(MiniArenaGame owner) { game = owner; rect = (RectTransform)transform; }

    public void OnPointerDown(PointerEventData eventData)
    {
        dragging = true;
        lastPointer = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || game == null) return;
        Vector2 delta = eventData.position - lastPointer;
        lastPointer = eventData.position;
        Camera camera = eventData.pressEventCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, camera, out Vector2 local);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position - delta, camera, out Vector2 previousLocal);
        game.SetMoveInput((local - previousLocal).normalized);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        dragging = false;
        game?.SetMoveInput(Vector2.zero);
    }
}
