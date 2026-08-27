using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설정 화면 — 도감/메모리얼과 동일한 전체 화면 오버레이(CanvasGroup으로만 보이기/숨기기 전환,
/// 절대 SetActive(false)로 자기 자신을 끄지 않음 — Awake/Start 스킵 문제 재발 방지).
///
/// [확장성] 지금은 "데이터 초기화" 하나뿐이지만, 나중에 BGM/효과음 볼륨, FPS 제한, 튜토리얼
/// 다시보기 등을 추가할 때 이 클래스나 씬 구조를 바꿀 필요가 없다 — ShopPanel/GrowthPanel과 똑같이
/// content는 그냥 세로 스크롤 영역(VerticalLayoutGroup)이라, PCLayoutBuilder.BuildSettingsPanel에서
/// 새 설정 UI(슬라이더/토글/버튼 등)를 content의 자식으로 추가하기만 하면 그대로 목록에 끼어든다.
/// 이 스크립트는 "지금 존재가 확정된 설정 항목"의 동작(데이터 초기화)만 담당하고, 항목 개수가
/// 늘어나는 것 자체에는 관여하지 않는 구조다.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    public static SettingsPanel Instance { get; private set; }

    [Header("루트 (비워두면 자기 자신 — 절대 SetActive(false)로 끄지 않음)")]
    public GameObject root;
    public Button openButton;
    public Button closeButton;

    [Header("데이터 초기화 — 반드시 확인 화면을 거친 뒤에만 실행된다")]
    public Button resetDataButton;
    public GameObject resetConfirmRoot; // 기본 비활성
    public Button resetConfirmYesButton;
    public Button resetConfirmNoButton;

    private CanvasGroup canvasGroup;

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
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (closeButton != null) closeButton.onClick.AddListener(Close);

        if (resetDataButton != null) resetDataButton.onClick.AddListener(ShowResetConfirm);
        if (resetConfirmYesButton != null) resetConfirmYesButton.onClick.AddListener(ConfirmResetData);
        if (resetConfirmNoButton != null) resetConfirmNoButton.onClick.AddListener(HideResetConfirm);

        SetVisible(false);
        if (resetConfirmRoot != null) resetConfirmRoot.SetActive(false);
    }

    public void Open()
    {
        HideResetConfirm(); // 예전에 확인창을 띄운 채로 닫았다가 다시 열리는 경우를 방지
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

    private void ShowResetConfirm()
    {
        if (resetConfirmRoot != null) resetConfirmRoot.SetActive(true);
    }

    private void HideResetConfirm()
    {
        if (resetConfirmRoot != null) resetConfirmRoot.SetActive(false);
    }

    /// <summary>
    /// 실제 초기화 실행 — 되돌릴 수 없는 동작이므로 확인 화면("정말요?")을 거친 뒤에만 호출된다.
    /// 초기화 직후엔 설정 화면도 함께 닫아서, 플레이어가 바로 리셋된 메인 화면을 보게 한다.
    /// </summary>
    private void ConfirmResetData()
    {
        if (SaveManager.Instance != null)
            SaveManager.Instance.ResetAllData();

        HideResetConfirm();
        Close();
    }
}
