using UnityEngine;

/// <summary>
/// PC/Mobile 레이아웃 전환 컨트롤러.
///
/// 사용법:
///   - leftSidebar  : LeftSidebar GameObject 연결
///   - rightSidebar : RightSidebar GameObject 연결
///   - leftPanel    : 현재 LeftSlot에 배치된 패널 (참조용 레이블, 런타임 이동 없음)
///   - rightPanel   : 현재 RightSlot에 배치된 패널 (참조용 레이블)
///
/// 배치 교체 방법 (코드 수정 불필요):
///   Unity Hierarchy에서 ShopPanel을 RightSlot 자식으로,
///   FlowerManagementPanel을 LeftSlot 자식으로 드래그하면 즉시 적용.
///   leftPanel / rightPanel Inspector 레이블도 함께 업데이트해 두면 관리가 편함.
/// </summary>
public class PCLayoutController : MonoBehaviour
{
    [Header("사이드바 루트 (ON/OFF 대상)")]
    public GameObject leftSidebar;
    public GameObject rightSidebar;

    [Header("현재 배치 패널 (Inspector 드래그로 교체 — 런타임 이동 없음)")]
    [Tooltip("LeftSlot 안에 직접 배치된 패널. 레이블 용도.")]
    public GameObject leftPanel;

    [Tooltip("RightSlot 안에 직접 배치된 패널. 레이블 용도.")]
    public GameObject rightPanel;

    [Header("모바일 설정")]
    [Tooltip("true면 모바일 플랫폼에서 자동으로 사이드바를 숨긴다.")]
    public bool hideSidebarsOnMobile = true;

    private void Start()
    {
        if (hideSidebarsOnMobile && Application.isMobilePlatform)
        {
            SetSidebarsActive(false);
        }
    }

    // ── 공개 API (버튼 또는 코드에서 호출 가능) ──────────────────────────

    /// <summary>좌우 사이드바를 동시에 켜거나 끈다.</summary>
    public void SetSidebarsActive(bool active)
    {
        SetLeftSidebarActive(active);
        SetRightSidebarActive(active);
    }

    /// <summary>왼쪽 사이드바만 켜거나 끈다.</summary>
    public void SetLeftSidebarActive(bool active)
    {
        if (leftSidebar != null)
            leftSidebar.SetActive(active);
    }

    /// <summary>오른쪽 사이드바만 켜거나 끈다.</summary>
    public void SetRightSidebarActive(bool active)
    {
        if (rightSidebar != null)
            rightSidebar.SetActive(active);
    }

    /// <summary>사이드바 가시 상태를 토글한다.</summary>
    public void ToggleSidebars()
    {
        bool current = leftSidebar != null && leftSidebar.activeSelf;
        SetSidebarsActive(!current);
    }
}
