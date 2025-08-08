using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public static class ShadowMeshJobHelper
{
    public static JobHandle GenerateShadowMeshAsync(Vector3[] shapePath, System.Action<MeshData> onComplete)
    {
        if (shapePath == null || shapePath.Length < 3)
        {
            onComplete?.Invoke(new MeshData());
            return default;
        }

        // 创建Native数组
        var shapePathNative = new NativeArray<Vector3>(shapePath, Allocator.TempJob);
        var outVertices = new NativeList<Vector3>(Allocator.TempJob);
        var outTriangles = new NativeList<int>(Allocator.TempJob);
        var outTangents = new NativeList<Vector4>(Allocator.TempJob);
        var outColors = new NativeList<Color>(Allocator.TempJob);
        var bounds = new NativeArray<Bounds>(1, Allocator.TempJob);


        // 创建Job
        var job = new ShadowMeshGenJob
        {
            shapePath = shapePathNative,
            outVertices = outVertices,
            outTriangles = outTriangles,
            outTangents = outTangents,
            outColors = outColors,
            bounds = bounds,
        };

        // 调度Job
        var jobHandle = job.Schedule();

        // 在Job完成后处理结果
        jobHandle.Complete();

        // 提取结果
        var meshData = new MeshData
        {
            vertices = outVertices.ToArray(),
            triangles = outTriangles.ToArray(),
            tangents = outTangents.ToArray(),
            colors = outColors.ToArray(),
            bounds = bounds[0]
        };

        // 清理Native数组
        shapePathNative.Dispose();
        outVertices.Dispose();
        outTriangles.Dispose();
        outTangents.Dispose();
        outColors.Dispose();
        bounds.Dispose();

        // 调用回调
        onComplete?.Invoke(meshData);

        return jobHandle;
    }

    public static MeshData GenerateShadowMeshSync(Vector3[] shapePath)
    {
        MeshData result = new MeshData();
        GenerateShadowMeshAsync(shapePath, (data) => result = data);
        return result;
    }

    public struct MeshData
    {
        public Vector3[] vertices;
        public int[] triangles;
        public Vector4[] tangents;
        public Color[] colors;
        public Bounds bounds;

        public void ApplyToMesh(Mesh mesh)
        {
            if (mesh == null) return;

            mesh.Clear();
            if (vertices != null && vertices.Length > 0)
            {
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.tangents = tangents;
                mesh.colors = colors;
                mesh.RecalculateNormals();
            }
        }
    }

    public struct JobData
    {
        public JobHandle handle;
        public ShadowMeshGenJob job;
        
        public bool IsCompleted => handle.IsCompleted;
        
        public MeshData GetResult()
        {
            handle.Complete(); // 确保完成
            
            var result = new MeshData
            {
                vertices = job.outVertices.ToArray(),
                triangles = job.outTriangles.ToArray(),
                tangents = job.outTangents.ToArray(),
                colors = job.outColors.ToArray(),
                bounds = job.bounds.Length > 0 ? job.bounds[0] : new Bounds()
            };

            Dispose();
            
            return result;
        }
        
        public void Dispose()
        {
            if (job.shapePath.IsCreated) job.shapePath.Dispose();
            if (job.outVertices.IsCreated) job.outVertices.Dispose();
            if (job.outTriangles.IsCreated) job.outTriangles.Dispose();
            if (job.outTangents.IsCreated) job.outTangents.Dispose();
            if (job.outColors.IsCreated) job.outColors.Dispose();
            if (job.bounds.IsCreated) job.bounds.Dispose();
        }
    }

    public static JobData AsyncMeshGen(Vector3[] shapePath)
    {
        if (shapePath == null || shapePath.Length < 3)
        {
            return default;
        }

        var shapePathNative = new NativeArray<Vector3>(shapePath, Allocator.TempJob);
        var outVertices = new NativeList<Vector3>(Allocator.TempJob);
        var outTriangles = new NativeList<int>(Allocator.TempJob);
        var outTangents = new NativeList<Vector4>(Allocator.TempJob);
        var outColors = new NativeList<Color>(Allocator.TempJob);
        var outBounds = new NativeArray<Bounds>(1, Allocator.TempJob);

        var job = new ShadowMeshGenJob
        {
            shapePath = shapePathNative,
            outVertices = outVertices,
            outTriangles = outTriangles,
            outTangents = outTangents,
            outColors = outColors,
            bounds = outBounds,
        };

        var jobHandle = job.Schedule();

        return new JobData
        {
            handle = jobHandle,
            job = job
        };
    }
}