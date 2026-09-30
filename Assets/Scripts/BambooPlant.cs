using UnityEngine;

public class BambooPlant : MonoBehaviour
{
    private void OnEnable()
    {
        BambooManager.RegisterBamboo(this);
    }

    private void OnDisable()
    {
        BambooManager.UnregisterBamboo(this);
    }
}