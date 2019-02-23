using UnityEngine;

public class PerformanceFetcher2 : MonoBehaviour
{

    // void OnPreRender()
    // {
    // }

    void OnPostRender()
    {
        PerformanceMeter.Instance.endRender();
    }

}