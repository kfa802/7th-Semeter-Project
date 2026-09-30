using UnityEngine;

public class DilemmaManager : MonoBehaviour
{
    [SerializeField] private EnvironmentSystem environment;

    public void ChooseA(Dilemma dilemma)
    {
        environment.ChangeTemperature(
            dilemma.choiceA_Temperature
        );
    }

    public void ChooseB(Dilemma dilemma)
    {
        environment.ChangeTemperature(
            dilemma.choiceB_Temperature
        );
    }
}