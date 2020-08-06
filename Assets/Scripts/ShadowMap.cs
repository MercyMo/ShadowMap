using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ShadowMap : MonoBehaviour
{
    public Camera viewCamera;
    public Light mainLight;

    public Renderer[] m_ShadowCaster;

    private Matrix4x4 m_ViewMatrix = Matrix4x4.identity;
    private Matrix4x4 m_ProjMatrix = Matrix4x4.identity;

    private Vector3[] m_NearCorner = new Vector3[4];
    private Vector3[] m_FarCorner = new Vector3[4];

    private RenderTexture m_ShadowMap;
    private CommandBuffer m_Cmd;

    private Material m_DepthMat;

    private void Awake()
    {
        m_ShadowMap = new RenderTexture(2048, 2048, 32, RenderTextureFormat.Shadowmap);
        m_Cmd = new CommandBuffer();
        m_Cmd.name = "ShadowPass";
        if (m_DepthMat == null)
        {
            m_DepthMat = new Material(Shader.Find("Unlit/ShadowCaster"));
        }
    }

    private bool UpdateFrame()
    {
        if (viewCamera && mainLight)
        {
            Rect viewport = new Rect(0, 0, 1, 1);
            viewCamera.CalculateFrustumCorners(viewport, viewCamera.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, m_NearCorner);
            viewCamera.CalculateFrustumCorners(viewport, viewCamera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, m_FarCorner);

            // view => world
            for (int i = 0; i < 4; i++)
            {
                m_NearCorner[i] = viewCamera.transform.TransformPoint(m_NearCorner[i]);
                m_FarCorner[i] = viewCamera.transform.TransformPoint(m_FarCorner[i]);
            }

            Matrix4x4 world2Light = m_ViewMatrix;
            Matrix4x4 light2World = world2Light.inverse;

            // world => light
            for (int i = 0; i < 4; i++)
            {
                m_NearCorner[i] = world2Light.MultiplyPoint(m_NearCorner[i]);
                m_FarCorner[i] = world2Light.MultiplyPoint(m_FarCorner[i]);
            }

            // Find Min & Max
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                min = Vector3.Min(min, Vector3.Min(m_NearCorner[i], m_FarCorner[i]));
                max = Vector3.Max(max, Vector3.Max(m_NearCorner[i], m_FarCorner[i]));
            }

            Vector3 center = 0.5f * (min + max);
            center = light2World.MultiplyPoint(center);
            m_ViewMatrix = Matrix4x4.LookAt(center, center + mainLight.transform.forward, mainLight.transform.up).inverse;
            m_ProjMatrix = Matrix4x4.Ortho(min.x, max.x, min.y, max.y, min.z, max.z);

            m_NearCorner[0] = new Vector3(min.x, min.y, min.z);
            m_NearCorner[1] = new Vector3(max.x, min.y, min.z);
            m_NearCorner[2] = new Vector3(max.x, max.y, min.z);
            m_NearCorner[3] = new Vector3(min.x, max.y, min.z);

            m_FarCorner[0] = new Vector3(min.x, min.y, max.z);
            m_FarCorner[1] = new Vector3(max.x, min.y, max.z);
            m_FarCorner[2] = new Vector3(max.x, max.y, max.z);
            m_FarCorner[3] = new Vector3(min.x, max.y, max.z);

            // Light => world
            for (int i = 0; i < 4; i++)
            {
                m_NearCorner[i] = light2World.MultiplyPoint(m_NearCorner[i]);
                m_FarCorner[i] = light2World.MultiplyPoint(m_FarCorner[i]);
            }

            return true;
        }
        else
            return false;
    }

    private void OnDrawGizmos()
    {
        bool bSuccessUpdate = true;
        if (!Application.isPlaying)
        {
            bSuccessUpdate = UpdateFrame();
        }
        
        if (bSuccessUpdate)
        {
            Gizmos.color = Color.green;

            for(int i = 0; i < 4; i++)
            {
                int nNext = (i + 1) % 4;

                Gizmos.DrawLine(m_NearCorner[i], m_NearCorner[nNext]);
                Gizmos.DrawLine(m_FarCorner[i], m_FarCorner[nNext]);

                Gizmos.DrawLine(m_NearCorner[i], m_FarCorner[i]);
            }
        }
    }

    private void Update()
    {
        UpdateFrame();
        m_Cmd.SetRenderTarget(m_ShadowMap, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        m_Cmd.ClearRenderTarget(true, true, Color.clear);
        Matrix4x4 viewMatrix = m_ViewMatrix;
        Vector4 zAxis = viewMatrix.GetRow(2);
        viewMatrix.SetRow(2, -zAxis);
        m_Cmd.SetViewProjectionMatrices(viewMatrix, m_ProjMatrix);
        foreach(var renderer in m_ShadowCaster)
        {
            m_Cmd.DrawRenderer(renderer, m_DepthMat);
        }

        m_Cmd.SetGlobalTexture("_ShadowMap", m_ShadowMap);
        Graphics.ExecuteCommandBuffer(m_Cmd);
        m_Cmd.Clear();
    }

    private void OnDestroy()
    {
        //RenderTexture.Release(m_ShadowMap);
        m_ShadowMap.Release();
        if (m_Cmd != null)
            m_Cmd.Release();
    }
}
