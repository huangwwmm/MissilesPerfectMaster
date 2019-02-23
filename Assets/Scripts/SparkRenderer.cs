﻿using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SparkRenderer : MonoBehaviour
{

    private MeshFilter mf_;
    private MeshRenderer mr_;

    void Start()
    {
        mf_ = GetComponent<MeshFilter>();
        mr_ = GetComponent<MeshRenderer>();
        mf_.sharedMesh = Spark.Instance.getMesh();
        mr_.sharedMaterial = Spark.Instance.getMaterial();
        mr_.SetPropertyBlock(Spark.Instance.getMaterialPropertyBlock());
    }
}