using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메모리얼 목록의 행 하나. 잠겨 있으면 제목까지 가린 잠금 상태로 보여준다 — 제목이 보이면
/// 내용이 유추되어 해금의 가치가 떨어지므로(요청 명세 5.3) 절대 제목을 새어나가게 하지 않는다.
/// 해금된 항목만 클릭 가능하고, 클릭하면 MemorialViewPanel이 본문을 보여주며 동시에 읽음 처리한다.
/// </summary>
public class MemorialEntryItem : MonoBehaviour
{
    public TMP_Text titleText;
    public TMP_Text newBadgeText; // "NEW" — 해금됐지만 아직 안 읽었을 때만 활성화
    public Button selectButton;

    private int bondLevel;
    private string body;
    private MemorialViewPanel panel;

    public void Setup(MemorialViewPanel owner, int level, string title, string bodyText, bool unlocked, bool unread)
    {
        panel = owner;
        bondLevel = level;
        body = bodyText;

        if (titleText != null)
            titleText.text = unlocked
                ? $"Lv.{level}   {(string.IsNullOrEmpty(title) ? "(제목 없음)" : title)}"
                : $"Lv.{level}   잠김";

        if (newBadgeText != null)
            newBadgeText.gameObject.SetActive(unlocked && unread);

        if (selectButton != null)
        {
            selectButton.interactable = unlocked;
            selectButton.onClick.RemoveAllListeners();
            if (unlocked)
                selectButton.onClick.AddListener(() => panel.OpenEntryDetail(bondLevel, title, body));
        }
    }
}
