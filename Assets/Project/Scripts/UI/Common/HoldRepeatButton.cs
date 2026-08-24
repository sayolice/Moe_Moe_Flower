using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 누르는 즉시 1회 발생 + 일정 시간 이상 누르고 있으면 반복 발생하는 범용 입력 컴포넌트.
/// 같은 GameObject의 Button.interactable을 매 프레임 확인해서, 버튼이 비활성화되면(예: 골드 부족)
/// 자동으로 반복을 멈춘다. Flower/Player 등 특정 기능을 모르므로 다른 반복 강화 버튼에도 재사용 가능.
/// </summary>
[RequireComponent(typeof(Button))]
public class HoldRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("반복 타이밍 (초)")]
    public float initialDelay = 0.45f;
    public float repeatInterval = 0.12f;

    /// <summary>
    /// false면 눌러도 최초 1회만 발생하고 반복하지 않는다 (MAX 모드처럼 "한 번만 실행" 이어야 할 때 사용).
    /// 외부(FlowerUpgradeItem 등)에서 매 프레임 선택된 모드에 맞춰 켜고 끈다.
    /// </summary>
    public bool allowRepeat = true;

    /// <summary> 클릭 1회 또는 반복 1틱마다 발생. </summary>
    public event Action OnTrigger;

    private Button button;
    private bool isPressed;
    private bool isRepeating;
    private float timer;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void Update()
    {
        if (!isPressed) return;

        if (button != null && !button.interactable)
        {
            // 골드 부족 등으로 버튼이 꺼지면 즉시 반복 중단
            isPressed = false;
            return;
        }

        if (!allowRepeat) return; // MAX 모드 등: 최초 1회 트리거 이후 반복 없음

        timer += Time.deltaTime;

        if (!isRepeating)
        {
            if (timer >= initialDelay)
            {
                isRepeating = true;
                timer = 0f;
                Fire();
            }
        }
        else if (timer >= repeatInterval)
        {
            timer = 0f;
            Fire();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;

        isPressed = true;
        isRepeating = false;
        timer = 0f;
        Fire();
    }

    public void OnPointerUp(PointerEventData eventData) => isPressed = false;
    public void OnPointerExit(PointerEventData eventData) => isPressed = false;

    private void Fire() => OnTrigger?.Invoke();
}
