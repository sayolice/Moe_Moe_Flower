/// <summary>
/// 한국어 조사 처리용 공용 유틸리티. 꽃 이름이 데이터(FlowerData.displayName)로 자유롭게
/// 입력되므로, 받침 유무에 따라 "이/가" 조사를 코드에서 자동으로 붙여준다.
/// </summary>
public static class KoreanUtil
{
    private const int HangulBase = 0xAC00;
    private const int HangulLast = 0xD7A3;

    /// <summary> 받침 유무에 따라 "이" 또는 "가" 조사를 붙여 반환한다 (예: "민들레" -> "민들레가", "장미" -> "장미가", "튤립" -> "튤립이"). </summary>
    public static string WithSubjectParticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return word;

        char last = word[word.Length - 1];
        if (last < HangulBase || last > HangulLast) return word + "가"; // 완성형 한글 범위 밖(영문 등)이면 기본값

        bool hasBatchim = (last - HangulBase) % 28 != 0;
        return word + (hasBatchim ? "이" : "가");
    }
}
