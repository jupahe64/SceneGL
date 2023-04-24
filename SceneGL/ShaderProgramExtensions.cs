using SceneGL.GLHelpers;
using SceneGL.GLWrappers;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace SceneGL
{
    public struct ProgramUniformScope : IDisposable
    {
        private readonly GL gl;
        private readonly uint uniformBlockCount;
        private readonly uint textureUnitCount;

        internal ProgramUniformScope(GL gl, uint uniformBlockCount, uint textureUnitCount)
        {
            this.gl = gl;
            this.uniformBlockCount = uniformBlockCount;
            this.textureUnitCount = textureUnitCount;
        }

        public void Dispose()
        {
            for (uint i = 0; i < uniformBlockCount; i++)
            {
                gl.BindBufferBase(BufferTargetARB.UniformBuffer, i, 0);
            }

            for (uint i = 0; i < textureUnitCount; i++)
            {
                gl.BindTextureUnit(i, 0);
                gl.BindSampler(i, 0);
            }

            gl.UseProgram(0);
        }
    }

    public static class ShaderProgramExtensions
    {
        public static bool TryUse(this ShaderProgram self, GL gl, 
            string? instanceBlockBinding, ShaderParams[] shaderParams, 
            out ProgramUniformScope scope, out uint? instanceBlockIndex)
        {
            instanceBlockIndex = null;

            if (!self.TryUse(gl, out uint program))
            {
                scope = new ProgramUniformScope(gl, 0, 0);
                return false;
            }

            uint nextFreeUnit = 0;
            uint nextFreeBlockBinding = 0;


            for (int i = 0; i < shaderParams.Length; i++)
            {
                var (samplers, uniformBuffers) = shaderParams[i].GetBindings();

                for (int j = 0; j < uniformBuffers.Count; j++)
                {
                    (string binding, BufferRange bufferRange) = uniformBuffers[j];

                    if (!self.TryGetUniformBlockIndex(binding, out uint blockIndex))
                        continue;

                    gl.UniformBlockBinding(program, blockIndex, nextFreeBlockBinding);
                    var (buffer, offset, size) = bufferRange;

                    gl.BindBufferRange(BufferTargetARB.UniformBuffer, nextFreeBlockBinding, buffer, offset, size);

                    nextFreeBlockBinding++;
                }

                for (int j = 0; j < samplers.Count; j++)
                {
                    (string name, uint sampler, uint texture) = samplers[j];

                    if (!self.TryGetUniformLoc(name, out int loc))
                        continue;

                    gl.Uniform1(loc, (int)nextFreeUnit);
                    gl.BindTextureUnit(nextFreeUnit, texture);
                    gl.BindSampler(nextFreeUnit, sampler);
                    nextFreeUnit++;
                }
            }


            if (instanceBlockBinding is not null)
            {
                if (self.TryGetUniformBlockIndex(instanceBlockBinding, out uint blockIndex))
                {
                    gl.UniformBlockBinding(program, blockIndex, nextFreeBlockBinding);

                    instanceBlockIndex = nextFreeBlockBinding;
                }
            }

            scope = new ProgramUniformScope(gl, nextFreeBlockBinding, nextFreeUnit);

            return true;
        }
    }
}
