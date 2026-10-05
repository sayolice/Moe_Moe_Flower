using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 화면 전체의 입력을 받아 탭(현재 꽃 터치)과 좌우 스와이프(이전/다음 보유 꽃 전환)를 구분한다.
/// 누르는 즉시 반응하지 않고, 뗄 때까지의 총 이동 거리를 봐서 임계값 미만이면 탭,
/// 이상이면 스와이프로 판정한다 — 같은 입력을 두 갈래로 나누는 지점을 하나로 통일해서
/// "탭인 줄 알고 애정/골드를 줬는데 사실 스와이프였다" 같은 충돌이 애초에 발생하지 않게 한다.
///
/// [클릭 판정 전담] 예전에는 FlowerDisplayController.OnMouseDown()도 별도로
/// FlowerManager.ClickCurrentFlower()를 직접 호출하고 있어서, 꽃을 한 번 탭할 때마다
/// 애정/골드가 이 클래스와 그쪽 양쪽에서 각각 처리되어 2배로 들어갔다. 지금은 클릭(탭) 판정을
/// 이 클래스 하나가 전담하고, FlowerDisplayController는 확대 중 드래그 패닝만 처리한다.
///
/// [UI 클릭 관통 방지] 누르기 시작한 지점이 UI(버튼 등) 위였다면, 그 제스처는 전부 무시한다
/// (뗄 때 다시 검사하지 않는 이유: 버튼 위에서 눌렀다가 화면 밖으로 나가서 떼는 경우처럼
/// "시작은 UI, 끝은 UI 아님"인 상황에서 뗄 때만 검사하면 오히려 새로한 탭이 잘못 발동할 수 있다.
/// 시작 시점 하나만 기준으로 삼는 것이 가장 단순하고 안전하다).
///
/// [확대 중 드래그] 꽃이 확대되어 있으면 큰 이동은 "다음/이전 꽃으로 스와이프"가 아니라
/// FlowerDisplayController가 처리하는 "화면 안에서 이동(패닝)"으로 취급해야 하므로,
/// 확대 중에는 스와이프 판정을 하지 않는다(탭 판정은 그대로 유지 — 확대 중에도 살짝 누르기만
/// 하면 터치 애정/골드는 정상적으로 들어가야 한다).
///
/// 마우스/터치 중 그 프레임에 눌린 쪽 하나만 처리한다(동시에 둘 다 감지되는 플랫폼에서
/// 같은 입력이 두 번 처리되는 것을 방지).
/// </summary>
public class ScreenTouchController : MonoBehaviour
{
    [Header("스와이프 판정 (픽셀)")]
    [Tooltip("뗄 때 이 거리 미만이면 탭, 이상이면 스와이프로 처리한다.")]
    [SerializeField] private float swipeThreshold = 80f;

    private bool isPressing;
    private Vector2 pressStartPos;

    /// <summary> 누르기 시작한 지점이 UI 위였는지 — 그렇다면 뗄 때까지 이 제스처는 통째로 무시한다. </summary>
    private bool gestureStartedOverUI;

