﻿using ImGuiNET;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace EditTK
{
    [Flags]
    public enum HoveredAxis
    {
        NONE = 0,
        X_AXIS = 1,
        Y_AXIS = 2,
        Z_AXIS = 4,
        XY_PLANE = X_AXIS | Y_AXIS,
        XZ_PLANE = X_AXIS | Z_AXIS,
        YZ_PLANE = Y_AXIS | Z_AXIS,
        ALL_AXES = X_AXIS | Y_AXIS | Z_AXIS,
        FREE = ALL_AXES,
        VIEW_AXIS = 8,
        TRACKBALL = 16
    }

    public record struct Rect(Vector2 TopLeft, Vector2 BottomRight)
    {
        public Vector2 Size => BottomRight - TopLeft;
    }

    public record struct CameraState(Vector3 Position, Vector3 ForwardVector, Vector3 UpVector, Quaternion Rotation)
    {
        public Vector3 RightVector => Vector3.Cross(ForwardVector, UpVector);
    }

    /// <summary>
    /// Provides useful methods for evaluating the results of the Gizmo functions in <see cref="GizmoDrawer"/>
    /// </summary>
    public static class GizmoResultHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSingleAxis(HoveredAxis hoveredAxis, int axis)
        {
            return (int)hoveredAxis == 1 << axis;
        }

        public static bool IsSingleAxis(HoveredAxis hoveredAxis, out int axis)
        {
            switch (hoveredAxis)
            {
                case HoveredAxis.X_AXIS:
                    axis = 0;
                    return true;
                case HoveredAxis.Y_AXIS:
                    axis = 1;
                    return true;
                case HoveredAxis.Z_AXIS:
                    axis = 2;
                    return true;
                default:
                    axis = -1;
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPlane(HoveredAxis hoveredAxis, int axisA, int axisB)
        {
            return (int)hoveredAxis == (1 << axisA | 1 << axisB);
        }

        public static bool IsPlane(HoveredAxis hoveredAxis, out int axisA, out int axisB)
        {
            switch (hoveredAxis)
            {
                case HoveredAxis.XY_PLANE:
                    axisA = 0;
                    axisB = 1;
                    return true;
                case HoveredAxis.XZ_PLANE:
                    axisA = 0;
                    axisB = 2;
                    return true;
                case HoveredAxis.YZ_PLANE:
                    axisA = 1;
                    axisB = 2;
                    return true;
                default:
                    axisA = -1;
                    axisB = -1;
                    return false;
            }
        }

        public static bool IsPlane(HoveredAxis hoveredAxis, out int orthogonalAxis)
        {
            switch (hoveredAxis)
            {
                case HoveredAxis.XY_PLANE:
                    orthogonalAxis = 2;
                    return true;
                case HoveredAxis.XZ_PLANE:
                    orthogonalAxis = 1;
                    return true;
                case HoveredAxis.YZ_PLANE:
                    orthogonalAxis = 0;
                    return true;
                default:
                    orthogonalAxis = -1;
                    return false;
            }
        }
    }

    /// <summary>
    /// Draws and handles Gizmos in Imgui
    /// </summary>
    public static class GizmoDrawer
    {
        [DllImport("cimgui")]
        private static extern bool igItemHoverable(Rect bb, uint id);

        private static bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float AP_x = p.X - a.X;
            float AP_y = p.Y - a.Y;

            float CP_x = p.X - b.X;
            float CP_y = p.Y - b.Y;

            bool s_ab = (b.X - a.X) * AP_y - (b.Y - a.Y) * AP_x > 0.0;

            if (/*s_ac*/   (c.X - a.X) * AP_y - (c.Y - a.Y) * AP_x > 0.0 == s_ab) return false;

            if (/*s_cb*/   (c.X - b.X) * CP_y - (c.Y - b.Y) * CP_x > 0.0 != s_ab) return false;

            return true;
        }


        private static bool IsPointInQuad(Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float AP_x = p.X - a.X;
            float AP_y = p.Y - a.Y;

            float CP_x = p.X - c.X;
            float CP_y = p.Y - c.Y;

            bool s_ab = (b.X - a.X) * AP_y - (b.Y - a.Y) * AP_x > 0.0;

            if (/*s_ad*/   (d.X - a.X) * AP_y - (d.Y - a.Y) * AP_x > 0.0 == s_ab) return false;

            if (/*s_cb*/   (b.X - c.X) * CP_y - (b.Y - c.Y) * CP_x > 0.0 == s_ab) return false;

            if (/*s_cd*/   (d.X - c.X) * CP_y - (d.Y - c.Y) * CP_x > 0.0 != s_ab) return false;

            return true;
        }

        public static Vector3 IntersectPoint(Vector3 rayVector, Vector3 rayPoint, Vector3 planeNormal, Vector3 planePoint)
        {
            //code from: https://rosettacode.org/wiki/Find_the_intersection_of_a_line_with_a_plane
            var diff = rayPoint - planePoint;
            var prod1 = Vector3.Dot(diff, planeNormal);
            var prod2 = Vector3.Dot(rayVector, planeNormal);
            var prod3 = prod1 / prod2;
            return rayPoint - rayVector * prod3;
        }



        //CRITICAL: Do not read from or write to these, unless you understand how they work!
        private static readonly Dictionary<uint, int> __lastFrameHoveredParts__ = new();
        private static int s__currentFrameHoveredPart__;
        private static int s__currentHoverIndex__;

        /// <summary>
        /// Declares a hoverable gizmo part and determines if it's actually hovered or occluded by another part
        /// </summary>
        /// <param name="isHovered">The supposite hover state</param>
        /// <returns>The actual hover state</returns>
        public static bool HoverablePart(bool isHovered)
        {
            bool wasHovered = __lastFrameHoveredParts__[s_itemID] ==
                              s__currentHoverIndex__;

            if (isHovered)
                s__currentFrameHoveredPart__ = s__currentHoverIndex__;

            s__currentHoverIndex__++;

            return wasHovered;
        }

        const uint HOVER_COLOR = 0xFF_33_FF_FF;

        const int ELLIPSE_NUM_SEGMENTS = 32;

        private static Matrix4x4 s_viewProjectionMat;
        private static CameraState s_cam;
        private static Rect s_viewport;
        private static uint s_itemID;
        private static readonly Vector2[] s_ellipsePoints = new Vector2[ELLIPSE_NUM_SEGMENTS + 1];
        private static readonly Vector3[] s_transformMatVectors = new Vector3[4];

        private static uint[] s_axisColors = new uint[]
        {
            0xFF_44_44_FF,
            0xFF_FF_88_44,
            0xFF_44_FF_44
        };
        private static IntPtr s_orientationCubeTexture;

        public static uint AlphaBlend(uint colA, uint colB)
        {
            float blend = (colB >> 24 & 0xFF) / 255f;

            uint r = (uint)((colA >> 0 & 0xFF) * (1 - blend) + (colB >> 0 & 0xFF) * blend);
            uint g = (uint)((colA >> 8 & 0xFF) * (1 - blend) + (colB >> 8 & 0xFF) * blend);
            uint b = (uint)((colA >> 16 & 0xFF) * (1 - blend) + (colB >> 16 & 0xFF) * blend);

            uint a = Math.Min((colA >> 24 & 0xFF) + (colB >> 24 & 0xFF), 255);

            return
                r |
                g << 8 |
                b << 16 |
                a << 24;
        }

        public static uint AdditiveBlend(uint colA, uint colB)
        {
            uint r = Math.Min((colA >> 0 & 0xFF) + (colB >> 0 & 0xFF), 255);
            uint g = Math.Min((colA >> 8 & 0xFF) + (colB >> 8 & 0xFF), 255);
            uint b = Math.Min((colA >> 16 & 0xFF) + (colB >> 16 & 0xFF), 255);
            uint a = Math.Min((colA >> 24 & 0xFF) + (colB >> 24 & 0xFF), 255);

            return
                r |
                g << 8 |
                b << 16 |
                a << 24;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ColorWithAlpha(uint colA, uint alpha) => colA & 0x00_FF_FF_FF | alpha << 24;

        /// <summary>
        /// Sets the color used by all gizmos for the x,y and z axis to the desired colrs
        /// </summary>
        /// <param name="xAxisColor">The new color for the X-Axis</param>
        /// <param name="yAxisColor">The new color for the Y-Axis</param>
        /// <param name="zAxisColor">The new color for the Z-Axis</param>
        public static void SetAxisColors(uint xAxisColor, uint yAxisColor, uint zAxisColor)
        {
            s_axisColors[0] = xAxisColor;
            s_axisColors[1] = yAxisColor;
            s_axisColors[2] = zAxisColor;
        }

        /// <summary>
        /// Gets the color used by all gizmos for the given axis
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetAxisColor(int axis) => s_axisColors[axis];

        /// <summary>
        /// Set's the texture used for the OrientationCube gizmo
        /// </summary>
        /// <param name="user_texture_id">The new texture for the OrientationCube</param>
        public static void SetOrientationCubeTexture(IntPtr user_texture_id)
        {
            s_orientationCubeTexture = user_texture_id;
        }

        /// <summary>
        /// The Drawlist used for drawing all Gizmos
        /// </summary>
        public static ImDrawListPtr Drawlist { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="drawlist">The drawlist that will be used for drawing all Gizmos</param>
        public static void BeginGizmoDrawing(string id, ImDrawListPtr drawlist, Matrix4x4 viewProjectionMat, Rect viewport, CameraState cam)
        {
            s__currentHoverIndex__ = 0;
            s__currentFrameHoveredPart__ = -1;
            s_viewProjectionMat = viewProjectionMat;
            Drawlist = drawlist;

            s_viewport = viewport;
            s_cam = cam;
            s_itemID = ImGui.GetID(id);

            if (!__lastFrameHoveredParts__.ContainsKey(s_itemID))
                __lastFrameHoveredParts__[s_itemID] = -1;
        }

        public static void EndGizmoDrawing()
        {
            bool isHovered = igItemHoverable(s_viewport, s_itemID);

            __lastFrameHoveredParts__[s_itemID] = -1;

            if (isHovered)
                __lastFrameHoveredParts__[s_itemID] = s__currentFrameHoveredPart__;
        }

        /// <summary>
        /// Maps a point in world(3d) space to a point screen(2d) space
        /// </summary>
        /// <param name="vec"></param>
        /// <returns></returns>
        public static Vector2 WorldToScreen(Vector3 vec)
        {
            var vec4 = Vector4.Transform(new Vector4(vec, 1), s_viewProjectionMat);

            var vec2 = new Vector2(vec4.X, vec4.Y) / Math.Max(0, vec4.W);

            vec2.Y *= -1;

            vec2 += Vector2.One;

            return s_viewport.TopLeft + vec2 * s_viewport.Size * 0.5f;
        }

        /// <summary>
        /// Draws a line clipped by the camera plane
        /// </summary>
        public static void ClippedLine(Vector3 pointA, Vector3 pointB, uint color, float thickness)
        {
            var clipPlaneNormal = s_cam.ForwardVector;
            var clipPlaneOrigin = s_cam.Position + s_cam.ForwardVector * 0.1f;

            bool pointABehindCam = Vector3.Dot(clipPlaneNormal, pointA - clipPlaneOrigin) <= 0;
            bool pointBBehindCam = Vector3.Dot(clipPlaneNormal, pointB - clipPlaneOrigin) <= 0;

            if (pointABehindCam && pointBBehindCam)
                return;

            if (pointABehindCam)
                pointA = IntersectPoint(Vector3.Normalize(pointA - pointB), pointB, clipPlaneNormal, clipPlaneOrigin);

            if (pointBBehindCam)
                pointB = IntersectPoint(Vector3.Normalize(pointB - pointA), pointA, clipPlaneNormal, clipPlaneOrigin);

            Drawlist.AddLine(WorldToScreen(pointA), WorldToScreen(pointB), color, thickness);
        }


        /// <summary>
        /// Draws/Handles an OrientationCube Gizmo
        /// </summary>
        /// <param name="position">The position on the screen the cube should be drawn at</param>
        /// <param name="radius">The radius of the Gizmo in screen(2d) space</param>
        /// <param name="facingDirection">The direction the hovered face is facing (in world(3d) space)</param>
        /// 
        /// <returns><see langword="true"/> if the gizmo is hovered, <see langword="false"/> if not</returns>
        public static bool OrientationCube(Vector2 position, float radius, out Vector3 facingDirection)
        {
            var rotMtx = Matrix4x4.CreateFromQuaternion(Quaternion.Inverse(s_cam.Rotation));

            var cubeToScreenSpace = rotMtx * Matrix4x4.CreateScale(radius / 2, -radius / 2, radius / 2) *
                                            Matrix4x4.CreateTranslation(new Vector3(position.X, position.Y, 0));

            float edgeWidth = 0.1f;

            Vector3 edgeHit = Vector3.Zero;
            Vector3 faceHit = Vector3.Zero;

            void CubeSide(Vector3 up, Vector3 forward, uint col, Vector2 uvOffset, in Matrix4x4 rotMtx)
            {
                Matrix4x4 mtx =
                                Matrix4x4.CreateTranslation(new Vector3(0, 0, 1)) *
                                Matrix4x4.CreateWorld(
                                    Vector3.Zero,
                                    forward,
                                    up
                                    ) *
                                cubeToScreenSpace;

                Vector2 Transform(Vector2 position)
                {
                    return Vector2.Transform(position, mtx);
                }

                if (Vector3.Transform(forward, rotMtx).Z < -0.001)
                {
                    var mpos = ImGui.GetMousePos();

                    var center = Transform(Vector2.Zero);

                    var u_vec = Transform(Vector2.UnitX) - center;
                    var v_vec = Transform(Vector2.UnitY) - center;

                    var m_vec = mpos - center;

                    var u_norm = Vector2.Normalize(u_vec);
                    var v_norm = Vector2.Normalize(v_vec);


                    //v up ortho coord system
                    var y_dir = v_norm;
                    var x_dir = new Vector2(y_dir.Y, -y_dir.X);

                    var slope = Vector2.Dot(u_norm, y_dir) / Vector2.Dot(u_norm, x_dir);

                    var mx = Vector2.Dot(m_vec, x_dir);
                    var my = Vector2.Dot(m_vec, y_dir);

                    var w = Vector2.Dot(u_vec, x_dir);
                    var h = Vector2.Dot(v_vec, y_dir);

                    var mu = mx / w;
                    var mv = (my - slope * mx) / h;

                    var mu_abs = Math.Abs(mu);
                    var mv_abs = Math.Abs(mv);

                    if (mu_abs <= 1 && mv_abs <= 1)
                    {
                        faceHit = -forward + mv * up + mu * Vector3.Cross(forward, up);

                        if (mu_abs <= 1 - edgeWidth && mv_abs <= 1 - edgeWidth)
                            col = AlphaBlend(col, 0x88_CC_FF_FF);
                        else
                            edgeHit = faceHit;
                    }


                    if (s_orientationCubeTexture != IntPtr.Zero)
                    {
                        Drawlist.AddImageQuad(
                        s_orientationCubeTexture,
                        Transform(new Vector2(-1, 1)),
                        Transform(new Vector2(1, 1)),
                        Transform(new Vector2(1, -1)),
                        Transform(new Vector2(-1, -1)),
                        uvOffset + new Vector2(0, 0),
                        uvOffset + new Vector2(0.25f, 0),
                        uvOffset + new Vector2(0.25f, 0.5f),
                        uvOffset + new Vector2(0, 0.5f),
                        col
                        );
                    }
                    else
                    {
                        Drawlist.AddQuadFilled(
                        Transform(new Vector2(-1, -1)),
                        Transform(new Vector2(1, -1)),
                        Transform(new Vector2(1, 1)),
                        Transform(new Vector2(-1, 1)), col);
                    }

                    Drawlist.AddQuad(
                        Transform(new Vector2(-1, -1)),
                        Transform(new Vector2(1, -1)),
                        Transform(new Vector2(1, 1)),
                        Transform(new Vector2(-1, 1)), col, 1.5f);
                }
            }


            CubeSide(Vector3.UnitY, Vector3.UnitX, s_axisColors[0], new Vector2(0.5f, 0), rotMtx);
            CubeSide(Vector3.UnitY, -Vector3.UnitX, s_axisColors[0], new Vector2(0.75f, 0), rotMtx);
            CubeSide(Vector3.UnitY, -Vector3.UnitZ, s_axisColors[2], new Vector2(0, 0), rotMtx);
            CubeSide(Vector3.UnitY, Vector3.UnitZ, s_axisColors[2], new Vector2(0.25f, 0), rotMtx);

            CubeSide(-Vector3.UnitZ, -Vector3.UnitY, s_axisColors[1], new Vector2(0, 0.5f), rotMtx);
            CubeSide(Vector3.UnitZ, Vector3.UnitY, s_axisColors[1], new Vector2(0.25f, 0.5f), rotMtx);

            float Round(float num) => (float)Math.Round(num);


            Vector2 Transform(Vector3 position)
            {
                var vec = Vector3.Transform(position, cubeToScreenSpace);

                return new Vector2(vec.X, vec.Y);
            }

            Vector3 snappedHitPos = new(
                Math.Abs(faceHit.X) < 1 - edgeWidth ? 0 : Round(faceHit.X),
                Math.Abs(faceHit.Y) < 1 - edgeWidth ? 0 : Round(faceHit.Y),
                Math.Abs(faceHit.Z) < 1 - edgeWidth ? 0 : Round(faceHit.Z)
                );

            uint highlight_col = 0xFF_88_CC_FF;

            if (edgeHit != Vector3.Zero)
            {
                if (snappedHitPos.X == 0)
                    Drawlist.AddLine(Transform(new Vector3(-1, snappedHitPos.Y, snappedHitPos.Z)),
                               Transform(new Vector3(1, snappedHitPos.Y, snappedHitPos.Z))
                        , highlight_col, 2.5f);

                else if (snappedHitPos.Y == 0)
                    Drawlist.AddLine(Transform(new Vector3(snappedHitPos.X, -1, snappedHitPos.Z)),
                               Transform(new Vector3(snappedHitPos.X, 1, snappedHitPos.Z))
                        , highlight_col, 2.5f);

                else if (snappedHitPos.Z == 0)
                    Drawlist.AddLine(Transform(new Vector3(snappedHitPos.X, snappedHitPos.Y, -1)),
                               Transform(new Vector3(snappedHitPos.X, snappedHitPos.Y, 1))
                        , highlight_col, 2.5f);
                else
                    Drawlist.AddCircleFilled(Transform(snappedHitPos), 2f, highlight_col);
            }
            else if (faceHit != Vector3.Zero)
            {
                Drawlist.AddCircleFilled(Transform(snappedHitPos), 2f, 0xFF_FF_FF_FF);
            }

            facingDirection = snappedHitPos;

            return HoverablePart(snappedHitPos != Vector3.Zero);
        }

        public static float Get3dGizmoScaling(Vector3 gizmoPosition, float gizmoSize2d)
            => gizmoSize2d / (WorldToScreen(gizmoPosition + s_cam.RightVector) - WorldToScreen(gizmoPosition)).X;


        /// <summary>
        /// Draws/Handles a Rotation Transform-Gizmo as seen in Blender, Unity, Maya etc.
        /// </summary>
        /// <param name="transformMatrix">The Transform Matrix of the object the Transform Gizmo is used for</param>
        /// <param name="radius">The radius of the Gizmo in screen(2d) space</param>
        /// <param name="hoveredAxis">The associated axis of the hovered part, use <see cref="GizmoResultHelper"/>
        /// to help interpreting it</param>
        /// 
        /// <returns><see langword="true"/> if the gizmo is hovered, <see langword="false"/> if not</returns>
        public static bool RotationGizmo(in Matrix4x4 transformMatrix, float radius, out HoveredAxis hoveredAxis)
        {
            const float HOVER_LINE_THICKNESS = 5;

            var mtx = transformMatrix;
            s_transformMatVectors[0] = new(mtx.M11, mtx.M12, mtx.M13);
            s_transformMatVectors[1] = new(mtx.M21, mtx.M22, mtx.M23);
            s_transformMatVectors[2] = new(mtx.M31, mtx.M32, mtx.M33);

            float r = radius - 2;

            Vector2 mousePos = ImGui.GetMousePos();

            Vector3 center = transformMatrix.Translation;
            Vector2 center2d = WorldToScreen(center);

            float gizmoScaleFactor = Get3dGizmoScaling(center, 1);

            bool AxisGimbal(int axis)
            {
                var (scaleVecX, scaleVecY) = GetBillboardPlane2d(axis, center, gizmoScaleFactor);

                #region generating lines
                for (int i = 0; i <= ELLIPSE_NUM_SEGMENTS / 2; i++)
                {
                    double angle = i * Math.PI * 2 / ELLIPSE_NUM_SEGMENTS;

                    Vector2 vec = scaleVecX * (float)Math.Sin(angle) + scaleVecY * (float)Math.Cos(angle);

                    s_ellipsePoints[i] = center2d + vec * r;
                }
                #endregion


                bool isHovered = false;

                {
                    #region hover test
                    Vector2 diff = mousePos - center2d;

                    if (Matrix3x2.Invert(new Matrix3x2(scaleVecX.X, scaleVecX.Y, scaleVecY.X, scaleVecY.Y, 0, 0), out var invScale))
                    {
                        //gimbal is an ellipse Width != 0

                        var orthoScaleVecY = new Vector2(scaleVecY.Y, -scaleVecY.X);

                        if (Vector2.Dot(scaleVecX, orthoScaleVecY) < 0)
                            orthoScaleVecY *= -1;

                        if (Vector2.Dot(diff, orthoScaleVecY) > 0) //TODO
                        {
                            float invScaleX = 1 / scaleVecX.Length();
                            float invScaleY = 1 / scaleVecY.Length();

                            Vector2 diffT = Vector2.Transform(diff, invScale);

                            isHovered = Math.Abs(diffT.Length() - r) < HOVER_LINE_THICKNESS * (Vector2.Normalize(diffT) * new Vector2(invScaleX, invScaleY)).Length();
                        }
                    }
                    else
                    {
                        //gimbal is a straigt line Width == 0

                        isHovered = Math.Abs(Vector2.Dot(diff, new Vector2(scaleVecY.Y, -scaleVecY.X))) < HOVER_LINE_THICKNESS &&
                                    Math.Abs(Vector2.Dot(diff, new Vector2(scaleVecY.X, scaleVecY.Y))) < r;
                    }
                    #endregion
                }

                uint color = HoverablePart(isHovered) ? HOVER_COLOR : s_axisColors[axis];

                //draw the Gimbal
                Drawlist.AddPolyline(ref s_ellipsePoints[0], ELLIPSE_NUM_SEGMENTS / 2 + 1, color, ImDrawFlags.None, 2.5f);

                return isHovered;
            }

            Drawlist.AddCircleFilled(center2d, radius, 0x55_FF_FF_FF, 32);
            Drawlist.AddCircle(center2d, radius, 0xFF_FF_FF_FF, 32, 1.5f);

            float distanceMouseToCenter2d = Vector2.Distance(center2d, mousePos);

            hoveredAxis = HoveredAxis.NONE;

            if (HoverablePart(distanceMouseToCenter2d < radius))
                hoveredAxis = HoveredAxis.TRACKBALL;

            if (hoveredAxis == HoveredAxis.TRACKBALL)
                Drawlist.AddCircleFilled(center2d, radius, 0x33_FF_FF_FF, 32);

            if (AxisGimbal(0))
                hoveredAxis = HoveredAxis.X_AXIS;
            if (AxisGimbal(1))
                hoveredAxis = HoveredAxis.Y_AXIS;
            if (AxisGimbal(2))
                hoveredAxis = HoveredAxis.Z_AXIS;

            float camVecGimbalRadius = radius + 10;


            if (HoverablePart(Math.Abs(distanceMouseToCenter2d - camVecGimbalRadius) < 5))
                hoveredAxis = HoveredAxis.VIEW_AXIS;

            Drawlist.AddCircle(center2d, camVecGimbalRadius - 1.5f, hoveredAxis == HoveredAxis.VIEW_AXIS ? 0x88_FF_FF_FF : 0x55_FF_FF_FF, 32, 1.5f);
            Drawlist.AddCircle(center2d, camVecGimbalRadius + 1.5f, hoveredAxis == HoveredAxis.VIEW_AXIS ? 0x88_FF_FF_FF : 0x55_FF_FF_FF, 32, 1.5f);



            return hoveredAxis != HoveredAxis.NONE;
        }

        private static (Vector2 planeVecX, Vector2 planeVecY) GetBillboardPlane2d(int axis, in Vector3 planeOrigin3d, float scaling)
        {
            Vector3 axisVec = s_transformMatVectors[axis];



            var planeVecY = Vector3.Normalize(Vector3.Cross(s_cam.ForwardVector, axisVec));

            var planeVecX = Vector3.Cross(planeVecY, axisVec);

            planeVecY *= scaling;
            planeVecX *= scaling;

            return (WorldToScreen(planeOrigin3d + planeVecX) - WorldToScreen(planeOrigin3d),
                    WorldToScreen(planeOrigin3d + planeVecY) - WorldToScreen(planeOrigin3d));
        }


        private static bool GizmoAxisHandle(in Vector3 center, in Vector2 center2d, in Vector2 mousePos, float lineLength, float gizmoScaleFactor, int axis, bool isArrow = false)
        {
            var handleEndPos = center + s_transformMatVectors[axis] * lineLength * gizmoScaleFactor;
            var handleEndPos2d = WorldToScreen(handleEndPos);
            var axisDir2d = Vector2.Normalize(handleEndPos2d - center2d);

            bool hovered =
                Math.Abs(Vector2.Dot(mousePos - center2d, new(-axisDir2d.Y, axisDir2d.X))) < 3 &&
                Vector2.Dot(mousePos - center2d, axisDir2d) >= 0 &&
                Vector2.Dot(mousePos - handleEndPos2d, axisDir2d) <= 0;

            hovered |= (mousePos - handleEndPos2d).LengthSquared() < 4.5f * 4.5f;

            hovered = HoverablePart(hovered);

            var col = hovered ? HOVER_COLOR : s_axisColors[axis];

            ClippedLine(center, handleEndPos, col, 2.5f);

            if (isArrow)
            {

                var (scaleVecX, scaleVecY) = GetBillboardPlane2d(axis, handleEndPos, gizmoScaleFactor * 5);




                #region generating lines
                for (int i = 0; i <= ELLIPSE_NUM_SEGMENTS; i++)
                {
                    double angle = i * Math.PI * 2 / ELLIPSE_NUM_SEGMENTS;

                    Vector2 vec = scaleVecX * (float)Math.Sin(angle) + scaleVecY * (float)Math.Cos(angle);

                    s_ellipsePoints[i] = handleEndPos2d + vec;
                }
                #endregion

                Drawlist.AddConvexPolyFilled(ref s_ellipsePoints[0], ELLIPSE_NUM_SEGMENTS, col);

                Drawlist.AddTriangleFilled(s_ellipsePoints[0], s_ellipsePoints[ELLIPSE_NUM_SEGMENTS / 2], WorldToScreen(center + s_transformMatVectors[axis] * (lineLength + 8) * gizmoScaleFactor), col);

            }
            else
                Drawlist.AddCircleFilled(handleEndPos2d, 4.5f, col);


            return hovered;
        }

        /// <summary>
        /// Draws/Handles a Scale Transform-Gizmo as seen in Blender, Unity, Maya etc.
        /// </summary>
        /// <param name="transformMatrix">The Transform Matrix of the object the Transform Gizmo is used for</param>
        /// <param name="radius">The radius of the Gizmo in screen(2d) space</param>
        /// <param name="hoveredAxis">The associated axis of the hovered part, use <see cref="GizmoResultHelper"/>
        /// to help interpreting it</param>
        /// 
        /// <returns><see langword="true"/> if the gizmo is hovered, <see langword="false"/> if not</returns>
        public static bool ScaleGizmo(in Matrix4x4 transformMatrix, float radius, out HoveredAxis hoveredAxis)
        {
            var mousePos = ImGui.GetMousePos();

            var mtx = transformMatrix;
            s_transformMatVectors[0] = new(mtx.M11, mtx.M12, mtx.M13);
            s_transformMatVectors[1] = new(mtx.M21, mtx.M22, mtx.M23);
            s_transformMatVectors[2] = new(mtx.M31, mtx.M32, mtx.M33);

            Vector3 center = transformMatrix.Translation;
            Vector2 center2d = WorldToScreen(center);

            float gizmoScaleFactor = 1 / (WorldToScreen(center + s_cam.RightVector) - WorldToScreen(center)).X;



            bool Plane(int axisA, int axisB)
            {
                var colA = s_axisColors[axisA];
                var colB = s_axisColors[axisB];

                Vector2 posA = WorldToScreen(center + s_transformMatVectors[axisA] * radius * gizmoScaleFactor * 0.7f);
                Vector2 posB = WorldToScreen(center + s_transformMatVectors[axisB] * radius * gizmoScaleFactor * 0.7f);

                bool hovered = HoverablePart(IsPointInTriangle(mousePos, center2d, posA, posB));

                var col = hovered ? 0xFF_FF_FF_FF : AdditiveBlend(colA, colB);

                Drawlist.AddTriangleFilled(center2d, posA, posB, ColorWithAlpha(col, 0x55));
                Drawlist.AddLine(posA, posB, col, 1.5f);

                return hovered;
            }

            hoveredAxis = HoveredAxis.NONE;

            if (Plane(0, 1))
                hoveredAxis = HoveredAxis.XY_PLANE;
            if (Plane(0, 2))
                hoveredAxis = HoveredAxis.XZ_PLANE;
            if (Plane(1, 2))
                hoveredAxis = HoveredAxis.YZ_PLANE;

            if (GizmoAxisHandle(center, center2d, mousePos, radius, gizmoScaleFactor, 0))
                hoveredAxis = HoveredAxis.X_AXIS;
            if (GizmoAxisHandle(center, center2d, mousePos, radius, gizmoScaleFactor, 1))
                hoveredAxis = HoveredAxis.Y_AXIS;
            if (GizmoAxisHandle(center, center2d, mousePos, radius, gizmoScaleFactor, 2))
                hoveredAxis = HoveredAxis.Z_AXIS;

            return hoveredAxis != HoveredAxis.NONE;
        }



        /// <summary>
        /// Draws/Handles a Translation (Move) Transform-Gizmo as seen in Blender, Unity, Maya etc.
        /// </summary>
        /// <param name="transformMatrix">The Transform Matrix of the object the Transform Gizmo is used for</param>
        /// <param name="radius">The radius of the Gizmo in screen(2d) space</param>
        /// <param name="hoveredAxis">The associated axis of the hovered part, use <see cref="GizmoResultHelper"/>
        /// to help interpreting it</param>
        /// 
        /// <returns><see langword="true"/> if the gizmo is hovered, <see langword="false"/> if not</returns>
        public static bool TranslationGizmo(in Matrix4x4 transformMatrix, float lineLength, out HoveredAxis hoveredAxis)
        {
            var mousePos = ImGui.GetMousePos();

            var mtx = transformMatrix;
            s_transformMatVectors[0] = new(mtx.M11, mtx.M12, mtx.M13);
            s_transformMatVectors[1] = new(mtx.M21, mtx.M22, mtx.M23);
            s_transformMatVectors[2] = new(mtx.M31, mtx.M32, mtx.M33);

            Vector3 center = transformMatrix.Translation;
            Vector2 center2d = WorldToScreen(center);

            float gizmoScaleFactor = 1 / (WorldToScreen(center + s_cam.RightVector) - WorldToScreen(center)).X;



            bool Plane(int axisA, int axisB)
            {
                var colA = s_axisColors[axisA];
                var colB = s_axisColors[axisB];

                Vector2 posA = WorldToScreen(center + s_transformMatVectors[axisA] * lineLength * gizmoScaleFactor * 0.5f);
                Vector2 posB = WorldToScreen(center + s_transformMatVectors[axisB] * lineLength * gizmoScaleFactor * 0.5f);
                Vector2 posAB = WorldToScreen(center + s_transformMatVectors[axisA] * lineLength * gizmoScaleFactor * 0.5f +
                                                       s_transformMatVectors[axisB] * lineLength * gizmoScaleFactor * 0.5f);

                bool hovered = HoverablePart(IsPointInQuad(mousePos, center2d, posA, posAB, posB));

                var col = hovered ? 0xFF_FF_FF_FF : AdditiveBlend(colA, colB);

                Drawlist.AddQuadFilled(center2d, posA, posAB, posB, ColorWithAlpha(col, 0x55));
                Drawlist.AddLine(posA, posAB, col, 1.5f);
                Drawlist.AddLine(posAB, posB, col, 1.5f);

                return hovered;
            }

            hoveredAxis = HoveredAxis.NONE;

            if (Plane(0, 1))
                hoveredAxis = HoveredAxis.XY_PLANE;
            if (Plane(0, 2))
                hoveredAxis = HoveredAxis.XZ_PLANE;
            if (Plane(1, 2))
                hoveredAxis = HoveredAxis.YZ_PLANE;

            if (GizmoAxisHandle(center, center2d, mousePos, lineLength, gizmoScaleFactor, 0, true))
                hoveredAxis = HoveredAxis.X_AXIS;
            if (GizmoAxisHandle(center, center2d, mousePos, lineLength, gizmoScaleFactor, 1, true))
                hoveredAxis = HoveredAxis.Y_AXIS;
            if (GizmoAxisHandle(center, center2d, mousePos, lineLength, gizmoScaleFactor, 2, true))
                hoveredAxis = HoveredAxis.Z_AXIS;

            float radius = 5;

            float distanceMouseToCenter2d = Vector2.Distance(center2d, mousePos);

            if (HoverablePart(distanceMouseToCenter2d < radius + 5))
                hoveredAxis = HoveredAxis.FREE;

            var col = hoveredAxis == HoveredAxis.FREE ? HOVER_COLOR : 0xFF_FF_FF_FF;
            Drawlist.AddCircleFilled(center2d, radius, col, 32);

            return hoveredAxis != HoveredAxis.NONE;
        }
    }
}
