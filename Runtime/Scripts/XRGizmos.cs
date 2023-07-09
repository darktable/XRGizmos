using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace com.darktable.utility
{
    public static partial class XRGizmos
    {
        // This must be defined in player settings for your platform is you want gizmos to render
        private const string k_XRGizmosDefine = "XR_GIZMOS";

        // Unity docs say you can only submit 1023 matrices per RenderMeshInstanced call
        private const int k_MaxInstances = 1023;

        // Make sure the selected shader is referenced in your project and supports instancing
        // If gizmos render in editor, but not on device it may be because the
        // instancing variants were stripped from the build.
        // In "graphics settings" you can add the shader to "always included shaders" and
        // set "instancing variants" from "strip unused" to "keep all" to avoid this.
        private const string k_ShaderName = "Unlit/XRGizmos";

        // Make sure this property matches the name of the Color/Tint property in the shader
        private static readonly int k_ColorID = Shader.PropertyToID("_Color");

        private const int k_CircleSegments = 24;
        private const float k_LineThickness = 0.003f;

        private static Mesh s_SphereMesh;
        private static Mesh s_CubeMesh;
        private static Mesh s_QuadMesh;

        private static Material s_GizmoMaterial;
        private static MaterialPropertyBlock s_GizmoProperties;
        private static RenderParams s_RenderParams;

        private static readonly Quaternion[] k_SphereRotations = new Quaternion[]
        {
            Quaternion.identity,
            Quaternion.FromToRotation(Vector3.up, Vector3.right),
            Quaternion.FromToRotation(Vector3.up, Vector3.forward),
        };
        private static readonly Vector3[] k_UnitCirclePoints = new Vector3[k_CircleSegments];
        private static readonly Vector3[] k_TRSCirclePoints = new Vector3[k_CircleSegments];

        private static readonly Vector3[] k_UnitCubePoints = new Vector3[8]
        {
            new Vector3(0.50f, 0.50f, 0.50f),
            new Vector3(-0.50f, 0.50f, 0.50f),
            new Vector3(-0.50f, -0.50f, 0.50f),
            new Vector3(0.50f, -0.50f, 0.50f),

            new Vector3(0.50f, 0.50f, -0.50f),
            new Vector3(-0.50f, 0.50f, -0.50f),
            new Vector3(-0.50f, -0.50f, -0.50f),
            new Vector3(0.50f, -0.50f, -0.50f),
        };
        private static readonly Vector3[] k_TRSCubePoints = new Vector3[8];

        private static readonly Vector3[] k_UnitArrowPoints = new Vector3[4]
        {
            new Vector3(0.0f, 0.0f, 0.50f),
            new Vector3(0.25f, 0.0f, -0.25f),
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(-0.25f, 0.0f, -0.25f),
        };
        private static readonly Vector3[] k_TRSArrowPoints = new Vector3[4];

        private static readonly Vector3[] k_UnitPointerPoints = new Vector3[8]
        {
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(0.25f, 0.0f, -0.5f),
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(-0.25f, 0.0f, -0.5f),
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3( 0.0f, 0.25f,-0.5f),
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3( 0.0f, -0.25f,-0.5f),
        };
        private static readonly Vector3[] k_TRSPointerPoints = new Vector3[8];

        private static NativeArray<Matrix4x4> s_Matrices = new NativeArray<Matrix4x4>(k_MaxInstances, Allocator.Persistent);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Conditional(k_XRGizmosDefine)]
        private static void Initialize()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var meshFilter = cube.GetComponent<MeshFilter>();
            s_CubeMesh = meshFilter.sharedMesh;
            Object.Destroy(cube);

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meshFilter = sphere.GetComponent<MeshFilter>();
            s_SphereMesh = meshFilter.sharedMesh;
            Object.Destroy(sphere);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            meshFilter = quad.GetComponent<MeshFilter>();
            s_QuadMesh = meshFilter.sharedMesh;
            Object.Destroy(quad);

            s_GizmoProperties = new MaterialPropertyBlock();

            var shader = Shader.Find(k_ShaderName);

            s_GizmoMaterial = new Material(shader)
            {
                enableInstancing = true,
            };

            s_RenderParams = new RenderParams(s_GizmoMaterial)
            {
                matProps = s_GizmoProperties,
                receiveShadows = false,
                motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
                lightProbeUsage = LightProbeUsage.Off,
                shadowCastingMode = ShadowCastingMode.Off,
                reflectionProbeUsage = ReflectionProbeUsage.Off,
            };

            var vector = Vector3.forward;
            var rotation = Quaternion.AngleAxis(360.0f / k_CircleSegments, Vector3.up);

            for (var i = 0; i < k_CircleSegments; i++)
            {
                k_UnitCirclePoints[i] = vector;
                vector = rotation * vector;
            }

            Application.quitting += OnApplicationQuitting;
        }

        /// <summary>
        ///   <para>Draws a line starting at from towards to.</para>
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        /// <param name="color"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawLine(Vector3 from, Vector3 to, Color color, float lineThickness = k_LineThickness)
        {
            if (!TryGetLineMatrix(from, to, lineThickness, out var matrix))
            {
                return;
            }

            s_GizmoProperties.SetColor(k_ColorID, color);

            Graphics.RenderMesh(in s_RenderParams, s_CubeMesh, 0, matrix);
        }

        /// <summary>
        ///   <para>Draws a ray starting at from along direction for length.</para>
        /// </summary>
        /// <param name="from"></param>
        /// <param name="direction"></param>
        /// <param name="color"></param>
        /// <param name="length"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawRay(Vector3 from, Vector3 direction, Color color, float length = 1f, float lineThickness = k_LineThickness)
        {
            if (direction == Vector3.zero)
                return;

            var ray = new Ray(from, direction);
            var end = ray.GetPoint(length);

            DrawLine(from, end, color, lineThickness);
        }

        /// <summary>
        ///   <para>Draws a ray of length length.</para>
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="color"></param>
        /// <param name="length"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawRay(Ray ray, Color color, float length = 1f, float lineThickness = k_LineThickness)
        {
            if (ray.direction == Vector3.zero)
            {
                return;
            }

            var end = ray.GetPoint(length);

            DrawLine(ray.origin, end, color, lineThickness);
        }

        /// <summary>
        ///   <para>Draws a 3d plus marking a point.</para>
        /// </summary>
        /// <param name="center"></param>
        /// <param name="color"></param>
        /// <param name="size"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawPoint(Vector3 center, Color color, float size = 0.1f, float lineThickness = k_LineThickness)
        {
            s_GizmoProperties.SetColor(k_ColorID, color);
            var lines = 0;

            var offset = new Vector3(size / 2.0f, 0, 0);
            var start = center - offset;
            var end = center + offset;

            TryGetLineMatrix(start, end, lineThickness, out var matrix);
            s_Matrices[lines++] = matrix;

            offset.Set(0, size / 2.0f, 0);
            start = center - offset;
            end = center + offset;

            TryGetLineMatrix(start, end, lineThickness, out matrix);
            s_Matrices[lines++] = matrix;

            offset.Set(0, 0, size / 2.0f);
            start = center - offset;
            end = center + offset;

            TryGetLineMatrix(start, end, lineThickness, out matrix);
            s_Matrices[lines++] = matrix;

            Graphics.RenderMeshInstanced(s_RenderParams, s_CubeMesh, 0, s_Matrices, lines);
        }

        /// <summary>
        ///   <para>Draws a solid sphere with center and radius.</para>
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="color"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawSphere(Vector3 center, float radius, Color color)
        {
            s_GizmoProperties.SetColor(k_ColorID, color);

            var sphereMatrix = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one * radius);
            Graphics.RenderMesh(in s_RenderParams, s_SphereMesh, 0, sphereMatrix);
        }

        /// <summary>
        ///   <para>Draw a solid box at center with size.</para>
        /// </summary>
        /// <param name="center"></param>
        /// <param name="rotation"></param>
        /// <param name="size"></param>
        /// <param name="color"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawCube(Vector3 center, Quaternion rotation, Vector3 size, Color color)
        {
            s_GizmoProperties.SetColor(k_ColorID, color);

            var cubeMatrix = Matrix4x4.TRS(center, rotation, size);
            Graphics.RenderMesh(in s_RenderParams, s_CubeMesh, 0, cubeMatrix);
        }

        /// <summary>
        ///   <para>Draws a circle with center and radius.</para>
        /// </summary>
        /// <param name="center"></param>
        /// <param name="rotation"></param>
        /// <param name="radius"></param>
        /// <param name="color"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawCircle(Vector3 center, Quaternion rotation, float radius, Color color, float lineThickness = k_LineThickness)
        {
            s_GizmoProperties.SetColor(k_ColorID, color);

            var trs = Matrix4x4.TRS(center, rotation, new Vector3(radius, radius, radius));

            for (var i = 0; i < k_CircleSegments; i++)
            {
                k_TRSCirclePoints[i] = trs.MultiplyPoint3x4(k_UnitCirclePoints[i]);
            }

            for (var i = 0; i < k_CircleSegments; i++)
            {
                TryGetLineMatrix(k_TRSCirclePoints[i], k_TRSCirclePoints[(i + 1) % k_CircleSegments], lineThickness, out var matrix);
                s_Matrices[i] = matrix;
            }

            Graphics.RenderMeshInstanced(s_RenderParams, s_CubeMesh, 0, s_Matrices, k_CircleSegments);
        }

        /// <summary>
        ///   <para>Draws a wireframe sphere with center and radius.</para>
        /// </summary>
        /// <param name="center"></param>
        /// <param name="radius"></param>
        /// <param name="color"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawWireSphere(Vector3 center, float radius, Color color, float lineThickness = k_LineThickness)
        {
            s_GizmoProperties.SetColor(k_ColorID, color);

            var lines = 0;
            foreach (var rotation in k_SphereRotations)
            {
                var trs = Matrix4x4.TRS(center, rotation, new Vector3(radius, radius, radius));

                for (var i = 0; i < k_CircleSegments; i++)
                {
                    k_TRSCirclePoints[i] = trs.MultiplyPoint3x4(k_UnitCirclePoints[i]);
                }

                for (var i = 0; i < k_CircleSegments; i++)
                {
                    TryGetLineMatrix(k_TRSCirclePoints[i], k_TRSCirclePoints[(i + 1) % k_CircleSegments], lineThickness, out var matrix);
                    s_Matrices[lines++] = matrix;
                }
            }

            Graphics.RenderMeshInstanced(s_RenderParams, s_CubeMesh, 0, s_Matrices, lines);
        }

        /// <summary>
        ///   <para>Draw a wireframe box with center and size.</para>
        /// </summary>
        /// <param name="center"></param>
        /// <param name="rotation"></param>
        /// <param name="size"></param>
        /// <param name="color"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawWireCube(Vector3 center, Quaternion rotation, Vector3 size, Color color, float lineThickness = k_LineThickness)
        {
            s_GizmoProperties.SetColor(k_ColorID, color);

            var trs = Matrix4x4.TRS(center, rotation, size);

            for (var i = 0; i < 8; i++)
            {
                k_TRSCubePoints[i] = trs.MultiplyPoint3x4(k_UnitCubePoints[i]);
            }

            var lines = 0;

            for (var i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;

                // "top" of the cube
                TryGetLineMatrix(k_TRSCubePoints[i], k_TRSCubePoints[j], lineThickness, out var matrix);
                s_Matrices[lines++] = matrix;

                // "bottom" of the cube
                TryGetLineMatrix(k_TRSCubePoints[i + 4], k_TRSCubePoints[j + 4], lineThickness, out matrix);
                s_Matrices[lines++] = matrix;

                // "sides" of the cube
                TryGetLineMatrix(k_TRSCubePoints[i], k_TRSCubePoints[i + 4], lineThickness, out matrix);
                s_Matrices[lines++] = matrix;
            }

            Graphics.RenderMeshInstanced(s_RenderParams, s_CubeMesh, 0, s_Matrices, lines);
        }

        /// <summary>
        ///   <para>Draw a 2d arrow.</para>
        /// </summary>
        /// <param name="center"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <param name="color"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawArrow(Vector3 center, Quaternion rotation, Color color, float scale = 1.0f, float lineThickness = k_LineThickness)
        {
            s_GizmoProperties.SetColor(k_ColorID, color);

            var trs = Matrix4x4.TRS(center, rotation, new Vector3(scale, scale, scale));

            for (var i = 0; i < 4; i++)
            {
                k_TRSArrowPoints[i] = trs.MultiplyPoint3x4(k_UnitArrowPoints[i]);
            }

            for (var i = 0; i < 4; i++)
            {
                TryGetLineMatrix(k_TRSArrowPoints[i], k_TRSArrowPoints[(i + 1) % 4], lineThickness, out var matrix);
                s_Matrices[i] = matrix;
            }

            Graphics.RenderMeshInstanced(s_RenderParams, s_CubeMesh, 0, s_Matrices, 4);
        }

        /// <summary>
        ///   <para>Draw a ray with an arrow at the end.</para>
        /// </summary>
        /// <param name="from"></param>
        /// <param name="direction"></param>
        /// <param name="color"></param>
        /// <param name="scale"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawPointer(Vector3 from, Vector3 direction, Color color, float scale = 1.0f, float lineThickness = k_LineThickness)
        {
            if (direction == Vector3.zero)
            {
                return;
            }

            s_GizmoProperties.SetColor(k_ColorID, color);

            var ray = new Ray(from, direction);
            var end = ray.GetPoint(scale);

            var lines = 0;

            TryGetLineMatrix(from, end, lineThickness, out var matrix);
            s_Matrices[lines++] = matrix;

            var rotation = Quaternion.LookRotation(direction);
            var pointerScale = Vector3.one * (scale * 0.25f);
            var trs = Matrix4x4.TRS(end, rotation, pointerScale);

            for (var i = 0; i < 8; i++)
            {
                k_TRSPointerPoints[i] = trs.MultiplyPoint3x4(k_UnitPointerPoints[i]);
            }

            for (var i = 0; i < 8; i += 2)
            {
                TryGetLineMatrix(k_TRSPointerPoints[i], k_TRSPointerPoints[i + 1], lineThickness, out matrix);
                s_Matrices[lines++] = matrix;
            }

            Graphics.RenderMeshInstanced(s_RenderParams, s_CubeMesh, 0, s_Matrices, lines);
        }

        /// <summary>
        ///   <para>Draw an axis representing a transform.</para>
        /// </summary>
        /// <param name="transform"></param>
        /// <param name="length"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawAxis(Transform transform, float length = 0.25f, float lineThickness = k_LineThickness)
        {
            var pos = transform.position;

            var ray = new Ray(pos, transform.up);
            DrawRay(ray, Color.green, length, lineThickness);

            ray.direction = transform.right;
            DrawRay(ray, Color.red, length, lineThickness);

            ray.direction = transform.forward;
            DrawRay(ray, Color.blue, length, lineThickness);
        }

        /// <summary>
        ///   <para>Draw lines connecting a set of points.</para>
        /// </summary>
        /// <param name="points"></param>
        /// <param name="color"></param>
        /// <param name="closeLoop"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawLineList(IReadOnlyList<Vector3> points, Color color, bool closeLoop = false, float lineThickness = k_LineThickness)
        {
            if (points.Count < 2)
            {
                return;
            }

            s_GizmoProperties.SetColor(k_ColorID, color);

            var lines = 0;
            int count = points.Count;

            for (var i = 1; i < count; i++)
            {
                TryGetLineMatrix(points[i - 1], points[i], lineThickness, out var matrix);
                s_Matrices[lines++] = matrix;

                if (lines + 2 >= k_MaxInstances)
                {
                    Graphics.RenderMeshInstanced(s_RenderParams, s_CubeMesh, 0, s_Matrices, lines);
                    lines = 0;
                }
            }

            if (closeLoop)
            {
                TryGetLineMatrix(points[count - 1], points[0], lineThickness, out var matrix);
                s_Matrices[lines++] = matrix;
            }

            if (lines == 0)
            {
                return;
            }

            Graphics.RenderMeshInstanced(s_RenderParams, s_CubeMesh, 0, s_Matrices, lines);
        }

        /// <summary>
        ///   <para>Draw set of points all with same size.</para>
        /// </summary>
        /// <param name="points"></param>
        /// <param name="color"></param>
        /// <param name="size"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawPointSet(IReadOnlyList<Vector3> points, Color color, float size = 0.1f, float lineThickness = k_LineThickness)
        {
            if (points == null || points.Count == 0)
            {
                return;
            }

            s_GizmoProperties.SetColor(k_ColorID, color);

            int count = points.Count;
            var lines = 0;
            var offsetX = Vector3.right * (size * 0.5f);
            var offsetY = Vector3.up * (size * 0.5f);
            var offsetZ = Vector3.forward * (size * 0.5f);

            for (var i = 0; i < count; i++)
            {
                var center = points[i];

                var start = center - offsetX;
                var end = center + offsetX;

                TryGetLineMatrix(start, end, lineThickness, out var matrix);
                s_Matrices[lines++] = matrix;

                start = center - offsetY;
                end = center + offsetY;

                TryGetLineMatrix(start, end, lineThickness, out matrix);
                s_Matrices[lines++] = matrix;

                start = center - offsetZ;
                end = center + offsetZ;

                TryGetLineMatrix(start, end, lineThickness, out matrix);
                s_Matrices[lines++] = matrix;

                if (lines + 3 >= k_MaxInstances)
                {
                    Graphics.RenderMeshInstanced(s_RenderParams, s_CubeMesh, 0, s_Matrices, lines);
                    lines = 0;
                }
            }

            if (lines == 0)
            {
                return;
            }

            Graphics.RenderMeshInstanced(s_RenderParams, s_CubeMesh, 0, s_Matrices, lines);
        }

        /// <summary>
        ///   <para>Draw set of cubes all with same size and rotation.</para>
        /// </summary>
        /// <param name="points"></param>
        /// <param name="rotation"></param>
        /// <param name="size"></param>
        /// <param name="color"></param>
        /// <param name="lineThickness"></param>
        [Conditional(k_XRGizmosDefine)]
        public static void DrawWireCubes(IReadOnlyList<Vector3> points, Quaternion rotation, Vector3 size, Color color, float lineThickness = k_LineThickness)
        {
            if (points == null || points.Count == 0)
            {
                return;
            }

            s_GizmoProperties.SetColor(k_ColorID, color);

            int count = points.Count;
            var lines = 0;

            for (var p = 0; p < count; p++)
            {
                var center = points[p];

                var trs = Matrix4x4.TRS(center, rotation, size);

                for (var i = 0; i < 8; i++)
                {
                    k_TRSCubePoints[i] = trs.MultiplyPoint3x4(k_UnitCubePoints[i]);
                }

                for (var i = 0; i < 4; i++)
                {
                    int j = (i + 1) % 4;

                    // "top" of the cube
                    TryGetLineMatrix(k_TRSCubePoints[i], k_TRSCubePoints[j], lineThickness, out var matrix);
                    s_Matrices[lines++] = matrix;

                    // "bottom" of the cube
                    TryGetLineMatrix(k_TRSCubePoints[i + 4], k_TRSCubePoints[j + 4], lineThickness, out matrix);
                    s_Matrices[lines++] = matrix;

                    // "sides" of the cube
                    TryGetLineMatrix(k_TRSCubePoints[i], k_TRSCubePoints[i + 4], lineThickness, out matrix);
                    s_Matrices[lines++] = matrix;
                }

                if (lines + 12 >= k_MaxInstances)
                {
                    Graphics.RenderMeshInstanced(s_RenderParams, s_CubeMesh, 0, s_Matrices, lines);
                    lines = 0;
                }
            }

            if (lines == 0)
            {
                return;
            }

            Graphics.RenderMeshInstanced(s_RenderParams, s_CubeMesh, 0, s_Matrices, lines);
        }

        private static bool TryGetLineMatrix(Vector3 from, Vector3 to, float thickness, out Matrix4x4 matrix)
        {
            var segment = to - from;
            float magnitude = segment.magnitude;

            if (magnitude == 0)
            {
                matrix = default;
                return false;
            }

            segment.Normalize();

            var rayPosition = from + segment * (magnitude * 0.5f);
            var rayRotation = Quaternion.LookRotation(segment);
            var rayScale = new Vector3(thickness, thickness, magnitude);

            matrix = Matrix4x4.TRS(rayPosition, rayRotation, rayScale);
            return true;
        }

        private static void OnApplicationQuitting()
        {
            Application.quitting -= OnApplicationQuitting;
            s_Matrices.Dispose();
        }
    }
}