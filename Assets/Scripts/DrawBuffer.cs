using UnityEngine;

public struct DrawBuffer
{
    public enum Type
    {
        None,
        Empty,
        MuscleMotionPlayer,
        FighterAlpha,
        DragonHead,
        DragonBody,
        DragonTail,
    }

    public const int OBJECT_MAX = 1024;

    public struct ObjectBuffer
    {
        public MyTransform transform_;
        public Type type_;
        public int versatile_data_;

        public void init()
        {
            transform_.init();
            type_ = Type.None;
            versatile_data_ = 0;
        }
        public void set(ref MyTransform transform, Type type, int versatile_data)
        {
            transform_ = transform;
            type_ = type;
            versatile_data_ = versatile_data;
        }
        public void set(ref Vector3 position, ref Quaternion rotation, Type type, int versatile_data)
        {
            transform_.position_ = position;
            transform_.setRotation(ref rotation);
            type_ = type;
            versatile_data_ = versatile_data;
        }
    }

    public MyTransform camera_transform_;
    public ObjectBuffer[] object_buffer_;
    public int object_num_;
    public Motion motion_;

    public void init()
    {
        object_buffer_ = new DrawBuffer.ObjectBuffer[OBJECT_MAX];
        for (int i = 0; i < OBJECT_MAX; ++i)
        {
            object_buffer_[i].init();
        }
        object_num_ = 0;
    }

    public void beginRender()
    {
        object_num_ = 0;
    }

    public void endRender()
    {
    }

    public void regist(ref MyTransform transform, Type type, int versatile_data = 0)
    {
        object_buffer_[object_num_].set(ref transform, type, versatile_data);
        ++object_num_;
        if (object_num_ > OBJECT_MAX)
        {
            Debug.LogError("EXCEED Fighter POOL!");
            Debug.Assert(false);
        }
    }

    public void regist(ref Vector3 position, ref Quaternion rotation, Type type, int versatile_data = 0)
    {
        object_buffer_[object_num_].set(ref position, ref rotation, type, versatile_data);
        ++object_num_;
        if (object_num_ > OBJECT_MAX)
        {
            Debug.LogError("EXCEED Fighter POOL!");
            Debug.Assert(false);
        }
    }

    public void registCamera(ref MyTransform transform)
    {
        camera_transform_ = transform;
    }

    public void registMotion(Motion motion)
    {
        motion_ = motion;
    }
}