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
    public static class UniformBufferHelper
    {
        /// <summary>
        /// packs the given transform matrix into a row_major 4x3 matrix for packing in a uniform buffer
        /// <para>Will only work reliably if the uniform block has <code>layout (std140, row_major)</code></para>
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix3X4<float> Pack3dTransformMatrix(in Matrix4x4 transform)
        {
            Matrix3X4<float> mtx = default;

            Pack3dTransformMatrix(transform, ref mtx);
            return mtx;
        }

        /// <summary>
        /// packs the given transform matrix into a row_major 4x3 matrix for packing in a uniform buffer
        /// <para>Will only work reliably if the uniform block has <code>layout (std140, row_major)</code></para>
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Pack3dTransformMatrix(in Matrix4x4 transform, ref Matrix3X4<float> dest)
        {
            dest.M11 = transform.M11;
            dest.M21 = transform.M12;
            dest.M31 = transform.M13;

            dest.M12 = transform.M21;
            dest.M22 = transform.M22;
            dest.M32 = transform.M23;

            dest.M13 = transform.M31;
            dest.M23 = transform.M32;
            dest.M33 = transform.M33;

            dest.M14 = transform.M41;
            dest.M24 = transform.M42;
            dest.M34 = transform.M43;
        }

        /// <summary>
        /// unpacks the given row_major 4x3 matrix into a transform matrix
        /// <para>meant to be used with/after <see cref="Pack3dTransformMatrix(in Matrix4x4, ref Matrix3X4{float})"/></para>
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 Unpack3dTransformMatrix(in Matrix3X4<float> packed)
        {
            return new(
                    packed.M11, packed.M21, packed.M31, 0,
                    packed.M12, packed.M22, packed.M32, 0,
                    packed.M13, packed.M23, packed.M33, 0,
                    packed.M14, packed.M24, packed.M34, 1
                    );
        }



        /// <summary>
        /// packs the given transform matrix into a row_major 4x3 matrix for packing in a uniform buffer
        /// <para>Will only work reliably if the uniform block has <code>layout (std140, row_major)</code></para>
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix2X4<float> Pack2dTransformMatrix(in Matrix3x2 transform)
        {
            Matrix2X4<float> mtx = default;

            Pack2dTransformMatrix(transform, ref mtx);
            return mtx;
        }

        /// <summary>
        /// packs the given transform matrix into a row_major 4x2 matrix for packing in a uniform buffer
        /// <para>Will only work reliably if the uniform block has <code>layout (std140, row_major)</code></para>
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Pack2dTransformMatrix(in Matrix3x2 transform, ref Matrix2X4<float> dest)
        {
            dest.M11 = transform.M11;
            dest.M21 = transform.M12;

            dest.M12 = transform.M21;
            dest.M22 = transform.M22;

            dest.M13 = transform.M31;
            dest.M23 = transform.M32;

            //padding for alignment
            dest.M14 = 0;
            dest.M24 = 0;
        }

        /// <summary>
        /// unpacks the given row_major 4x2 matrix into a transform matrix
        /// <para>meant to be used with/after <see cref="Pack2dTransformMatrix(in Matrix3x2, ref Matrix2X4{float})"/></para>
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix3x2 Unpack2dTransformMatrix(in Matrix2X4<float> packed)
        {
            return new(
                    packed.M11, packed.M21,
                    packed.M12, packed.M22,
                    packed.M13, packed.M23
                    );
        }
    }
}
