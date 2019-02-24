using UnityEngine;
using System.Runtime.InteropServices;

public class MissileManager : MonoBehaviour
{
    private const int MAX_RQUESTS = 4;
    private const float FLOAT_MAX = 1e+38f;
    private const int THREAD_MAX = 512;
    private const int MISSILE_MAX = THREAD_MAX * 16;
    private const int SPAWN_MAX = 64;
    private const int TARGET_MAX = 256;
    private const int TRAIL_LENGTH = 32;
    private const float MISSILE_ALIVE_PERIOD = 20f;
    private const float MISSILE_ALIVE_PERIOD_AFTER_TARGET_DEATH = 2f;
    private const float TRAIL_REMAIN_PERIOD_AFTER_MISSILE_DEATH = 1.2f;

    private static readonly int SHADER_PROPERTYID_CURRENTTIME = Shader.PropertyToID("_CurrentTime");
    private static readonly int SHADER_PROPERTYID_DT = Shader.PropertyToID("_DT");
    private static readonly int SHADER_PROPERTYID_CAMUP = Shader.PropertyToID("_CamUp");
    private static readonly int SHADER_PROPERTYID_MATRIXVIEW2 = Shader.PropertyToID("_MatrixView2");
    private static readonly int SHADER_PROPERTYID_CAMPOS = Shader.PropertyToID("_CamPos");
    private static readonly int SHADER_PROPERTYID_FRAMECOUNT = Shader.PropertyToID("_FrameCount");
    private static readonly int SHADER_PROPERTYID_DISPLAYNUM = Shader.PropertyToID("_DisplayNum");

    [SerializeField]
    private Mesh m_MissileMesh;
    [SerializeField]
    private Mesh m_BurnerMesh;
    [SerializeField]
    private Material m_MissileMaterial;
    [SerializeField]
    private Material m_BurnerMaterial;
    [SerializeField]
    private Material m_TrailMaterial;
    [SerializeField]
    private Material m_ExplosionMaterial;
    [SerializeField]
    ComputeShader m_SpawnCshader;
    [SerializeField]
    ComputeShader m_UpdateCshader;
    [SerializeField]
    ComputeShader m_SortCshader;

    private Camera m_Camera;
    private Mesh m_TrailMesh;
    private Mesh m_ExplosionMesh;
    private uint[] m_MissileDrawindirectArgs;
    private uint[] m_BurnerDrawindirectArgs;
    private uint[] m_TrailDrawindirectArgs;
    private uint[] m_ExplosionDrawindirectArgs;
    private MissileData[] m_Missiles;
    private SpawnData[] m_Spawns;
    private TargetData[] m_Targets;
    private ResultData[] m_Results;
    private SortData[] m_MissileSortKeys;
    private Vector4[] m_FrustumPlanes;
    private Vector4[] m_Trails;
    private int[] m_TrailIndexs;
    private int m_FrameCount = -1;
    private float[] m_MissileStatuss;
    private byte[] m_TargetHits;
    private int m_SpawnIdx;
    private int m_SpawnCkernel;
    private int m_UpdateCkernel;
    private int m_SortCkernel;
    private ComputeBuffer m_MissileCbufferDrawindirectArgs;
    private ComputeBuffer m_MissileCbuffer;
    private ComputeBuffer m_BurnerCbufferDrawindirectArgs;
    private ComputeBuffer m_SpawnCbuffer;
    private ComputeBuffer m_TargetCbuffer;
    private ComputeBuffer m_MissileResultCbuffer;
    private ComputeBuffer m_MissileSortKeysCbuffer;
    private ComputeBuffer m_FrustumPlanesCbuffer;
    private ComputeBuffer m_TrailCbufferDrawindirectArgs;
    private ComputeBuffer m_TrailCbuffer;
    private ComputeBuffer m_TrailIndexCbuffer;
    private ComputeBuffer m_ExplosionCbufferDrawindirectArgs;
    private Bounds m_UnlimitedBounds;
    private int m_NextSpawnMissileIdx;
    private float m_DrawnUpdateTime_;
    private int m_MissileDrawnAliveCount;
    private int m_MaxMissileDrwawCount;
    private UnityEngine.Rendering.AsyncGPUReadbackRequest[] m_Requests;
    private int m_RequestIdx;

