using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 범용 탭-패널 전환기. 특정 위치(오른쪽 사이드바 등)에 종속되지 않는다.
/// "버튼 하나 = 패널 하나" 쌍을 리스트로 관리하고, 한 번에 하나의 패널만 활성화한다.
/// 어떤 Canvas 영역에 붙여도 동작하며, Flower/Player/Shop 등 구체적인 기능을 전혀 모른다.
/// 탭 추가/삭제는 Inspector에서 tabs 리스트 항목을 늘리거나 줄이기만 하면 된다.
/// </summary>
public class PanelSwitcher : MonoBehaviour
{
    [System.Serializable]
    public class Tab
    {
        public Button button;
        public GameObject panel;
    }

    [Header("탭 목록 (버튼-패널 쌍, 순서/개수 자유롭게 변경 가능)")]
    public List<Tab> tabs = new List<Tab>();

    [Header("시작 시 활성화할 탭 인덱스")]
    public int defaultTabIndex = 0;

    [Header("탭 강조 색상 (Inspector에서 자유롭게 변경 — 특정 색을 코드에 고정하지 않음)")]
    public Color activeTabColor = Color.white;
    public Color inactiveTabColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    private void Start()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i; // 람다 캡처용 로컬 복사 (반복 변수 그대로 캡처하면 전부 마지막 값이 됨)
            if (tabs[i].button != null)
                tabs[i].button.onClick.AddListener(() => SwitchTo(index));
        }

        if (tabs.Count > 0)
            SwitchTo(Mathf.Clamp(defaultTabIndex, 0, tabs.Count - 1));
    }

    /// <summary> 인덱스로 지정한 탭의 패널만 활성화하고 나머지는 비활성화한다. 탭 버튼 강조 색상도 함께 갱신한다. </summary>
    public void SwitchTo(int index)
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            bool isActive = (i == index);

            if (tabs[i].panel != null)
                tabs[i].panel.SetActive(isActive);

            ApplyTabColor(tabs[i], isActive);
        }
    }

    private void ApplyTabColor(Tab tab, bool isActive)
    {
        if (tab.button == null || tab.button.targetGraphic == null) return;
        tab.button.targetGraphic.color = isActive ? activeTabColor : inactiveTabColor;
    }

    /// <summary> 패널 오브젝트로 직접 전환한다 (탭 인덱스를 몰라도 됨). </summary>
    public void SwitchTo(GameObject panel)
    {
        int index = tabs.FindIndex(t => t.panel == panel);
        if (index >= 0) SwitchTo(index);
    }
}
