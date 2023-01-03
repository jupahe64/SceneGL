using Silk.NET.OpenGL;
using System.Diagnostics;
using SceneGL.Util;
using System.Runtime.InteropServices;
using SceneGL.GLHelpers;

namespace SceneGL.GLWrappers
{
    public delegate void CompileResultCallback(bool success, string? errorMessage);
    public delegate void ShaderSourceUpdate(CompileResultCallback? resultCallback);

    public class ShaderSource
    {
        private string _code;

        public ShaderSource(string? name, ShaderType type, string source)
        {
            _code = source;
            Name = name;
            Type = type;
        }

        public string? Name { get; init; }
        public ShaderType Type { get; init; }
        public string Code => _code;

        public void UpdateSource(string newSource, CompileResultCallback? compileResultCallback = null)
        {
            _code = newSource;
            Update?.Invoke(compileResultCallback);
        }

        public event ShaderSourceUpdate? Update;
    }
    public record UniformInfo(string Name, UniformType Type, int Location);

    /// <summary>
    /// A thin wrapper of an OpenGL ShaderProgram that simplifies common operations and working with multiple contexts
    /// </summary>
    public sealed class ShaderProgram
    {
        [Flags]
        private enum ResourceState
        {
            None,
            /// <summary>
            /// There is source code availible that the shader program wasn't compiled with yet
            /// </summary>
            IsDirty,
            /// <summary>
            /// The current program is ready to be used in rendering, it just might not use the latest shader code
            /// </summary>
            IsReadyToUse
        }

        private Dictionary<string, uint> _uniformBlockBindings = new();

        private (uint program, ResourceState state)? _shaderProgram = null;

        private Dictionary<string, int>? _uniformLocations = null;
        private Dictionary<string, uint>? _uniformBlockIndices = null;

        public IReadOnlyList<UniformInfo> UniformInfos
        {
            get
            {
                EnsureUniformDataRetrieved();
                return _uniformInfos;
            }
        }

        private UniformInfo[] _uniformInfos = Array.Empty<UniformInfo>();
        private readonly ShaderSource[] _shadersSources;
        private readonly string? _name;

        private List<CompileResultCallback> _compileResultCallbacks = new();


        public ShaderProgram(params ShaderSource[] sources)
        {
            foreach (var source in sources)
            {
                source.Update += OnSourceUpdated!;
            }

            _shadersSources = sources;
        }

        private void OnSourceUpdated(CompileResultCallback resultCallback)
        {
            if(_shaderProgram.HasValue)
            {
                var (program, resourceState) = _shaderProgram.Value;

                _shaderProgram = (program, resourceState | ResourceState.IsDirty);
            }

            _compileResultCallbacks.Add(resultCallback);
        }

        public static ShaderProgram FromVertexFragmentSource((string? name, string source) vertexSource, (string? name, string source) fragmentSource)
        {
            return new(
                new(vertexSource.name, ShaderType.VertexShader, vertexSource.source),
                new(fragmentSource.name, ShaderType.FragmentShader, fragmentSource.source)
            );
        }

        private bool TryCreateAndCompileProgram(GL gl, out uint program)
        {
            //heavily modified from https://github.com/dotnet/Silk.NET/blob/main/src/OpenGL/Extensions/Silk.NET.OpenGL.Extensions.ImGui/Shader.cs

            //generate shaders and program name/label(for debugging)

            string programLabel = "P[";

            uint[] shaders = new uint[_shadersSources.Length];

            for (int i = 0; i < _shadersSources.Length; i++)
            {
                if (i != 0)
                    programLabel += ';';

                ShaderSource source = _shadersSources[i];

                uint shader = gl.CreateShader(source.Type);
                string shaderLabel = source.Name ?? $"Shader {shader}";

                ObjectLabelHelper.SetShaderLabel(gl, shader, shaderLabel);
                gl.ShaderSource(shader, source.Code);

                programLabel += shaderLabel;

                shaders[i] = shader;
            }

            programLabel += "]";


            program = gl.CreateProgram();

            ObjectLabelHelper.SetShaderProgramLabel(gl, program, programLabel);



            foreach (uint shader in shaders)
            {
                gl.CompileShader(shader);
                gl.AttachShader(program, shader);
            }

            gl.LinkProgram(program);

            gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var val);
            bool success = val == 1;

            foreach (uint shader in shaders)
            {
                gl.DetachShader(program, shader);
                gl.DeleteShader(shader);
            }

