using UnityEngine;

public class SystemManager : MonoBehaviour
{
    private const int UPDATE_FPS = 60;
    private const float UPDATE_DELTA_TIME = 1f / UPDATE_FPS;
    private const int ALPHA_MAX = 128;
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
    private Sprite[] m_Sprites;
    [SerializeField]
    private Material m_SpriteMaterial;
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
    private Camera m_Camera;
    private GameObject m_CameraFinalHolder;
    private Camera m_CameraFinal;
    private RenderTexture m_RenderTexture;
    private Matrix4x4 m_ProjectionMatrix;
    private System.Diagnostics.Stopwatch m_Stopwatch;
    private int m_RenderingFront;
    private DrawBuffer[] m_DrawBuffer;
    private DebugCamera m_DebugCamera;
    private SpectatorCamera m_SpectatorCamera;
    private long m_UpdateFrame;
    private long m_RenderFrame;
    private long m_RenderSyncFrame;
    private double m_TotalUpdateTime;
    private bool m_SpectatorMode;
    private bool m_IsInitialized = false;

    public static SystemManager GetInstance()
    {
        return ms_Instance ?? (ms_Instance = GameObject.Find("system_manager").GetComponent<SystemManager>());
    }

    public Matrix4x4 GetProjectionMatrix()
    {
        return m_ProjectionMatrix;
    }

    private void SetCamera()
    {
        if (!m_SpectatorMode)
        {
            m_DebugCamera.setup(m_SpectatorCamera);
        }
        m_DebugCamera.active_ = !m_SpectatorMode;
        m_SpectatorCamera.active_ = m_SpectatorMode;
    }

    private void Initialize()
    {
        MyRandom.SetSeed(12345L);
        m_CameraFinal = GameObject.Find("FinalCamera").GetComponent<Camera>();
        m_CameraFinal.enabled = false;
        if ((float)Screen.width / (float)Screen.height < 16f / 9f)
        {
            var size = m_CameraFinal.orthographicSize * ((16f / 9f) * ((float)Screen.height / (float)Screen.width));
            m_CameraFinal.orthographicSize = size;
        }

        Application.targetFrameRate = UPDATE_FPS;

        m_Stopwatch = new System.Diagnostics.Stopwatch();
        m_Stopwatch.Start();
        m_RenderingFront = 0;

        m_UpdateFrame = 0;
        m_TotalUpdateTime = 100.0;    // ゼロクリア状態を過去のものにするため増やしておく
        m_RenderFrame = 0;
        m_RenderSyncFrame = 0;
        m_SpectatorMode = true;

        m_Camera = GameObject.Find("Main Camera").GetComponent<Camera>();
        m_ProjectionMatrix = m_Camera.projectionMatrix;

        MissileManager.Instance.initialize(m_Camera);
        InputManager.Instance.init();
        Controller.Instance.init(false /* auto */);
        TaskManager.GetInstance().Initialize();
        Fighter.createPool();
        Spark.Instance.init(m_SparkMaterial);
        Debris.Instance.init(m_DebrisMaterial);
        MySprite.Instance.init(m_Sprites, m_SpriteMaterial);
        MySpriteRenderer.Instance.init(m_Camera);

        m_DrawBuffer = new DrawBuffer[2];
        for (int i = 0; i < 2; ++i)
        {
            m_DrawBuffer[i].init();
        }

        m_DebugCamera = DebugCamera.create();
        m_SpectatorCamera = SpectatorCamera.create();
        SetCamera();

        GameManager.GetInstance().Initialize(m_DebugMode);

#if UNITY_PS4 || UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
        int rw = 1920;
        int rh = 1080;
#else
        int rw = 1024;
        int rh = 576;
#endif
        m_RenderTexture = new RenderTexture(rw, rh, 24 /* depth */, RenderTextureFormat.ARGB32);
        m_RenderTexture.Create();
        m_Camera.targetTexture = m_RenderTexture;
        m_FinalMaterial.mainTexture = m_RenderTexture;
        m_AlphaMatrices = new Matrix4x4[ALPHA_MAX];
        m_FrustumPlanes = new Vector4[6];

        m_IsInitialized = true;
        m_CameraFinal.enabled = true;
    }

    private int GetFront()
    {
        int updating_front = m_RenderingFront;            // don't flip
        return updating_front;
    }

