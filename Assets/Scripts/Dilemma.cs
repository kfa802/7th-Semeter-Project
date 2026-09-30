using UnityEngine;

[CreateAssetMenu(fileName = "New Dilemma", menuName = "Panda/Dilemma")]
public class Dilemma : ScriptableObject
{
    public string title;

    [TextArea]
    public string description;

    public string choiceA;
    public string choiceB;

    public float choiceA_Disturbance;
    public float choiceA_Temperature;

    public float choiceB_Disturbance;
    public float choiceB_Temperature;
}