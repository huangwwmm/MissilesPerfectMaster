using UnityEngine;

public class SystemManager : MonoBehaviour
{
    private const int UPDATE_FPS = 60;
    private const float UPDATE_DELTA_TIME = 1f / UPDATE_FPS;
    private const float ASPECT_RATIO = 16f / 9f;

    [SerializeField]
    private MissileManager m_MissileManager;
    [SerializeField]
    private Material m_FinalMaterial;
 
    private Camera m_MainCamera;
    private double m_TotalUpdateTime;
    /// <summary>
    /// 导弹攻击的目标
    /// </summary>
    private int m_TargetOfMissileIdx;

    protected void Awake()
    {
        Application.targetFrameRate = UPDATE_FPS;
        // 这个Camera是用来渲染它子节点的Quad, Quad的材质上MainTexture是Main Camera渲染出来的
        Camera finalCamera = GameObject.Find("Final Camera").GetComponent<Camera>();
        if ((float)Screen.width / Screen.height < ASPECT_RATIO)
        {
            finalCamera.orthographicSize = finalCamera.orthographicSize
                * (ASPECT_RATIO * ((float)Screen.height / Screen.width)); ;
        }

        m_TotalUpdateTime = 0.0f;

        RenderTexture renderTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        renderTexture.Create();
        m_MainCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
        m_MainCamera.targetTexture = renderTexture;
        m_FinalMaterial.mainTexture = renderTexture;

        m_MissileManager.Initialize(m_MainCamera);
        m_TargetOfMissileIdx = m_MissileManager.SpawnTarget(new Vector3(100.0f, 0.0f, 80.0f), 1.0f);
    }

    protected void OnDestroy()
    {
        m_MissileManager.Release();
    }

    protected void Update()
    {
        m_MissileManager.Spawn(new Vector3(0f, 0f, -40f)
                  , Quaternion.Euler(0f, -30f, 0f)
                  , m_TargetOfMissileIdx, m_TotalUpdateTime);
        m_MissileManager.update(UPDATE_DELTA_TIME, m_TotalUpdateTime);

        m_TotalUpdateTime += UPDATE_DELTA_TIME;
    }
}