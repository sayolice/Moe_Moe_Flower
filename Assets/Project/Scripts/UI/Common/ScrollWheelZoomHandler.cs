using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

/// <summary>
/// 마우스 휠 스크롤을 감지해서 이벤트로 흘려보내는 범용 컴포넌트.
/// ScrollRect가 이미 이 오브젝트 위에서 휠로 자체 패닝(스크롤)을 하고 있을 때, ScrollRect의
/// scrollSensitivity를 0으로 낮춰 자체 반응은 죽이고, 대신 이 컴포넌트로 "휠 = 확대/축소" 같은
/// 다른 동작에 재활용하는 용도(도감 이미지 뷰어의 확대/축소가 첫 사용처).
/// PanelSwitcher/HoldRepeatButton과 동일하게 특정 기능을 모르는 범용 부품으로 만든다 —
/// UnityEvent라 인스펙터/코드 어느 쪽에서 리스너를 붙여도 동작한다.
/// </summary>
public class ScrollWheelZoomHandler : MonoBehaviour, IScrollHandler
{
    /// <summary> 휠 스크롤량(세로, eventData.scrollDelta.y)을 그대로 전달한다. </summary>
    public UnityEvent<float> onScroll = new UnityEvent<float>();

    public void OnScroll(PointerEventData eventData)
    {
        onScroll.Invoke(eventData.scrollDelta.y);
    }
}
