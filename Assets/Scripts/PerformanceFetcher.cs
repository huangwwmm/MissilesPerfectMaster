using UnityEngine;

public class PerformanceFetcher : MonoBehaviour
{

    void OnPreRender()
    {
        PerformanceMeter.Instance.beginConsoleRender();
        MissileManager.Instance.SyncComputeBuffer();
    }

    void OnPreCull()
    {
        PerformanceMeter.Instance.endConsoleRender();
        // MissileManager.Instance.SyncComputeBuffer();
    }

    void OnPostRender()
    {
        // MissileManager.Instance.SyncComputeBuffer();
    }
}