using UnityEngine;
using System.Collections;

public class GameManager
{
    private const float FIGHTER_RANGE_POSITION = 300f;

    private static GameManager ms_Instance;

    private IEnumerator m_Enumerator;
    /// <summary>
    /// Cahce这个变量是为了在Ienumerator中使用
    /// </summary>
    private double m_TotalUpdateTime;

    public static GameManager GetInstance()
    {
        return ms_Instance ?? (ms_Instance = new GameManager());
    }

    public void Initialize(bool debug)
    {
        m_Enumerator = debug ? DEBUG_DoUpdate_Co() : DoUpdate_Co();
    }

    public void DoUpdate(float dt, double totalUpdateTime)
    {
        m_TotalUpdateTime = totalUpdateTime;
        m_Enumerator.MoveNext();
    }

    /// <summary>
    /// 启动时创建100个Fighter
    /// </summary>
    private IEnumerator DoUpdate_Co()
    {
        for (var iFighter = 0; iFighter < 100; ++iFighter)
        {
            Fighter.create(Fighter.Type.Alpha
                , new Vector3(MyRandom.Probability(0.5f) ? -FIGHTER_RANGE_POSITION : FIGHTER_RANGE_POSITION
                    , MyRandom.Range(-FIGHTER_RANGE_POSITION, FIGHTER_RANGE_POSITION)
                    , MyRandom.Range(-FIGHTER_RANGE_POSITION, FIGHTER_RANGE_POSITION))
                , Quaternion.LookRotation(MyRandom.PointOnSphere(1f))
                , m_TotalUpdateTime);
        }

        while (true)
        {
            yield return null;
        }
    }

    /// <summary>
    /// 每隔一段时间发射一个Missile
    /// </summary>
    /// <returns></returns>
    private IEnumerator DEBUG_DoUpdate_Co()
    {
        int missileId = MissileManager.Instance.RegistMissile(m_TotalUpdateTime);
        MissileManager.Instance.SetMissileRadius(missileId, 1f);
        MissileManager.Instance.UpdateMissilePosition(missileId, new Vector3(0f, 0f, 10f));

        while (true)
        {
            MissileManager.Instance.Spawn(new Vector3(0f, 0f, -40f)
                , Quaternion.Euler(0f, -30f, 0f)
                , missileId, m_TotalUpdateTime);

            for (var i = new Utility.WaitForSeconds(2f, m_TotalUpdateTime); !i.end(m_TotalUpdateTime);)
            {
                var pos = new Vector3(100f, 0f, 10f);
                MissileManager.Instance.UpdateMissilePosition(missileId, pos);
                MissileManager.Instance.checkHitAndClear(missileId);
                yield return null;
            }
        }
    }
}