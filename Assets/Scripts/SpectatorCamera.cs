using UnityEngine;

public class SpectatorCamera : Task
{
    private const float FINAL_CAMERA_SCREEN_HEIGHT = 360f;

    private MyTransform m_Transform;
    private Fighter current_target_;
    private Quaternion offset_rotation_;
    private RigidbodyTransform rigidbody_;
    private int phase_;
    private float time_;

    public void applyTransform(ref Vector3 pos, ref Quaternion rot)
    {
        m_Transform.position_ = pos;
        m_Transform.rotation_ = rot;
    }

    public static SpectatorCamera create()
    {
        var camera = new SpectatorCamera();
        camera.Initialize();
        return camera;
    }

    public override void Initialize()
    {
        base.Initialize();

        m_Transform.init();

        var pos = new Vector3(-1f, 0f, -40f);
        rigidbody_.init();
        rigidbody_.setPosition(ref pos);
        var target = new Vector3(1f, 0f, 0f);
        var diff = target - rigidbody_.transform_.position_;
        var rot = Quaternion.LookRotation(diff);
        rigidbody_.transform_.setRotation(ref rot);
        rigidbody_.setDamper(1f);
        rigidbody_.setRotateDamper(8f);
        current_target_ = null;
        offset_rotation_ = Quaternion.identity;
        phase_ = 0;
        time_ = 0f;
    }

    public override void DoUpdate(float dt, double update_time)
    {
        bool refresh_target = false;
        if (current_target_ == null)
        {
            refresh_target = true;
        }
        else
        {
            var probability = current_target_.getHitElapsed(update_time) < 1f ? 1f : 0.01f;
            if (MyRandom.Probability(probability, dt))
            {
                refresh_target = true;
            }
        }
        if (refresh_target)
        {
            Fighter closest_fighter = Fighter.searchClosest(ref rigidbody_.transform_.position_, current_target_);
            if (closest_fighter != null)
            {
                current_target_ = closest_fighter;
            }
        }
        if (current_target_ != null)
        {
            switch (phase_)
            {
                case 0:
                    {
                        var target = current_target_.rigidbody_.transform_.transformPositionZ(16f);
                        rigidbody_.addSpringForce(ref target, 4f /* spring ratio */);
                        if (time_ < update_time)
                        {
                            phase_ = 1;
                            time_ = (float)update_time + MyRandom.Range(5f, 28f);
                        }
                    }
                    break;
                case 1:
                    {
                        var target = current_target_.rigidbody_.transform_.transformPositionZ(-2f);
                        rigidbody_.addSpringForce(ref target, 2f /* spring ratio */);
                        if (time_ < update_time)
                        {
                            phase_ = 0;
                            time_ = (float)update_time + MyRandom.Range(5f, 28f);
                        }
                    }
                    break;
            }
            rigidbody_.addSpringTorqueCalcUp(ref current_target_.rigidbody_.transform_.position_, 40f /* torque_level */);
            rigidbody_.update(dt);
        }
    }

    public override void DoRenderUpdate(SpectatorCamera dummy, DrawBuffer draw_buffer)
    {
        var offset = new Vector3(0f, 0f, 15f);
        var pos = rigidbody_.transform_.position_ + rigidbody_.transform_.rotation_ * offset;
        pos -= (rigidbody_.transform_.rotation_ * offset_rotation_) * offset;
        var rot = rigidbody_.transform_.rotation_ * offset_rotation_;
        applyTransform(ref pos, ref rot);
        draw_buffer.registCamera(ref m_Transform);
    }

    public void rotateOffsetRotation(float x, float y)
    {
        offset_rotation_ *= Quaternion.Euler(x, y, 0f);
        offset_rotation_ = Quaternion.Euler(offset_rotation_.eulerAngles);
    }
}