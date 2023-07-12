using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace com.darktable.utility
{
    public static partial class XRGizmos
    {
        /// <summary>
        ///   <para>Draws a wire frame box collider.</para>
        /// </summary>
        /// <param name="collider"></param>
        /// <param name="color"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawCollider(BoxCollider collider, Color color, float lineThickness = k_LineThickness)
        {
            var transform = collider.transform;
            var scale = transform.lossyScale;
            var size = ComponentMultiply(collider.size, scale);
            var center = transform.TransformPoint(collider.center);

            DrawWireCube(center, transform.rotation, size, color, lineThickness);
        }

        /// <summary>
        ///   <para>Draws a wire frame sphere collider.</para>
        /// </summary>
        /// <param name="collider"></param>
        /// <param name="color"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawCollider(SphereCollider collider, Color color, float lineThickness = k_LineThickness)
        {
            var transform = collider.transform;
            var scale = transform.lossyScale;
            float largestScale = LargetComponent(scale);
            float radius = collider.radius;
            var center = transform.TransformPoint(collider.center);

            DrawWireSphere(center, transform.rotation, radius * largestScale, color, lineThickness);
        }

        /// <summary>
        ///   <para>Draws a wire frame axis-aligned bounding box around mesh collider</para>
        /// </summary>
        /// <param name="collider"></param>
        /// <param name="color"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawCollider(MeshCollider collider, Color color, float lineThickness = k_LineThickness)
        {
            DrawColliderBounds(collider, color, lineThickness);
        }

        /// <summary>
        ///   <para>Draws a wire frame axis-aligned bounding box around a collider</para>
        /// </summary>
        /// <param name="collider"></param>
        /// <param name="color"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawColliderBounds(Collider collider, Color color, float lineThickness = k_LineThickness)
        {
            if (!collider.enabled)
            {
                return;
            }

            var bounds = collider.bounds;

            DrawWireCube(bounds.center, Quaternion.identity, bounds.size, color, lineThickness);
        }

        /// <summary>
        ///   <para>Draws a wire frame capsule collider</para>
        /// </summary>
        /// <param name="collider"></param>
        /// <param name="color"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawCollider(CapsuleCollider collider, Color color, float lineThickness = k_LineThickness)
        {
            s_GizmoProperties.SetColor(k_ColorID, color);

            const int xDirection = 0;
            const int yDirection = 1;
            const int zDirection = 2;

            var transform = collider.transform;
            var scale = transform.lossyScale;
            float radius = collider.radius;
            var center = transform.TransformPoint(collider.center);
            float height = Mathf.Max(collider.height, 0);

            Quaternion direction;
            switch (collider.direction)
            {
                case xDirection:
                    height *= Mathf.Abs(scale.x);
                    radius *= Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                    direction = Quaternion.FromToRotation(Vector3.up, Vector3.right);
                    break;
                case yDirection:
                default:
                    height *= Mathf.Abs(scale.y);
                    radius *= Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    direction = Quaternion.identity;
                    break;
                case zDirection:
                    height *= Mathf.Abs(scale.z);
                    radius *= Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                    direction = Quaternion.FromToRotation(Vector3.up, Vector3.forward);
                    break;
            }

            var offset = new Vector3(0, Mathf.Max(0, height * 0.5f - radius), 0);

            // top of capsule
            var index = 0;
            var trs = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(radius, radius, radius));
            for (var i = 0; i < k_HemisphereSegments; i++)
            {
                k_TRSPoints[index++] = trs.MultiplyPoint3x4(k_UnitHemiSpherePoints[i]);
            }

            // bottom of capsule
            trs = Matrix4x4.TRS(-offset, Quaternion.Euler(180, 0, 0), new Vector3(radius, radius, radius));
            for (var i = 0; i < k_HemisphereSegments; i++)
            {
                k_TRSPoints[index++] = trs.MultiplyPoint3x4(k_UnitHemiSpherePoints[i]);
            }

            // lines down the side
            var top = new Vector3(radius, offset.y, 0);
            var bottom = new Vector3(radius, -offset.y, 0);

            var rotateLine = Quaternion.AngleAxis(90, Vector3.up);

            for (int i = 0; i < 4; i++)
            {
                top = rotateLine * top;
                bottom = rotateLine * bottom;

                k_TRSPoints[index++] = top;
                k_TRSPoints[index++] = bottom;
            }

            trs = Matrix4x4.TRS(center, transform.rotation * direction, Vector3.one);

            // now apply the transforms from the center + direction of the capsule.
            for (int i = 0; i < index; i++)
            {
                k_TRSPoints[i] = trs.MultiplyPoint3x4(k_TRSPoints[i]);
            }

            Matrix4x4 matrix;
            var lines = 0;

            // generate lines for the caps
            for (var i = 0; i < (k_HemisphereSegments * 2) - 1; i++)
            {
                // skip lines
                if (i == (int)(k_CircleSegments * 1.5f) // first semicircle to second
                    || i == (k_CircleSegments * 2) + 1 // first hemisphere to second
                    || i == (int)(k_CircleSegments * 3.5f) + 2) // third semicircle to fourth
                {
                    continue;
                }

                var a = k_TRSPoints[i];
                var b = k_TRSPoints[i + 1];

                TryGetLineMatrix(a, b, lineThickness, out matrix);
                s_Matrices[lines++] = matrix;
            }

            // draw the lines on the side
            var shift = k_HemisphereSegments * 2;
            for (int i = 0; i < 8; i += 2)
            {
                var a = k_TRSPoints[i + shift];
                var b = k_TRSPoints[i + 1 + shift];

                TryGetLineMatrix(a, b, lineThickness, out matrix);
                s_Matrices[lines++] = matrix;
            }

            Graphics.RenderMeshInstanced(s_RenderParams, s_CubeMesh, 0, s_Matrices, lines);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float LargetComponent(Vector3 v)
        {
            return Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ComponentMultiply(Vector3 a, Vector3 b)
        {
            a.Set(a.x * b.x, a.y * b.y, a.z * b.z);
            return a;
        }
    }
}
