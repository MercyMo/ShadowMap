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
        RenderShadowMap();
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

}
