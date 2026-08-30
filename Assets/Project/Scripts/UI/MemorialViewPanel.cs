using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꽃 1개의 유대 메모리얼 목록/본문을 보여주는 전체 화면 패널. 도감 상세의 "메모리얼" 버튼이나
/// 메인화면 유대 게이지 탭이 특정 flowerId를 지정해서 Open()을 호출한다 — 도감처럼 꽃을 바꾸는
/// 화면이 아니라, "지금 이 꽃 하나"의 회상만 보여주는 별도 화면이다.
///
/// [중요] 이 컴포넌트가 붙은 GameObject(root)는 절대 SetActive(false)로 끄지 않는다 — FlowerDexPanel과
/// 동일한 이유(비활성 오브젝트는 Awake/Start가 스킵되어 다음 씬 로드 때 버튼 리스너가 영영 안 붙는
/// 버그가 됨). CanvasGroup(alpha/interactable/blocksRaycasts)으로만 보이기/숨기기를 전환한다.
/// </summary>
public class MemorialViewPanel : MonoBehaviour
{
    public static MemorialViewPanel Instance { get; private set; }

    [Header("루트 (비워두면 자기 자신 — 절대 SetActive(false)로 끄지 않음)")]
    public GameObject root;
    public Button closeButton;
    public TMP_Text flowerNameText;

    [Header("목록 화면")]
    public GameObject listRoot;
    public Transform listContent;
    public MemorialEntryItem itemPrefab;
    public TMP_Text emptyStateText; // 메모리얼이 아예 0편인 꽃(작성 전)일 때 안내

    [Header("본문 화면")]
    public GameObject detailRoot;
    public TMP_Text detailTitleText;
    public TMP_Text detailBodyText;
    public Button detailBackButton;

    private CanvasGroup canvasGroup;
    private string currentFlowerId;
    private readonly List<MemorialEntryItem> spawnedItems = new List<MemorialEntryItem>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (root == null) root = gameObject;
        canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = root.AddComponent<CanvasGroup>();
    }

    private void Start()
    {
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (detailBackButton != null) detailBackButton.onClick.AddListener(ShowList);

        SetVisible(false);
        if (detailRoot != null) detailRoot.SetActive(false);
    }

    /// <summary> 지정한 꽃의 메모리얼 화면을 연다(항상 목록부터 시작). </summary>
    public void Open(string flowerId)
    {
        currentFlowerId = flowerId;
        RebuildList();
        ShowList();
        SetVisible(true);
    }

    public void Close() => SetVisible(false);

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;
    }

    private void ShowList()
    {
        if (listRoot != null) listRoot.SetActive(true);
        if (detailRoot != null) detailRoot.SetActive(false);
    }

    private void RebuildList()
    {
        foreach (MemorialEntryItem item in spawnedItems)
            if (item != null) Destroy(item.gameObject);
        spawnedItems.Clear();

        if (FlowerManager.Instance == null || listContent == null || itemPrefab == null) return;

        FlowerData data = FlowerManager.Instance.GetFlowerData(currentFlowerId);
        FlowerInstance instance = FlowerManager.Instance.GetInstance(currentFlowerId);
        if (data == null) return;

        if (flowerNameText != null)
            flowerNameText.text = $"{data.displayName} - 메모리얼";

        bool hasAny = data.memorialEntries != null && data.memorialEntries.Count > 0;
        if (emptyStateText != null)
            emptyStateText.gameObject.SetActive(!hasAny);
        if (!hasAny) return;

        int currentBondLevel = instance != null ? instance.bondLevel : 0;

        foreach (MemorialData entry in data.memorialEntries)
        {
            if (entry == null) continue;

            bool unlocked = entry.unlockBondLevel <= currentBondLevel;
            bool unread = unlocked && (instance == null ||
                instance.readMemorialBondLevels == null ||
                !instance.readMemorialBondLevels.Contains(entry.unlockBondLevel));

            MemorialEntryItem item = Instantiate(itemPrefab, listContent);
            item.Setup(this, entry.unlockBondLevel, entry.title, entry.body, entry.lines, unlocked, unread);
            spawnedItems.Add(item);
        }
    }

    /// <summary>
    /// MemorialEntryItem이 클릭됐을 때 호출(해금된 항목만 클릭 가능하므로 여기 도달하면 항상 해금 상태).
    /// lines(태그 대본)가 있으면 MemorialPlayerPanel로 재생하고, 없으면(작성 전이거나 예전 방식) 기존
    /// 줄글 body 화면을 그대로 보여준다 — lines 필드를 새로 추가해도 body만 채워둔 메모리얼이 깨지지
    /// 않아야 하기 때문이다.
    /// </summary>
    public void OpenEntryDetail(int bondLevel, string title, string body, List<string> lines)
    {
        if (lines != null && lines.Count > 0 && MemorialPlayerPanel.Instance != null)
        {
            if (listRoot != null) listRoot.SetActive(false);

            MemorialPlayerPanel.Instance.Play(lines, completed =>
            {
                // 요청 명세: "완료되면 목록으로 복귀하며 읽음 처리됩니다" — 중간에 닫기로 나간 경우엔
                // 읽음 처리하지 않는다(끝까지 보지 않았으므로).
                if (completed && FlowerManager.Instance != null && !string.IsNullOrEmpty(currentFlowerId))
                    FlowerManager.Instance.MarkMemorialRead(currentFlowerId, bondLevel);

                ShowList();
                RebuildList(); // NEW 배지·잠금 상태 갱신
            });
            return;
        }

        // ── 기존 방식: 줄글 body 화면 ──
        if (listRoot != null) listRoot.SetActive(false);
        if (detailRoot != null) detailRoot.SetActive(true);

        if (detailTitleText != null)
            detailTitleText.text = string.IsNullOrEmpty(title) ? "(제목 없음)" : title;
        if (detailBodyText != null)
            detailBodyText.text = string.IsNullOrEmpty(body) ? "(아직 작성되지 않았습니다)" : body;

        if (FlowerManager.Instance != null && !string.IsNullOrEmpty(currentFlowerId))
            FlowerManager.Instance.MarkMemorialRead(currentFlowerId, bondLevel);
    }
}
