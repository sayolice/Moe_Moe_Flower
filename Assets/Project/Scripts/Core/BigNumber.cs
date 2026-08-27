using System;

/// <summary>
/// 자릿수 상한이 사실상 없는 큰 수(가수 × 10^지수). 유니티 double은 약 10^308에서 한계에
/// 부딪히는데, 이 게임은 레벨을 무한히 올릴 수 있고 성장 곡선이 레벨마다 배율을 곱하는 지수
/// 함수라 결국 이 상한에도 도달한다 — 이번 세션에서 int → float → double로 세 번 겪은 "언젠가
/// 다시 터지는" 패턴을 여기서 근본적으로 끝낸다.
///
/// [적용 범위 — 일부러 전체를 바꾸지 않았다]
/// "레벨에 지수적으로 비례해서 무한히 커질 수 있는" 값만 BigNumber를 쓴다: 누적 골드
/// (GameManager.totalGold), G/s·레벨업 비용·스탯 값처럼 레벨을 인자로 받는 계산 결과.
/// 반대로 "사람이 인스펙터에 직접 타이핑하는 Lv.1 기준값"(baseGoldPerSecond, seedPrice,
/// levelUpBaseCost, baseValue, upgradeBaseCost)과 "설계상 상한이 있는 값"(requiredAffection,
/// currentAffection — 개화하면 더 안 자람, bond — Lv.5에서 멈춤)은 double 그대로 둔다. 디자이너가
/// 손으로 10^300을 입력할 일은 없고, 상한이 있는 값은 애초에 double 범위를 넘을 수 없으므로,
/// 그 필드들까지 (가수, 지수) 두 칸짜리 인스펙터 UX로 바꿀 이유가 없다.
///
/// [계산 원리] a^b를 직접 계산하면 결과가 double 상한을 넘는 순간 그대로 Infinity가 되어버려서
/// 아무 의미가 없다. 그래서 로그(log10)로 변환해서 "지수부가 몇인지"부터 구하고, 그 소수부만
/// 10의 거듭제곱으로 되돌려 가수를 얻는다 — PowDouble이 이 방식을 쓴다.
/// </summary>
[Serializable]
public struct BigNumber : IComparable<BigNumber>, IEquatable<BigNumber>
{
    /// <summary> 값이 0이 아니면 항상 [1, 10) 범위(부호는 별도) — Normalize가 보장한다. </summary>
    public double mantissa;
    public long exponent;

    public static readonly BigNumber Zero = new BigNumber(0, 0);

    public BigNumber(double mantissa, long exponent)
    {
        this.mantissa = mantissa;
        this.exponent = exponent;
        Normalize(ref this.mantissa, ref this.exponent);
    }

    public static BigNumber FromDouble(double value)
    {
        if (value == 0 || double.IsNaN(value)) return Zero;

        bool negative = value < 0;
        double abs = negative ? -value : value;

        if (double.IsPositiveInfinity(abs)) return negative ? MinValue : MaxValue;

        long exp = (long)Math.Floor(Math.Log10(abs));
        double mant = abs / Math.Pow(10, exp);

        // log10/Pow 왕복 과정의 부동소수점 오차로 mant가 [1,10) 경계를 살짝 벗어나는 경우 보정.
        if (mant >= 10.0) { mant /= 10.0; exp += 1; }
        else if (mant < 1.0) { mant *= 10.0; exp -= 1; }

        return new BigNumber(negative ? -mant : mant, exp);
    }

    /// <summary> long.MaxValue 자릿수만큼의 지수를 쓰는, 사실상 무한대에 준하는 값(오버플로우 방어용 상한). </summary>
    public static readonly BigNumber MaxValue = new BigNumber(9.999999, long.MaxValue / 2);
    public static readonly BigNumber MinValue = new BigNumber(-9.999999, long.MaxValue / 2);

    private static void Normalize(ref double mantissa, ref long exponent)
    {
        if (mantissa == 0) { exponent = 0; return; }

        bool negative = mantissa < 0;
        double abs = negative ? -mantissa : mantissa;

        // 극단적인 반복을 막기 위한 안전장치 — 정상적인 입력이라면 이 루프는 몇 번 안에 끝난다.
        int safety = 0;
        while (abs >= 10.0 && safety++ < 1000) { abs /= 10.0; exponent++; }
        while (abs < 1.0 && abs > 0 && safety++ < 1000) { abs *= 10.0; exponent--; }

        mantissa = negative ? -abs : abs;
    }

    /// <summary>
    /// baseValue^exponentValue. baseValue는 일반 double(성장률, 예: 1.15)이고 exponentValue도
    /// 일반 double(레벨)이다 — 이 둘 자체는 작은 수이지만, 결과가 커야 할 수 있다는 게 핵심.
    /// Math.Pow를 직접 쓰지 않고 로그로 변환하는 이유는 클래스 주석 참고.
    /// </summary>
    public static BigNumber PowDouble(double baseValue, double exponentValue)
    {
        if (baseValue <= 0) return Zero;
        double log10Result = exponentValue * Math.Log10(baseValue);
        return FromLog10(log10Result);
    }

