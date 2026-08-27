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

    /// <summary>
    /// BigNumber 전용 오버로드 — double이 감당 못 하는(약 10^308 초과) 값도 여기로 들어온다.
    /// double.Format처럼 1000배씩 나눠가는 방식은 이런 값에서 무한루프/Infinity가 되므로 쓸 수 없고,
    /// 대신 BigNumber가 이미 들고 있는 (가수, 지수)를 3의 배수 단위로 재배치해서 같은 접미사 표를 쓴다.
    /// 접미사 표 범위(Dc, 10^36 미만)를 넘어서면 "1.23e123" 같은 지수 표기로 자연스럽게 전환된다.
    /// </summary>
    public static string Format(BigNumber value)
    {
        if (value.mantissa == 0) return "0";

        // 표 범위 안(대략 10^36 미만)이면 double 경로로 그대로 위임 — 축약 규칙을 하나로 유지한다.
        if (value.exponent < Suffixes.Length * 3L && value.exponent > -Suffixes.Length * 3L)
            return Format(value.ToDouble());

        bool isNegative = value.mantissa < 0;
        string sign = isNegative ? "-" : "";
        double mantissa = isNegative ? -value.mantissa : value.mantissa;
        long exponent = value.exponent;

        int tier = (int)(exponent / 3);
        int remainder = (int)(exponent % 3);
        if (remainder < 0) { remainder += 3; tier -= 1; } // 음수 지수의 나머지를 0~2로 정규화

        // remainder만큼 소수점을 오른쪽으로 옮겨 "그 tier 안에서의 값"(1~999.99...)으로 만든다.
        double scaled = mantissa * System.Math.Pow(10, remainder);

        if (tier >= 0 && tier < Suffixes.Length)
        {
            string body = scaled < 10d ? scaled.ToString("0.##")
                         : scaled < 100d ? scaled.ToString("0.#")
                         : scaled.ToString("0");
            return sign + body + Suffixes[tier];
        }

        // 접미사 표를 완전히 벗어난 천문학적인 값 — 과학적 표기로 대체(방치형 게임 후반 관례).
        return sign + value.mantissa.ToString("0.##") + "e" + exponent;
    }

    /// <summary> Format()에 "G"를 붙인 골드 전용 표기. 예: "1.23M G". </summary>
    public static string FormatGold(double value) => Format(value) + " G";

    /// <summary> BigNumber 버전의 FormatGold. </summary>
    public static string FormatGold(BigNumber value) => Format(value) + " G";

    /// <summary>
    /// Format()과 같은 축약 규칙을 쓰되, 1000 미만도 소수점을 보여준다.
    ///
    /// [왜 필요한가] Format()은 1000 미만을 그냥 정수로 버림한다(누적 골드 총액처럼 "딱 떨어지는
    /// 정수로 보여도 되는" 값에는 맞는 선택). 하지만 G/s나 스탯 강화량 같은 "증가분·비율" 값은
    /// 1 미만인 게 정상이라(예: 레벨 1 자동 애정 0.5, 레벨업 1회당 G/s 증가분 0.15), Format()으로
    /// 찍으면 전부 "0"으로 뭉개져서 강화를 해도 아무것도 안 오르는 것처럼 보인다 — 이 버그 때문에
    /// 새로 만들었다. 1000 이상이면 어차피 소수점 유무가 체감상 중요하지 않으므로 Format()과 동일하게
    /// 축약한다.
    /// </summary>
    public static string FormatPrecise(double value)
    {
        if (double.IsNaN(value)) return "0";

        bool isNegative = value < 0;
        double abs = isNegative ? -value : value;
        string sign = isNegative ? "-" : "";

        if (double.IsPositiveInfinity(abs)) return sign + "매우 큼";
        if (abs >= 1000d) return Format(value);

        string body = abs < 10d ? abs.ToString("0.##")
                     : abs < 100d ? abs.ToString("0.#")
                     : abs.ToString("0");
        return sign + body;
    }

    /// <summary> BigNumber 버전의 FormatPrecise. 1000 이상이면 Format(BigNumber)로 위임한다. </summary>
    public static string FormatPrecise(BigNumber value)
    {
        if (value.exponent >= 3) return Format(value);
        return FormatPrecise(value.ToDouble()); // exponent < 3이면 |value| < 1000이라 double 범위 안에서 항상 안전
    }
}
