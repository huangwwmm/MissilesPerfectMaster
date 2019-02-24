using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 其实相当于一个链表, 一个Task是一个Node
/// </summary>
public class TaskManager
{
    private const int DEFAULT_CAPACITY = 1024;

    private static TaskManager m_Instance;

    private Task m_First;
    private Task m_Last;
    /// <summary>
    /// <see cref="m_WillRemoves"/>
    /// 这两个List为了避免在遍历Update Task时添加移除Task
    /// 
    /// 会在遍历Update后Add和Remove
    /// </summary>
    private List<Task> m_WillAdds;
    /// <summary>
    /// <see cref="m_WillAdds"/>
    /// </summary>
    private List<Task> m_WillRemoves;

    public static TaskManager GetInstance()
    {
        return m_Instance ?? (m_Instance = new TaskManager());
    }

    public void Initialize()
    {
        m_First = null;
        m_Last = null;
        m_WillAdds = new List<Task>(DEFAULT_CAPACITY);
        m_WillRemoves = new List<Task>(DEFAULT_CAPACITY);
    }

    public void Add(Task task)
    {
        m_WillAdds.Add(task);
    }

    public void Remove(Task task)
    {
        m_WillRemoves.Add(task);
    }

    public void DoUpdate(float deltaTime, double totalUpdateTime)
    {
        // foreach update tasks
        for (Task iterTask = m_First; iterTask != null; iterTask = iterTask._NextTask)
        {
            iterTask.DoUpdate(deltaTime, totalUpdateTime);
        }

        // remove tasks
        for (int iTask = 0; iTask < m_WillRemoves.Count; iTask++)
        {
            Task iterTask = m_WillRemoves[iTask];
            if (iterTask._PrevTask == null)
            {
                m_First = iterTask._NextTask;
            }
            else
            {
                iterTask._PrevTask._NextTask = iterTask._NextTask;
            }
            if (iterTask._NextTask == null)
            {
                m_Last = iterTask._PrevTask;
            }
            else
            {
                iterTask._NextTask._PrevTask = iterTask._PrevTask;
            }
            iterTask._NextTask = iterTask._PrevTask = null;
        }
        m_WillRemoves.Clear();

        // add tasks
        for (int iTask = 0; iTask < m_WillAdds.Count; iTask++)
        {
            Task iterTask = m_WillAdds[iTask];
            if (m_First == null)
            {
                Debug.Assert(m_Last == null);
                m_First = iterTask;
                m_Last = iterTask;
                iterTask._NextTask = iterTask._PrevTask = null;
            }
            else
            {
                iterTask._PrevTask = m_Last;
                iterTask._NextTask = null;
                m_Last._NextTask = iterTask;
                m_Last = iterTask;
            }
        }
        m_WillAdds.Clear();
    }

    public void DoRendererUpdate(DrawBuffer drawBuffer)
    {
        int x = 0;
        for (Task iterTask = m_First; iterTask != null; iterTask = iterTask._NextTask)
        {
            x++;
            iterTask.DoRenderUpdate(drawBuffer);
        }
    }
}