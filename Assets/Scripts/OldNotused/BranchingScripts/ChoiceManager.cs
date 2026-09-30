using System.Collections.Generic;
using UnityEngine;

public class ChoiceManager : MonoBehaviour
{
    public static ChoiceManager Instance;

    private Dictionary<string, string> choices =
        new Dictionary<string, string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // Save a choice
    public void SetChoice(string choiceName, string value)
    {
        choices[choiceName] = value;

        Debug.Log("Choice saved: " + choiceName + " = " + value);
    }

    // Check what the player chose
    public string GetChoice(string choiceName)
    {
        if (choices.ContainsKey(choiceName))
        {
            return choices[choiceName];
        }

        return "";
    }

    // Check if a specific choice was made
    public bool HasChoice(string choiceName, string value)
    {
        return GetChoice(choiceName) == value;
    }

    // Useful for testing
    public void PrintChoices()
    {
        foreach (var choice in choices)
        {
            Debug.Log(choice.Key + " = " + choice.Value);
        }
    }
}