using Unity.Burst;
using Unity.Collections;
using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using LibTessDotNet;
using System.Linq;
using System.Diagnostics;

public struct ShadowMeshGenJob : IJob
{
    [ReadOnly] public NativeArray<Vector3> shapePath;
    // 输出使用 NativeList（动态大小）
    public NativeList<Vector3> outVertices;
    public NativeList<int> outTriangles;
    public NativeList<Vector4> outTangents;
    public NativeList<Color> outColors;
    
    // 单个值仍可使用 NativeArray
    [WriteOnly] public NativeArray<Bounds> bounds;
    

    public void Execute()
    {
        Bounds b = GenerateShadowMesh(shapePath, out var vertices, out var triangles, out var tangents, out var colors);
        
        // 清空并添加所有数据
        outVertices.Clear();
        outVertices.AddRange(vertices);
        
        outTriangles.Clear();
        outTriangles.AddRange(triangles);
        
        outTangents.Clear();
        outTangents.AddRange(tangents);
        
        outColors.Clear();
        outColors.AddRange(colors);
        
        bounds[0] = b;
        
        // 释放临时数据
        vertices.Dispose();
        triangles.Dispose();
        tangents.Dispose();
        colors.Dispose();
    }

    static object InterpCustomVertexData(Vec3 position, object[] data, float[] weights)
    {
        return data[0];
    }

    static void InitializeTangents(int tangentsToAdd, NativeList<Vector4> tangents)
    {
        for (int i = 0; i < tangentsToAdd; i++)
            tangents.Add(Vector4.zero);
    }

    internal struct Edge : IComparable<Edge>
    {
        public int vertexIndex0;
        public int vertexIndex1;
        public Vector4 tangent;
        private bool compareReversed; // This is done so that edge AB can equal edge BA

        public void AssignVertexIndices(int vi0, int vi1)
        {
            vertexIndex0 = vi0;
            vertexIndex1 = vi1;
            compareReversed = vi0 > vi1;
        }

        public int Compare(Edge a, Edge b)
        {
            int adjustedVertexIndex0A = a.compareReversed ? a.vertexIndex1 : a.vertexIndex0;
            int adjustedVertexIndex1A = a.compareReversed ? a.vertexIndex0 : a.vertexIndex1;
            int adjustedVertexIndex0B = b.compareReversed ? b.vertexIndex1 : b.vertexIndex0;
            int adjustedVertexIndex1B = b.compareReversed ? b.vertexIndex0 : b.vertexIndex1;

            // Sort first by VI0 then by VI1
            int deltaVI0 = adjustedVertexIndex0A - adjustedVertexIndex0B;
            int deltaVI1 = adjustedVertexIndex1A - adjustedVertexIndex1B;

            if (deltaVI0 == 0)
                return deltaVI1;
            else
                return deltaVI0;
        }

        public int CompareTo(Edge edgeToCompare)
        {
            return Compare(this, edgeToCompare);
        }
    }

    static void PopulateEdgeArray(NativeList<Vector3> vertices, NativeList<int> triangles, NativeList<Edge> edges)
    {
        for (int triangleIndex = 0; triangleIndex < triangles.Length; triangleIndex += 3)
        {
            edges.Add(CreateEdge(triangleIndex, triangleIndex + 1, vertices, triangles));
            edges.Add(CreateEdge(triangleIndex + 1, triangleIndex + 2, vertices, triangles));
            edges.Add(CreateEdge(triangleIndex + 2, triangleIndex, vertices, triangles));
        }
    }

    static Edge CreateEdge(int triangleIndexA, int triangleIndexB, NativeList<Vector3> vertices, NativeList<int> triangles)
    {
        Edge retEdge = new Edge();

        retEdge.AssignVertexIndices(triangles[triangleIndexA], triangles[triangleIndexB]);

        Vector3 vertex0 = vertices[retEdge.vertexIndex0];
        vertex0.z = 0;
        Vector3 vertex1 = vertices[retEdge.vertexIndex1];
        vertex1.z = 0;

        Vector3 edgeDir = Vector3.Normalize(vertex1 - vertex0);
        retEdge.tangent = Vector3.Cross(-Vector3.forward, edgeDir);

        return retEdge;
    }

    static void SortEdges(NativeList<Edge> edgesToProcess)
    {
        edgesToProcess.Sort();
    }

    static void CreateShadowTriangles(NativeList<Vector3> vertices, NativeList<Color> colors, NativeList<int> triangles, NativeList<Vector4> tangents, NativeList<Edge> edges)
    {
        for (int edgeIndex = 0; edgeIndex < edges.Length; edgeIndex++)
        {
            if (IsOutsideEdge(edgeIndex, edges))
            {
                Edge edge = edges[edgeIndex];
                tangents[edge.vertexIndex1] = -edge.tangent;

                int newVertexIndex = vertices.Length;
                vertices.Add(vertices[edge.vertexIndex0]);
                colors.Add(colors[edge.vertexIndex0]);

                tangents.Add(-edge.tangent);

                triangles.Add(edge.vertexIndex0);
                triangles.Add(newVertexIndex);
                triangles.Add(edge.vertexIndex1);
            }
        }
    }