            string? errorMessage = null;



            if (!success)
            {
                errorMessage = gl.GetProgramInfoLog(program);

                gl.DeleteProgram(program);

                program = 0;
            }

            if (_compileResultCallbacks.Count == 0)
            {
                Debug.WriteLine(errorMessage);
            }

            foreach (var callback in _compileResultCallbacks)
            {
                callback(success, errorMessage);
            }

            _compileResultCallbacks.Clear();


            return success;
        }

        /// <summary>
        /// forces the creation of a ShaderProgram for this GL context
        /// <para>Only necessary if you want to get uniform information before calling <see cref="Use(GL)"/></para>
        /// </summary>
        /// <param name="gl"></param>
        public void Create(GL gl)
        {
            TryGetOrCreateProgram(gl, out _);
        }

        public bool TryUse(GL gl,  out uint program)
        {
            if (!TryGetOrCreateProgram(gl, out program))
                return false;

            gl.UseProgram(program);
            return true;
        }

        private bool TryGetOrCreateProgram(GL gl, out uint program)
        {
            var entry = _shaderProgram ?? (0, ResourceState.IsDirty);

            if (entry.state.HasFlag(ResourceState.IsDirty))
            {
                if (TryCreateAndCompileProgram(gl, out program))
                {
                    _shaderProgram = (program, state: ResourceState.IsReadyToUse);
                    RetrieveUniformData(gl, program);

                    if (entry.program != 0)
                        gl.DeleteProgram(entry.program);

                    foreach (var (name, binding) in _uniformBlockBindings)
                    {
                        if (_uniformBlockIndices!.TryGetValue(name, out uint index))
                            gl.UniformBlockBinding(program, index, binding);
                    }

                    return true;
                }

                _shaderProgram = (entry.program, state: entry.state & ~ResourceState.IsDirty);
            }

            if (entry.state.HasFlag(ResourceState.IsReadyToUse))
            {
                program = entry.program;
                return true;
            }

            //initial creation was not successful and there is no new data to retry it with => program can't be used
            program = 0;
            return false;
        }

        /// <summary>
        /// Gets the location of the uniform with the given name
        /// <para>The program has to be created atleast once to do this, call <see cref="Create(GL)"/> if unsure</para>
        /// </summary>
        /// <param name="gl"></param>
        /// <param name="name"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public bool TryGetUniformLoc(string name, out int loc)
        {
            EnsureUniformDataRetrieved();

            return _uniformLocations!.TryGetValue(name, out loc);
        }

        /// <summary>
        /// Gets the index of the uniform block with the given name
        /// <para>The program has to be created atleast once to do this, call <see cref="Create(GL)"/> if unsure</para>
        /// </summary>
        /// <param name="gl"></param>
        /// <param name="name"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public bool TryGetUniformBlockIndex(string name, out uint index)
        {
            EnsureUniformDataRetrieved();

            return _uniformBlockIndices!.TryGetValue(name, out index);
        }

        private void EnsureUniformDataRetrieved()
        {
            if (_uniformLocations == null)
                throw new InvalidOperationException("Can't get uniform data, program hasn't been created yet");
        }

        private void RetrieveUniformData(GL gl, uint program)
        {
            _uniformLocations = new();
            _uniformBlockIndices = new();

            gl.GetProgram(program, ProgramPropertyARB.ActiveUniforms, out int activeUniformCount);

            var infos = new UniformInfo[activeUniformCount];
            for (uint i = 0; i < activeUniformCount; i++)
            {
                string name = gl.GetActiveUniform(program, i, out int size, out UniformType type);
                int location = gl.GetUniformLocation(program, name);

                _uniformLocations[name] = location;

                infos[i] = new(name, type, location);
            }

            _uniformInfos = infos;


            gl.GetProgram(program, ProgramPropertyARB.ActiveUniformBlocks, out int activeBlockCount);

            for (uint i = 0; i < activeBlockCount; i++)
            {
                uint length;
                gl.GetProgram(program, GLEnum.ActiveUniformBlockMaxNameLength, out var lengthTmp);
                length = (uint)lengthTmp;

                gl.GetActiveUniformBlockName
                    (program, i, length == 0 ? 1 : length, out length, out string str);

                string name = str.Substring(0, (int)length);

                _uniformBlockIndices[name] = i;
            }

            _uniformInfos = infos;
        }
    }
}