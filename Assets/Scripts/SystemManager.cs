using UnityEngine;

public class SystemManager : MonoBehaviour
{
    public const int AUDIO_CHANNEL_MAX = 4;
    private const int DEFAULT_FPS = 60;
    private const float RENDER_FPS = 60f;
    private const float RENDER_DT = 1f / RENDER_FPS;
    private const int AUDIOSOURCE_EXPLOSION_MAX = 8;
    private const int AUDIOSOURCE_LASER_MAX = 4;
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
    private Font m_Font;
    [SerializeField]
    private Material m_FontMaterial;
    [SerializeField]
    private Mesh m_FighterAlphaMesh;
    [SerializeField]
    private Material m_FighterAlphaMaterial;
    [SerializeField]
    private Mesh m_AlphaBurnerMesh;
    [SerializeField]
    private Material m_AlphaBurnerMaterial;
    [SerializeField]
    private AudioClip m_ExplosionAudio;
    [SerializeField]
    private AudioClip m_LaserAudio;

    private Matrix4x4[] m_AlphaMatrices;
    private Vector4[] m_FrustumPlanes;
    private Camera m_Camera;
    private GameObject m_CameraFinalHolder;
    private Camera m_CameraFinal;
    private RenderTexture m_RenderTexture;
    private Matrix4x4 m_ProjectionMatrix;
    private bool m_MeterDraw;
    private System.Diagnostics.Stopwatch m_Stopwatch;
    private int m_RenderingFront;
    private DrawBuffer[] m_DrawBuffer;
    private float m_DeltaTime;
    private DebugCamera m_DebugCamera;
    private SpectatorCamera m_SpectatorCamera;
    private long m_UpdateFrame;
    private long m_RenderFrame;
    private long m_RenderSyncFrame;
    private double m_TotalUpdateTime;
    private AudioSource[] m_AudioSourcesExplosion;
    private int m_AudioSourceExplosionIndex;
    private AudioSource[] m_AudioSourcesLaser;
    private int m_AudioSourceLaserIndex;
    private bool m_SpectatorMode;
    private bool m_IsInitialized = false;

    public static SystemManager GetInstance()
    {
        return ms_Instance ?? (ms_Instance = GameObject.Find("system_manager").GetComponent<SystemManager>());
    }

    public void SetFPS(int fps)
    {
        m_DeltaTime = 1f / (float)fps;
    }

    public int GetFPS()
    {
        if (m_DeltaTime <= 0f)
        {
            return 0;
        }
        else
        {
            return (int)(1f / m_DeltaTime + 0.5f);
        }
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
        m_MeterDraw = true;

        Application.targetFrameRate = (int)RENDER_FPS; // necessary for iOS because the default is 30fps.
        // QualitySettings.vSyncCount = 1;

        m_Stopwatch = new System.Diagnostics.Stopwatch();
        m_Stopwatch.Start();
        m_RenderingFront = 0;

        SetFPS(DEFAULT_FPS);
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
        MyFont.Instance.init(m_Font, m_FontMaterial);
        MyFontRenderer.Instance.init();

        PerformanceMeter.Instance.init();

        m_DrawBuffer = new DrawBuffer[2];
        for (int i = 0; i < 2; ++i)
        {
            m_DrawBuffer[i].init();
        }

        m_DebugCamera = DebugCamera.create();
        m_SpectatorCamera = SpectatorCamera.create();
        SetCamera();

        // audio
        m_AudioSourcesExplosion = new AudioSource[AUDIOSOURCE_EXPLOSION_MAX];
        for (var i = 0; i < AUDIOSOURCE_EXPLOSION_MAX; ++i)
        {
            m_AudioSourcesExplosion[i] = gameObject.AddComponent<AudioSource>();
            m_AudioSourcesExplosion[i].clip = m_ExplosionAudio;
            m_AudioSourcesExplosion[i].volume = 0.01f;
            m_AudioSourcesExplosion[i].pitch = 0.25f;
        }
        m_AudioSourceExplosionIndex = 0;
        m_AudioSourcesLaser = new AudioSource[AUDIOSOURCE_LASER_MAX];
        for (var i = 0; i < AUDIOSOURCE_LASER_MAX; ++i)
        {
            m_AudioSourcesLaser[i] = gameObject.AddComponent<AudioSource>();
            m_AudioSourcesLaser[i].clip = m_LaserAudio;
            m_AudioSourcesLaser[i].volume = 0.025f;
        }
        m_AudioSourceLaserIndex = 0;

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
        PerformanceMeter.Instance.beginUpdate();
        int updating_front = GetFront();

        // fetch
        Controller.Instance.fetch(m_TotalUpdateTime);
        var controller = Controller.Instance.getLatest();

        // update
        float dt = m_DeltaTime;
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
        PerformanceMeter.Instance.endUpdate();

        PerformanceMeter.Instance.beginRenderUpdate();
        CameraBase current_camera = m_SpectatorMode ? m_SpectatorCamera as CameraBase : m_DebugCamera as CameraBase;
        // begin
        UnityEngine.Profiling.Profiler.BeginSample("renderUpdate_begin");
        MySprite.Instance.begin();
        MyFont.Instance.begin();
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

        // performance meter
        if (m_MeterDraw)
        {
            PerformanceMeter.Instance.drawMeters(updating_front);
        }

        // end
        UnityEngine.Profiling.Profiler.BeginSample("renderUpdate_end");
        Spark.Instance.end();
        MyFont.Instance.end();
        MySprite.Instance.end();
        UnityEngine.Profiling.Profiler.EndSample();

        PerformanceMeter.Instance.endRenderUpdate();
    }