    public void Initialize(Camera camera)
    {
        Debug.Assert(SystemInfo.supportsInstancing);
        Debug.Assert(SystemInfo.supportsAsyncGPUReadback);

        m_Camera = camera;

        // mesh trail
        {
            var vertices = new Vector3[TRAIL_LENGTH * 2];
            var triangles = new int[(TRAIL_LENGTH - 1) * 6];
            for (var i = 0; i < TRAIL_LENGTH - 1; ++i)
            {
                triangles[i * 6 + 0] = (i + 0) * 2 + 0;
                triangles[i * 6 + 1] = (i + 0) * 2 + 1;
                triangles[i * 6 + 2] = (i + 1) * 2 + 0;
                triangles[i * 6 + 3] = (i + 1) * 2 + 0;
                triangles[i * 6 + 4] = (i + 0) * 2 + 1;
                triangles[i * 6 + 5] = (i + 1) * 2 + 1;
            }
            m_TrailMesh = new Mesh();
            m_TrailMesh.name = "trail";
            m_TrailMesh.vertices = vertices;
            m_TrailMesh.triangles = triangles;
        }
        // mesh explosion
        {
            var vertices = new Vector3[4];
            var triangles = new int[6];
            triangles[0] = 0;
            triangles[1] = 1;
            triangles[2] = 2;
            triangles[3] = 2;
            triangles[4] = 1;
            triangles[5] = 3;
            m_ExplosionMesh = new Mesh();
            m_ExplosionMesh.name = "explosion";
            m_ExplosionMesh.vertices = vertices;
            m_ExplosionMesh.triangles = triangles;
        }

        m_MissileDrawindirectArgs = new uint[5] { 0, 0, 0, 0, 0 };
        m_BurnerDrawindirectArgs = new uint[5] { 0, 0, 0, 0, 0 };
        m_Missiles = new MissileData[MISSILE_MAX];
        for (var i = 0; i < m_Missiles.Length; ++i)
        {
            m_Missiles[i].Position = Vector3.zero;
            m_Missiles[i].SpawnTime = FLOAT_MAX;
            m_Missiles[i].Omega = Vector3.zero;
            m_Missiles[i].Rotation = Quaternion.identity;
            m_Missiles[i].TargetId = -1;
            m_Missiles[i].DeadTime = FLOAT_MAX;
        }
        m_Spawns = new SpawnData[SPAWN_MAX];
        for (var i = 0; i < m_Spawns.Length; ++i)
        {
            m_Spawns[i].MissileId = -1;
            m_Spawns[i].TargetId = -1;
            m_Spawns[i].Valid = 0;
        }
        m_Targets = new TargetData[TARGET_MAX];
        for (var i = 0; i < m_Targets.Length; ++i)
        {
            m_Targets[i].DeadTime = FLOAT_MAX;
            m_Targets[i].SpawnTime = FLOAT_MAX;
        }

        m_Results = new ResultData[MISSILE_MAX * 2];
        for (var i = 0; i < m_Results.Length; ++i)
        {
            m_Results[i].Cond = 0;
            m_Results[i].Dist = 0;
            m_Results[i].TargetId = 0;
            m_Results[i].FrameCount = 0;
        }

        m_MissileSortKeys = new SortData[MISSILE_MAX];
        m_MissileStatuss = new float[MISSILE_MAX];
        for (var i = 0; i < m_MissileStatuss.Length; ++i)
        {
            m_MissileStatuss[i] = 0f;
        }
        m_FrustumPlanes = new Vector4[6];
        for (var i = 0; i < m_FrustumPlanes.Length; ++i)
        {
            m_FrustumPlanes[i] = new Vector4(0, 0, 0, 0);
        }

        m_TargetHits = new byte[TARGET_MAX];
        for (var i = 0; i < m_TargetHits.Length; ++i)
        {
            m_TargetHits[i] = 0;
        }
        m_SpawnIdx = 0;

        m_TrailDrawindirectArgs = new uint[5] { 0, 0, 0, 0, 0 };
        m_Trails = new Vector4[TRAIL_LENGTH * MISSILE_MAX];
        for (var i = 0; i < m_Trails.Length; ++i)
        {
            m_Trails[i].x = 0f;
            m_Trails[i].y = 0f;
            m_Trails[i].z = 0f;
            m_Trails[i].w = 0f;
        }
        m_TrailIndexs = new int[MISSILE_MAX];
        for (var i = 0; i < m_TrailIndexs.Length; ++i)
        {
            m_TrailIndexs[i] = 0;
        }

        m_ExplosionDrawindirectArgs = new uint[5] { 0, 0, 0, 0, 0 };

        /* compute buffers */
        // missile
        m_MissileCbufferDrawindirectArgs = new ComputeBuffer(1 /* count */,
                                                               (m_MissileDrawindirectArgs.Length *
                                                                Marshal.SizeOf(typeof(uint))) /* stride */,
                                                               ComputeBufferType.IndirectArguments);
        m_MissileDrawindirectArgs[0] = m_MissileMesh.GetIndexCount(0 /* submesh */);
        m_MissileCbufferDrawindirectArgs.SetData(m_MissileDrawindirectArgs);
        m_BurnerCbufferDrawindirectArgs = new ComputeBuffer(1 /* count */,
                                                              (m_BurnerDrawindirectArgs.Length *
                                                               Marshal.SizeOf(typeof(uint))) /* stride */,
                                                              ComputeBufferType.IndirectArguments);
        m_BurnerDrawindirectArgs[0] = m_BurnerMesh.GetIndexCount(0 /* submesh */);
        m_BurnerCbufferDrawindirectArgs.SetData(m_BurnerDrawindirectArgs);
        m_MissileCbuffer = new ComputeBuffer(m_Missiles.Length, Marshal.SizeOf(typeof(MissileData)));
        m_MissileCbuffer.SetData(m_Missiles);
        m_SpawnCbuffer = new ComputeBuffer(m_Spawns.Length, Marshal.SizeOf(typeof(SpawnData)));
        m_TargetCbuffer = new ComputeBuffer(m_Targets.Length, Marshal.SizeOf(typeof(TargetData)));
        m_TargetCbuffer.SetData(m_Targets);
        m_MissileResultCbuffer = new ComputeBuffer(m_Results.Length, Marshal.SizeOf(typeof(ResultData)));
        m_MissileResultCbuffer.SetData(m_Results);
        m_MissileSortKeysCbuffer = new ComputeBuffer(m_MissileSortKeys.Length, Marshal.SizeOf(typeof(SortData)));
        m_MissileSortKeysCbuffer.SetData(m_MissileSortKeys);
        m_FrustumPlanesCbuffer = new ComputeBuffer(m_FrustumPlanes.Length, Marshal.SizeOf(typeof(Vector4)));
        m_FrustumPlanesCbuffer.SetData(m_FrustumPlanes);

        // trail
        m_TrailCbufferDrawindirectArgs = new ComputeBuffer(1 /* count */,
                                                             (m_TrailDrawindirectArgs.Length *
                                                              Marshal.SizeOf(typeof(uint))) /* stride */,
                                                             ComputeBufferType.IndirectArguments);
        m_TrailDrawindirectArgs[0] = m_TrailMesh.GetIndexCount(0 /* submesh */);
        m_TrailCbufferDrawindirectArgs.SetData(m_TrailDrawindirectArgs);
        m_TrailCbuffer = new ComputeBuffer(m_Trails.Length, Marshal.SizeOf(typeof(Vector4)));
        m_TrailCbuffer.SetData(m_Trails);
        m_TrailIndexCbuffer = new ComputeBuffer(m_TrailIndexs.Length, Marshal.SizeOf(typeof(int)));
        m_TrailIndexCbuffer.SetData(m_TrailIndexs);

        // explosion
        m_ExplosionCbufferDrawindirectArgs = new ComputeBuffer(1 /* count */,
                                                                 (m_ExplosionDrawindirectArgs.Length *
                                                                  Marshal.SizeOf(typeof(uint))) /* stride */,
                                                                 ComputeBufferType.IndirectArguments);
        m_ExplosionDrawindirectArgs[0] = m_ExplosionMesh.GetIndexCount(0 /* submesh */);
        m_ExplosionCbufferDrawindirectArgs.SetData(m_ExplosionDrawindirectArgs);

        m_Requests = new UnityEngine.Rendering.AsyncGPUReadbackRequest[MAX_RQUESTS];
        m_RequestIdx = 0;
        m_Requests[m_RequestIdx] = UnityEngine.Rendering.AsyncGPUReadback.Request(m_MissileResultCbuffer);
        ++m_RequestIdx;
        m_RequestIdx %= MAX_RQUESTS;

        // setup for missile_spawn compute
        m_SpawnCkernel = m_SpawnCshader.FindKernel("missile_spawn");
        m_SpawnCshader.SetBuffer(m_SpawnCkernel, "cbuffer_spawn", m_SpawnCbuffer);
        m_SpawnCshader.SetBuffer(m_SpawnCkernel, "cbuffer_missile", m_MissileCbuffer);
        m_SpawnCshader.SetBuffer(m_SpawnCkernel, "cbuffer_trail", m_TrailCbuffer);
        m_SpawnCshader.SetBuffer(m_SpawnCkernel, "cbuffer_trail_index", m_TrailIndexCbuffer);
        // setup for missile_update compute
        m_UpdateCkernel = m_UpdateCshader.FindKernel("missile_update");
        m_UpdateCshader.SetBuffer(m_UpdateCkernel, "cbuffer_missile", m_MissileCbuffer);
        m_UpdateCshader.SetBuffer(m_UpdateCkernel, "cbuffer_target", m_TargetCbuffer);
        m_UpdateCshader.SetBuffer(m_UpdateCkernel, "cbuffer_missile_result", m_MissileResultCbuffer);
        m_UpdateCshader.SetFloat("_MissileAlivePeriod", MISSILE_ALIVE_PERIOD);
        m_UpdateCshader.SetFloat("_MissileAlivePeriodAfterTargetDeath", MISSILE_ALIVE_PERIOD_AFTER_TARGET_DEATH);
        m_UpdateCshader.SetFloat("_TrailRemainPeriodAfterMissileDeath", TRAIL_REMAIN_PERIOD_AFTER_MISSILE_DEATH);
        m_UpdateCshader.SetBuffer(m_UpdateCkernel, "cbuffer_trail", m_TrailCbuffer);
        m_UpdateCshader.SetBuffer(m_UpdateCkernel, "cbuffer_trail_index", m_TrailIndexCbuffer);
        m_UpdateCshader.SetBuffer(m_UpdateCkernel, "cbuffer_frustum_planes", m_FrustumPlanesCbuffer);
        m_UpdateCshader.SetBuffer(m_UpdateCkernel, "cbuffer_missile_sort_key_list", m_MissileSortKeysCbuffer);
        // setup for missile_update compute
        m_SortCkernel = m_SortCshader.FindKernel("missile_sort");
        m_SortCshader.SetBuffer(m_SortCkernel, "cbuffer_missile_sort_key_list", m_MissileSortKeysCbuffer);

        // setup for missile shader
        m_MissileMaterial.SetBuffer("cbuffer_missile", m_MissileCbuffer);
        m_MissileMaterial.SetBuffer("cbuffer_missile_sort_key_list", m_MissileSortKeysCbuffer);
        // setup for burner shader
        m_BurnerMaterial.SetBuffer("cbuffer_missile", m_MissileCbuffer);
        m_BurnerMaterial.SetBuffer("cbuffer_missile_sort_key_list", m_MissileSortKeysCbuffer);
        // setup for trail shader
        m_TrailMaterial.SetBuffer("cbuffer_trail", m_TrailCbuffer);
        m_TrailMaterial.SetBuffer("cbuffer_trail_index", m_TrailIndexCbuffer);
        m_TrailMaterial.SetBuffer("cbuffer_missile_sort_key_list", m_MissileSortKeysCbuffer);
        // setup for explosion shader
        m_ExplosionMaterial.SetBuffer("cbuffer_missile", m_MissileCbuffer);
        m_ExplosionMaterial.SetBuffer("cbuffer_missile_sort_key_list", m_MissileSortKeysCbuffer);

        m_UnlimitedBounds = new Bounds(Vector3.zero, new Vector3(999999f, 999999f, 999999f));
        m_NextSpawnMissileIdx = 0;
        m_DrawnUpdateTime_ = 0f;
        m_MissileDrawnAliveCount = 0;
        m_MaxMissileDrwawCount = 4096;

        UnityEditor.SceneView.onSceneGUIDelegate += OnSceneGUI;
    }

