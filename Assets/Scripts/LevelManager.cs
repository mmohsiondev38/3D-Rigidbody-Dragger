using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
    public List<Draggable> draggables = new();

    bool levelCompleted;
    public System.Action onLevelWin;
    public System.Action onLevelFail;
    void Update()
    {
        CheckLevelComplete();
    }

    public void CheckLevelComplete()
    {
        if (levelCompleted) return;
        if (draggables == null || draggables.Count == 0) return;

        for (int i = 0; i < draggables.Count; i++)
        {
            Draggable draggable = draggables[i];
            if (draggable == null || !draggable.snapped)
            {
                return;
            }
        }

        LevelComplete();
    }

    public void LevelComplete()
    {
        levelCompleted = true;
        onLevelWin?.Invoke();
    }
}
