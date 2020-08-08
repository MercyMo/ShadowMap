using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
public enum XE_PROJECT_TYPE
{
    emAABB,
    emSPHERE,
}

public class ShadowMap
{
    public ShadowMap(Camera camera, Light light, int nCascadeCount, Vector3 cascadeRatio, int nShadowMapSize = 2048, bool bStable = true)
    {
        m_ViewCamera = camera;
        m_Light = light;
        m_nShadowMapSize = nShadowMapSize;
        m_bIsStable = bStable;

        m_Cmd = new CommandBuffer();
        m_Cmd.name = "ShadowMap";

        m_ShadowMap = new RenderTexture(nShadowMapSize, nShadowMapSize, 32, RenderTextureFormat.Shadowmap);

        m_ShadowCasterMat = new Material(Shader.Find("Unlit/ShadowCaster"));

        m_NearCorner = new Vector3[4];
        m_FarCorner = new Vector3[4];

        m_CullingSphere = new Vector4[4];

        SetCascade(nCascadeCount, cascadeRatio);

        InitShadowBuffer();
    }

    public void SetCascade(int nCascadeCount, Vector3 cascadeRatio)
    {
        m_nCascadeCount = nCascadeCount;

        m_ShadowViewMatrices = new Matrix4x4[nCascadeCount];
        m_ShadowProjMatrices = new Matrix4x4[nCascadeCount];

        m_Splits = new float[m_nCascadeCount + 1];
        m_Splits[0] = m_ViewCamera.nearClipPlane;

        float fLength = m_ViewCamera.farClipPlane - m_ViewCamera.nearClipPlane;
        Vector4 completedRatio = Vector4.zero;
        if (nCascadeCount == 1)
            completedRatio.x = 1.0f;
        else
        {
            float fLeftRatio = 1.0f;
            for (int i = 0; i < m_nCascadeCount - 1; i++)
            {
                completedRatio[i] = cascadeRatio[i];
                fLeftRatio -= cascadeRatio[i];
            }
            completedRatio[m_nCascadeCount - 1] = fLeftRatio;
        }

        for (int i = 0; i < m_nCascadeCount; i++)
        {
            m_Splits[i + 1] = m_Splits[i] + fLength * cascadeRatio[i];
        }
    }
    
    private Vector4 CalCascadeSphere()
    {
        Vector4 sphere = new Vector4();
        Vector3 v0 = m_NearCorner[0];
        Vector3 v1 = m_FarCorner[0];
        Vector3 v2 = m_FarCorner[2];

        Vector3 e02 = v2 - v0;
        Vector3 e01 = v1 - v0;
        Vector3 normal = Vector3.Cross(e01, e02).normalized;
        Vector3 center01 = v0 + 0.5f * e01;
        Vector3 center02 = v0 + 0.5f * e02;
        Vector3 dir01 = Vector3.Cross(normal, e01);
        Vector3 dir02 = Vector3.Cross(normal, e02);

        Vector3 circumcenter = CalLineIntersect(center01, dir01, center02, dir02); //halfAB + t * prepAB;
        float fRadius = Vector3.Distance(v0, circumcenter);

        circumcenter = m_ViewCamera.transform.TransformPoint(circumcenter);

        float x = Mathf.Ceil(Vector3.Dot(circumcenter, m_Light.transform.up) * m_nShadowMapSize / fRadius) * fRadius / m_nShadowMapSize;
        float y = Mathf.Ceil(Vector3.Dot(circumcenter, m_Light.transform.right) * m_nShadowMapSize / fRadius) * fRadius / m_nShadowMapSize;

        circumcenter = m_Light.transform.up * x + m_Light.transform.right * y +
            m_Light.transform.forward * Vector3.Dot(circumcenter, m_Light.transform.forward);

        sphere = new Vector4(circumcenter.x, circumcenter.y, circumcenter.z, fRadius);
        return sphere;
    }

