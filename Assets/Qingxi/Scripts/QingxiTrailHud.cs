using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class QingxiTrailHud : MonoBehaviour
{
    [SerializeField] Text location;
    [SerializeField] GameObject discovery;
    QingxiWalker walker;

    void OnEnable()
    {
        walker = FindAnyObjectByType<QingxiWalker>();
        if (walker == null) return;
        walker.StateChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (walker != null) walker.StateChanged -= Refresh;
    }

    void Refresh()
    {
        location.text = walker.CurrentPlace;
        discovery.SetActive(walker.TrailDiscovered);
    }
}
