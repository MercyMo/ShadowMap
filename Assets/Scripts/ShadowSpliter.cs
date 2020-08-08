using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShadowSpliter
{
    public ShadowSpliter(Camera viewCamera, Light light, Vector3 vCascadeRatio, int nCascadeCount)
    {
        m_ViewCamera = viewCamera;
        m_Light = light;
        m_vNearCorner = new Vector3[4];
        m_vFarCorner = new Vector3[4];
        m_mShadowProjMatrices = new Matrix4x4[4];
        m_mShadowViewMatrices = new Matrix4x4[4];
        for(int i = 0; i < 4; i++)
        {
            m_mShadowViewMatrices[i] = Matrix4x4.identity;
            m_mShadowProjMatrices[i] = Matrix4x4.identity;
        }
    }

    public Vector4 CalculateDirectionalViewAndProjMatrix(int nSplitIndex, int nSplitCount, Vector3 vSplitRatio, int nShowdowMapSize,
        out Matrix4x4 mViewMatrix, out Matrix4x4 mProjMatrix)
    {
        float fNeadPlane = 0.0f;
        float fFarPlane = 0.0f;
        Vector4 vDistance = Vector4.one * (m_ViewCamera.farClipPlane - m_ViewCamera.nearClipPlane);

        Vector4 vNearRatio = Vector4.zero;
        Vector4 vFarRatio = Vector4.zero;
        for (int i = 0; i < nSplitIndex; i++)
            vNearRatio[i] = vSplitRatio[i];
        for (int i = 0; i < nSplitCount + 1; i++)
            vFarRatio[i] = vSplitRatio[i];

        fNeadPlane = Vector4.Dot(vNearRatio, vDistance) + m_ViewCamera.nearClipPlane;
        fFarPlane = Vector4.Dot(vFarRatio, vDistance) + m_ViewCamera.nearClipPlane;

        Rect viewport = new Rect(0, 0, 1, 1);
        // View Space
        m_ViewCamera.CalculateFrustumCorners(viewport, fNeadPlane, Camera.MonoOrStereoscopicEye.Mono, m_vNearCorner);
        m_ViewCamera.CalculateFrustumCorners(viewport, fFarPlane, Camera.MonoOrStereoscopicEye.Mono, m_vFarCorner);

        Vector3 v0 = m_vNearCorner[0];
        Vector3 v1 = m_vFarCorner[0];
        Vector3 v2 = m_vFarCorner[2];

        Vector3 e01 = v1 - v0;
        Vector3 e02 = v2 - v0;
        Vector3 normal = Vector3.Cross(e01, e02).normalized;
        Vector3 center01 = v0 + 0.5f * e01;
        Vector3 center02 = v0 + 0.5f * e02;
        Vector3 dir01 = Vector3.Cross(normal, e01);
        Vector3 dir02 = Vector3.Cross(normal, e02);

        Vector3 center = CalLineIntersect(center01, dir01, center02, dir02);
        float fRadius = Vector3.Distance(v0, center);

        center = m_ViewCamera.transform.TransformPoint(center);
        float x = Mathf.Ceil(Vector3.Dot(center, m_Light.transform.up) * nShowdowMapSize / fRadius) * fRadius / nShowdowMapSize;
        float y = Mathf.Ceil(Vector3.Dot(center, m_Light.transform.right) * nShowdowMapSize / fRadius) * fRadius / nShowdowMapSize;

        center = m_Light.transform.up * x + m_Light.transform.right * y +
            m_Light.transform.forward * Vector3.Dot(center, m_Light.transform.forward);

        mViewMatrix = Matrix4x4.LookAt(center, center + m_Light.transform.forward, m_Light.transform.up);
        mProjMatrix = Matrix4x4.Ortho(-fRadius, fRadius, -fRadius, fRadius, -fRadius, fRadius);

        Vector3 vShadowOrigin = (mProjMatrix * mViewMatrix).MultiplyPoint(Vector3.zero);
        vShadowOrigin = vShadowOrigin * nShowdowMapSize * 0.5f;
        Vector3 roundedOrigin = new Vector3(Mathf.Round(vShadowOrigin.x), Mathf.Round(vShadowOrigin.y), Mathf.Round(vShadowOrigin.z));
        Vector3 roundedOffset = roundedOrigin - vShadowOrigin;
        roundedOffset = roundedOffset * 2.0f / nShowdowMapSize;
        roundedOffset.z = 0.0f;
        mProjMatrix = Matrix4x4.Translate(roundedOffset) * mProjMatrix;
        return new Vector4(center.x, center.y, center.z, fRadius);
    }

    private Vector3 CalLineIntersect(Vector3 origin0, Vector3 dir0, Vector3 origin1, Vector3 dir1)
    {
        Vector3 origin1To0 = origin1 - origin0;
        Vector3 vDenominator = Vector3.Cross(dir0, dir1);
        Vector3 vNumerator = Vector3.Cross(origin1To0, dir1);
        float t = vNumerator.magnitude / vDenominator.magnitude;

        return origin0 + t * dir0;
    }

    private Camera m_ViewCamera;
    private Light m_Light;
    private float[] m_fSplitDistances;
    private Vector3[] m_vNearCorner;
    private Vector3[] m_vFarCorner;
    //private Vector4[] m_vCullingSphere;
    private Matrix4x4[] m_mShadowViewMatrices;
    private Matrix4x4[] m_mShadowProjMatrices;
}
