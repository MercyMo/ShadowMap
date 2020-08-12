using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShadowSpliter
{
    public ShadowSpliter(Camera viewCamera, Light light)
    {
        m_ViewCamera = viewCamera;
        m_Light = light;
        m_vNearCorner = new Vector3[4];
        m_vFarCorner = new Vector3[4];
    }

    public Vector4 CalculateDirectionalViewAndProjMatrix(int nSplitIndex, int nSplitCount, Vector3 vSplitRatio, int nShowdowMapSize,
        out Matrix4x4 mViewMatrix, out Matrix4x4 mProjMatrix)
    {
        float fNeadPlane = 0.0f;
        float fFarPlane = 0.0f;
        Vector4 vDistance = Vector4.one * (m_ViewCamera.farClipPlane - m_ViewCamera.nearClipPlane);
        Vector4 vRatio = new Vector4(vSplitRatio.x, vSplitRatio.y, vSplitRatio.z);

        Vector4 vNearRatio = Vector4.zero;
        Vector4 vFarRatio = Vector4.zero;
        for (int i = 0; i < nSplitIndex; i++)
            vNearRatio[i] = vSplitRatio[i];
        if (nSplitIndex == nSplitCount - 1)
        {
            vFarRatio = new Vector4(1, 0, 0, 0);
       
        }
        else
        {
            for (int i = 0; i < nSplitIndex + 1; i++)
                vFarRatio[i] = vSplitRatio[i];
        }
        

        fNeadPlane = Vector4.Dot(vNearRatio, vDistance) + m_ViewCamera.nearClipPlane;
        fFarPlane = Vector4.Dot(vFarRatio, vDistance) + m_ViewCamera.nearClipPlane;

        Rect viewport = new Rect(0, 0, 1, 1);
        // View Space
        m_ViewCamera.CalculateFrustumCorners(viewport, fNeadPlane, Camera.MonoOrStereoscopicEye.Mono, m_vNearCorner);
        m_ViewCamera.CalculateFrustumCorners(viewport, fFarPlane, Camera.MonoOrStereoscopicEye.Mono, m_vFarCorner);

        Vector3 v0 = m_vNearCorner[0];
        Vector3 v1 = m_vFarCorner[0];
        Vector3 v2 = m_vFarCorner[2];

        
        Matrix4x4 light2World = Matrix4x4.LookAt(m_Light.transform.position, m_Light.transform.position + m_Light.transform.forward,
            m_Light.transform.up);
        Matrix4x4 world2Light = light2World.inverse;

        // View => World
        for (int i = 0; i < 4; i++)
        {
            m_vNearCorner[i] = m_ViewCamera.transform.TransformPoint(m_vNearCorner[i]);
            m_vFarCorner[i] = m_ViewCamera.transform.TransformPoint(m_vFarCorner[i]);
        }

        // World => Light
        for(int i = 0; i < 4; i++)
        {
            m_vNearCorner[i] = world2Light.MultiplyPoint(m_vNearCorner[i]);
            m_vFarCorner[i] = world2Light.MultiplyPoint(m_vFarCorner[i]);
        }

        float fFarPlaneDiagonalLen = Vector3.Distance(m_vFarCorner[0], m_vFarCorner[2]);
        float fAABBDiagonalLen = Vector3.Distance(m_vNearCorner[0], m_vFarCorner[2]);
        float fMaxLen = Mathf.Max(fFarPlaneDiagonalLen, fAABBDiagonalLen);

        Vector3 min = Vector3.one * float.MaxValue;
        Vector3 max = Vector3.one * float.MinValue;

        for(int i = 0; i < 4; i++)
        {
            min = Vector3.Min(min, Vector3.Min(m_vNearCorner[i], m_vFarCorner[i]));
            max = Vector3.Max(max, Vector3.Max(m_vNearCorner[i], m_vFarCorner[i]));
        }

        Vector3 center = 0.5f * (min + max);

        nShowdowMapSize = nSplitCount == 1 ? nShowdowMapSize : nShowdowMapSize / 2;

        float fWorldUnitTexel = fMaxLen / nShowdowMapSize;
        center.x = center.x / fWorldUnitTexel;
        center.x = Mathf.Floor(center.x);
        center.x = center.x * fWorldUnitTexel;

        center.y = center.y / fWorldUnitTexel;
        center.y = Mathf.Floor(center.y);
        center.y = center.y * fWorldUnitTexel;

        center.z = min.z;

        center = light2World.MultiplyPoint(center);

        mViewMatrix = Matrix4x4.LookAt(center, center + m_Light.transform.forward, m_Light.transform.up).inverse;
        Vector4 zAxis = mViewMatrix.GetRow(2);
        mViewMatrix.SetRow(2, -zAxis);
        float fHalfLen = 0.5F * fMaxLen;
        mProjMatrix = Matrix4x4.Ortho(-fHalfLen, fHalfLen, -fHalfLen, fHalfLen, 0, max.z - min.z);
        float fHalfLen2 = fHalfLen * fHalfLen;
        return new Vector4(center.x, center.y, center.z, Mathf.Sqrt(fHalfLen2 + fHalfLen2));
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
    private Vector3[] m_vNearCorner;
    private Vector3[] m_vFarCorner;
}
