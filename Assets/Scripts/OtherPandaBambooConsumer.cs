
using UnityEngine;

public class OtherPandaBambooConsumer : MonoBehaviour
{
    [Header("Midway Zone")]
    [SerializeField] private EcosystemZone midwayZone;

    [Header("Eating Settings")]
    [Min(0.5f)]
    [SerializeField] private float secondsBetweenMeals = 5f;

    [Min(1)]
    [SerializeField] private int bambooPerMeal = 2;

    [Header("Bamboo Regrowth")]
    [Min(1)]
    [SerializeField] private int mealsBeforePooping = 3;

    private float mealTimer;
    private int successfulMeals;

    private void Update()
    {
        if (midwayZone == null ||
            EnvironmentSystem.Instance == null)
            return;

        // Only act while the player's panda is in the midway zone.
        if (EnvironmentSystem.Instance.ActiveZone != midwayZone)
        {
            mealTimer = 0f;
            return;
        }

        mealTimer += Time.deltaTime;

        if (mealTimer < secondsBetweenMeals)
            return;

        mealTimer = 0f;

        EatBamboo();
    }

    private void EatBamboo()
    {
        if (BambooManager.Instance == null)
            return;

        int eaten = 0;

        for (int i = 0; i < bambooPerMeal; i++)
        {
            if (!BambooManager.Instance.ConsumeBambooInZone(midwayZone))
                break;

            eaten++;
        }

        if (eaten == 0)
            return;

        Debug.Log("Other panda ate " + eaten + " bamboo.");

        successfulMeals++;

        if (successfulMeals >= mealsBeforePooping)
        {
            bool grewBamboo =
                BambooManager.Instance.OtherPandaPooped(midwayZone);

            if (grewBamboo)
            {
                successfulMeals = 0;
                Debug.Log("Other panda helped regrow bamboo!");
            }
            else
            {
                Debug.Log("Other panda cannot grow bamboo: no free spot.");
            }
        }
    }
}