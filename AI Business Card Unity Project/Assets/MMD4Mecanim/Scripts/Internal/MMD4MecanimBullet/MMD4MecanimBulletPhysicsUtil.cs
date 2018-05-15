using UnityEngine;
using System.Collections;
using BulletXNA;
using BulletXNA.BulletCollision;
using BulletXNA.BulletDynamics;
using BulletXNA.LinearMath;

public class MMD4MecanimBulletPhysicsUtil
{
    public class SimpleMotionState : IMotionState
    {
        public SimpleMotionState()
            : this(IndexedMatrix.Identity)
        {
        }

        public SimpleMotionState(IndexedMatrix startTrans)
        {
            m_graphicsWorldTrans = startTrans;
        }

        public SimpleMotionState(ref IndexedMatrix startTrans)
        {
            m_graphicsWorldTrans = startTrans;
        }
		
        public virtual void GetWorldTransform(out IndexedMatrix centerOfMassWorldTrans)
        {
            centerOfMassWorldTrans = m_graphicsWorldTrans;
        }

        public virtual void SetWorldTransform(IndexedMatrix centerOfMassWorldTrans)
        {
            SetWorldTransform(ref centerOfMassWorldTrans);
        }

        public virtual void SetWorldTransform(ref IndexedMatrix centerOfMassWorldTrans)
        {
            m_graphicsWorldTrans = centerOfMassWorldTrans;
        }

        public virtual void Rotate(IndexedQuaternion iq)
        {
            IndexedMatrix im = IndexedMatrix.CreateFromQuaternion(iq);
            im._origin = m_graphicsWorldTrans._origin;
            SetWorldTransform(ref im);
        }

        public virtual void Translate(IndexedVector3 v)
        {
            m_graphicsWorldTrans._origin += v;
        }

        public IndexedMatrix m_graphicsWorldTrans;
    }
	
    public class KinematicMotionState : IMotionState
    {
        public KinematicMotionState()
        {
			m_graphicsWorldTrans = IndexedMatrix.Identity;
        }

        public KinematicMotionState(ref IndexedMatrix startTrans)
        {
            m_graphicsWorldTrans = startTrans;
        }

        public virtual void GetWorldTransform(out IndexedMatrix centerOfMassWorldTrans)
        {
            centerOfMassWorldTrans = m_graphicsWorldTrans;
        }

        public virtual void SetWorldTransform(IndexedMatrix centerOfMassWorldTrans)
        {
			// Nothing.
        }

        public virtual void SetWorldTransform(ref IndexedMatrix centerOfMassWorldTrans)
        {
			// Nothing.
        }

        public virtual void Rotate(IndexedQuaternion iq)
        {
			// Nothing.
        }

        public virtual void Translate(IndexedVector3 v)
        {
			// Nothing.
        }

        public IndexedMatrix m_graphicsWorldTrans;
    }

	public static IndexedMatrix MakeIndexedMatrix( ref Vector3 position, ref Quaternion rotation )
	{
		IndexedQuaternion indexedQuaternion = new IndexedQuaternion(ref rotation);
		IndexedBasisMatrix indexedBasisMatrix = new IndexedBasisMatrix(indexedQuaternion);
		IndexedVector3 origin = new IndexedVector3(ref position);
		return new IndexedMatrix(indexedBasisMatrix, origin);
	}

	public static void MakeIndexedMatrix( ref IndexedMatrix matrix, ref Vector3 position, ref Quaternion rotation )
	{
		matrix.SetRotation( new IndexedQuaternion(ref rotation) );
		matrix._origin = position;
	}

    public static void CopyBasis(ref IndexedBasisMatrix m0, ref Matrix4x4 m1)
    {
        m0._el0.X = m1.m00;
        m0._el1.X = m1.m10;
        m0._el2.X = m1.m20;

        m0._el0.Y = m1.m01;
        m0._el1.Y = m1.m11;
        m0._el2.Y = m1.m21;

        m0._el0.Z = m1.m02;
        m0._el1.Z = m1.m12;
        m0._el2.Z = m1.m22;
    }

    public static void CopyBasis(ref Matrix4x4 m0, ref IndexedBasisMatrix m1)
    {
        m0.m00 = m1._el0.X;
        m0.m10 = m1._el1.X;
        m0.m20 = m1._el2.X;

        m0.m01 = m1._el0.Y;
        m0.m11 = m1._el1.Y;
        m0.m21 = m1._el2.Y;

        m0.m02 = m1._el0.Z;
        m0.m12 = m1._el1.Z;
        m0.m22 = m1._el2.Z;
    }

