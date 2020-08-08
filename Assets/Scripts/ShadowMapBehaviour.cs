using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;




public class ShadowMapBehaviour : MonoBehaviour
{
    public Camera viewCamera;
    public Light mainLight;
    public int shadowmapSize = 2048;
    //public int cascadeCount = 4;
    //public Vector4 cascadeRatio = new Vector4(0.067f, 0.133f, 0.267f, 0.533f);
    public XE_PROJECT_TYPE projectType = XE_PROJECT_TYPE.emSPHERE;
    public Renderer[] m_ShadowCaster;
    
    private Matrix4x4 m_ShadowViewMatrix = Matrix4x4.identity;
    private Matrix4x4 m_ShadowProjMatrix = Matrix4x4.identity;

    private Vector3[] m_NearCorner = new Vector3[4];
    private Vector3[] m_FarCorner = new Vector3[4];

    private RenderTexture m_ShadowMap;
    private CommandBuffer m_Cmd;
    private Material m_DepthMat;

    private float m_fRadius;
    private Vector3 m_Center;

   

    private void Awake()
    {
        m_ShadowMap = new RenderTexture(shadowmapSize, shadowmapSize, 32, RenderTextureFormat.Shadowmap);
        m_ShadowMap.filterMode = FilterMode.Bilinear;
        
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
            ValidateCascadeRatio();

            Rect viewport = new Rect(0, 0, 1, 1);
            viewCamera.CalculateFrustumCorners(viewport, viewCamera.nearClipPlane, Camera.MonoOrStereoscopicEye.Mono, m_NearCorner);
            viewCamera.CalculateFrustumCorners(viewport, viewCamera.farClipPlane, Camera.MonoOrStereoscopicEye.Mono, m_FarCorner);


            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;

            if (projectType == XE_PROJECT_TYPE.emSPHERE)
            {
                #region Sphere
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

                m_Center = viewCamera.transform.TransformPoint(circumcenter);
                m_fRadius = fRadius;

                float x = Mathf.Ceil(Vector3.Dot(m_Center, mainLight.transform.up) * shadowmapSize / fRadius) * fRadius / shadowmapSize;
                float y = Mathf.Ceil(Vector3.Dot(m_Center, mainLight.transform.right) * shadowmapSize / fRadius) * fRadius / shadowmapSize;

                m_Center = mainLight.transform.up * x + mainLight.transform.right * y + mainLight.transform.forward * Vector3.Dot(m_Center, mainLight.transform.forward);
                min = Vector3.one * (-fRadius);
                max = Vector3.one * fRadius;

                #endregion
            }
            else if (projectType == XE_PROJECT_TYPE.emAABB)
            {
                #region AABB
                // view => world
                for (int i = 0; i < 4; i++)
                {
                    m_NearCorner[i] = viewCamera.transform.TransformPoint(m_NearCorner[i]);
                    m_FarCorner[i] = viewCamera.transform.TransformPoint(m_FarCorner[i]);
                }

                Matrix4x4 world2Light = m_ShadowViewMatrix;
                Matrix4x4 light2World = world2Light.inverse;

                // world => light
                for(int i = 0; i < 4; i++)
                {
                    m_NearCorner[i] = world2Light.MultiplyPoint(m_NearCorner[i]);
                    m_FarCorner[i] = world2Light.MultiplyPoint(m_FarCorner[i]);
                }

                // Find Min & Max
                for(int i = 0; i < 4; i++)
                {
                    min = Vector3.Min(m_FarCorner[i], Vector3.Min(m_NearCorner[i], min));
                    max = Vector3.Max(m_FarCorner[i], Vector3.Max(m_NearCorner[i], max));
                }

                m_Center = 0.5f * (min + max);
                m_Center = light2World.MultiplyPoint(m_Center);// light to world

                m_NearCorner[0] = new Vector3(min.x, min.y, min.z);
                m_NearCorner[1] = new Vector3(max.x, min.y, min.z);
                m_NearCorner[2] = new Vector3(max.x, max.y, min.z);
                m_NearCorner[3] = new Vector3(min.x, max.y, min.z);

                m_FarCorner[0] = new Vector3(min.x, min.y, max.z);
                m_FarCorner[1] = new Vector3(max.x, min.y, max.z);
                m_FarCorner[2] = new Vector3(max.x, max.y, max.z);
                m_FarCorner[3] = new Vector3(min.x, max.y, max.z);

                // AABB in world space
                for(int i = 0; i < 4; i++)
                {
                    m_NearCorner[i] = light2World.MultiplyPoint(m_NearCorner[i]);
                    m_FarCorner[i] = light2World.MultiplyPoint(m_FarCorner[i]);
                }
                #endregion
            }

            // Remove shimmering edge
            m_ShadowViewMatrix = Matrix4x4.LookAt(m_Center, m_Center + mainLight.transform.forward, mainLight.transform.up).inverse;
            m_ShadowProjMatrix = Matrix4x4.Ortho(min.x, max.x, min.y, max.y, min.z, max.z);

            Vector3 shadowOrigin = (m_ShadowProjMatrix * m_ShadowViewMatrix).MultiplyPoint(Vector3.zero);
            shadowOrigin = shadowOrigin * shadowmapSize / 2.0f;

            Vector3 roundedOrigin = new Vector3(Mathf.Round(shadowOrigin.x), Mathf.Round(shadowOrigin.y), Mathf.Round(shadowOrigin.z));
            Vector3 roundOffset = roundedOrigin - shadowOrigin;
            roundOffset = roundOffset * 2.0f / shadowmapSize;
            roundOffset.z = 0.0f;
            m_ShadowProjMatrix = Matrix4x4.Translate(roundOffset) * m_ShadowProjMatrix;
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
            if (projectType == XE_PROJECT_TYPE.emAABB)
            {
                Gizmos.color = Color.green;
                for (int i = 0; i < 4; i++)
                {
                    int nNext = (i + 1) % 4;

                    Gizmos.DrawLine(m_NearCorner[i], m_NearCorner[nNext]);
                    Gizmos.DrawLine(m_FarCorner[i], m_FarCorner[nNext]);
                    Gizmos.DrawLine(m_NearCorner[i], m_FarCorner[i]);
                }
            }
            else if (projectType == XE_PROJECT_TYPE.emSPHERE)
            {
                Gizmos.color = Color.green;
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(m_Center, m_fRadius);
            }
        } 
    }

    private void Update()
    {
        UpdateFrame();
        m_Cmd.SetRenderTarget(m_ShadowMap, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        m_Cmd.ClearRenderTarget(true, true, Color.clear);
        Matrix4x4 viewMatrix = m_ShadowViewMatrix;
        Vector4 zAxis = viewMatrix.GetRow(2);
        viewMatrix.SetRow(2, -zAxis);
        m_Cmd.SetViewProjectionMatrices(viewMatrix, m_ShadowProjMatrix);
        foreach(var renderer in m_ShadowCaster)
        {
            m_Cmd.DrawRenderer(renderer, m_DepthMat);
        }

        Matrix4x4 correct = Matrix4x4.identity;
        correct.m00 = correct.m11 = correct.m22 = correct.m03 = correct.m13 = correct.m23 = 0.5f;
        Matrix4x4 world2Shadow = m_ShadowProjMatrix * viewMatrix;
        if (SystemInfo.usesReversedZBuffer)
        {
            world2Shadow.m20 *= (-1);
            world2Shadow.m21 *= (-1);
            world2Shadow.m22 *= (-1);
            world2Shadow.m23 *= (-1);
        }
        world2Shadow = correct * world2Shadow;
        m_Cmd.SetGlobalMatrix("_ShadowMatrix", world2Shadow);
        m_Cmd.SetGlobalTexture("_ShadowMapCus", m_ShadowMap);
        float fTexelSize = 2.0f * m_fRadius / shadowmapSize;
        m_Cmd.SetGlobalFloat("_Bias", fTexelSize * 1.4142136f);
        Graphics.ExecuteCommandBuffer(m_Cmd);
        m_Cmd.Clear();
    }

    private void OnDestroy()
    {
        m_ShadowMap.Release();
        if (m_Cmd != null)
            m_Cmd.Release();
    }

    private Vector3 CalLineIntersect(Vector3 origin0, Vector3 dir0, Vector3 origin1, Vector3 dir1)
    {
        Vector3 origin1To0 = origin1 - origin0;  
        Vector3 vDenominator = Vector3.Cross(dir0, dir1);
        Vector3 vNumerator = Vector3.Cross(origin1To0, dir1);
        float t = vNumerator.magnitude / vDenominator.magnitude;

        return origin0 + t * dir0;
    }

    private void ValidateCascadeRatio()
    {
        //float fLastRatio = 1.0f - cascadeRatio.x - cascadeRatio.y - cascadeRatio.z;
        //cascadeRatio.w = fLastRatio;
    }
}
