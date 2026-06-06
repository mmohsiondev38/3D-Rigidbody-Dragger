using UnityEngine;

public class Draggable : MonoBehaviour
{
    public Transform draggableTarget;
    [Min(0f)]
    public float snapDistance = 0.5f;
    [Min(0f)]
    public float snapLerpDuration = 0.5f;
    [Min(0.01f)]
    public float snapScaleMultiplier = 1.08f;
    [Min(0f)]
    public float snapScaleDuration = 0.15f;

    [HideInInspector]
    public bool snapped;

    [HideInInspector]
    public bool snapping;
}