    public void Release()
    {
        UnityEditor.SceneView.onSceneGUIDelegate -= OnSceneGUI;

        // release compute buffers
        m_ExplosionCbufferDrawindirectArgs.Release();
        m_TrailIndexCbuffer.Release();
        m_TrailCbuffer.Release();
        m_TrailCbufferDrawindirectArgs.Release();
        m_FrustumPlanesCbuffer.Release();
        m_MissileSortKeysCbuffer.Release();
        m_MissileResultCbuffer.Release();
        m_TargetCbuffer.Release();
        m_SpawnCbuffer.Release();
        m_MissileCbuffer.Release();
        m_BurnerCbufferDrawindirectArgs.Release();
        m_MissileCbufferDrawindirectArgs.Release();
    }

    private void dispatch_compute(float dt, float current_time)
    {
        // set data for compute
        m_SpawnCbuffer.SetData(m_Spawns);
        m_SpawnCshader.SetFloat(SHADER_PROPERTYID_CURRENTTIME, current_time);
        m_TargetCbuffer.SetData(m_Targets);
        m_UpdateCshader.SetFloat(SHADER_PROPERTYID_DT, dt);
        m_UpdateCshader.SetFloat(SHADER_PROPERTYID_CURRENTTIME, current_time);
        var view = m_Camera.worldToCameraMatrix;
        m_UpdateCshader.SetVector(SHADER_PROPERTYID_MATRIXVIEW2, new Vector4(view.m20, view.m21, view.m22, view.m23));
        m_UpdateCshader.SetVector(SHADER_PROPERTYID_CAMPOS, m_Camera.transform.position);
        m_UpdateCshader.SetInt(SHADER_PROPERTYID_FRAMECOUNT, m_FrameCount);
        {
            Utility.GetPlanesFromFrustum(ref m_FrustumPlanes
                , m_Camera.projectionMatrix * view);
            m_FrustumPlanesCbuffer.SetData(m_FrustumPlanes);
        }
        if (m_SpawnIdx > 0)
        {
            m_SpawnCshader.Dispatch(m_SpawnCkernel, 1, 1, 1);
        }
        m_UpdateCshader.Dispatch(m_UpdateCkernel, MISSILE_MAX / THREAD_MAX, 1, 1);
        m_SortCshader.Dispatch(m_SortCkernel, 1, 1, 1);
    }