    static bool IsOutsideEdge(int edgeIndex, NativeList<Edge> edgesToProcess)
    {
        int previousIndex = edgeIndex - 1;
        int nextIndex = edgeIndex + 1;
        int numberOfEdges = edgesToProcess.Length;
        Edge currentEdge = edgesToProcess[edgeIndex];

        return (previousIndex < 0 || (currentEdge.CompareTo(edgesToProcess[edgeIndex - 1]) != 0)) && (nextIndex >= numberOfEdges || (currentEdge.CompareTo(edgesToProcess[edgeIndex + 1]) != 0));
    }

    static internal Bounds CalculateLocalBounds(Vector3[] inVertices)
    {
        if (inVertices.Length <= 0)
            return new Bounds(Vector3.zero, Vector3.zero);

        Vector2 minVec = Vector2.positiveInfinity;
        Vector2 maxVec = Vector2.negativeInfinity;

        int inVerticesLength = inVertices.Length;

        // Add outline vertices
        for (int i = 0; i < inVerticesLength; i++)
        {
            Vector2 vertex = new Vector2(inVertices[i].x, inVertices[i].y);

            minVec = Vector2.Min(minVec, vertex);
            maxVec = Vector2.Max(maxVec, vertex);
        }

        return new Bounds { max = maxVec, min = minVec };
    }

    public static Bounds GenerateShadowMesh(NativeArray<Vector3> shapePath, 
        out NativeList<Vector3> outVertices,
        out NativeList<int> outTriangles, 
        out NativeList<Vector4> outTangents,
        out NativeList<Color> outColors)
    {
        // var sw = System.Diagnostics.Stopwatch.StartNew();
        NativeList<Vector3> vertices = new NativeList<Vector3>(Allocator.Temp);
        NativeList<int> triangles = new NativeList<int>(Allocator.Temp);
        NativeList<Vector4> tangents = new NativeList<Vector4>(Allocator.Temp);
        NativeList<Color> extrusion = new NativeList<Color>(Allocator.Temp);

        // Create interior geometry
        int pointCount = shapePath.Length;
        var inputs = new ContourVertex[1 * pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            Color extrusionData = new Color(shapePath[i].x, shapePath[i].y, shapePath[i].x, shapePath[i].y);
            int nextPoint = (i + 1) % pointCount;
            inputs[1 * i] = new ContourVertex() { Position = new Vec3() { X = shapePath[i].x, Y = shapePath[i].y, Z = 0 }, Data = extrusionData };

            // extrusionData = new Color(shapePath[i].x, shapePath[i].y, shapePath[nextPoint].x, shapePath[nextPoint].y);
            // Vector2 midPoint = 0.5f * (shapePath[i] + shapePath[nextPoint]);
            // inputs[2 * i + 1] = new ContourVertex() { Position = new Vec3() { X = midPoint.x, Y = midPoint.y, Z = 0 }, Data = extrusionData };
        }

        // sw.Stop();
        // double time = sw.Elapsed.TotalMilliseconds;
        // UnityEngine.Debug.LogError($"生成阴影网格耗时：{time:F3}ms");

        Tess tessI = new Tess();
        tessI.AddContour(inputs, ContourOrientation.Original);
        tessI.Tessellate(WindingRule.EvenOdd, ElementType.Polygons, 3, InterpCustomVertexData);

        // sw.Stop();
        // double time = sw.Elapsed.TotalMilliseconds;
        // UnityEngine.Debug.LogError($"生成阴影网格耗时：{time:F3}ms");

        var indicesI = tessI.Elements.Select(i => i).ToArray();
        var verticesI = tessI.Vertices.Select(v => new Vector3(v.Position.X, v.Position.Y, 0)).ToArray();
        var extrusionI = tessI.Vertices.Select(v => new Color(((Color)v.Data).r, ((Color)v.Data).g, ((Color)v.Data).b, ((Color)v.Data).a)).ToArray();
        
        for (int i = 0; i < verticesI.Length; i++)
        {
            vertices.Add(verticesI[i]);
        }
        for (int i = 0; i < indicesI.Length; i++)
        {
            triangles.Add(indicesI[i]);
        }
        for (int i = 0; i < extrusionI.Length; i++)
        {
            extrusion.Add(extrusionI[i]);
        }

        InitializeTangents(vertices.Length, tangents);

        NativeList<Edge> edges = new NativeList<Edge>(Allocator.Temp);
        PopulateEdgeArray(vertices, triangles, edges);
        SortEdges(edges);
        CreateShadowTriangles(vertices, extrusion, triangles, tangents, edges);

        edges.Dispose();

        // Color[] finalExtrusion = extrusion.ToArray();
        // Vector3[] finalVertices = vertices.ToArray();
        // int[] finalTriangles = triangles.ToArray();
        // Vector4[] finalTangents = tangents.ToArray();

        // 将结果赋值给 out 参数
        outVertices = vertices;
        outTriangles = triangles;
        outTangents = tangents;
        outColors = extrusion;

        Vector3[] finalVertices = vertices.ToArray();

        // sw.Stop();
        // double time = sw.Elapsed.TotalMilliseconds;
        // UnityEngine.Debug.LogError($"生成阴影网格耗时：{time:F3}ms");

        return CalculateLocalBounds(finalVertices);
    }
}
