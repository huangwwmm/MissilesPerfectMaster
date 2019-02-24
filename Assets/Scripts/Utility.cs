using UnityEngine;

public static class Utility
{
    public static void PlaneNormalize(this Vector4 vec)
    {
        float rlen = 1f / Mathf.Sqrt(vec.x * vec.x + vec.y * vec.y + vec.z * vec.z);
        vec.x *= rlen;
        vec.y *= rlen;
        vec.z *= rlen;
        vec.w *= rlen;
    }

    public static void GetPlanesFromFrustum(ref Vector4[] planes, Matrix4x4 vp)
    {
        Debug.Assert(planes.Length >= 6);
        int idx = 0;

        // left
        planes[idx].x = vp.m30 + vp.m00;
        planes[idx].y = vp.m31 + vp.m01;
        planes[idx].z = vp.m32 + vp.m02;
        planes[idx].w = vp.m33 + vp.m03;
        planes[idx].PlaneNormalize();
        ++idx;
        // right
        planes[idx].x = vp.m30 - vp.m00;
        planes[idx].y = vp.m31 - vp.m01;
        planes[idx].z = vp.m32 - vp.m02;
        planes[idx].w = vp.m33 - vp.m03;
        planes[idx].PlaneNormalize();
        ++idx;

        // bottom
        planes[idx].x = vp.m30 + vp.m10;
        planes[idx].y = vp.m31 + vp.m11;
        planes[idx].z = vp.m32 + vp.m12;
        planes[idx].w = vp.m33 + vp.m13;
        planes[idx].PlaneNormalize();
        ++idx;
        // top
        planes[idx].x = vp.m30 - vp.m10;
        planes[idx].y = vp.m31 - vp.m11;
        planes[idx].z = vp.m32 - vp.m12;
        planes[idx].w = vp.m33 - vp.m13;
        planes[idx].PlaneNormalize();
        ++idx;

        // near
        planes[idx].x = vp.m30 + vp.m20;
        planes[idx].y = vp.m31 + vp.m21;
        planes[idx].z = vp.m32 + vp.m22;
        planes[idx].w = vp.m33 + vp.m23;
        planes[idx].PlaneNormalize();
        ++idx;
        // far
        planes[idx].x = vp.m30 - vp.m20;
        planes[idx].y = vp.m31 - vp.m21;
        planes[idx].z = vp.m32 - vp.m22;
        planes[idx].w = vp.m33 - vp.m23;
        planes[idx].PlaneNormalize();
        ++idx;
    }
}