    private void draw(Camera camera, float current_time, int missile_alive_count)
    {
        Debug.Assert(missile_alive_count >= 0);
        missile_alive_count = m_MaxMissileDrwawCount < missile_alive_count ? m_MaxMissileDrwawCount : missile_alive_count;

        // set data for missile
        m_MissileDrawindirectArgs[1] = (uint)missile_alive_count;
        m_MissileCbufferDrawindirectArgs.SetData(m_MissileDrawindirectArgs);
        m_MissileMaterial.SetFloat(SHADER_PROPERTYID_CURRENTTIME, current_time);
        // set data for burner
        m_BurnerDrawindirectArgs[1] = (uint)missile_alive_count;
        m_BurnerCbufferDrawindirectArgs.SetData(m_BurnerDrawindirectArgs);
        m_BurnerMaterial.SetFloat(SHADER_PROPERTYID_CURRENTTIME, current_time);
        // set data for trail
        m_TrailMaterial.SetFloat(SHADER_PROPERTYID_CURRENTTIME, current_time);
        m_TrailMaterial.SetInt(SHADER_PROPERTYID_DISPLAYNUM, missile_alive_count);
        m_TrailDrawindirectArgs[1] = (uint)missile_alive_count;
        m_TrailCbufferDrawindirectArgs.SetData(m_TrailDrawindirectArgs);
        // set data for explosion
        m_ExplosionMaterial.SetFloat(SHADER_PROPERTYID_CURRENTTIME, current_time);
        m_ExplosionMaterial.SetVector(SHADER_PROPERTYID_CAMUP, m_Camera.transform.TransformVector(Vector3.up));
        m_ExplosionDrawindirectArgs[1] = (uint)missile_alive_count;
        m_ExplosionCbufferDrawindirectArgs.SetData(m_ExplosionDrawindirectArgs);

        if (missile_alive_count > 0)
        {
            Graphics.DrawMeshInstancedIndirect(m_MissileMesh,
                                               0 /* submesh */,
                                               m_MissileMaterial,
                                               m_UnlimitedBounds,
                                               m_MissileCbufferDrawindirectArgs,
                                               0 /* argsOffset */,
                                               null /* properties */,
                                               UnityEngine.Rendering.ShadowCastingMode.Off,
                                               false /* receiveShadows */,
                                               0 /* layer */,
                                               camera);
            Graphics.DrawMeshInstancedIndirect(m_BurnerMesh,
                                               0 /* submesh */,
                                               m_BurnerMaterial,
                                               m_UnlimitedBounds,
                                               m_BurnerCbufferDrawindirectArgs,
                                               0 /* argsOffset */,
                                               null /* properties */,
                                               UnityEngine.Rendering.ShadowCastingMode.Off,
                                               false /* receiveShadows */,
                                               0 /* layer */,
                                               camera);
            Graphics.DrawMeshInstancedIndirect(m_TrailMesh,
                                               0 /* submesh */,
                                               m_TrailMaterial,
                                               m_UnlimitedBounds,
                                               m_TrailCbufferDrawindirectArgs,
                                               0 /* argsOffset */,
                                               null /* properties */,
                                               UnityEngine.Rendering.ShadowCastingMode.Off,
                                               false /* receiveShadows */,
                                               0 /* layer */,
                                               camera);
            Graphics.DrawMeshInstancedIndirect(m_ExplosionMesh,
                                               0 /* submesh */,
                                               m_ExplosionMaterial,
                                               m_UnlimitedBounds,
                                               m_ExplosionCbufferDrawindirectArgs,
                                               0 /* argsOffset */,
                                               null /* properties */,
                                               UnityEngine.Rendering.ShadowCastingMode.Off,
                                               false /* receiveShadows */,
                                               0 /* layer */,
                                               camera);
        }
    }

