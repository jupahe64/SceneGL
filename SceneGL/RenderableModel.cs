using SceneGL.GLHelpers;
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

    public record struct TriangleU8(byte IndexA, byte IndexB, byte IndexC);
    public record struct TriangleU16(ushort IndexA, ushort IndexB, ushort IndexC);
    public record struct TriangleU32(uint IndexA, uint IndexB, uint IndexC);

    public class RenderableModel
    {
        private readonly VertexArrayObject _vao;
        private uint ElementCount { get; set; }
        private readonly DrawElementsType _indexType;
        private readonly uint? _indexBuffer;

        public static RenderableModel Create<TVertex>(GL gl, ReadOnlySpan<TVertex> vertices)
            where TVertex : unmanaged
        {
            VertexStructDescription description = VertexStructDescription.From<TVertex>();

            var (vertexBuffer, _, _) = BufferHelper.CreateBuffer(gl, BufferUsageARB.StaticDraw, vertices);
            ObjectLabelHelper.SetBufferLabel(gl, vertexBuffer, $"VertexBuffer {vertexBuffer}");

            return Create((uint)vertices.Length, null, (vertexBuffer, description));
        }

        public static RenderableModel Create<TIndex, TVertex>(GL gl, ReadOnlySpan<TIndex> indices, ReadOnlySpan<TVertex> vertices)
            where TVertex : unmanaged
            where TIndex : unmanaged
        {
            VertexStructDescription description = VertexStructDescription.From<TVertex>();

            var drawElementsType = GetDrawElementsType<TIndex>(out int indicesPerElement);

            var (indexBuffer, _, _) = BufferHelper.CreateBuffer(gl, BufferUsageARB.StaticDraw, indices);
            ObjectLabelHelper.SetBufferLabel(gl, indexBuffer, $"IndexBuffer {indexBuffer}");

            var (vertexBuffer, _, _) = BufferHelper.CreateBuffer(gl, BufferUsageARB.StaticDraw, vertices);
            ObjectLabelHelper.SetBufferLabel(gl, vertexBuffer, $"VertexBuffer {vertexBuffer}");

            return Create((uint)(indices.Length * indicesPerElement), (drawElementsType, indexBuffer), (vertexBuffer, description));
        }

        public static RenderableModel Create(uint elementCount, (DrawElementsType indexType, uint buffer)? indexBuffer, 
            params (uint buffer, VertexStructDescription vertexStructDesc)[] vertexBufferInfos)
        {
            return new RenderableModel(
                CreateVaoFromStructDescriptions(indexBuffer?.buffer, vertexBufferInfos),
                elementCount,
                indexBuffer?.indexType ?? default,
                indexBuffer?.buffer
            );
        }

        private RenderableModel(VertexArrayObject vao, uint elementCount, DrawElementsType indexType, uint? indexBuffer)
        {
            _vao = vao;
            ElementCount = elementCount;
            _indexType = indexType;
            _indexBuffer = indexBuffer;
        }

        public unsafe void Draw(GL gl, uint instanceCount = 1)
        {
            if (instanceCount == 0)
                return;

            _vao.Bind(gl);
            if (_indexBuffer != null)
                gl.DrawElementsInstanced(PrimitiveType.Triangles, ElementCount, _indexType, null, instanceCount);
            else
                gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, ElementCount, instanceCount);

            gl.BindVertexArray(0);
        }

        public void CleanUp(GL gl) => _vao?.CleanUp(gl);

        #region Helper functions
        private static DrawElementsType GetDrawElementsType<TIndex>(out int indicesPerElement)
        {
            var indexType = typeof(TIndex);

            indicesPerElement = 1;

            DrawElementsType drawElementsType;

            if (indexType == typeof(byte))
            {
                drawElementsType = DrawElementsType.UnsignedByte;
            }
            else if (indexType == typeof(ushort))
            {
                drawElementsType = DrawElementsType.UnsignedShort;
            }
            else if (indexType == typeof(uint))
            {
                drawElementsType = DrawElementsType.UnsignedInt;
            }
            else if (indexType == typeof(TriangleU8))
            {
                drawElementsType = DrawElementsType.UnsignedByte;
                indicesPerElement = 3;
            }
            else if (indexType == typeof(TriangleU16))
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

            return drawElementsType;
        }

        private static VertexArrayObject CreateVaoFromStructDescriptions(uint? indexBuffer, (uint buffer, VertexStructDescription vertexStructDesc)[] vertexBufferInfos)
        {
            var attributeInfos = new List<VertexAttributeInfo>();
            ushort shaderLocAssignments = 0;

            for (int i = 0; i < vertexBufferInfos.Length; i++)
            {
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

            return new VertexArrayObject(indexBuffer, attributeInfos.ToArray());
        }
        #endregion
    }
}
