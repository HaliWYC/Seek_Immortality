using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class QingxiTrailHud : MonoBehaviour
{
    [SerializeField] Text location;
    [SerializeField] Text controls;
    [SerializeField] GameObject discovery;
    QingxiWalker walker;
    const string MovementHint = "WASD / 方向键  行走       J  学徒手记       T  就地试阵       R  ";

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
        if (controls) controls.text = MovementHint + (DemoRun.Active ? "调息" : "从镇里重走");
        discovery.SetActive(walker.TrailDiscovered);
    }
}
