using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Silk.NET.Maths;
using System.Runtime.CompilerServices;

namespace SceneGL.GLHelpers
{
    /// <summary>
    /// A Matrix4x3 packed as column major (row_major mat4x3 in glsl) for usage in efficiently packed uniform buffers
    /// </summary>
    public struct PackedMatrix4x3
    {
        public float M11;
        public float M21;
        public float M31;
        public float M41;
        public float M12;
        public float M22;
        public float M32;
        public float M42;
        public float M13;
        public float M23;
        public float M33;
        public float M43;

        public static implicit operator PackedMatrix4x3(Matrix4x4 transform) => FromTransformMatrix(in transform);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PackedMatrix4x3 FromTransformMatrix(in Matrix4x4 transform)
        {
            PackedMatrix4x3 mtx = default;

            PackTransformMatrix(transform, ref mtx);
            return mtx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PackTransformMatrix(in Matrix4x4 transform, ref PackedMatrix4x3 dest)
        {
            dest.M11 = transform.M11;
            dest.M12 = transform.M12;
            dest.M13 = transform.M13;
            dest.M21 = transform.M21;
            dest.M22 = transform.M22;
            dest.M23 = transform.M23;
            dest.M31 = transform.M31;
            dest.M32 = transform.M32;
            dest.M33 = transform.M33;
            dest.M41 = transform.M41;
            dest.M42 = transform.M42;
            dest.M43 = transform.M43;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matrix4x4 AsMatrix4x4()
        {
            Matrix4x4 mtx = default;
            UnpackTo(ref mtx);
            return mtx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnpackTo(ref Matrix4x4 dest)
        {
            dest.M11 = M11;
            dest.M12 = M12;
            dest.M13 = M13;
            dest.M21 = M21;
            dest.M22 = M22;
            dest.M23 = M23;
            dest.M31 = M31;
            dest.M32 = M32;
            dest.M33 = M33;
            dest.M41 = M41;
            dest.M42 = M42;
            dest.M43 = M43;
        }
    }

    /// <summary>
    /// A Matrix3x2 packed as column major (row_major mat3x2 in glsl) for usage in efficiently packed uniform buffers
    /// </summary>
    public struct PackedMatrix3x2
    {
        public float M11;
        public float M21;
        public float M31;
        private readonly uint PaddingRow1;
        public float M12;
        public float M22;
        public float M32;
        private readonly uint PaddingRow2;

        public static implicit operator PackedMatrix3x2(Matrix3x2 transform) => FromTransformMatrix(in transform);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PackedMatrix3x2 FromTransformMatrix(in Matrix3x2 transform)
        {
            PackedMatrix3x2 mtx = default;

            PackTransformMatrix(transform, ref mtx);
            return mtx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void PackTransformMatrix(in Matrix3x2 transform, ref PackedMatrix3x2 dest)
        {
            dest.M11 = transform.M11;
            dest.M12 = transform.M12;
            dest.M21 = transform.M21;
            dest.M22 = transform.M22;
            dest.M31 = transform.M31;
            dest.M32 = transform.M32;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Matrix3x2 AsMatrix4x4()
        {
            Matrix3x2 mtx = default;
            UnpackTo(ref mtx);
            return mtx;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnpackTo(ref Matrix3x2 dest)
        {
            dest.M11 = M11;
            dest.M12 = M12;
            dest.M21 = M21;
            dest.M22 = M22;
            dest.M31 = M31;
            dest.M32 = M32;
        }
    }
}
