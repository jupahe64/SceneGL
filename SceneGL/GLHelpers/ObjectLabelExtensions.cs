using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL.GLHelpers
{
    public static class ObjectLabelExtensions
    {
        public static void SetBufferLabel(this GL gl, uint buffer, string label)
        {
            var prev = gl.GetInteger((GLEnum)GetPName.ArrayBufferBinding);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)prev);

            gl.SetObjectLabel(ObjectIdentifier.Buffer, buffer, label);
        }

        public static void SetShaderLabel(this GL gl, uint shader, string label)
            => gl.SetObjectLabel(ObjectIdentifier.Shader, shader, label);

        public static void SetShaderProgramLabel(this GL gl, uint program, string label)
            => gl.SetObjectLabel(ObjectIdentifier.Program, program, label);

        public static void SetSamplerLabel(this GL gl, uint sampler, string label)
            => gl.SetObjectLabel(ObjectIdentifier.Sampler, sampler, label);

        public static void SetTextureLabel(this GL gl, uint texture, string label)
            => gl.SetObjectLabel(ObjectIdentifier.Texture, texture, label);

        public static void SetFramebufferLabel(this GL gl, uint frameBuffer, string label)
            => gl.SetObjectLabel(ObjectIdentifier.Framebuffer, frameBuffer, label);

        public static void SetObjectLabel(this GL gl, ObjectIdentifier identifier, uint name, string label)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(label);

            int maxLength = gl.GetInteger((GLEnum)GetPName.MaxLabelLength);

            if(bytes.Length > maxLength)
                throw new ArgumentException($"Object Label {label} exceeds the allowed number bytes ({maxLength})");

            gl.ObjectLabel(identifier, name, (uint)bytes.Length, bytes);
        }
    }
}