    private void Update()
    {
        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.leftButton.wasReleasedThisFrame))
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            HandlePointer(Mouse.current.leftButton.wasPressedThisFrame,
                          Mouse.current.leftButton.wasReleasedThisFrame,
                          Mouse.current.position.ReadValue(),
                          overUI);
        }
        else if (Touchscreen.current != null &&
                 (Touchscreen.current.primaryTouch.press.wasPressedThisFrame ||
                  Touchscreen.current.primaryTouch.press.wasReleasedThisFrame))
        {
            int touchId = Touchscreen.current.primaryTouch.touchId.ReadValue();
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touchId);
            HandlePointer(Touchscreen.current.primaryTouch.press.wasPressedThisFrame,
                          Touchscreen.current.primaryTouch.press.wasReleasedThisFrame,
                          Touchscreen.current.primaryTouch.position.ReadValue(),
                          overUI);
        }

        HandleArrowKeyNavigation();
    }

    /// <summary>
    /// 방향키(←/→)로도 스와이프와 동일하게 이전/다음 보유 꽃으로 전환한다. 확대(패닝) 중이어도
    /// 스와이프처럼 손동작을 가로챌 일이 없으므로(제스처가 아니라 딱 한 번의 키 입력) 확대 여부와
    /// 무관하게 항상 동작한다. 단, 텍스트 입력 칸(도감의 확대율 직접 입력 등)에 커서가 가 있을 때
    /// 방향키를 누르면 그건 "커서 이동"이 목적이지 "꽃 전환"이 아니므로 그럴 때는 무시한다.
    /// </summary>
    private void HandleArrowKeyNavigation()
    {
        if (Keyboard.current == null || FlowerManager.Instance == null) return;
        if (IsTextInputFieldFocused()) return;

        if (Keyboard.current.leftArrowKey.wasPressedThisFrame) FlowerManager.Instance.SwipePrevious();
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame) FlowerManager.Instance.SwipeNext();
    }

    private static bool IsTextInputFieldFocused()
    {
        if (EventSystem.current == null) return false;
        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null) return false;
        return selected.GetComponent<TMP_InputField>() != null;
    }

    private void HandlePointer(bool pressedThisFrame, bool releasedThisFrame, Vector2 currentPos, bool overUI)
    {
        if (pressedThisFrame)
        {
            isPressing = true;
            pressStartPos = currentPos;
            gestureStartedOverUI = overUI;
            return;
        }

        if (releasedThisFrame && isPressing)
        {
            isPressing = false;
            if (gestureStartedOverUI) return; // UI 버튼 등에서 시작된 클릭 — 여기서는 아무 것도 하지 않는다.

            Vector2 delta = currentPos - pressStartPos;
            bool zoomedIn = FlowerDisplayController.Instance != null && FlowerDisplayController.Instance.IsZoomedIn;

            if (delta.magnitude < swipeThreshold)
            {
                TouchCurrentFlower(currentPos);
            }
            else if (zoomedIn)
            {
                // 확대 중의 큰 이동은 스와이프 의도가 아니라 FlowerDisplayController가 이미 처리한
                // 패닝 드래그다 — 여기서 다음/이전 꽃으로 넘겨버리면 패닝하려던 사용자가 놀란다.
            }
            else if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
            {
                // 왼쪽으로 드래그(다음 콘텐츠를 왼쪽에서 끌어옴) → 다음 꽃, 오른쪽 → 이전 꽃
                if (delta.x < 0) SwipeToNext();
                else SwipeToPrevious();
            }
            // 세로 방향이 더 크면 스크롤 등으로 보고 무시한다.
        }
    }

    private void TouchCurrentFlower(Vector2 screenPos)
    {
        if (FlowerManager.Instance == null)
        {
            Debug.LogWarning("[ScreenTouchController] FlowerManager.Instance가 없습니다.");
            return;
        }

        FlowerInstance instance = FlowerManager.Instance.GetCurrentInstance();
        bool isBloomed = instance != null && instance.isBloomed;

        FlowerManager.Instance.ClickCurrentFlower();

        // 1. 사운드 재생 (연속 터치 시 피치 상승 연출)
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayTapSound(isBloomed);
        }

        // 2. 터치 위치 VFX 피드백 (플로팅 텍스트 & 반짝임 파티클)
        if (TouchFeedbackManager.Instance != null)
        {
            if (!isBloomed)
            {
                double touchAffection = PlayerStatManager.Instance != null
                    ? PlayerStatManager.Instance.GetCurrentValue(PlayerStatType.TouchAffection).ToDouble()
                    : 1.0;
                string text = $"+{NumberFormatUtil.Format(touchAffection)}";
                TouchFeedbackManager.Instance.SpawnTapFeedback(screenPos, text, new Color(1f, 0.45f, 0.65f, 1f));
            }
            else
            {
                TouchFeedbackManager.Instance.SpawnTapFeedback(screenPos, "+1 유대", new Color(0.65f, 0.85f, 1f, 1f));
            }
        }

        // 3. 꽃소녀 터치 상호작용 (바운스 애니메이션 + 말풍선 대사 + 하트)
        if (FlowerDisplayController.Instance != null)
        {
            FlowerDisplayController.Instance.TriggerTouchInteraction();
        }
    }

    private void SwipeToNext()
    {
        if (FlowerManager.Instance == null) return;
        FlowerManager.Instance.SwipeNext();
    }

    private void SwipeToPrevious()
    {
        if (FlowerManager.Instance == null) return;
        FlowerManager.Instance.SwipePrevious();
    }
}
