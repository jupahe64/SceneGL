﻿using SceneGL.GLWrappers;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SceneGL
{
    public record struct BufferRange(uint Buffer, int Offset, uint Size)
    {
        public BufferRange(uint Buffer, uint Size) : 
            this(Buffer, 0, Size) { }
    }

    public record struct BufferBinding(string Name, BufferRange Data);
    public record struct SamplerBinding(string Binding, uint Sampler, uint Texture);

    public struct MaterialShaderScope : IDisposable
    {
        private readonly GL gl;
        private readonly uint uniformBlockCount;
        private readonly uint textureUnitCount;

        internal MaterialShaderScope(GL gl, uint uniformBlockCount, uint textureUnitCount)
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

    public class MaterialShader
    {
        private readonly ShaderProgram _program;
        private readonly string _sceneBlockBinding;
        private readonly string _materialBlockBinding;
        private readonly (string binding, uint count)? _instanceDataBlock;

        public uint? MaxInstanceCount => _instanceDataBlock?.count;

        public MaterialShader(ShaderProgram program, string sceneBlockBinding, string materialBlockBinding, 
            (string binding, uint count)? instanceDataBlock)
        {
            _program = program;
            _sceneBlockBinding = sceneBlockBinding;
            _materialBlockBinding = materialBlockBinding;
            _instanceDataBlock = instanceDataBlock;
        }

        public Material CreateMaterial(ReadOnlySpan<byte> data, SamplerBinding[] samplers)
            => new(this, data, samplers);

        public Material<TData> CreateMaterial<TData>(in TData data, SamplerBinding[] samplers)
            where TData : unmanaged
            => new(this, data, samplers);

        public bool TryUse(GL gl, BufferRange sceneData, BufferRange materialData, IReadOnlyList<SamplerBinding> materialSamplers,
            IReadOnlyList<BufferBinding>? otherUBOData,
            IReadOnlyList<SamplerBinding>? otherSamplers, out MaterialShaderScope scope, out uint? instanceBlockIndex)
        {
            instanceBlockIndex = null;

            if (!_program.TryUse(gl, out uint program))
            {
                scope = new MaterialShaderScope(gl, 0,0);
                return false;
            }

            uint nextFreeUnit = 0;
            uint nextFreeBlockBinding = 0;
            

            void BindUniformBlockBufferRange(string name, BufferRange bufferRange)
            {
                if (!_program.TryGetUniformBlockIndex(name, out uint blockIndex))
                    return;
                
                gl.UniformBlockBinding(program, blockIndex, nextFreeBlockBinding);

                var (buffer, offset, size) = bufferRange;

                gl.BindBufferRange(BufferTargetARB.UniformBuffer, nextFreeBlockBinding, buffer, offset, size);

                nextFreeBlockBinding++;
            }

            BindUniformBlockBufferRange(_sceneBlockBinding, sceneData);
            BindUniformBlockBufferRange(_materialBlockBinding, materialData);

            if (otherUBOData != null)
            {
                foreach (var (name, bufferRange) in otherUBOData)
                {
                    BindUniformBlockBufferRange(name, bufferRange);
                }
            }
            

            if (_instanceDataBlock.HasValue)
            {
                if (_program.TryGetUniformBlockIndex(_instanceDataBlock.Value.binding, out uint blockIndex))
                {
                    gl.UniformBlockBinding(program, blockIndex, nextFreeBlockBinding);

                    instanceBlockIndex = nextFreeBlockBinding;
                }
            }

            IEnumerable<SamplerBinding> samplers = materialSamplers;

            if(otherSamplers!=null)
                samplers = samplers.Concat(otherSamplers);

            foreach (var (name, sampler, texture) in samplers)
            {
                if (!_program.TryGetUniformLoc(name, out int loc))
                    continue;

                gl.Uniform1(loc, (int)nextFreeUnit);
                gl.BindTextureUnit(nextFreeUnit, texture);
                gl.BindSampler(nextFreeUnit, sampler);

                nextFreeUnit++;
            }

            scope = new MaterialShaderScope(gl, nextFreeBlockBinding, nextFreeUnit);

            return true;
        }
    }
}
