using UnityEngine;

public class SpectatorCamera : Task
{
    private const float FINAL_CAMERA_SCREEN_HEIGHT = 360f;

    private MyTransform m_Transform;
    private Fighter m_CurrentTarget;
    private Quaternion m_RotationOffset;
    private RigidbodyTransform m_RigidbodyTransform;
    private int m_Phase;
    private float m_Time;

    public override void Initialize()
    {
        base.Initialize();

        m_Transform.init();

        var pos = new Vector3(-1f, 0f, -40f);
        m_RigidbodyTransform.init();
        m_RigidbodyTransform.setPosition(ref pos);
        var target = new Vector3(1f, 0f, 0f);
        var diff = target - m_RigidbodyTransform.transform_.position_;
        var rot = Quaternion.LookRotation(diff);
        m_RigidbodyTransform.transform_.setRotation(ref rot);
        m_RigidbodyTransform.setDamper(1f);
        m_RigidbodyTransform.setRotateDamper(8f);
        m_CurrentTarget = null;
        m_RotationOffset = Quaternion.identity;
        m_Phase = 0;
        m_Time = 0f;
    }

    public override void DoUpdate(float deltaTime, double totalUpdateTime)
    {
        bool refresh_target = false;
        if (m_CurrentTarget == null)
        {
            refresh_target = true;
        }
        else
        {
            var probability = m_CurrentTarget.getHitElapsed(totalUpdateTime) < 1f ? 1f : 0.01f;
            if (MyRandom.Probability(probability, deltaTime))
            {
                refresh_target = true;
            }
        }
        if (refresh_target)
        {
            Fighter closest_fighter = Fighter.searchClosest(ref m_RigidbodyTransform.transform_.position_, m_CurrentTarget);
            if (closest_fighter != null)
            {
                m_CurrentTarget = closest_fighter;
            }
        }
        if (m_CurrentTarget != null)
        {
            switch (m_Phase)
            {
                case 0:
                    {
                        var target = m_CurrentTarget.rigidbody_.transform_.transformPositionZ(16f);
                        m_RigidbodyTransform.addSpringForce(ref target, 4f /* spring ratio */);
                        if (m_Time < totalUpdateTime)
                        {
                            m_Phase = 1;
                            m_Time = (float)totalUpdateTime + MyRandom.Range(5f, 28f);
                        }
                    }
                    break;
                case 1:
                    {
                        var target = m_CurrentTarget.rigidbody_.transform_.transformPositionZ(-2f);
                        m_RigidbodyTransform.addSpringForce(ref target, 2f /* spring ratio */);
                        if (m_Time < totalUpdateTime)
                        {
                            m_Phase = 0;
                            m_Time = (float)totalUpdateTime + MyRandom.Range(5f, 28f);
                        }
                    }
                    break;
            }
            m_RigidbodyTransform.addSpringTorqueCalcUp(ref m_CurrentTarget.rigidbody_.transform_.position_, 40f /* torque_level */);
            m_RigidbodyTransform.update(deltaTime);
        }
    }

    public override void DoRenderUpdate(SpectatorCamera dummy, DrawBuffer drawBuffer)
    {
        Vector3 offset = new Vector3(0f, 0f, 15f);
        Vector3 pos = m_RigidbodyTransform.transform_.position_ + m_RigidbodyTransform.transform_.rotation_ * offset;
        pos -= (m_RigidbodyTransform.transform_.rotation_ * m_RotationOffset) * offset;
        Quaternion rot = m_RigidbodyTransform.transform_.rotation_ * m_RotationOffset;
        m_Transform.position_ = pos;
        m_Transform.rotation_ = rot;
        drawBuffer.registCamera(ref m_Transform);
    }

    public void AddRotationOffset(float x, float y)
    {
        m_RotationOffset *= Quaternion.Euler(x, y, 0f);
    }
}