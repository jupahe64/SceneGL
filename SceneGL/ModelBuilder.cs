using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL
{
    /// <summary>
    /// Helps with the construction of a <see cref="RenderableModel"/>
    /// by providing simple to use methods for adding primitives
    /// and generating the needed vertex and index buffers behind the scenes
    /// <para>The resulting <see cref="RenderableModel"/> will use <see langword="uint"/> as it's index format</para>
    /// </summary>
    /// <typeparam name="TVertex"></typeparam>
    public class ModelBuilder<TVertex>
        where TVertex : unmanaged
    {
        private readonly List<TVertex> _vertices = new();
        private readonly Dictionary<TVertex, uint> _vertexLookup = new();
        private readonly List<uint> _indices = new();

        private void AddVertex(TVertex vertex)
        {
            if (_vertexLookup.TryGetValue(vertex, out var index))
            {
                _indices.Add(index);
            }
            else
            {
                uint last = (uint)_vertices.Count;
                _indices.Add(last);
                _vertices.Add(vertex);
                _vertexLookup.Add(vertex, last);
            }
        }

        public ModelBuilder<TVertex> AddTriangle(TVertex v1, TVertex v2, TVertex v3)
        {
            AddVertex(v1);
            AddVertex(v2);
            AddVertex(v3);

            return this;
        }

        public ModelBuilder<TVertex> AddPlane(TVertex v1, TVertex v2, TVertex v3, TVertex v4)
        {
            AddTriangle(v2, v1, v3);
            AddTriangle(v2, v3, v4);

            return this;
        }

        public RenderableModel GetModel(GL gl)
        {
            return RenderableModel.Create<uint, TVertex>(gl,
                CollectionsMarshal.AsSpan(_indices),
                CollectionsMarshal.AsSpan(_vertices)
                );
        }

        public void GetData(out ReadOnlySpan<uint> indices, out ReadOnlySpan<TVertex> vertices)
        {
            indices = CollectionsMarshal.AsSpan(_indices);
            vertices = CollectionsMarshal.AsSpan(_vertices);
        }
    }
}
