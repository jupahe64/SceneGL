using SceneGL.Util;
using Silk.NET.Core.Contexts;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SceneGL.GLWrappers
{
    public abstract class AbstractGLResourceHolder
    {
        private readonly Dictionary<GL, uint> _resourcesPerContext = new();

        protected uint GetOrCreateResource(GL gl) => _resourcesPerContext.GetOrCreate(gl, () => CreateResource(gl));

        protected abstract uint CreateResource(GL gl);

        protected abstract void CleanUpResource(GL gl, uint resource);

        public void CleanUp(GL gl)
        {
            if (_resourcesPerContext.TryGetValue(gl, out var resource))
                CleanUpResource(gl, resource);

            _resourcesPerContext.Remove(gl);
        }
    }
}
