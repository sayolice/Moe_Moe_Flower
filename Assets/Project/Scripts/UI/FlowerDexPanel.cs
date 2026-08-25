using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도감 그리드 팝업. 보유한 꽃만 나열하고(미보유 제외), 항목을 선택하면
/// 해당 꽃을 메인에 표시하고 도감을 닫는다.
/// OfflineSummaryPopup과 동일한 "root GameObject를 SetActive로 여닫는다" 관례를 따른다.
/// </summary>
public class FlowerDexPanel : MonoBehaviour
{
    [Header("루트 (Show/Hide 대상, 비워두면 자기 자신)")]
    public GameObject root;

    [Header("참조")]
    public Transform content;
    public FlowerDexItem itemPrefab;

    [Header("버튼 (Inspector/빌더에서 필드만 연결 — 리스너는 런타임에 스스로 붙인다)")]
    [Tooltip("이 도감을 여는 버튼(TopBar 등 외부 오브젝트). 에디터 스크립트에서 onClick.AddListener를 " +
             "직접 호출하면 Play 모드 밖이라 씬에 저장되지 않으므로, 필드 참조만 받아 Start()에서 스스로 연결한다.")]
    public Button openButton;
    public Button closeButton;

    private readonly List<FlowerDexItem> items = new List<FlowerDexItem>();

    private void Awake()
    {
        if (root == null) root = gameObject;
        root.SetActive(false);
    }

    private void Start()
    {
        if (openButton != null)
            openButton.onClick.AddListener(Open);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnOwnedFlowersChanged += RebuildList;
            FlowerManager.Instance.OnFlowerBloomed += HandleFlowerBloomed;
            FlowerManager.Instance.OnDisplayedFlowerChanged += HandleDisplayedFlowerChanged;
        }
    }

    private void OnDestroy()
    {
        if (FlowerManager.Instance != null)
        {
            FlowerManager.Instance.OnOwnedFlowersChanged -= RebuildList;
            FlowerManager.Instance.OnFlowerBloomed -= HandleFlowerBloomed;
            FlowerManager.Instance.OnDisplayedFlowerChanged -= HandleDisplayedFlowerChanged;
        }
    }

    private void HandleFlowerBloomed(string _) => RefreshItems(); // 개화 시 상태(레벨/성장%) 갱신
    private void HandleDisplayedFlowerChanged(string _) => RefreshItems(); // 현재 표시 중 강조 갱신

    /// <summary> 도감 열기. 열 때마다 최신 보유 목록으로 다시 그린다. </summary>
    public void Open()
    {
        RebuildList();
        root.SetActive(true);
    }

    public void Close()
    {
        root.SetActive(false);
    }

    /// <summary> FlowerDexItem이 선택됐을 때 호출: 해당 꽃을 메인에 띄우고 도감을 닫는다. </summary>
    public void SelectAndClose(string flowerId)
    {
        if (FlowerManager.Instance != null)
            FlowerManager.Instance.TryJumpTo(flowerId);

        Close();
    }

    private void RebuildList()
    {
        if (content == null || itemPrefab == null || FlowerManager.Instance == null)
            return;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
        items.Clear();

        // 도감순 = FlowerManager.allFlowers 순서. 보유(구매)한 꽃만 대상.
        foreach (string id in FlowerManager.Instance.GetOwnedIdsInDexOrder())
        {
            FlowerDexItem item = Instantiate(itemPrefab, content);
            item.Setup(id, this);
            items.Add(item);
        }
    }

    private void RefreshItems()
    {
        foreach (FlowerDexItem item in items)
        {
            if (item != null) item.Refresh();
        }
    }
}
