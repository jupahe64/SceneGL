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
    public class Material
    {
        public IReadOnlyList<SamplerBinding> Samplers;
        uint _dataBuffer = 0;

        byte[] _data;
        int _dataBufferSize = -1;
        bool _isDataDirty;

        public Material(ReadOnlySpan<byte> data, SamplerBinding[] samplers)
        {
            _data = data.ToArray();
            Samplers = samplers;
            _isDataDirty = true;
        }

        public void SetData(ReadOnlySpan<byte> data)
        {
            _data = data.ToArray();
        }

        public BufferRange GetDataBuffer(GL gl)
        {
            if (_dataBuffer == 0)
                _dataBuffer = gl.GenBuffer();

            if (_data.Length > _dataBufferSize)
            {
                gl.BindBuffer(BufferTargetARB.UniformBuffer, _dataBuffer);
                gl.BufferData<byte>(BufferTargetARB.UniformBuffer, _data, BufferUsageARB.StaticDraw);
                gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
            }
            else if (_isDataDirty)
            {
                gl.BindBuffer(BufferTargetARB.UniformBuffer, _dataBuffer);
                gl.BufferSubData<byte>(BufferTargetARB.UniformBuffer, 0, _data);
                gl.BindBuffer(BufferTargetARB.UniformBuffer, 0);
            }

            return new BufferRange(_dataBuffer, 0, (uint)_data.Length);
        }
    }

    public class Material<TData> : Material
        where TData : unmanaged
    {
        public Material(TData data, SamplerBinding[] samplers) 
            : base(data.ToByteArray(), samplers)
        {

        }

        public void SetData(TData data)
        {
            SetData(data.ToByteArray());
        }
    }
}