    private int update_status_list(float dt)
    {
        int missile_alive_count = 0;
        for (var i = 0; i < m_MissileStatuss.Length; ++i)
        {
            float missile_status = m_MissileStatuss[i];
            if (missile_status > 0f)
            {
                if (missile_status > dt)
                { // must live for a few frames.
                    m_MissileStatuss[i] -= dt;      // countdown.
                }
            }
            else if (missile_status < 0f)
            {
                m_MissileStatuss[i] += dt;
                if (m_MissileStatuss[i] > 0f)
                {
                    m_MissileStatuss[i] = 0f;
                }
            }
            if (missile_status != 0f)
            {
                ++missile_alive_count;
            }
        }
        return missile_alive_count;
    }

    public void update(float dt, double update_time)
    {
        ++m_FrameCount;

        setup_target((float)update_time);

        for (var k = 0; k < MAX_RQUESTS; ++k)
        {
            if (m_Requests[k].done && !m_Requests[k].hasError)
            {
                Unity.Collections.NativeArray<ResultData> buffer = m_Requests[k].GetData<ResultData>();
                for (var i = 0; i < m_Results.Length; ++i)
                {
                    m_Results[i] = buffer[i];
                }
                m_RequestIdx = k;
                break;
            }
        }
        for (var k = 0; k < MAX_RQUESTS; ++k)
        {
            if (m_Requests[m_RequestIdx].done)
            {
                m_Requests[m_RequestIdx] = UnityEngine.Rendering.AsyncGPUReadback.Request(m_MissileResultCbuffer);
                break;
            }
            ++m_RequestIdx;
            m_RequestIdx %= MAX_RQUESTS;
            Debug.Assert(k < MAX_RQUESTS);
        }

        // collect active missiles and get the count.
        int missile_alive_count = update_status_list(dt);

        // gpu execution
        dispatch_compute(dt, (float)update_time);

        // draw
        draw(m_Camera, (float)update_time, missile_alive_count);
        m_DrawnUpdateTime_ = (float)update_time;
        m_MissileDrawnAliveCount = missile_alive_count;

        // cleanup
        m_SpawnIdx = 0;
        for (var i = 0; i < m_Spawns.Length; ++i)
        {
            m_Spawns[i].Valid = 0;
        }
    }

