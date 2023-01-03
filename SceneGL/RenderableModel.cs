using SceneGL.GLWrappers;
using SceneGL.Util;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL
{
    public enum AttributeShaderLoc : ushort
    {
        Loc0 = 1,
        Loc1 = 2,
        Loc2 = 4,
        Loc3 = 8,
        Loc4 = 16,
        Loc5 = 32,
        Loc6 = 64,
        Loc7 = 128,
        Loc8 = 256,
        Loc9 = 512,
        Loc10 = 1024,
        Loc11 = 2048,
        Loc12 = 4096,
        Loc13 = 8192,
        Loc14 = 16384,
        Loc15 = 32768
    }


    [AttributeUsage(AttributeTargets.Field)]
    public class VertexAttributeAttribute : Attribute
    {
        public readonly AttributeShaderLoc ShaderLocMapping;
        public readonly int Size;
        public readonly VertexAttribPointerType Type;
        public readonly bool Normalized;

        public VertexAttributeAttribute(AttributeShaderLoc shaderLocMapping, int size, VertexAttribPointerType type, bool normalized)
        {
            ShaderLocMapping = shaderLocMapping;
            Size = size;
            Type = type;
            Normalized = normalized;
        }
    }

    public class VertexStructDescription
    {
        public string Name { get; private set; }
        public IReadOnlyList<(string name, Type type, VertexAttributeAttribute attribute)> Fields => _fields;

        private readonly (string name, Type type, VertexAttributeAttribute attribute)[] _fields;

        public VertexStructDescription(string name, params (string name, Type type, VertexAttributeAttribute description)[] fields)
        {
            Name = name;
            _fields = fields;
        }

        private static readonly Dictionary<Type, VertexStructDescription> _cache = new();

        public static VertexStructDescription From<TVertex>() where TVertex : unmanaged
        {
            Type type = typeof(TVertex);

            return _cache.GetOrCreate(type, () =>
            {
                var fieldInfos = type.GetFields(
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

                int targetSize = Marshal.SizeOf(type);
                int gotSize = 0;


                var fields = new (string name, Type type, VertexAttributeAttribute attribute)[fieldInfos.Length];

                int i = 0;

                foreach (var fieldInfo in fieldInfos)
                {
                    VertexAttributeAttribute? attribute = (VertexAttributeAttribute?)fieldInfo.GetCustomAttributes(typeof(VertexAttributeAttribute), true).FirstOrDefault();

                    if (attribute == null)
                        throw new ArgumentException($"Field {fieldInfo.Name} of vertex struct {type.Name} has no VertexAttribute-Attribute");

                    gotSize += Marshal.SizeOf(fieldInfo.FieldType);

                    fields[i++] = (fieldInfo.Name, fieldInfo.FieldType, attribute);
                }

                if (gotSize != targetSize)
                    Debugger.Break(); //we didn't get all fields or something else went wrong

                return new(type.Name, fields);
            });
        }
    }

    public struct TriangleU16
    {
        public ushort IndexA;
        public ushort IndexB;
        public ushort IndexC;

        public TriangleU16(ushort indexA, ushort indexB, ushort indexC)
        {
            IndexA = indexA;
            IndexB = indexB;
            IndexC = indexC;
        }
    }

    public struct TriangleU32
    {
        public uint IndexA;
        public uint IndexB;
        public uint IndexC;

        public TriangleU32(uint indexA, uint indexB, uint indexC)
        {
            IndexA = indexA;
            IndexB = indexB;
            IndexC = indexC;
        }
    }

    public class RenderableModel
    {
        private VertexArrayObject _vao;
        private uint _elementCount;
        private readonly DrawElementsType _indexType;
        private readonly uint? _indexBuffer;
        private readonly uint[] _vertexBuffers;

        public static RenderableModel Create<TVertex>(GL gl, ReadOnlySpan<TVertex> vertices) 
            where TVertex : unmanaged
        {
            VertexStructDescription description = VertexStructDescription.From<TVertex>();

            uint vertexBuffer = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
            gl.BufferData(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.StaticDraw);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

            return Create((uint)vertices.Length, null, (vertexBuffer, description));
        }

        public static RenderableModel Create<TIndex, TVertex>(GL gl, ReadOnlySpan<TIndex> indices, ReadOnlySpan<TVertex> vertices) 
            where TVertex : unmanaged
            where TIndex : unmanaged
        {
            VertexStructDescription description = VertexStructDescription.From<TVertex>();

            var indexType = typeof(TIndex);

            uint indicesPerElement = 1;

            DrawElementsType drawElementsType;

            if (indexType == typeof(ushort))
                drawElementsType = DrawElementsType.UnsignedShort;
            else if (indexType == typeof(uint))
                drawElementsType = DrawElementsType.UnsignedInt;
            else if(indexType == typeof(TriangleU16))
            {
                drawElementsType = DrawElementsType.UnsignedShort;
                indicesPerElement = 3;
            }
            else if (indexType == typeof(TriangleU32))
            {
                drawElementsType = DrawElementsType.UnsignedInt;
                indicesPerElement = 3;
            }
            else
                throw new ArgumentException($"Index type has to be either byte, ushort or uint, was {typeof(TIndex).Name}");

            uint indexBuffer = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer);
            gl.BufferData(BufferTargetARB.ElementArrayBuffer, indices, BufferUsageARB.StaticDraw);
            gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, 0);

            uint vertexBuffer = gl.GenBuffer();
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, vertexBuffer);
            gl.BufferData(BufferTargetARB.ArrayBuffer, vertices, BufferUsageARB.StaticDraw);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

            return Create((uint)indices.Length * indicesPerElement, (drawElementsType, indexBuffer), (vertexBuffer, description));
        }

        public static RenderableModel Create(uint elementCount, (DrawElementsType indexType, uint buffer)? indexBuffer, params (uint buffer, VertexStructDescription vertexStructDesc)[] vertexBufferInfos)
        {
            var vertexBuffers = new uint[vertexBufferInfos.Length];

            var attributeInfos = new List<VertexAttributeInfo>();

            ushort shaderLocAssignments = 0;

            for (int i = 0; i < vertexBufferInfos.Length; i++)
            {
                vertexBuffers[i] = vertexBufferInfos[i].buffer;

                var (vtxBuffer, vtxStructDesc) = vertexBufferInfos[i];

                int firstIdxOfAddition = attributeInfos.Count;

                uint stride = 0;

                foreach (var (name, type, attribute) in vtxStructDesc.Fields)
                {
                    ushort bitField = (ushort)attribute.ShaderLocMapping;

                    if ((bitField & shaderLocAssignments) != 0)
                        throw new InvalidOperationException($"Shader location(s) of {name} overlap with atleast 1 other attribute");

                    foreach (var loc in BitUtils.AllSetBits(bitField))
                    {
                        attributeInfos.Add(new VertexAttributeInfo(vtxBuffer, (uint)loc, attribute.Size, attribute.Type, attribute.Normalized, 0, stride));
                    }

                    stride += (uint)Marshal.SizeOf(type);

                    shaderLocAssignments |= bitField;
                }

                for (int j = firstIdxOfAddition; j < attributeInfos.Count; j++)
                    attributeInfos[j] = attributeInfos[j] with { Stride = stride };
            }

            return new RenderableModel(
                new VertexArrayObject(indexBuffer?.buffer, attributeInfos.ToArray()),
                elementCount,
                indexBuffer?.indexType ?? default,
                indexBuffer?.buffer,
                vertexBuffers
            );
        }

        private RenderableModel(VertexArrayObject vao, uint elementCount, DrawElementsType indexType, uint? indexBuffer, uint[] vertexBuffers)
        {
            _vao = vao;
            _elementCount = elementCount;
            _indexType = indexType;
            _indexBuffer = indexBuffer;
            _vertexBuffers = vertexBuffers;
        }

        public unsafe void Draw(GL gl, uint instanceCount = 1)
        {
            if (instanceCount == 0)
                return;

            _vao.Bind(gl);
            if (_indexBuffer != null)
                gl.DrawElementsInstanced(PrimitiveType.Triangles, _elementCount, _indexType, null, instanceCount);
            else
                gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, _elementCount, instanceCount);

            gl.BindVertexArray(0);
        }

        public void CleanUp(GL gl) => _vao?.CleanUp(gl);
    }
}