    public void CalCascade()
    {
        Rect viewport = new Rect(0, 0, 1, 1);
        for(int nCascadeIdx = 0; nCascadeIdx < m_nCascadeCount; nCascadeIdx++)
        {
            float fNear = m_Splits[nCascadeIdx];
            float fFar = m_Splits[nCascadeIdx + 1];

            // Get Near and Far plane's corner in view space
            m_ViewCamera.CalculateFrustumCorners(viewport, fNear, Camera.MonoOrStereoscopicEye.Mono, m_NearCorner);
            m_ViewCamera.CalculateFrustumCorners(viewport, fFar, Camera.MonoOrStereoscopicEye.Mono, m_FarCorner);

            // View Space to World Space
            for(int i = 0; i < 4; i++)
            {
                m_NearCorner[i] = m_ViewCamera.transform.TransformPoint(m_NearCorner[i]);
                m_FarCorner[i] = m_ViewCamera.transform.TransformPoint(m_FarCorner[i]);
            }

            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;
            Vector3 center = new Vector3();
            if (m_bIsStable)
            {
                Vector4 data = CalCascadeSphere();
                min = Vector3.one * (-data.w);
                max = Vector3.one * data.w;
                m_CullingSphere[nCascadeIdx] = data;
                center.x = data.x;
                center.y = data.y;
                center.z = data.z;
            }
            else
            {
                // view => world
                for (int i = 0; i < 4; i++)
                {
                    m_NearCorner[i] = m_Light.transform.TransformPoint(m_NearCorner[i]);
                    m_FarCorner[i] = m_Light.transform.TransformPoint(m_FarCorner[i]);
                }

                Matrix4x4 world2Light = m_ShadowViewMatrices[nCascadeIdx];
                Matrix4x4 light2World = world2Light.inverse;

                // world => light
                for (int i = 0; i < 4; i++)
                {
                    m_NearCorner[i] = world2Light.MultiplyPoint(m_NearCorner[i]);
                    m_FarCorner[i] = world2Light.MultiplyPoint(m_FarCorner[i]);
                }

                // Find Min & Max
                for (int i = 0; i < 4; i++)
                {
                    min = Vector3.Min(m_FarCorner[i], Vector3.Min(m_NearCorner[i], min));
                    max = Vector3.Max(m_FarCorner[i], Vector3.Max(m_NearCorner[i], max));
                }

                center = 0.5f * (min + max);
                center = light2World.MultiplyPoint(center);// light to world

                m_NearCorner[0] = new Vector3(min.x, min.y, min.z);
                m_NearCorner[1] = new Vector3(max.x, min.y, min.z);
                m_NearCorner[2] = new Vector3(max.x, max.y, min.z);
                m_NearCorner[3] = new Vector3(min.x, max.y, min.z);

                m_FarCorner[0] = new Vector3(min.x, min.y, max.z);
                m_FarCorner[1] = new Vector3(max.x, min.y, max.z);
                m_FarCorner[2] = new Vector3(max.x, max.y, max.z);
                m_FarCorner[3] = new Vector3(min.x, max.y, max.z);

                // AABB in world space
                for (int i = 0; i < 4; i++)
                {
                    m_NearCorner[i] = light2World.MultiplyPoint(m_NearCorner[i]);
                    m_FarCorner[i] = light2World.MultiplyPoint(m_FarCorner[i]);
                }
            }

            Matrix4x4 shadowViewMatrix = Matrix4x4.LookAt(center, m_Light.transform.forward + center, m_Light.transform.up).inverse;
            Matrix4x4 shadowProjMatrix = Matrix4x4.Ortho(min.x, max.x, min.y, max.y, min.z, max.z);

            Vector3 shadowOrigin = (shadowProjMatrix * shadowViewMatrix).MultiplyPoint(Vector3.zero);
            shadowOrigin = shadowOrigin * m_nShadowMapSize * 0.5f;

            Vector3 roundedOrigin = new Vector3(Mathf.Round(shadowOrigin.x), Mathf.Round(shadowOrigin.y), Mathf.Round(shadowOrigin.z));
            Vector3 roundedOffset = roundedOrigin - shadowOrigin;
            roundedOffset = roundedOffset * 2.0f / m_nShadowMapSize;
            roundedOffset.z = 0.0f;
            shadowProjMatrix = Matrix4x4.Translate(roundedOffset) * shadowProjMatrix;

            m_ShadowViewMatrices[nCascadeIdx] = shadowViewMatrix;
            m_ShadowProjMatrices[nCascadeIdx] = shadowProjMatrix;
        }
    }

    public void UnInit()
    {
        if (m_Cmd != null)
            m_Cmd.Release();

        if (m_ShadowMap != null)
            m_ShadowMap.Release();
    }

    private void InitShadowBuffer()
    {
        ShadowBuffer._ShadowMap = Shader.PropertyToID("_ShadowMap");
        ShadowBuffer._WorldToShadowAtlas = Shader.PropertyToID("_WorldToShadowAtlas");
    }

    private Vector3 CalLineIntersect(Vector3 origin0, Vector3 dir0, Vector3 origin1, Vector3 dir1)
    {
        Vector3 origin1To0 = origin1 - origin0;
        Vector3 vDenominator = Vector3.Cross(dir0, dir1);
        Vector3 vNumerator = Vector3.Cross(origin1To0, dir1);
        float t = vNumerator.magnitude / vDenominator.magnitude;

        return origin0 + t * dir0;
    }

    static internal class ShadowBuffer
    {
        static public int _ShadowMap;
        static public int _WorldToShadowAtlas;
    }



    private Camera m_ViewCamera;
    private Light m_Light;
    private int m_nShadowMapSize;
    private bool m_bIsStable;
    private float[] m_Splits;
    private int m_nCascadeCount;
    private CommandBuffer m_Cmd;
    private RenderTexture m_ShadowMap;
    private Matrix4x4[] m_ShadowViewMatrices;
    private Matrix4x4[] m_ShadowProjMatrices;
    private Material m_ShadowCasterMat;
    private Vector3[] m_NearCorner;
    private Vector3[] m_FarCorner;
    private Vector4[] m_CullingSphere;
}