    public void RegistSound(DrawBuffer.SE se)
    {
        int front = GetFront();
        m_DrawBuffer[front].registSound(se);
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
        // audio
        for (var i = 0; i < AUDIO_CHANNEL_MAX; ++i)
        {
            if (draw_buffer.se_[i] != DrawBuffer.SE.None)
            {
                switch (draw_buffer.se_[i])
                {
                    case DrawBuffer.SE.Explosion:
                        m_AudioSourcesExplosion[m_AudioSourceExplosionIndex].Play();
                        ++m_AudioSourceExplosionIndex;
                        if (m_AudioSourceExplosionIndex >= AUDIOSOURCE_EXPLOSION_MAX)
                        {
                            m_AudioSourceExplosionIndex = 0;
                        }
                        break;
                    case DrawBuffer.SE.Laser:
                        m_AudioSourcesLaser[m_AudioSourceLaserIndex].Play();
                        ++m_AudioSourceLaserIndex;
                        if (m_AudioSourceLaserIndex >= AUDIOSOURCE_LASER_MAX)
                        {
                            m_AudioSourceLaserIndex = 0;
                        }
                        break;
                }
                draw_buffer.se_[i] = DrawBuffer.SE.None;
            }
        }
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
        MyFont.Instance.render();
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

    public void OnRestartClick()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("main");
    }

    public void OnFPSSliderChange(float value)
    {
        if (value <= 0f)
        {
            m_DeltaTime = 0f;
        }
        else
        {
            m_DeltaTime = 1f / value;
        }
    }

    public void OnMeterToggle()
    {
        m_MeterDraw = !m_MeterDraw;
    }

    public void OnMissileMaxChange()
    {
        MissileManager.Instance.changeMissileDrawMax();
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
        PerformanceMeter.Instance.beginRender();
        if (!m_IsInitialized)
        {
            return;
        }
        // MissileManager.Instance.SyncComputeBuffer();

        PerformanceMeter.Instance.beginBehaviourUpdate();

        InputManager.Instance.update();
        UnityEngine.Profiling.Profiler.BeginSample("main_loop");
        MainLoop();
        UnityEngine.Profiling.Profiler.EndSample();
        // MissileManager.Instance.SyncComputeBuffer();
        UnityEngine.Profiling.Profiler.BeginSample("unity_update");
        UnityUpdate();
        UnityEngine.Profiling.Profiler.EndSample();
        // MissileManager.Instance.SyncComputeBuffer();
        EndOfFrame();
    }

    protected void LateUpdate()
    {
        if (!m_IsInitialized)
        {
            return;
        }
        CameraUpdate();
        PerformanceMeter.Instance.endBehaviourUpdate();
        // MissileManager.Instance.SyncComputeBuffer();
    }

#if UNITY_EDITOR
    private void OnSceneGUI(UnityEditor.SceneView sceneView)
    {
        MissileManager.Instance.OnSceneGUI(sceneView.camera);
    }
#endif
}