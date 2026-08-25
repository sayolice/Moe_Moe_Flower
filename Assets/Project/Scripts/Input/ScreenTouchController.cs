using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 화면 전체의 입력을 받아 탭(현재 꽃 터치)과 좌우 스와이프(이전/다음 보유 꽃 전환)를 구분한다.
/// 누르는 즉시 반응하지 않고, 뗄 때까지의 총 이동 거리를 봐서 임계값 미만이면 탭,
/// 이상이면 스와이프로 판정한다 — 같은 입력을 두 갈래로 나누는 지점을 하나로 통일해서
/// "탭인 줄 알고 애정/골드를 줬는데 사실 스와이프였다" 같은 충돌이 애초에 발생하지 않게 한다.
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

    private void Update()
    {
        if (Mouse.current != null &&
            (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.leftButton.wasReleasedThisFrame))
        {
            HandlePointer(Mouse.current.leftButton.wasPressedThisFrame,
                          Mouse.current.leftButton.wasReleasedThisFrame,
                          Mouse.current.position.ReadValue());
        }
        else if (Touchscreen.current != null &&
                 (Touchscreen.current.primaryTouch.press.wasPressedThisFrame ||
                  Touchscreen.current.primaryTouch.press.wasReleasedThisFrame))
        {
            HandlePointer(Touchscreen.current.primaryTouch.press.wasPressedThisFrame,
                          Touchscreen.current.primaryTouch.press.wasReleasedThisFrame,
                          Touchscreen.current.primaryTouch.position.ReadValue());
        }
    }

    private void HandlePointer(bool pressedThisFrame, bool releasedThisFrame, Vector2 currentPos)
    {
        if (pressedThisFrame)
        {
            isPressing = true;
            pressStartPos = currentPos;
            return;
        }

        if (releasedThisFrame && isPressing)
        {
            isPressing = false;
            Vector2 delta = currentPos - pressStartPos;

            if (delta.magnitude < swipeThreshold)
            {
                TouchCurrentFlower();
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

    private void TouchCurrentFlower()
    {
        if (FlowerManager.Instance == null)
        {
            Debug.LogWarning("[ScreenTouchController] FlowerManager.Instance가 없습니다.");
            return;
        }

        FlowerManager.Instance.ClickCurrentFlower();
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
