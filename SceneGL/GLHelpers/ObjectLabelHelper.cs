using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL.GLHelpers
{
    public static class ObjectLabelHelper
    {
        public static void SetBufferLabel(GL gl, uint buffer, string label)
        {
            var prev = gl.GetInteger((GLEnum)GetPName.ArrayBufferBinding);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)prev);

            SetObjectLabel(gl, ObjectIdentifier.Buffer, buffer, label);
        }

        public static void SetShaderLabel(GL gl, uint shader, string label)
            => SetObjectLabel(gl, ObjectIdentifier.Shader, shader, label);

        public static void SetShaderProgramLabel(GL gl, uint program, string label)
            => SetObjectLabel(gl, ObjectIdentifier.Program, program, label);

        public static void SetSamplerLabel(GL gl, uint sampler, string label)
            => SetObjectLabel(gl, ObjectIdentifier.Sampler, sampler, label);

        public static void SetTextureLabel(GL gl, uint texture, string label)
            => SetObjectLabel(gl, ObjectIdentifier.Texture, texture, label);

        public static void SetFramebufferLabel(GL gl, uint frameBuffer, string label)
            => SetObjectLabel(gl, ObjectIdentifier.Framebuffer, frameBuffer, label);

        public static void SetObjectLabel(GL gl, ObjectIdentifier identifier, uint name, string label)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(label);

            int maxLength = gl.GetInteger((GLEnum)GetPName.MaxLabelLength);
            int clampedByteCount = Math.Min(bytes.Length, maxLength);

            if(clampedByteCount > 0)
                gl.ObjectLabel(identifier, name, (uint)clampedByteCount, bytes);
        }
    }
}