    public static void CopyBasis(ref Matrix4x4 m0, ref Matrix4x4 m1)
    {
        m0.m00 = m1.m00;
        m0.m10 = m1.m10;
        m0.m20 = m1.m20;

        m0.m01 = m1.m01;
        m0.m11 = m1.m11;
        m0.m21 = m1.m21;

        m0.m02 = m1.m02;
        m0.m12 = m1.m12;
        m0.m22 = m1.m22;
    }

    public static void SetRotation(ref Matrix4x4 m, ref Quaternion q)
    {
        float d = q.x * q.x + q.y + q.y + q.z * q.z + q.w * q.w;
        float s = (d > Mathf.Epsilon) ? (2.0f / d) : 0.0f;
        float xs = q.x * s, ys = q.y * s, zs = q.z * s;
        float wx = q.w * xs, wy = q.w * ys, wz = q.w * zs;
        float xx = q.x * xs, xy = q.x * ys, xz = q.x * zs;
        float yy = q.y * ys, yz = q.y * zs, zz = q.z * zs;
        // el0
        m.m00 = 1.0f - (yy + zz);
        m.m01 = xy - wz;
        m.m02 = xz + wy;
        // el1
        m.m10 = xy + wz;
        m.m11 = 1.0f - (xx + zz);
        m.m12 = yz - wx;
        // el2
        m.m20 = xz - wy;
        m.m21 = yz + wx;
        m.m22 = 1.0f - (xx + yy);
    }

    public static void SetPosition(ref Matrix4x4 m, Vector3 v)
    {
        m.m03 = v.x;
        m.m13 = v.y;
        m.m23 = v.z;
    }

    public static void SetPosition(ref Matrix4x4 m, ref Vector3 v)
    {
        m.m03 = v.x;
        m.m13 = v.y;
        m.m23 = v.z;
    }

    public static Vector3 GetPosition(ref Matrix4x4 m)
    {
        return new Vector3(m.m03, m.m13, m.m23);
    }

	/*
    public static Quaternion GetRotation(ref Matrix4x4 m)
    {
        Quaternion q = new Quaternion();
        q.w = Mathf.Sqrt(Mathf.Max(0, 1 + m[0, 0] + m[1, 1] + m[2, 2])) / 2;
        q.x = Mathf.Sqrt(Mathf.Max(0, 1 + m[0, 0] - m[1, 1] - m[2, 2])) / 2;
        q.y = Mathf.Sqrt(Mathf.Max(0, 1 - m[0, 0] + m[1, 1] - m[2, 2])) / 2;
        q.z = Mathf.Sqrt(Mathf.Max(0, 1 - m[0, 0] - m[1, 1] + m[2, 2])) / 2;
        q.x *= Mathf.Sign(q.x * (m[2, 1] - m[1, 2]));
        q.y *= Mathf.Sign(q.y * (m[0, 2] - m[2, 0]));
        q.z *= Mathf.Sign(q.z * (m[1, 0] - m[0, 1]));
        return q;
    }
    */

	// from: Bullet Physics 2.79(C/C++)
	public static IndexedBasisMatrix EulerZYX( float eulerX, float eulerY, float eulerZ )
	{ 
		float ci = Mathf.Cos(eulerX);
		float cj = Mathf.Cos(eulerY);
		float ch = Mathf.Cos(eulerZ);
		float si = Mathf.Sin(eulerX);
		float sj = Mathf.Sin(eulerY);
		float sh = Mathf.Sin(eulerZ);
		float cc = ci * ch; 
		float cs = ci * sh; 
		float sc = si * ch; 
		float ss = si * sh;
		return new IndexedBasisMatrix(
				cj * ch, sj * sc - cs, sj * cc + ss,
		        cj * sh, sj * ss + cc, sj * cs - sc, 
		        -sj,      cj * si,      cj * ci);
	}

	public static IndexedBasisMatrix BasisRotationYXZ(ref Vector3 rotation)
    {
		IndexedBasisMatrix rx = EulerZYX( rotation.x, 0.0f, 0.0f );
		IndexedBasisMatrix ry = EulerZYX( 0.0f, rotation.y, 0.0f );
		IndexedBasisMatrix rz = EulerZYX( 0.0f, 0.0f, rotation.z );
        return ry * rx * rz; // Yaw-Pitch-Roll
    }
}