    public void OnSceneGUI(UnityEditor.SceneView sceneView)
    {
        draw(sceneView.camera, m_DrawnUpdateTime_, m_MissileDrawnAliveCount);
    }

    void set_spawn(ref SpawnData spawn, ref Vector3 pos, ref Quaternion rot, int missile_id, int target_id)
    {
        Debug.Assert(target_id >= 0);
        spawn.Position = pos;
        spawn.Rotation = rot * Quaternion.Euler(MyRandom.Range(-3f, 3f), MyRandom.Range(-10, 10f), 0f);
        spawn.MissileId = missile_id;
        spawn.TargetId = target_id;
        spawn.Valid = 1 /* true */;
        spawn.RandomValue = MyRandom.Range(0f, 1f);
        spawn.RandomValueSecond = MyRandom.Range(0f, 1f);
    }

    public void Spawn(Vector3 pos, Quaternion rot, int target_id, double update_time)
    {
        if (((float)update_time - m_Targets[target_id].DeadTime) >= 0f)
        {
            Debug.Log("no target exists for spawn.");
            return;                // no target exists.
        }

        Debug.Assert(m_MissileStatuss.Length == MISSILE_MAX);
        int idx = -1;
        for (var i = 0; i < MISSILE_MAX; ++i)
        {
            var j = (i + m_NextSpawnMissileIdx) % MISSILE_MAX;
            if (m_MissileStatuss[j] == 0f)
            {
                m_MissileStatuss[j] = 0.1f; // live 6 frames at least.
                idx = j;
                break;
            }
        }
        if (idx < 0)
        {
            /* Debug.LogError("exceed missiles.."); */
            return;
        }
        if (m_SpawnIdx >= m_Spawns.Length)
        {
            Debug.LogError("exceed spawn..");
            return;
        }
        set_spawn(ref m_Spawns[m_SpawnIdx], ref pos, ref rot, idx, target_id);
        ++m_SpawnIdx;
        m_NextSpawnMissileIdx = idx + 1;
    }

