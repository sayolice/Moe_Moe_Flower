using UnityEngine;
using UnityEngine.UI;

public sealed class MiniArenaShapeCharacterView : MiniArenaCharacterView
{
    private Image body;
    private Image facingMark;
    private Color baseColor = Color.white;

    public void Initialize(Transform parent, Color color, float size)
    {
        transform.SetParent(parent, false);
        RectTransform rect = RectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.one * size;

        body = CreatePart(transform, "Body", color, new Vector2(size, size), Vector2.zero);
        facingMark = CreatePart(body.transform, "Facing", Color.white, new Vector2(size * 0.25f, size * 0.55f), new Vector2(size * 0.35f, 0f));
        baseColor = color;
    }

    public override void SetAppearance(Color color, float size)
    {
        baseColor = color;
        if (body != null) body.color = color;
        if (body != null) body.rectTransform.sizeDelta = Vector2.one * size;
        if (facingMark != null)
        {
            facingMark.rectTransform.sizeDelta = new Vector2(size * 0.25f, size * 0.55f);
            facingMark.rectTransform.anchoredPosition = new Vector2(size * 0.35f, 0f);
        }
    }

    public override void SetFacing(Vector2 direction)
    {
        if (direction.sqrMagnitude < 0.001f || body == null) return;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);
    }

    public override void SetMoving(bool moving)
    {
        if (body == null) return;
        float pulse = moving ? 1f + Mathf.Sin(Time.unscaledTime * 18f) * 0.08f : 1f;
        transform.localScale = Vector3.one * pulse;
    }

    public override void SetHitFlash(float amount)
    {
        if (body != null) body.color = Color.Lerp(baseColor, Color.white, Mathf.Clamp01(amount));
    }

    private static Image CreatePart(Transform parent, string name, Color color, Vector2 size, Vector2 position)
    {
        GameObject part = new GameObject(name, typeof(RectTransform), typeof(Image));
        part.transform.SetParent(parent, false);
        RectTransform rect = part.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        Image image = part.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
