/// <summary>
/// 초 단위 시간을 사람이 읽기 쉬운 한국어 문자열로 바꾸는 공용 유틸리티.
/// 방치 보상 팝업(경과 시간)과 개화까지 남은 시간(ETA) 양쪽에서 재사용해서, 시간 표시 형식이
/// 여러 UI에 흩어져 각자 미묘하게 달라지는 것을 막는다.
///
/// [방어적 설계] 어떤 입력값이 들어와도(NaN, Infinity, 음수, 자동 애정이 거의 0이라 나눗셈 결과가
/// 천문학적으로 커진 경우 등) 절대 예외를 던지거나 오버플로우로 음수/쓰레기 숫자를 보여주지 않는다.
/// 예전에는 Mathf.FloorToInt/CeilToInt로 double을 int(32비트, 약 21억 한계)로 변환해서
/// "속도가 0에 아주 가까울 때 남은시간이 끝없이 커지다가 오버플로우"하는 문제가 있었다 —
/// 지금은 long(64비트) 연산 + 상한 클램프로 원천 차단한다.
/// </summary>
public static class TimeFormatUtil
{
    // 9999일을 넘어가면 어차피 플레이어에게는 "사실상 무한"과 다를 게 없으므로,
    // 그 이상은 전부 상한값으로 잘라서 표시한다(오버플로우 방지용 안전망).
    private const long MaxSafeSeconds = 9999L * 86400L;

    /// <summary>
    /// 개화 예상 시간처럼 "남은량 / 속도"로 나눗셈하는 곳에서 쓰는 판정 기준값(초당 0.01).
    /// 속도가 이 값보다 크면(터치 중이거나 자동 애정이 붙어있으면) 정상적으로 나눠서 실제 예상
    /// 시간을 보여주고, 이 값 이하면(사실상 아무 진행도 없는 상태) 나눗셈 자체를 하지 않고
    /// "예측 불가" 같은 문구로 명시한다. 굳이 나눠서 큰 숫자를 억지로 만들지 않는 이유: 정말 아무
    /// 진행이 없는데도 "27시간 뒤 개화"처럼 그럴듯한 숫자를 보여주면 "가만히 둬도 언젠가 될 것"이라는
    /// 잘못된 인상을 준다. 반대로 이 값보다 살짝이라도 크면(터치 한 번만 해도) 즉시 실제 숫자로
    /// 전환되므로, "터치하면 반영돼야 한다"는 요구와도 충돌하지 않는다.
    /// </summary>
    public const float MinDisplayRatePerSecond = 0.01f;

    /// <summary>
    /// 일/시간/분/초 중 상황에 맞는 두 단위로 표시한다.
    /// 예: "2일 5시간", "3시간 20분", "12분 30초", "50초".
    /// </summary>
    public static string Format(double totalSeconds)
    {
        long seconds = ClampToSafeSeconds(totalSeconds);

        long days = seconds / 86400;
        long hours = (seconds % 86400) / 3600;
        long minutes = (seconds % 3600) / 60;
        long secs = seconds % 60;

        if (days > 0) return hours > 0 ? $"{days}일 {hours}시간" : $"{days}일";
        if (hours > 0) return minutes > 0 ? $"{hours}시간 {minutes}분" : $"{hours}시간";
        if (minutes > 0) return secs > 0 ? $"{minutes}분 {secs}초" : $"{minutes}분";
        return $"{secs}초";
    }

    private static long ClampToSafeSeconds(double totalSeconds)
    {
        if (double.IsNaN(totalSeconds) || totalSeconds <= 0) return 0L;
        if (double.IsPositiveInfinity(totalSeconds) || totalSeconds >= MaxSafeSeconds) return MaxSafeSeconds;
        return (long)totalSeconds;
    }
}
