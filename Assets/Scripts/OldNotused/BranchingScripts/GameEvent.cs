using UnityEngine;
using System;

[CreateAssetMenu(
    fileName = "New Game Event",
    menuName = "Game/Game Event"
)]
public class GameEvent : ScriptableObject
{
    public event Action OnEventRaised;

    public void Raise()
    {
        Debug.Log("EVENT RAISED: " + name);
        OnEventRaised?.Invoke();
    }
}