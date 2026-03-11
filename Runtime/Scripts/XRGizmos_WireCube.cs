using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Utilities.XR
{
    public static partial class XRGizmos
    {
        private const float k_Half = 0.5f;

        // Define the 12 edges of a cube (as pairs of vertex indices)
        private static readonly (int a, int b)[] k_WireCubeEdgeIndices = new (int,int)[]
        {
            // Bottom face
            (0, 1), (1, 5), (5, 4), (4, 0),
            // Top face
            (3, 2), (2, 6), (6, 7), (7, 3),
            // Vertical edges
            (0, 3), (1, 2), (5, 6), (4, 7)
        };

        // Define the 8 vertices of a cube
        private static readonly Vector3[] k_WireCubeVertices = new Vector3[8]
        {
            new Vector3(-k_Half, -k_Half, -k_Half), // 0: left-bottom-back
            new Vector3( k_Half, -k_Half, -k_Half), // 1: right-bottom-back
            new Vector3( k_Half,  k_Half, -k_Half), // 2: right-top-back
            new Vector3(-k_Half,  k_Half, -k_Half), // 3: left-top-back
            new Vector3(-k_Half, -k_Half,  k_Half), // 4: left-bottom-front
            new Vector3( k_Half, -k_Half,  k_Half), // 5: right-bottom-front
            new Vector3( k_Half,  k_Half,  k_Half), // 6: right-top-front
            new Vector3(-k_Half,  k_Half,  k_Half)  // 7: left-top-front
        };

        private static readonly List<Matrix4x4> k_WireCubeMatrices = new List<Matrix4x4>();

        private static Mesh s_WireCubeMesh;

        [RuntimeInitializeOnLoadMethod]
        private static void InitializeWireCube()
        {
            s_WireCubeMesh = BuildWireCubeMesh();
        }

        // Builds a one-meter wire cube.
        private static Mesh BuildWireCubeMesh()
        {
            var edgeIndicesLength = k_WireCubeEdgeIndices.Length;

            Mesh mesh = new Mesh
            {
                name = "WireCube"
            };

            List<Vector3> verts = new List<Vector3>();
            List<int> indices = new List<int>();

            int vertexIndex = 0;
            for (int i = 0; i < edgeIndicesLength; i++)
            {
                Vector3 v1 = k_WireCubeVertices[k_WireCubeEdgeIndices[i].a];
                Vector3 v2 = k_WireCubeVertices[k_WireCubeEdgeIndices[i].b];

                verts.Add(v1);
                verts.Add(v2);

                indices.Add(vertexIndex);
                indices.Add(vertexIndex + 1);

                vertexIndex += 2;
            }

            mesh.SetVertices(verts);
            mesh.SetIndices(indices, MeshTopology.Lines, 0, true);

            return mesh;
        }

        /// <summary>
        /// Draws wire frame cubes. These cubes use a single pixel wireframe,
        /// they don't take a lineThickness parameter. (faster than other version)
        /// </summary>
        /// <param name="points"></param>
        /// <param name="cubeSize"></param>
        /// <param name="color"></param>
        /// <param name="localToWorld"></param>
        public static void DrawWireCubes2(IEnumerable<Vector3> points, float cubeSize, Color color, Matrix4x4 localToWorld)
        {
            k_WireCubeMatrices.Clear();

            foreach (var point in points)
            {
                var matrix = localToWorld * Matrix4x4.TRS(point, Quaternion.identity, Vector3.one * cubeSize);
                k_WireCubeMatrices.Add(matrix);
            }

            s_GizmoProperties.SetColor(k_ColorID, color);

            Graphics.RenderMeshInstanced(in s_RenderParams, s_WireCubeMesh, 0, k_WireCubeMatrices);
        }
    }
}
