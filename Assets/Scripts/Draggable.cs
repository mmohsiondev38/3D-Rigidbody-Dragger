using UnityEngine;

public class Draggable : MonoBehaviour
{
    public Transform draggableTarget;
    [Min(0f)]
    public float snapDistance = 0.5f;

    [HideInInspector]
    public bool snapped;
}
