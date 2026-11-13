using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pointless
{
    public sealed class Batch : IDisposable
    {
        const int Quads = 4096;
        const float TriangleLow = 0.57735f;
        const float TriangleHigh = 1.1547f;
        const float TrianglePad = 0.08f;
        const float LinePad = 0.5f;
        const MeshUpdateFlags Quiet = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices
            | MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontResetBoneBounds;

        [StructLayout(LayoutKind.Sequential)]
        struct Vertex
        {
            public Vector3 At;
            public Color32 Tint;
            public Vector4 Shape;
        }

        static readonly Bounds everywhere = new Bounds(Vector3.zero, new Vector3(1000f, 1000f, 1000f));

        readonly Mesh mesh;
        NativeArray<Vertex> vertices;
        int count;

        public Mesh Mesh => mesh;

        public Batch()
        {
            mesh = new Mesh { name = "Pointless" };
            mesh.MarkDynamic();
            mesh.SetVertexBufferParams(Quads * 4,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
                new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4));
            mesh.SetIndexBufferParams(Quads * 6, IndexFormat.UInt16);

            var indices = new NativeArray<ushort>(Quads * 6, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            for (int quad = 0, i = 0; quad < Quads; quad++)
            {
                int first = quad * 4;
                indices[i++] = (ushort)first;
                indices[i++] = (ushort)(first + 1);
                indices[i++] = (ushort)(first + 2);
                indices[i++] = (ushort)(first + 2);
                indices[i++] = (ushort)(first + 1);
                indices[i++] = (ushort)(first + 3);
            }
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length, Quiet);
            indices.Dispose();

            vertices = new NativeArray<Vertex>(Quads * 4, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            mesh.subMeshCount = 1;
            count = 0;
            End();
        }

        public void Begin() => count = 0;

        public void End()
        {
            if (count > 0) mesh.SetVertexBufferData(vertices, 0, 0, count, 0, Quiet);
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, count / 4 * 6) { bounds = everywhere, vertexCount = count }, Quiet);
            mesh.bounds = everywhere;
        }

        public void Dispose()
        {
            if (vertices.IsCreated) vertices.Dispose();
            UnityEngine.Object.Destroy(mesh);
        }

        public void Tri(float x, float y, float half, bool up, float turn, Color32 top, Color32 low, float corner, float soft = 0f)
        {
            if (count + 4 > vertices.Length || half <= 0f) return;
            float pad = soft > 0f ? soft : TrianglePad;
            float right = 1f + pad;
            float bottom = (up ? -TriangleLow : -TriangleHigh) - pad;
            float peak = (up ? TriangleHigh : TriangleLow) + pad;
            float flip = up ? 1f : -1f;
            float cos = 1f, sin = 0f;
            if (turn != 0f)
            {
                cos = Mathf.Cos(turn);
                sin = Mathf.Sin(turn);
            }
            Put(x, y, half, cos, sin, -right, bottom, bottom * flip, low, corner, soft);
            Put(x, y, half, cos, sin, right, bottom, bottom * flip, low, corner, soft);
            Put(x, y, half, cos, sin, -right, peak, peak * flip, top, corner, soft);
            Put(x, y, half, cos, sin, right, peak, peak * flip, top, corner, soft);
        }

        public void Line(float fromX, float fromY, float toX, float toY, float radius, Color32 tint, float soft = 0f)
        {
            if (count + 4 > vertices.Length || radius <= 0f) return;
            float alongX = toX - fromX, alongY = toY - fromY;
            float length = Mathf.Sqrt(alongX * alongX + alongY * alongY);
            float cos = 1f, sin = 0f;
            if (length > 1e-5f)
            {
                cos = alongX / length;
                sin = alongY / length;
            }
            float reach = length * 0.5f / radius;
            float pad = soft > 0f ? soft : LinePad;
            float wide = reach + 1f + pad, tall = 1f + pad;
            float x = (fromX + toX) * 0.5f, y = (fromY + toY) * 0.5f;
            float kind = -1f - reach;
            Put(x, y, radius, cos, sin, -wide, -tall, -tall, tint, kind, soft);
            Put(x, y, radius, cos, sin, wide, -tall, -tall, tint, kind, soft);
            Put(x, y, radius, cos, sin, -wide, tall, tall, tint, kind, soft);
            Put(x, y, radius, cos, sin, wide, tall, tall, tint, kind, soft);
        }

        public void Dot(float x, float y, float radius, Color32 tint, float soft = 0f) => Line(x, y, x, y, radius, tint, soft);

        void Put(float x, float y, float scale, float cos, float sin, float localX, float localY, float shapeY, Color32 tint, float kind, float soft)
        {
            vertices[count++] = new Vertex
            {
                At = new Vector3(x + (localX * cos - localY * sin) * scale, y + (localX * sin + localY * cos) * scale, 0f),
                Tint = tint,
                Shape = new Vector4(localX, shapeY, kind, soft)
            };
        }
    }
}