    private static BigNumber FromLog10(double log10Value)
    {
        if (double.IsNaN(log10Value)) return Zero;
        if (double.IsPositiveInfinity(log10Value)) return MaxValue;
        if (double.IsNegativeInfinity(log10Value)) return Zero;

        long exp = (long)Math.Floor(log10Value);
        double mant = Math.Pow(10, log10Value - exp);
        return new BigNumber(mant, exp);
    }

    /// <summary>
    /// double로 좁힌다 — 표시/직렬화 등 "어차피 유한한 값이라고 알려진" 경우에만 쓴다.
    /// 지수가 double 표현 범위(약 308)를 넘으면 Infinity/0을 반환한다(예외를 던지지 않음 —
    /// currentAffection처럼 "상한을 넘긴 것 자체가 유의미한 신호"인 곳에서 안전하게 쓰기 위함).
    /// </summary>
    public double ToDouble()
    {
        if (mantissa == 0) return 0;
        if (exponent > 308) return mantissa > 0 ? double.PositiveInfinity : double.NegativeInfinity;
        if (exponent < -308) return 0;
        return mantissa * Math.Pow(10, exponent);
    }

    public static BigNumber operator +(BigNumber a, BigNumber b)
    {
        if (a.mantissa == 0) return b;
        if (b.mantissa == 0) return a;

        // 지수 차이가 double 유효자릿수(약 15~17)를 넘으면 작은 쪽은 결과에 전혀 영향을 못 준다.
        long diff = a.exponent - b.exponent;
        if (diff > 17) return a;
        if (diff < -17) return b;

        if (diff >= 0)
            return new BigNumber(a.mantissa + b.mantissa / Math.Pow(10, diff), a.exponent);
        else
            return new BigNumber(a.mantissa / Math.Pow(10, -diff) + b.mantissa, b.exponent);
    }

    public static BigNumber operator -(BigNumber a) => new BigNumber(-a.mantissa, a.exponent);
    public static BigNumber operator -(BigNumber a, BigNumber b) => a + (-b);

    public static BigNumber operator *(BigNumber a, BigNumber b)
    {
        if (a.mantissa == 0 || b.mantissa == 0) return Zero;
        return new BigNumber(a.mantissa * b.mantissa, a.exponent + b.exponent);
    }

    public static BigNumber operator /(BigNumber a, BigNumber b)
    {
        if (a.mantissa == 0) return Zero;
        if (b.mantissa == 0) return a.mantissa > 0 ? MaxValue : MinValue; // 0 나누기 방어(정상 흐름에서 발생하면 안 됨)
        return new BigNumber(a.mantissa / b.mantissa, a.exponent - b.exponent);
    }

    // ── double과의 상호운용 — 디자이너 손입력 값(설계 상수)은 double로 남겨뒀으므로 자주 필요하다. ──
    public static implicit operator BigNumber(double value) => FromDouble(value);
    public static BigNumber operator *(BigNumber a, double b) => a * FromDouble(b);
    public static BigNumber operator *(double a, BigNumber b) => FromDouble(a) * b;
    public static BigNumber operator /(BigNumber a, double b) => a / FromDouble(b);
    public static BigNumber operator +(BigNumber a, double b) => a + FromDouble(b);
    public static BigNumber operator -(BigNumber a, double b) => a - FromDouble(b);

    public int CompareTo(BigNumber other)
    {
        if (mantissa == 0 && other.mantissa == 0) return 0;
        if (mantissa == 0) return other.mantissa > 0 ? -1 : 1;
        if (other.mantissa == 0) return mantissa > 0 ? 1 : -1;

        bool aNeg = mantissa < 0, bNeg = other.mantissa < 0;
        if (aNeg != bNeg) return aNeg ? -1 : 1;

        int magnitudeCompare = exponent != other.exponent
            ? exponent.CompareTo(other.exponent)
            : mantissa.CompareTo(other.mantissa);

        return aNeg ? -magnitudeCompare : magnitudeCompare;
    }

    public static bool operator >(BigNumber a, BigNumber b) => a.CompareTo(b) > 0;
    public static bool operator <(BigNumber a, BigNumber b) => a.CompareTo(b) < 0;
    public static bool operator >=(BigNumber a, BigNumber b) => a.CompareTo(b) >= 0;
    public static bool operator <=(BigNumber a, BigNumber b) => a.CompareTo(b) <= 0;
    public static bool operator ==(BigNumber a, BigNumber b) => a.CompareTo(b) == 0;
    public static bool operator !=(BigNumber a, BigNumber b) => a.CompareTo(b) != 0;

    public static BigNumber Max(BigNumber a, BigNumber b) => a >= b ? a : b;
    public static BigNumber Min(BigNumber a, BigNumber b) => a <= b ? a : b;

    public bool Equals(BigNumber other) => CompareTo(other) == 0;
    public override bool Equals(object obj) => obj is BigNumber other && Equals(other);
    public override int GetHashCode() => mantissa.GetHashCode() ^ exponent.GetHashCode();

    /// <summary> 표시 전용 — NumberFormatUtil.Format(BigNumber)를 그대로 위임한다. </summary>
    public override string ToString() => NumberFormatUtil.Format(this);
}
