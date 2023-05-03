using ImGuiNET;
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
        public Matrix4x4 SmoothFinalMatrix { get; private set; }

        public Matrix4x4 DeltaMatrix { get; protected set; }
        public Matrix4x4 SmoothDeltaMatrix { get; protected set; }

        public TransformAction(Matrix4x4 baseMatrix, Vector3 center)
        {
            _baseMatrix = baseMatrix;
            _center = center;
        }

        protected abstract void OnStart(in CameraState camera, in Vector3 mouseRayDirection);

        protected abstract void OnUpdate(in CameraState camera, in Vector3 mouseRayDirection, float? snappingInterval);


        public void StartTransform(in CameraState camera, in Vector3 mouseRayDirection)
        {
            OnStart(camera, in mouseRayDirection);
        }

        public ActionUpdateResult Update(in CameraState camera, in Vector3 mouseRayDirection, float? snappingInterval)
        {
            if (ImGui.IsMouseDown(ImGuiMouseButton.Right))
            {
                DeltaMatrix = Matrix4x4.Identity;
                FinalMatrix = _baseMatrix;
                return ActionUpdateResult.Cancel;
            }

            OnUpdate(camera, in mouseRayDirection, snappingInterval);

            var moveCenterToOrigin = Matrix4x4.CreateTranslation(_center);
            var moveBack = Matrix4x4.CreateTranslation(-_center);

            DeltaMatrix = moveBack * DeltaMatrix * moveCenterToOrigin;
            SmoothDeltaMatrix = moveBack * SmoothDeltaMatrix * moveCenterToOrigin;

            FinalMatrix = _baseMatrix * DeltaMatrix;
            SmoothFinalMatrix = _baseMatrix * SmoothDeltaMatrix;

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
        private ValueTracker? _rotationTracker;

        public static AxisRotationAction Start(int axis, Vector3 center, Matrix4x4 baseMatrix, in CameraState camera, in Vector3 mouseRayDirection)
        {
            var mtx = baseMatrix;
            ReadOnlySpan<Vector3> axisVecs = stackalloc Vector3[]
            {
                new(mtx.M11, mtx.M12, mtx.M13),
                new(mtx.M21, mtx.M22, mtx.M23),
                new(mtx.M31, mtx.M32, mtx.M33),
            };
            var action = new AxisRotationAction(center, axisVecs[axis], AxisInfo.Axes[axis], baseMatrix);
            action.StartTransform(in camera, in mouseRayDirection);
            return action;
        }

        public static AxisRotationAction StartViewAxisRotation(Vector3 center, Matrix4x4 baseMatrix, in CameraState camera, in Vector3 mouseRayDirection)
        {
            var action = new AxisRotationAction(center, camera.ForwardVector, AxisInfo.ViewRotationAxis, baseMatrix);
            action.StartTransform(in camera, in mouseRayDirection);
            return action;
        }

        private AxisRotationAction(Vector3 center, Vector3 axisVector, AxisInfo axisInfo, Matrix4x4 baseMatrix)
            : base(baseMatrix, center)
        {
            _axisVector = axisVector;
            _axisInfo = axisInfo;
        }


        protected override void OnStart(in CameraState camera, in Vector3 mouseRayDirection)
        {
            _rotationTracker = new ValueTracker(GetAngle(camera));
        }

        protected override void OnUpdate(in CameraState camera, in Vector3 mouseRayDirection, float? snappingInterval)
        {
            Debug.Assert(_rotationTracker != null);
            
            double inputAngle = GetAngle(camera);
            double newAngle = _rotationTracker.Value + GetShortestRotationBetween(_rotationTracker.Value, inputAngle, 360);
            _rotationTracker.Update(newAngle, snappingInterval);

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
            SmoothDeltaMatrix = Matrix4x4.CreateFromAxisAngle(_axisVector, (float)(_rotationTracker.SmoothDeltaValue * Math.PI / 180f));
        }

        private double GetAngle(in CameraState camera)
        {
            Vector2 relativeMousePos = ImGui.GetMousePos() - GizmoDrawer.WorldToScreen(_center);
            Vector3 centerToCamDir = Vector3.Normalize(camera.Position - _center);
            float axisCamDot = Vector3.Dot(centerToCamDir, _axisVector);

            if (Math.Abs(axisCamDot) < 0.5f)
            {
                Vector3 lineDir = Vector3.Normalize(Vector3.Cross(_axisVector, centerToCamDir));
                Vector2 lineDir2d = Vector2.Normalize(GizmoDrawer.WorldToScreen(_center + lineDir) - GizmoDrawer.WorldToScreen(_center));

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
        private ValueTracker? _rotationXTracker;
        private ValueTracker? _rotationYTracker;

        public static TrackballRotationAction Start(Vector3 center, Matrix4x4 baseMatrix, in CameraState camera, in Vector3 mouseRayDirection)
        {
            var action = new TrackballRotationAction(center, baseMatrix);
            action.StartTransform(in camera, in mouseRayDirection);
            return action;
        }

        public TrackballRotationAction(Vector3 center, Matrix4x4 baseMatrix)
            : base(baseMatrix, center)
        {

        }

        protected override void OnStart(in CameraState camera, in Vector3 mouseRayDirection)
        {
            (double angleX, double angleY) = GetAngles();

            _rotationXTracker = new ValueTracker(angleX);
            _rotationYTracker = new ValueTracker(angleY);
        }

        protected override void OnUpdate(in CameraState camera, in Vector3 mouseRayDirection, float? snappingInterval)
        {
            if (_rotationXTracker == null || _rotationYTracker == null)
                throw new NullReferenceException($"Important instance variables are null, {nameof(ITransformAction.StartTransform)} has not been called");

            (double angleX, double angleY) = GetAngles();

            _rotationXTracker.Update(angleX, snappingInterval);
            _rotationYTracker.Update(angleY, snappingInterval);

            Vector2 center2d = GizmoDrawer.WorldToScreen(_center);

            //GizmoDrawer.Drawlist.AddText(
            //    ImGui.GetFont(), 18,
            //    center2d + new Vector2(RotationGizmo.GIMBAL_SIZE * 2, -RotationGizmo.GIMBAL_SIZE * 0.5f), AxisInfo.ViewRotationAxis.Color,
            //    $"Rotaing along Trackball : {-_rotationXTracker.DeltaValue,5:0.#}° {-_rotationYTracker.DeltaValue,5:0.#}°");

            DeltaMatrix = Matrix4x4.CreateFromAxisAngle(camera.UpVector, (float)(_rotationXTracker.DeltaValue * MathF.PI / 180f));
            DeltaMatrix *= Matrix4x4.CreateFromAxisAngle(camera.RightVector, (float)(_rotationYTracker.DeltaValue * MathF.PI / 180f));
        }

        private (double angleX, double angleY) GetAngles()
        {
            Vector2 relativeMousePos = ImGui.GetMousePos() - GizmoDrawer.WorldToScreen(_center);

            return (
                relativeMousePos.X,
                relativeMousePos.Y
                );
        }
    }



    public interface ITransformAction
    {
        public Matrix4x4 FinalMatrix { get; }
        public Matrix4x4 DeltaMatrix { get; }

        public void StartTransform(in CameraState camera, in Vector3 mouseRayDirection);
        public ActionUpdateResult Update(in CameraState camera, in Vector3 mouseRayDirection, float? snappingInterval);
    }

    public class ValueTracker
    {
        public double StartValue { get; private set; }
        public double DeltaValue { get; private set; }
        public double SmoothDeltaValue { get; private set; }

        public double Value => StartValue + DeltaValue;
        public double SmoothValue => StartValue + SmoothDeltaValue;

        public ValueTracker(double startValue)
        {
            StartValue = startValue;

            if (StartValue == -0)
                StartValue = 0;
        }

        public void Update(double newValue, float? snappingInterval)
        {
            DeltaValue = newValue - StartValue;

            if (snappingInterval is not null)
            {
                var snapping = snappingInterval.Value;
                DeltaValue = Math.Round(DeltaValue / snapping) * snapping;
            }

            if (DeltaValue == -0)
                DeltaValue = 0; //ensure that we never get -0
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
