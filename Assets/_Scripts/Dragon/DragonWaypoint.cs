using UnityEngine;

/// <summary>Một điểm trên đường đi của rồng. Speed bằng 0 sẽ dùng tốc độ mặc định từ DragonController.</summary>
public sealed class DragonWaypoint : MonoBehaviour
{
    [Tooltip("0 = dùng Default Move Speed của DragonController.")]
    [SerializeField, Min(-0.1f)] private float moveSpeed;

    public float GetMoveSpeed(float defaultMoveSpeed) => moveSpeed > 0f ? moveSpeed : defaultMoveSpeed;

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position, 0.12f);
    }
}
