using UnityEngine;

public class SystemManager : MonoBehaviour
{
    private const int UPDATE_FPS = 60;
    private const float UPDATE_DELTA_TIME = 1f / UPDATE_FPS;
    private const int ALPHA_MAX = 128;
    private const float ASPECT_RATIO = 16f / 9f;
    private static readonly int SHADER_PROPERTYID_CURRENT_TIME = Shader.PropertyToID("_CurrentTime");

    private static SystemManager ms_Instance;

    [SerializeField]
    private bool m_DebugMode;
    [SerializeField]
    private Material m_FinalMaterial;
    [SerializeField]
    private Material m_SparkMaterial;
    [SerializeField]
    private Material m_DebrisMaterial;
    [SerializeField]
    private Mesh m_FighterAlphaMesh;
    [SerializeField]
    private Material m_FighterAlphaMaterial;
    [SerializeField]
    private Mesh m_AlphaBurnerMesh;
    [SerializeField]
    private Material m_AlphaBurnerMaterial;

    private Matrix4x4[] m_AlphaMatrices;
    private Vector4[] m_FrustumPlanes;
    private Camera m_MainCamera;
    private GameObject m_CameraFinalHolder;
    private RenderTexture m_RenderTexture;
    private DrawBuffer m_DrawBuffer;
    private double m_TotalUpdateTime;

    public static SystemManager GetInstance()
    {
        return ms_Instance;
    }

    protected void Awake()
    {
        ms_Instance = gameObject.GetComponent<SystemManager>();

        // 这个Camera是用来渲染它子节点的Quad, Quad的材质上MainTexture是Main Camera渲染出来的
        {
            Camera finalCamera = GameObject.Find("Final Camera").GetComponent<Camera>();
            if ((float)Screen.width / Screen.height < ASPECT_RATIO)
            {
                finalCamera.orthographicSize = finalCamera.orthographicSize
                    * (ASPECT_RATIO * ((float)Screen.height / Screen.width)); ;
            }
        }
        Application.targetFrameRate = UPDATE_FPS;

        m_TotalUpdateTime = 100.0; // 猜测是用这个变量当余数，为了避免除0才随便赋了个大于0的值

        m_MainCamera = GameObject.Find("Main Camera").GetComponent<Camera>();

        MyRandom.SetSeed(12345L);
        MissileManager.Instance.Initialize(m_MainCamera);
        TaskManager.GetInstance().Initialize();
        Fighter.CreatePool();
        Spark.Instance.Initialize(m_SparkMaterial);
        Debris.Instance.Initialize(m_DebrisMaterial);

        m_DrawBuffer = new DrawBuffer();
        m_DrawBuffer.init();

        GameManager.GetInstance().Initialize(m_DebugMode);

        m_RenderTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        m_RenderTexture.Create();
        m_MainCamera.targetTexture = m_RenderTexture;
        m_FinalMaterial.mainTexture = m_RenderTexture;
        m_AlphaMatrices = new Matrix4x4[ALPHA_MAX];
        m_FrustumPlanes = new Vector4[6];
    }

    protected void OnEnable()
    {
#if UNITY_EDITOR
        UnityEditor.SceneView.onSceneGUIDelegate -= OnSceneGUI;
        UnityEditor.SceneView.onSceneGUIDelegate += OnSceneGUI;
#endif
    }

    protected void OnDisable()
    {
        MissileManager.Instance.Release();

#if UNITY_EDITOR
        UnityEditor.SceneView.onSceneGUIDelegate -= OnSceneGUI;
#endif
    }

    protected void Update()
    {
        // update
        float dt = UPDATE_DELTA_TIME;
        GameManager.GetInstance().DoUpdate(dt, m_TotalUpdateTime);
        TaskManager.GetInstance().DoUpdate(dt, m_TotalUpdateTime);
        m_TotalUpdateTime += dt;

        MissileManager.Instance.update(dt, m_TotalUpdateTime);

        // begin
        Spark.Instance.begin();

        // renderUpdate
        m_DrawBuffer.beginRender();
        TaskManager.GetInstance().DoRendererUpdate(m_DrawBuffer);
        m_DrawBuffer.endRender();

        // end
        Spark.Instance.end();

        double render_time = m_TotalUpdateTime;
        DoRender(m_DrawBuffer);
        Spark.Instance.render(m_MainCamera, render_time);
        Debris.Instance.render(m_MainCamera, render_time);
    }

    private void DoRender(DrawBuffer drawBuffer)
    {
        Utility.GetPlanesFromFrustum(ref m_FrustumPlanes
            , m_MainCamera.projectionMatrix * m_MainCamera.worldToCameraMatrix);

        int alpha_count = 0;
        for (var i = 0; i < drawBuffer.object_num_; ++i)
        {
            switch (drawBuffer.object_buffer_[i].type_)
            {
                case DrawBuffer.Type.None:
                    Debug.Assert(false);
                    break;
                case DrawBuffer.Type.Empty:
                    break;
                case DrawBuffer.Type.FighterAlpha:
                    if (Utility.InFrustum(m_FrustumPlanes,
                                          ref drawBuffer.object_buffer_[i].transform_.position_,
                                          2f /* radius */))
                    {
                        drawBuffer.object_buffer_[i].transform_.getLocalToWorldMatrix(ref m_AlphaMatrices[alpha_count]);
                        ++alpha_count;
                    }
                    break;
            }
        }
        Graphics.DrawMeshInstanced(m_FighterAlphaMesh, 0 /* submeshIndex */,
                                   m_FighterAlphaMaterial,
                                   m_AlphaMatrices,
                                   alpha_count,
                                   null,
                                   UnityEngine.Rendering.ShadowCastingMode.Off,
                                   false /* receiveShadows */,
                                   0 /* layer */,
                                   null /* camera */);
        m_AlphaBurnerMaterial.SetFloat(SHADER_PROPERTYID_CURRENT_TIME, (float)m_TotalUpdateTime);
        Graphics.DrawMeshInstanced(m_AlphaBurnerMesh, 0 /* submeshIndex */,
                                   m_AlphaBurnerMaterial,
                                   m_AlphaMatrices,
                                   alpha_count,
                                   null,
                                   UnityEngine.Rendering.ShadowCastingMode.Off,
                                   false /* receiveShadows */,
                                   0 /* layer */,
                                   null /* camera */);
    }

#if UNITY_EDITOR
    private void OnSceneGUI(UnityEditor.SceneView sceneView)
    {
        MissileManager.Instance.OnSceneGUI(sceneView.camera);
    }
#endif
}