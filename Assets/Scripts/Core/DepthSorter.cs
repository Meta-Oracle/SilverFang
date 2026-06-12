using UnityEngine;

namespace SilverFang.Core
{
    /// Belt-scroller depth: lower on screen renders in front.
    [RequireComponent(typeof(SpriteRenderer))]
    public class DepthSorter : MonoBehaviour
    {
        [SerializeField] private int precision = 100;
        [SerializeField] private bool staticObject;

        private SpriteRenderer sr;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            UpdateOrder();
            if (staticObject) enabled = false;
        }

        private void LateUpdate() => UpdateOrder();

        private void UpdateOrder()
        {
            sr.sortingOrder = -Mathf.RoundToInt(transform.position.y * precision);
        }
    }
}
