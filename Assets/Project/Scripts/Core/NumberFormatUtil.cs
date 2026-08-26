/// <summary>
/// 큰 숫자(골드/비용/초당 생산량)를 방치형 게임 관례대로 K/M/B/T... 축약 표기로 바꾸는 공용 유틸리티.
/// 골드가 표시되는 모든 곳(TopBar, 상점, 꽃 레벨업, 플레이어 강화, 오프라인 정산 팝업)이 이 클래스
/// 하나만 쓰도록 통일해서, 화면마다 표기가 달라지는 것을 막는다(TimeFormatUtil과 동일한 원칙).
///
/// [중요: 오버플로우 방어] 계산과 포맷을 전부 double로만 처리하고, 중간에 float이나 int로 절대
/// 캐스팅하지 않는다. 예전 UIManager.FormatGold는 Mathf.FloorToInt((float)value)를 썼는데,
/// float은 정수 정밀도가 약 1,670만까지라 그 이상에서 숫자가 부정확해지고, int는 약 21억을 넘으면
/// 음수로 뒤집힌다. 방치형 게임의 지수 성장 곡선에서는 두 한계 모두 반드시 도달하므로,
/// 표기 축약과 무관하게 double 경로를 유지하는 것 자체가 이 클래스의 핵심이다.
/// </summary>
public static class NumberFormatUtil
{
    // 1000배마다 한 단계씩 올라간다. K(천) M(백만) B(십억) T(조) 이후는 방치형 게임에서 흔히 쓰는
    // 라틴 약어(Qa=Quadrillion, Qi=Quintillion, ...)를 이어 붙인다.
    private static readonly string[] Suffixes =
        { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };

    /// <summary>
    /// 예: 999 -> "999", 1234 -> "1.23K", 12345 -> "12.3K", 123456 -> "123K", 1234567 -> "1.23M".
    /// 1000 미만은 소수점 없이 정수 그대로 보여준다(초반 플레이에서 1G 단위가 중요하므로).
    /// </summary>
    public static string Format(double value)
    {
        if (double.IsNaN(value)) return "0";

        bool isNegative = value < 0;
        double abs = isNegative ? -value : value;
        string sign = isNegative ? "-" : "";

        // 무한대는 숫자로 표기할 수 없다. 폰트에 없는 기호(∞)를 쓰면 네모로 깨지므로 한글로 쓴다.
        if (double.IsPositiveInfinity(abs)) return sign + "매우 큼";

        if (abs < 1000d) return sign + ((long)abs).ToString();

        int tier = 0;
        while (abs >= 1000d && tier < Suffixes.Length - 1)
        {
            abs /= 1000d;
            tier++;
        }

        // 유효숫자 3자리로 맞춘다 (1.23K / 12.3K / 123K).
        string body;
        if (abs < 10d) body = abs.ToString("0.##");
        else if (abs < 100d) body = abs.ToString("0.#");
        else body = abs.ToString("0");

        return sign + body + Suffixes[tier];
    }

    /// <summary> Format()에 "G"를 붙인 골드 전용 표기. 예: "1.23M G". </summary>
    public static string FormatGold(double value) => Format(value) + " G";
}
