using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 화면 전체의 터치를 받아 현재 표시 중인 꽃에게 전달한다.
/// 모바일/PC에서 화면 어디를 클릭해도 현재 꽃이 반응한다.
/// </summary>
public class ScreenTouchController : MonoBehaviour
{
    private void Update()
    {
        // PC 마우스 클릭
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TouchScreen();
        }

        // 모바일 터치
        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            TouchScreen();
        }
    }

    private void TouchScreen()
    {
        if (FlowerManager.Instance == null)
        {
            Debug.LogWarning("[ScreenTouchController] FlowerManager.Instance가 없습니다.");
            return;
        }

        Debug.Log("화면 터치!");
        FlowerManager.Instance.ClickCurrentFlower();
    }
}