public abstract class Task
{
    public bool IsAlive;

    /// <summary>
    /// 仅用于TaskManager
    /// </summary>
    internal Task _PrevTask;
    /// <summary>
    /// 仅用于TaskManager
    /// </summary>
    internal Task _NextTask;

    public virtual void Initialize()
    {
        IsAlive = true;
        TaskManager.GetInstance().Add(this);
    }

    public virtual void Destroy()
    {
        TaskManager.GetInstance().Remove(this);
        IsAlive = false;
    }

    /// <summary>
    /// 在<see cref="DoRendererUpdate"/>前执行
    /// </summary>
    /// <param name="deltaTime">当前帧的时间</param>
    /// <param name="totalUpdateTime">累计的Update时间（非真实时间），不包括当前帧</param>
    public abstract void DoUpdate(float deltaTime, double totalUpdateTime);

    public abstract void DoRenderUpdate(SpectatorCamera camera, DrawBuffer drawBuffer);
}