    private void MainLoop()
    {
        int updating_front = GetFront();

        // fetch
        Controller.Instance.fetch(m_TotalUpdateTime);
        var controller = Controller.Instance.getLatest();

        // update
        float dt = UPDATE_DELTA_TIME;
        m_SpectatorCamera.rotateOffsetRotation(-controller.flick_y_, controller.flick_x_);
        UnityEngine.Profiling.Profiler.BeginSample("Task update");
        GameManager.GetInstance().DoUpdate(dt, m_TotalUpdateTime);
        TaskManager.GetInstance().DoUpdate(dt, m_TotalUpdateTime);
        UnityEngine.Profiling.Profiler.EndSample();
        ++m_UpdateFrame;
        m_TotalUpdateTime += dt;

        UnityEngine.Profiling.Profiler.BeginSample("MissileManager.update");
        MissileManager.Instance.update(dt, m_TotalUpdateTime);
        UnityEngine.Profiling.Profiler.EndSample();

        CameraBase current_camera = m_SpectatorMode ? m_SpectatorCamera as CameraBase : m_DebugCamera as CameraBase;
        // begin
        UnityEngine.Profiling.Profiler.BeginSample("renderUpdate_begin");
        MySprite.Instance.begin();
        Spark.Instance.begin();
        UnityEngine.Profiling.Profiler.EndSample();

        // renderUpdate
        UnityEngine.Profiling.Profiler.BeginSample("renderUpdate");
        m_DrawBuffer[updating_front].beginRender();
        TaskManager.GetInstance().DoRendererUpdate(updating_front,
                                          current_camera,
                                          ref m_DrawBuffer[updating_front]);
        m_DrawBuffer[updating_front].endRender();
        UnityEngine.Profiling.Profiler.EndSample();

        // end
        UnityEngine.Profiling.Profiler.BeginSample("renderUpdate_end");
        Spark.Instance.end();
        MySprite.Instance.end();
        UnityEngine.Profiling.Profiler.EndSample();
    }

    private void Render(ref DrawBuffer draw_buffer)
    {
        // camera
        m_Camera.transform.position = draw_buffer.camera_transform_.position_;
        m_Camera.transform.rotation = draw_buffer.camera_transform_.rotation_;
        m_Camera.enabled = true;
        var vp = m_Camera.projectionMatrix * m_Camera.worldToCameraMatrix;
        Utility.GetPlanesFromFrustum(m_FrustumPlanes, ref vp);

        int alpha_count = 0;
        for (var i = 0; i < draw_buffer.object_num_; ++i)
        {
            switch (draw_buffer.object_buffer_[i].type_)
            {
                case DrawBuffer.Type.None:
                    Debug.Assert(false);
                    break;
                case DrawBuffer.Type.Empty:
                    break;
                case DrawBuffer.Type.FighterAlpha:
                    if (Utility.InFrustum(m_FrustumPlanes,
                                          ref draw_buffer.object_buffer_[i].transform_.position_,
                                          2f /* radius */))
                    {
                        draw_buffer.object_buffer_[i].transform_.getLocalToWorldMatrix(ref m_AlphaMatrices[alpha_count]);
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

    private void CameraUpdate()
    {
    }

    private void UnityUpdate()
    {
        m_ProjectionMatrix = m_Camera.projectionMatrix;

        double render_time = m_TotalUpdateTime;
        UnityEngine.Profiling.Profiler.BeginSample("SystemManager.render");
        Render(ref m_DrawBuffer[m_RenderingFront]);
        UnityEngine.Profiling.Profiler.EndSample();
        UnityEngine.Profiling.Profiler.BeginSample("SystemManager.render components");
        Spark.Instance.render(m_Camera, render_time);
        Debris.Instance.render(m_Camera, render_time);

        MySprite.Instance.render();
        UnityEngine.Profiling.Profiler.EndSample();
    }

    private void EndOfFrame()
    {
        if (Time.deltaTime > 0)
        {
            ++m_RenderSyncFrame;
            ++m_RenderFrame;
            m_Stopwatch.Start();
        }
        else
        {
            m_Stopwatch.Stop();
        }
    }

    protected void OnApplicationQuit()
    {
        m_FinalMaterial.mainTexture = null; // suppress error-messages on console.
    }

    protected void OnEnable()
    {
#if UNITY_EDITOR
        UnityEditor.SceneView.onSceneGUIDelegate -= OnSceneGUI;
        UnityEditor.SceneView.onSceneGUIDelegate += OnSceneGUI;
#endif
    }

    protected void Start()
    {
        ms_Instance = GameObject.Find("system_manager").GetComponent<SystemManager>(); ;
        Initialize();
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
        if (!m_IsInitialized)
        {
            return;
        }

        InputManager.Instance.update();
        UnityEngine.Profiling.Profiler.BeginSample("main_loop");
        MainLoop();
        UnityEngine.Profiling.Profiler.EndSample();
        UnityEngine.Profiling.Profiler.BeginSample("unity_update");
        UnityUpdate();
        UnityEngine.Profiling.Profiler.EndSample();
        EndOfFrame();
    }

    protected void LateUpdate()
    {
        if (!m_IsInitialized)
        {
            return;
        }
        CameraUpdate();
    }

#if UNITY_EDITOR
    private void OnSceneGUI(UnityEditor.SceneView sceneView)
    {
        MissileManager.Instance.OnSceneGUI(sceneView.camera);
    }
#endif
}