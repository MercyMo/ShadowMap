using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ShadowMapDrawer : MonoBehaviour
{
    public int shadowMapSize;
    public Camera viewCamera;
    public Light mainLight;
    public Renderer[] shadowCasters;
    
    private ShadowSpliter m_Spliter;
    private CommandBuffer m_Cmd;
    private RenderTexture m_ShadowMap;
    private Material m_ShadowCasterMat;


    public void Awake()
    {
        if (viewCamera && mainLight)
        {
            m_Spliter = new ShadowSpliter(viewCamera, mainLight);

            m_Cmd = new CommandBuffer();
            m_Cmd.name = "DrawShadowMap";

            m_ShadowMap = new RenderTexture(shadowMapSize, shadowMapSize, 32, RenderTextureFormat.Shadowmap);
            m_ShadowMap.filterMode = FilterMode.Bilinear;

            m_ShadowCasterMat = new Material(Shader.Find("Unlit/ShadowCaster"));
            m_ShadowCasterMat.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    public void Update()
    {
        //RenderShadowMap();
        RenderCascadeShadowMap();
    }

    private void RenderShadowMap()
    {
        if (m_Spliter != null)
        {
            m_Cmd.SetRenderTarget(m_ShadowMap, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            m_Cmd.ClearRenderTarget(true, false, Color.clear);

            Vector4 sphere = m_Spliter.CalculateDirectionalViewAndProjMatrix(0, 1, Vector3.one, shadowMapSize, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix);
            m_Cmd.SetViewProjectionMatrices(viewMatrix, projMatrix);
            foreach(var renderer in shadowCasters)
            {
                m_Cmd.DrawRenderer(renderer, m_ShadowCasterMat);
            }
            Matrix4x4 correct = Matrix4x4.identity;
            correct.m00 = correct.m11 = correct.m22 = correct.m03 = correct.m13 = correct.m23 = 0.5f;
            Matrix4x4 world2Shadow = projMatrix * viewMatrix;
            if (SystemInfo.usesReversedZBuffer)
            {
                Vector4 tmp = world2Shadow.GetRow(2);
                world2Shadow.SetRow(2, -tmp);
            }
            world2Shadow = correct * world2Shadow;
            m_Cmd.SetGlobalMatrix("_ShadowMatrix", world2Shadow);
            m_Cmd.SetGlobalTexture("_ShadowMapCus", m_ShadowMap);
            float fTexelSize = 2.0f * sphere.w / shadowMapSize;
            m_Cmd.SetGlobalFloat("_Bias", fTexelSize * 1.4142136f);
            Graphics.ExecuteCommandBuffer(m_Cmd);
            m_Cmd.Clear();
        }
    }

    private void RenderCascadeShadowMap()
    {
        if (m_Spliter != null)
        {
            m_Cmd.SetRenderTarget(m_ShadowMap, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            m_Cmd.ClearRenderTarget(true, false, Color.clear);
            Vector3 cascadeRatio = new Vector3(0.1f, 0.2f, 0.3f);
            for(int i = 0; i < 4; i++)
            {
                Vector4 sphere = m_Spliter.CalculateDirectionalViewAndProjMatrix(i, 4, cascadeRatio, shadowMapSize,
                    out Matrix4x4 mViewMatrix, out Matrix4x4 mProjMatrix);

                Vector2 offset = new Vector2(i % 2, Mathf.Floor(i / 2));

                m_Cmd.SetViewport(new Rect(offset.x * 1024, offset.y * 1024, 1024, 1024));
                m_Cmd.SetViewProjectionMatrices(mViewMatrix, mProjMatrix);
                foreach(var renderer in shadowCasters)
                {
                    m_Cmd.DrawRenderer(renderer, m_ShadowCasterMat);
                }
                Matrix4x4 world2Shadow = mProjMatrix * mViewMatrix;
                if (SystemInfo.usesReversedZBuffer)
                {
                    Vector4 zAxis = world2Shadow.GetRow(2);
                    world2Shadow.SetRow(2, -zAxis);
                }
                Matrix4x4 mShadow2Atlas = Matrix4x4.identity;
                mShadow2Atlas.m00 = mShadow2Atlas.m11 = mShadow2Atlas.m22 = mShadow2Atlas.m03 = mShadow2Atlas.m13 = mShadow2Atlas.m23 = 0.5f;
                mShadow2Atlas = Matrix4x4.TRS(new Vector3(offset.x * 0.5f, offset.y * 0.5f, 0.0f),
                    Quaternion.identity,
                    new Vector3(0.5f, 0.5f, 1.0f)) * mShadow2Atlas;
                m_World2Shadows[i] = mShadow2Atlas * world2Shadow;

                float fTexelSize = 2.0f * sphere.w / 1024.0f;
                m_Bias[i] = fTexelSize * 1.4142136f;

                sphere.w = sphere.w * sphere.w;
                m_CullingSpheres[i] = sphere;
            }

            m_Cmd.SetGlobalTexture("_ShadowMapCus", m_ShadowMap);
            m_Cmd.SetGlobalFloatArray("_BiasArray", m_Bias);
            m_Cmd.SetGlobalMatrixArray("_WorldToShadowAtlas", m_World2Shadows);
            m_Cmd.SetGlobalVectorArray("_CullingSphere", m_CullingSpheres);
            Graphics.ExecuteCommandBuffer(m_Cmd);
            m_Cmd.Clear();
        }


    }

    private Vector4[] m_CullingSpheres = new Vector4[4];
    private float[] m_Bias = new float[4];
    private Matrix4x4[] m_World2Shadows = new Matrix4x4[4];
}
