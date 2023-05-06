using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Input.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace EditTK
{
    public enum ActionUpdateResult
    {
        None,
        Apply,
        Cancel
    }

    public abstract class TransformAction : ITransformAction
    {
        protected readonly Matrix4x4 _baseMatrix;
        protected readonly Vector3 _center;

        public Matrix4x4 FinalMatrix { get; private set; }

        public Matrix4x4 DeltaMatrix { get; protected set; }

        public TransformAction(Matrix4x4 baseMatrix, Vector3 center)
        {
            _baseMatrix = baseMatrix;
            _center = center;
        }

        protected abstract void OnStart(in SceneViewState view);

        protected abstract void OnUpdate(in SceneViewState view, bool isSnapping);


        public void StartTransform(in SceneViewState view)
        {
            OnStart(in view);
        }

        public ActionUpdateResult Update(in SceneViewState view, bool isSnapping)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
            {
                DeltaMatrix = Matrix4x4.Identity;
                FinalMatrix = _baseMatrix;
                return ActionUpdateResult.Cancel;
            }

            OnUpdate(in view, isSnapping);

            var moveCenterToOrigin = Matrix4x4.CreateTranslation(_center);
            var moveBack = Matrix4x4.CreateTranslation(-_center);

            DeltaMatrix = moveBack * DeltaMatrix * moveCenterToOrigin;
            FinalMatrix = _baseMatrix * DeltaMatrix;

            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                return ActionUpdateResult.Apply;

            return ActionUpdateResult.None;
        }
    }

    public class AxisRotationAction : TransformAction
    {
        public static double GetShortestRotationBetween(double angleA, double angleB, double fullRotation = Math.Tau)
        {
            double oldR = (angleA % fullRotation + fullRotation) % fullRotation;
            double newR = (angleB % fullRotation + fullRotation) % fullRotation;

            double delta = newR - oldR;
            double abs = Math.Abs(delta);
            double sign = Math.Sign(delta);

            if (abs > fullRotation/2)
                return -(fullRotation - abs) * sign;
            else
                return delta;
        }

        private readonly Vector3 _axisVector;
        private readonly AxisInfo _axisInfo;
        private ValueTracker<double>? _rotationTracker;
        private double _snappingInterval;

        public static AxisRotationAction Start(int axis, Vector3 center, Matrix4x4 baseMatrix,
            double snappingInterval, in SceneViewState view)
        {
            var mtx = baseMatrix;
            ReadOnlySpan<Vector3> axisVecs = stackalloc Vector3[]
            {
                Vector3.Normalize(new(mtx.M11, mtx.M12, mtx.M13)),
                Vector3.Normalize(new(mtx.M21, mtx.M22, mtx.M23)),
                Vector3.Normalize(new(mtx.M31, mtx.M32, mtx.M33)),
            };
            var action = new AxisRotationAction(center, axisVecs[axis], AxisInfo.Axes[axis], baseMatrix, snappingInterval);
            action.StartTransform(in view);
            return action;
        }

        public static AxisRotationAction StartViewAxisRotation(Vector3 center, Matrix4x4 baseMatrix,
            double snappingInterval, in SceneViewState view)
        {
            var action = new AxisRotationAction(center, view.CamForwardVector, AxisInfo.ViewRotationAxis, baseMatrix, snappingInterval);
            action.StartTransform(in view);
            return action;
        }

        private AxisRotationAction(Vector3 center, Vector3 axisVector, AxisInfo axisInfo, Matrix4x4 baseMatrix, double snappingInterval)
            : base(baseMatrix, center)
        {
            _axisVector = axisVector;
            _axisInfo = axisInfo;
            _snappingInterval = snappingInterval;
        }


        protected override void OnStart(in SceneViewState view)
        {
            _rotationTracker = new ValueTracker<double>(GetAngle(in view));
        }

        protected override void OnUpdate(in SceneViewState view, bool isSnapping)
        {
            Debug.Assert(_rotationTracker != null);
            
            double inputAngle = GetAngle(in view);
            double newAngle = _rotationTracker.Value + GetShortestRotationBetween(_rotationTracker.Value, inputAngle, 360);
            _rotationTracker.Update(newAngle, isSnapping ? _snappingInterval : null);

            Vector2 center2d = GizmoDrawer.WorldToScreen(_center);

            GizmoDrawer.Drawlist.AddText(
                ImGui.GetFont(), 18,
                center2d, _axisInfo.Color,
                $"Rotaing along {_axisInfo.Name} : {_rotationTracker.DeltaValue,5:0.#}°");

            if (_axisInfo != AxisInfo.ViewRotationAxis)
            {
                GizmoDrawer.ClippedLine(_center, _center + _axisVector * 1000, _axisInfo.Color, 1.5f);
                GizmoDrawer.ClippedLine(_center, _center - _axisVector * 1000, _axisInfo.Color & 0xAA_FF_FF_FF, 1.5f);
            }

            GizmoDrawer.Drawlist.AddCircleFilled(center2d, 3, _axisInfo.Color);


            DeltaMatrix = Matrix4x4.CreateFromAxisAngle(_axisVector, (float)(_rotationTracker.DeltaValue * MathF.PI / 180f));
        }

        private double GetAngle(in SceneViewState view)
        {
            Vector2 relativeMousePos = view.MousePosition - view.WorldToScreen(_center);
            Vector3 centerToCamDir = Vector3.Normalize(view.CamPosition - _center);
            float axisCamDot = Vector3.Dot(centerToCamDir, _axisVector);

            if (Math.Abs(axisCamDot) < 0.5f)
            {
                Vector3 lineDir = Vector3.Normalize(Vector3.Cross(_axisVector, centerToCamDir));
                Vector2 lineDir2d = Vector2.Normalize(view.WorldToScreen(_center + lineDir) - view.WorldToScreen(_center));

                float a = Vector2.Dot(lineDir2d, relativeMousePos) / 100f;
                return Math.Pow(Math.Abs(a), 0.9) * Math.Sign(a) * 180;
            }
            else
            {
                var rotationSign = Math.Sign(axisCamDot);
                Vector2 direction = Vector2.Normalize(relativeMousePos);
                return Math.Atan2(-direction.Y, direction.X) * rotationSign * 180f / MathF.PI;
            }
        }
    }

    public class TrackballRotationAction : TransformAction
    {
        private ValueTracker<double>? _rotationXTracker;
        private ValueTracker<double>? _rotationYTracker;
        private double _snappingInterval;

        public static TrackballRotationAction Start(Vector3 center, Matrix4x4 baseMatrix,
            double snappingInterval, in SceneViewState view)
        {
            var action = new TrackballRotationAction(center, baseMatrix, snappingInterval);
            action.StartTransform(in view);
            return action;
        }

        public TrackballRotationAction(Vector3 center, Matrix4x4 baseMatrix, double snappingInterval)
            : base(baseMatrix, center)
        {
            _snappingInterval = snappingInterval;
        }

        protected override void OnStart(in SceneViewState view)
        {
            (double angleX, double angleY) = GetAngles(in view);

            _rotationXTracker = new ValueTracker<double>(angleX);
            _rotationYTracker = new ValueTracker<double>(angleY);
        }

        protected override void OnUpdate(in SceneViewState view, bool isSnapping)
        {
            if (_rotationXTracker == null || _rotationYTracker == null)
                throw new NullReferenceException($"Important instance variables are null, {nameof(ITransformAction.StartTransform)} has not been called");

            (double angleX, double angleY) = GetAngles(in view);

            _rotationXTracker.Update(angleX, isSnapping ? _snappingInterval : null);
            _rotationYTracker.Update(angleY, isSnapping ? _snappingInterval : null);

            Vector2 center2d = GizmoDrawer.WorldToScreen(_center);

            GizmoDrawer.Drawlist.AddText(
                ImGui.GetFont(), 18,
                center2d, AxisInfo.ViewRotationAxis.Color,
                $"Rotaing along Trackball : {-_rotationXTracker.DeltaValue,5:0.#}° {-_rotationYTracker.DeltaValue,5:0.#}°");

            DeltaMatrix = Matrix4x4.CreateFromAxisAngle(view.CamUpVector, (float)(_rotationXTracker.DeltaValue * MathF.PI / 180f));
            DeltaMatrix *= Matrix4x4.CreateFromAxisAngle(view.CamRightVector, (float)(_rotationYTracker.DeltaValue * MathF.PI / 180f));
        }

        private (double angleX, double angleY) GetAngles(in SceneViewState view)
        {
            Vector2 relativeMousePos = view.MousePosition - view.WorldToScreen(_center);

            return (
                relativeMousePos.X,
                relativeMousePos.Y
                );
        }
    }


    public class AxisTranslateAction : TransformAction
    {
        private readonly Vector3 _axisVector;
        private readonly AxisInfo _axisInfo;
        private ValueTracker<float>? _offsetTracker;
        private float _snappingInterval;

        public static AxisTranslateAction Start(int axis, Vector3 center, Matrix4x4 baseMatrix,
            float snappingInterval, in SceneViewState view)
        {
            var mtx = baseMatrix;
            ReadOnlySpan<Vector3> axisVecs = stackalloc Vector3[]
            {
                Vector3.Normalize(new(mtx.M11, mtx.M12, mtx.M13)),
                Vector3.Normalize(new(mtx.M21, mtx.M22, mtx.M23)),
                Vector3.Normalize(new(mtx.M31, mtx.M32, mtx.M33)),
            };
            var action = new AxisTranslateAction(center, axisVecs[axis], AxisInfo.Axes[axis], baseMatrix, snappingInterval);
            action.StartTransform(in view);
            return action;
        }

        private AxisTranslateAction(Vector3 center, Vector3 axisVector, AxisInfo axisInfo, Matrix4x4 baseMatrix, float snappingInterval)
            : base(baseMatrix, center)
        {
            _axisVector = axisVector;
            _axisInfo = axisInfo;
            _snappingInterval = snappingInterval;
        }


        protected override void OnStart(in SceneViewState view)
        {
            _offsetTracker = new ValueTracker<float>(GetOffset(in view));
        }

        protected override void OnUpdate(in SceneViewState view, bool isSnapping)
        {
            Debug.Assert(_offsetTracker != null);

            float newOffset = GetOffset(in view);
            _offsetTracker.Update(newOffset, isSnapping ? _snappingInterval : null);

            Vector2 center2d = GizmoDrawer.WorldToScreen(_center);

            GizmoDrawer.Drawlist.AddText(
                ImGui.GetFont(), 18,
                center2d, _axisInfo.Color,
                $"Moving along {_axisInfo.Name} : {_offsetTracker.DeltaValue,5:0.###}m");

            if (_axisInfo != AxisInfo.ViewRotationAxis)
            {
                GizmoDrawer.ClippedLine(_center, _center + _axisVector * 1000, _axisInfo.Color, 1.5f);
                GizmoDrawer.ClippedLine(_center, _center - _axisVector * 1000, _axisInfo.Color & 0xAA_FF_FF_FF, 1.5f);
            }

            GizmoDrawer.Drawlist.AddCircleFilled(center2d, 3, _axisInfo.Color);


            DeltaMatrix = Matrix4x4.CreateTranslation(_axisVector * _offsetTracker.DeltaValue);
        }

        private float GetOffset(in SceneViewState view)
        {
            var billBoardVec = Vector3.Cross(
                Vector3.Cross(view.CamForwardVector, _axisVector),
                _axisVector
            );

            Vector3 hitPoint = view.MouseRayHitOnPlane(billBoardVec, _center);
            Vector3 offset = hitPoint - _center;
            return Vector3.Dot( offset, _axisVector );
        }
    }

    public class PlaneTranslateAction : TransformAction
    {
        private readonly Vector3 _axisVectorA;
        private readonly Vector3 _axisVectorB;
        private readonly Vector3 _planeNormal;
        private readonly AxisInfo _axisInfoA;
        private readonly AxisInfo _axisInfoB;
        private ValueTracker<float>? _offsetTrackerAxisA;
        private ValueTracker<float>? _offsetTrackerAxisB;
        private float _snappingInterval;

        public static PlaneTranslateAction Start(int axisA, int axisB, Vector3 center, Matrix4x4 baseMatrix,
            float snappingInterval, in SceneViewState view)
        {
            var mtx = baseMatrix;
            ReadOnlySpan<Vector3> axisVecs = stackalloc Vector3[]
            {
                Vector3.Normalize(new(mtx.M11, mtx.M12, mtx.M13)),
                Vector3.Normalize(new(mtx.M21, mtx.M22, mtx.M23)),
                Vector3.Normalize(new(mtx.M31, mtx.M32, mtx.M33)),
            };
            var action = new PlaneTranslateAction(center, axisVecs[axisA], axisVecs[axisB], AxisInfo.Axes[axisA], AxisInfo.Axes[axisB], baseMatrix, snappingInterval);
            action.StartTransform(in view);
            return action;
        }

        private PlaneTranslateAction(Vector3 center, Vector3 axisVectorA, Vector3 axisVectorB, AxisInfo axisInfoA, AxisInfo axisInfoB, 
            Matrix4x4 baseMatrix, float snappingInterval)
            : base(baseMatrix, center)
        {
            _axisVectorA = axisVectorA;
            _axisVectorB = axisVectorB;
            _planeNormal = Vector3.Cross(axisVectorA, axisVectorB);
            _axisInfoA = axisInfoA;
            _axisInfoB = axisInfoB;
            _snappingInterval = snappingInterval;
        }


        protected override void OnStart(in SceneViewState view)
        {
            var (a, b) = GetOffsets(in view);

            _offsetTrackerAxisA = new ValueTracker<float>(a);
            _offsetTrackerAxisB = new ValueTracker<float>(b);
        }

        protected override void OnUpdate(in SceneViewState view, bool isSnapping)
        {
            Debug.Assert(_offsetTrackerAxisA != null);
            Debug.Assert(_offsetTrackerAxisB != null);

            var (newOffsetA, newOffsetB) = GetOffsets(in view);

            _offsetTrackerAxisA.Update(newOffsetA, isSnapping ? _snappingInterval : null);
            _offsetTrackerAxisB.Update(newOffsetB, isSnapping ? _snappingInterval : null);

            Vector2 center2d = GizmoDrawer.WorldToScreen(_center);

            uint planeColor = GizmoDrawer.AdditiveBlend(_axisInfoA.Color, _axisInfoB.Color);

            GizmoDrawer.Drawlist.AddText(
                ImGui.GetFont(), 18,
                center2d, planeColor,
                $"Moving along {_axisInfoA.Name} : {_offsetTrackerAxisA.DeltaValue,5:0.###}m\n" +
                $"             {_axisInfoB.Name} : {_offsetTrackerAxisB.DeltaValue,5:0.###}m");

            GizmoDrawer.ClippedLine(_center, _center + _axisVectorA * 1000, _axisInfoA.Color, 1.5f);
            GizmoDrawer.ClippedLine(_center, _center - _axisVectorA * 1000, _axisInfoA.Color & 0xAA_FF_FF_FF, 1.5f);

            GizmoDrawer.ClippedLine(_center, _center + _axisVectorB * 1000, _axisInfoB.Color, 1.5f);
            GizmoDrawer.ClippedLine(_center, _center - _axisVectorB * 1000, _axisInfoB.Color & 0xAA_FF_FF_FF, 1.5f);

            GizmoDrawer.Drawlist.AddCircleFilled(center2d, 3, planeColor);


            DeltaMatrix = Matrix4x4.CreateTranslation(
                _axisVectorA * _offsetTrackerAxisA.DeltaValue +
                _axisVectorB * _offsetTrackerAxisB.DeltaValue
            );
        }

        private (float a, float b) GetOffsets(in SceneViewState view)
        {
            Vector3 hitPoint = view.MouseRayHitOnPlane(_planeNormal, _center);
            Vector3 offset = hitPoint - _center;
            return (Vector3.Dot(offset, _axisVectorA), Vector3.Dot(offset, _axisVectorB));
        }
    }

    public class FreeTranslateAction : TransformAction
    {
        private readonly Vector3 _axisVectorX;
        private readonly Vector3 _axisVectorY;
        private readonly Vector3 _axisVectorZ;
        private ValueTracker<float>? _offsetTrackerX;
        private ValueTracker<float>? _offsetTrackerY;
        private ValueTracker<float>? _offsetTrackerZ;
        private float _snappingInterval;

        public FreeTranslateAction(
            Vector3 center,
            Vector3 axisVectorX, Vector3 axisVectorY, Vector3 axisVectorZ,
            Matrix4x4 baseMatrix, float snappingInterval)
            : base(baseMatrix, center)
        {
            _axisVectorX = axisVectorX;
            _axisVectorY = axisVectorY;
            _axisVectorZ = axisVectorZ;
            _snappingInterval = snappingInterval;
        }

        public static FreeTranslateAction Start(Vector3 center, Matrix4x4 baseMatrix,
            float snappingInterval, in SceneViewState view)
        {
            var mtx = baseMatrix;
            ReadOnlySpan<Vector3> axisVecs = stackalloc Vector3[]
            {
                Vector3.Normalize(new(mtx.M11, mtx.M12, mtx.M13)),
                Vector3.Normalize(new(mtx.M21, mtx.M22, mtx.M23)),
                Vector3.Normalize(new(mtx.M31, mtx.M32, mtx.M33)),
            };
            var action = new FreeTranslateAction(center,
                axisVecs[0], axisVecs[1], axisVecs[2],
                baseMatrix, snappingInterval);
            action.StartTransform(in view);
            return action;
        }


        protected override void OnStart(in SceneViewState view)
        {
            var offset = GetOffset(in view);

            _offsetTrackerX = new ValueTracker<float>(offset.X);
            _offsetTrackerY = new ValueTracker<float>(offset.Y);
            _offsetTrackerZ = new ValueTracker<float>(offset.Z);
        }

        protected override void OnUpdate(in SceneViewState view, bool isSnapping)
        {
            Debug.Assert(_offsetTrackerX != null);
            Debug.Assert(_offsetTrackerY != null);
            Debug.Assert(_offsetTrackerZ != null);

            var newOffset = GetOffset(in view);

            _offsetTrackerX.Update(newOffset.X, isSnapping ? _snappingInterval : null);
            _offsetTrackerY.Update(newOffset.Y, isSnapping ? _snappingInterval : null);
            _offsetTrackerZ.Update(newOffset.Z, isSnapping ? _snappingInterval : null);

            Vector2 center2d = GizmoDrawer.WorldToScreen(_center);

            GizmoDrawer.Drawlist.AddText(
                ImGui.GetFont(), 18,
                center2d, 0xFF_FF_FF_FF,
                $"Moving along {AxisInfo.Axis0.Name} : {_offsetTrackerX.DeltaValue,5:0.###}m\n" +
                $"             {AxisInfo.Axis1.Name} : {_offsetTrackerY.DeltaValue,5:0.###}m\n" +
                $"             {AxisInfo.Axis2.Name} : {_offsetTrackerZ.DeltaValue,5:0.###}m");


            GizmoDrawer.Drawlist.AddCircleFilled(center2d, 3, 0xFF_FF_FF_FF);


            DeltaMatrix = Matrix4x4.CreateTranslation(
                _axisVectorX * _offsetTrackerX.DeltaValue +
                _axisVectorY * _offsetTrackerY.DeltaValue +
                _axisVectorZ * _offsetTrackerZ.DeltaValue
            );
        }

        private Vector3 GetOffset(in SceneViewState view)
        {
            Vector3 hitPoint = view.MouseRayHitOnPlane(view.CamForwardVector, _center);
            Vector3 offset = hitPoint - _center;
            return offset;
        }
    }

    public class ScaleAction : TransformAction
    {
        private Vector2 _initialDirection;
        private ValueTracker<float>? _distanceTracker;
        private readonly (Vector3 axisVector, AxisInfo axisInfo)[] _scaleAxes;
        private readonly bool _isUniformScaling;
        private float _snappingInterval;

        public static ScaleAction Start(int axis, Vector3 center, Matrix4x4 baseMatrix,
            float snappingInterval, in SceneViewState view)
        {
            return Start(new int[] { axis }, false, center, baseMatrix, snappingInterval, in view);
        }

        public static ScaleAction Start(int axisA, int axisB, Vector3 center, Matrix4x4 baseMatrix,
            float snappingInterval, in SceneViewState view)
        {
            return Start(new int[] { axisA, axisB }, false, center, baseMatrix, snappingInterval, in view);
        }

        public static ScaleAction StartUniformScale(Vector3 center, Matrix4x4 baseMatrix,
            float snappingInterval, in SceneViewState view)
        {
            return Start(new int[] { 0, 1, 2 }, true, center, baseMatrix, snappingInterval, in view);
        }

        private static ScaleAction Start(int[] axes, bool isUniformScaling, Vector3 center, Matrix4x4 baseMatrix,
            float snappingInterval, in SceneViewState view)
        {
            var mtx = baseMatrix;
            var axisVecs = new Vector3[]
            {
                Vector3.Normalize(new(mtx.M11, mtx.M12, mtx.M13)),
                Vector3.Normalize(new(mtx.M21, mtx.M22, mtx.M23)),
                Vector3.Normalize(new(mtx.M31, mtx.M32, mtx.M33)),
            };
            var action = new ScaleAction(center,
                axes.Select(axis=>(axisVecs[axis], AxisInfo.Axes[axis])).ToArray(), 
                isUniformScaling, baseMatrix, snappingInterval
            );

            action.StartTransform(in view);
            return action;
        }

        private ScaleAction(Vector3 center, (Vector3 axisVector, AxisInfo axisInfo)[] scaleAxes, bool isUniformScaling, Matrix4x4 baseMatrix, float snappingInterval)
            : base(baseMatrix, center)
        {
            _scaleAxes = scaleAxes;
            _isUniformScaling = isUniformScaling;
            _snappingInterval = snappingInterval;
        }


        protected override void OnStart(in SceneViewState view)
        {
            (float distance, _initialDirection) = GetDistanceAndDirection(in view);
            _distanceTracker = new ValueTracker<float>(distance);
        }

        protected override void OnUpdate(in SceneViewState view, bool isSnapping)
        {
            Debug.Assert(_distanceTracker != null);

            var (newDistance, direction) = GetDistanceAndDirection(in view);
            _distanceTracker.Update(newDistance, isSnapping ? _snappingInterval : null);

            float sign = Vector2.Dot(direction, _initialDirection) > 0 ? 1 : -1;
            float scaleFactor = _distanceTracker.DeltaScaleFactor * sign;


            Vector2 center2d = GizmoDrawer.WorldToScreen(_center);
            string scaleAxisStr = string.Join(string.Empty, _scaleAxes.Select(x => x.axisInfo.Name));

            GizmoDrawer.Drawlist.AddText(
                ImGui.GetFont(), 18,
                center2d, 0xFF_FF_FF_FF,
                $"Scaling {scaleAxisStr} : {scaleFactor,5:0.###}x");

            if (!_isUniformScaling)
            {
                for (int i = 0; i < _scaleAxes.Length; i++)
                {
                    var (axisVector, axisInfo) = _scaleAxes[i];
                    GizmoDrawer.ClippedLine(_center, _center + axisVector * 1000, axisInfo.Color, 1.5f);
                    GizmoDrawer.ClippedLine(_center, _center - axisVector * 1000, axisInfo.Color & 0xAA_FF_FF_FF, 1.5f);
                }
            }

            GizmoDrawer.Drawlist.AddCircleFilled(center2d, 3, 0xFF_FF_FF_FF);

            if (float.IsNaN(scaleFactor))
                scaleFactor = 1;

            DeltaMatrix = CreateScaleMatrix(scaleFactor);
        }

        private Matrix4x4 CreateScaleMatrix(float scaleFactor)
        {
            var unitX = Vector3.UnitX;
            var unitY = Vector3.UnitY;
            var unitZ = Vector3.UnitZ;

            //scale each unit vector along each of the given axes
            for (int i = 0; i < _scaleAxes.Length; i++)
            {
                var (axisVector, _) = _scaleAxes[i];

                unitX += Vector3.Dot(unitX, axisVector) * axisVector * (scaleFactor - 1);
                unitY += Vector3.Dot(unitY, axisVector) * axisVector * (scaleFactor - 1);
                unitZ += Vector3.Dot(unitZ, axisVector) * axisVector * (scaleFactor - 1);
            }

            //construct transform matrix from computed unit vectors
            return new Matrix4x4(
                unitX.X, unitX.Y, unitX.Z, 0,
                unitY.X, unitY.Y, unitY.Z, 0,
                unitZ.X, unitZ.Y, unitZ.Z, 0,
                0, 0, 0, 1
                );
        }

        private (float distance, Vector2 direction) GetDistanceAndDirection(in SceneViewState view)
        {
            Vector2 vec = view.MousePosition - view.WorldToScreen(_center);
            float distance = vec.Length();
            return (distance, vec/distance);
        }
    }


    public interface ITransformAction
    {
        public Matrix4x4 FinalMatrix { get; }
        public Matrix4x4 DeltaMatrix { get; }

        public void StartTransform(in SceneViewState view);
        public ActionUpdateResult Update(in SceneViewState view, bool isSnapping);
    }

    public class ValueTracker<TNumber>
        where TNumber : struct, IFloatingPoint<TNumber>
    {
        public TNumber StartValue { get; private set; }
        public TNumber DeltaValue { get; private set; }

        public TNumber Value => StartValue + DeltaValue;
        public TNumber DeltaScaleFactor => Value / StartValue;

        public ValueTracker(TNumber startValue)
        {
            StartValue = startValue;
            DeltaValue = TNumber.Zero;

            if (StartValue == -TNumber.Zero)
                StartValue = TNumber.Zero; //ensure that we never get -0
        }

        public void Update(TNumber newValue, TNumber? snappingInterval)
        {
            DeltaValue = newValue - StartValue;

            if (snappingInterval is not null)
            {
                TNumber snapping = snappingInterval.Value;
                DeltaValue = TNumber.Round(DeltaValue / snapping) * snapping;
            }

            if (DeltaValue == -TNumber.Zero)
                DeltaValue = TNumber.Zero; //ensure that we never get -0
        }
    }

    public class AxisInfo
    {
        public static readonly AxisInfo ViewRotationAxis = new(0xFF_FF_FF_FF, "View");

        public static readonly AxisInfo Axis0 = new(0xFF_44_44_FF, "X", ImGuiKey.X);
        public static readonly AxisInfo Axis1 = new(0xFF_FF_88_44, "Y", ImGuiKey.Y);
        public static readonly AxisInfo Axis2 = new(0xFF_44_FF_44, "Z", ImGuiKey.Z);

        public static readonly AxisInfo[] Axes = new AxisInfo[]
        {
            Axis0,
            Axis1,
            Axis2
        };

        public AxisInfo(uint color, string name, ImGuiKey key = ImGuiKey.None)
        {
            Color = color;
            Name = name;
            Key = key;
        }

        public uint Color { get; private set; }

        public string Name { get; private set; }

        public ImGuiKey Key { get; private set; }
    }
}
