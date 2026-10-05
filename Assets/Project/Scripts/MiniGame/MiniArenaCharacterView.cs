using UnityEngine;

public abstract class MiniArenaCharacterView : MonoBehaviour
{
    public RectTransform RectTransform => (RectTransform)transform;

    public abstract void SetAppearance(Color color, float size);
    public abstract void SetFacing(Vector2 direction);
    public abstract void SetMoving(bool moving);
    public abstract void SetHitFlash(float amount);
}
