using System.Collections.Generic;
using UnityEngine;

public class GameEventManager : MonoBehaviour
{
    public static GameEventManager Instance;

    [Header("Events available at the start")]
    [SerializeField] private GameEvent[] initiallyUnlockedEvents;

    private HashSet<GameEvent> unlockedEvents =
        new HashSet<GameEvent>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        foreach (GameEvent gameEvent in initiallyUnlockedEvents)
        {
            if (gameEvent != null)
            {
                unlockedEvents.Add(gameEvent);
            }
        }
    }

    public void Unlock(GameEvent gameEvent)
    {
        if (gameEvent == null)
            return;

        unlockedEvents.Add(gameEvent);

        Debug.Log("UNLOCKED: " + gameEvent.name);
    }

    public void Lock(GameEvent gameEvent)
    {
        if (gameEvent == null)
            return;

        unlockedEvents.Remove(gameEvent);

        Debug.Log("LOCKED: " + gameEvent.name);
    }

    public bool IsUnlocked(GameEvent gameEvent)
    {
        if (gameEvent == null)
            return false;

        return unlockedEvents.Contains(gameEvent);
    }
}