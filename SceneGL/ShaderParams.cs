using SceneGL.GLHelpers;
using SceneGL.Util;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SceneGL
{
    public record struct BufferBinding(string Binding, BufferRange BufferRange);
    public record struct SamplerBinding(string Binding, uint Sampler, uint Texture);

    public class ShaderParams
    {
        public event Action? EvaluatingResources;

        private readonly SamplerBinding[] _samplers;
        private readonly BufferBinding[] _uniformBuffers;

        public ShaderParams(BufferBinding[] uniformBuffers, SamplerBinding[] samplers)
        {
            _uniformBuffers = uniformBuffers;
            _samplers = samplers;
        }

        public static ShaderParams FromSamplers(SamplerBinding[] samplers) => new(Array.Empty<BufferBinding>(), samplers);

        public static ShaderParams FromUniformBlockDataAndSamplers<TData>(GL gl,
            string uniformBlockBinding, TData uniformBlockData, string? bufferLabel,
            SamplerBinding[] samplers, out UniformBuffer<TData> buffer)
            where TData : unmanaged
        {
            var _buffer = new UniformBuffer<TData>(uniformBlockData, bufferLabel);

            var param = new ShaderParams(new BufferBinding[]
            {
                new BufferBinding(uniformBlockBinding, default)
            }, samplers);

            param.EvaluatingResources += () => param.SetBufferBinding(uniformBlockBinding, _buffer.GetDataBuffer(gl));

            buffer = _buffer;

            return param;
        }

        public void EvaluateResources()
        {
            EvaluatingResources?.Invoke();
        }

        public (IReadOnlyList<SamplerBinding> samplers, IReadOnlyList<BufferBinding> uniformBuffers) GetBindings()
        {
            EvaluateResources();

            return (_samplers, _uniformBuffers);
        }

        public void SetSamplerBinding(string binding, uint sampler, uint texture)
        {
            for (int i = 0; i < _samplers.Length; i++)
            {
                if (_samplers[i].Binding == binding)
                {
                    _samplers[i].Sampler = sampler;
                    _samplers[i].Texture = texture;
                    return;
                }
            }

            throw new KeyNotFoundException($"No sampler with binding {binding}");
        }

        public void SetBufferBinding(string binding, BufferRange bufferRange)
        {
            for (int i = 0; i < _uniformBuffers.Length; i++)
            {
                if (_uniformBuffers[i].Binding == binding)
                {
                    _uniformBuffers[i].BufferRange = bufferRange;
                    return;
                }
            }

            throw new KeyNotFoundException($"No uniform buffer with binding {binding}");
        }
    }

    public class UniformArrayBuffer<T>
        where T : unmanaged
    {
        private uint _dataBuffer = 0;

        private T[] _data;
        private int _dataBufferSize = -1;
        private bool _isDataDirty;
        private string? _label;

        public UniformArrayBuffer(ReadOnlySpan<T> data, string? label = null)
        {
            _data = data.ToArray();
            _isDataDirty = true;
            _label = label;
        }

        /// <summary>
        /// Will not write to the GPU, changes only exist on the CPU until <see cref="GetDataBuffer(GL)"/> is called
        /// </summary>
        /// <param name="data"></param>
        public void SetData(ReadOnlySpan<T> data)
        {
            _data = data.ToArray();
        }

        public ReadOnlySpan<T> GetData() => _data;

        /// <summary>
        /// Uploads data to the GPU if necessary
        /// </summary>
        /// <param name="gl"></param>
        /// <returns></returns>
        public BufferRange GetDataBuffer(GL gl)
        {
            if (_dataBuffer == 0)
                _dataBuffer = gl.GenBuffer();

            if (_data.Length > _dataBufferSize)
            {
                gl.BindBuffer(BufferTargetARB.UniformBuffer, _dataBuffer);
                gl.BufferData<T>(BufferTargetARB.UniformBuffer, _data, BufferUsageARB.StaticDraw);
                gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);

                if (_label is not null)
                    ObjectLabelHelper.SetBufferLabel(gl, _dataBuffer, _label);
            }
            else if (_isDataDirty)
            {
                gl.BindBuffer(BufferTargetARB.UniformBuffer, _dataBuffer);
                gl.BufferSubData<T>(BufferTargetARB.UniformBuffer, 0, _data);
                gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
            }

            return new BufferRange(_dataBuffer, 0, (uint)_data.Length);
        }
    }

    public class UniformBuffer<TData>
        where TData : unmanaged
    {
        public TData Data { get => _data; }

        private bool _isDataDirty = true;
        private bool _isBufferInitialized = false;
        private uint _dataBuffer;
        private TData _data;
        private string? _label;

        public UniformBuffer(TData data, string? label)
        {
            _data = data;
            _label = label;
        }

        /// <summary>
        /// Will not write to the GPU, changes only exist on the CPU until <see cref="GetDataBuffer(GL)"/> is called
        /// </summary>
        /// <param name="data"></param>
        public void SetData(in TData data)
        {
            _data = data;
            _isDataDirty = true;
        }

        /// <summary>
        /// Uploads data to the GPU if necessary
        /// </summary>
        /// <param name="gl"></param>
        /// <returns></returns>
        public unsafe BufferRange GetDataBuffer(GL gl)
        {
            if (_dataBuffer == 0)
                _dataBuffer = gl.GenBuffer();

            if (!_isBufferInitialized)
            {
                gl.BindBuffer(BufferTargetARB.UniformBuffer, _dataBuffer);
                gl.BufferData<TData>(BufferTargetARB.UniformBuffer, (uint)sizeof(TData), in _data, BufferUsageARB.StaticDraw);
                gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
                if (_label is not null)
                    ObjectLabelHelper.SetBufferLabel(gl, _dataBuffer, _label);

                _isBufferInitialized = true;
            }
            else if (_isDataDirty)
            {
                gl.BindBuffer(BufferTargetARB.UniformBuffer, _dataBuffer);
                gl.BufferSubData(BufferTargetARB.UniformBuffer, 0, (uint)sizeof(TData), in _data);
                gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
            }

            return new BufferRange(_dataBuffer, 0, (uint)sizeof(TData));
        }
    }
}
