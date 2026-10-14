using UnityEngine;

public class PandaController : MonoBehaviour
{
    Animator anim;

    void Start() => anim = GetComponent<Animator>();

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))   // replace with your own condition
            anim.SetTrigger("StandUp");
    }
}