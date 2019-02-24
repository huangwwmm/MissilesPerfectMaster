using UnityEngine;
using System;

/// <summary>
/// 基于Xorshift实现的伪随机数
/// <see cref="https://en.wikipedia.org/wiki/Xorshift"/>
/// </summary>
public static class MyRandom
{
    private static uint ms_SeedX;
    private static uint ms_SeedY;
    private static uint ms_SeedZ;
    private static uint ms_SeedW;

    static MyRandom()
    {
        SetSeed((ulong)DateTime.Now.Ticks);
    }

    public static void SetSeed(ulong seed)
    {
        ms_SeedX = 521288629u;
        ms_SeedY = (uint)(seed >> 32) & 0xFFFFFFFF;
        ms_SeedZ = (uint)(seed & 0xFFFFFFFF);
        ms_SeedW = ms_SeedX ^ ms_SeedZ;
    }

    public static uint Rand()
    {
        uint t = ms_SeedX ^ (ms_SeedX << 11);
        ms_SeedX = ms_SeedY;
        ms_SeedY = ms_SeedZ;
        ms_SeedZ = ms_SeedW;
        ms_SeedW = (ms_SeedW ^ (ms_SeedW >> 19)) ^ (t ^ (t >> 8));
        return ms_SeedW;
    }

    /// <summary>
    /// [min, max)
    /// </summary>
    public static float Range(float min, float max)
    {
        int val = (int)(Rand() & 0xffff);
        return (val * (max - min) / 0xffff) + min;
    }
}