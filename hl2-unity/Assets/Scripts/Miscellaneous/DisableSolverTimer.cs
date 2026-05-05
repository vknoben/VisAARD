/// <summary>
/// Disabled solver on this GameObject after predefined time on start
/// </summary>

using MixedReality.Toolkit.SpatialManipulation;
using System.Collections;
using UnityEngine;

public class DisableSolverTimer : MonoBehaviour
{
    [Tooltip("Time in seconds after which this script disabled solver on this GameObject")]
    public float time = 1;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        StartCoroutine(TimeSolver(time));
    }

    private IEnumerator TimeSolver(float waitTime)
    {
        // Wait for predefined amount of time
        yield return new WaitForSeconds(waitTime);

        // Disable solver component on this object
        GetComponent<Solver>().enabled = false;
    }
}