    public int RegistMissile(double totaleUpdateTime)
    {
        int id = -1;
        for (int iMissile = 0; iMissile < m_Targets.Length; ++iMissile)
        {
            if (totaleUpdateTime - m_Targets[iMissile].SpawnTime < 0)
            {
                id = iMissile;
                m_Targets[iMissile].Position = Vector3.zero;
                m_Targets[iMissile].RadiusSqr = 0.4f * 0.4f;
                m_Targets[iMissile].DeadTime = FLOAT_MAX;
                m_Targets[iMissile].SpawnTime = (float)totaleUpdateTime;
                break;
            }
        }
        Debug.Assert(id >= 0);
        return id;
    }

    public void SetMissileRadius(int missileId, float radius)
    {
        m_Targets[missileId].RadiusSqr = radius * radius;
    }

    public void UpdateMissilePosition(int missileId, Vector3 pos)
    {
        m_Targets[missileId].Position = pos;
    }

    private void setup_target(float update_time)
    {
        for (var i = 0; i < m_Targets.Length; ++i)
        {
            if (update_time - m_Targets[i].SpawnTime > 0)
            {
                if ((update_time - m_Targets[i].DeadTime) > (MISSILE_ALIVE_PERIOD_AFTER_TARGET_DEATH +
                                                                   TRAIL_REMAIN_PERIOD_AFTER_MISSILE_DEATH))
                {
                    // can be reused
                    m_Targets[i].DeadTime = FLOAT_MAX;
                    m_Targets[i].SpawnTime = FLOAT_MAX;
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SpawnData
    {
        public Vector3 Position;
        public int MissileId;
        public Quaternion Rotation;
        public int TargetId;
        public int Valid;
        public float RandomValue;
        public float RandomValueSecond;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TargetData
    {
        public Vector3 Position;
        public float RadiusSqr;
        public float DeadTime;
        public float SpawnTime;
        public float Dummy0;
        public float Dummy1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MissileData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float SpawnTime;
        public Vector3 Omega;
        public float DeadTime;
        public int TargetId;
        public float RandomValue;
        public float Dummy0;
        public float Dummy1;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ResultData
    {
        public byte Cond;
        public byte Dist;
        public byte TargetId;
        public byte FrameCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SortData
    {
        public int Packed;
    }
}