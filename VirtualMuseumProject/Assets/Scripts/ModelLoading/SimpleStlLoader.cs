using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace VirtualMuseum.ModelLoading
{
    /// <summary>
    /// 외부 에셋(TriLib) 없이 동작하는 경량 STL 로더 (바이너리/ASCII 자동 판별).
    /// 파싱(순수 C# 연산)은 백그라운드 스레드에서, Mesh 생성은 메인 스레드에서 수행한다.
    /// STL에는 UV가 없으므로 발굴 마스크 페인팅이 가능하도록 구면 투영 UV를 생성한다.
    /// </summary>
    public static class SimpleStlLoader
    {
        private class ParsedStl
        {
            public Vector3[] Vertices;
            public int[] Triangles;
            public Vector2[] Uvs;
        }

        public static async Task<Mesh> LoadMeshAsync(string filePath)
        {
            byte[] bytes = await Task.Run(() => File.ReadAllBytes(filePath));
            ParsedStl parsed = await Task.Run(() => Parse(bytes));

            if (parsed == null || parsed.Vertices.Length == 0)
                return null;

            // Mesh API는 메인 스레드 전용
            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(filePath) };
            if (parsed.Vertices.Length > 65535)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.vertices = parsed.Vertices;
            mesh.triangles = parsed.Triangles;
            mesh.uv = parsed.Uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static ParsedStl Parse(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 15) return null;
            return IsBinary(bytes) ? ParseBinary(bytes) : ParseAscii(bytes);
        }

        private static bool IsBinary(byte[] bytes)
        {
            // 바이너리 STL도 "solid"로 시작할 수 있으므로 파일 크기 일치 여부로 판별한다.
            if (bytes.Length < 84) return false;
            uint triCount = BitConverter.ToUInt32(bytes, 80);
            long expected = 84L + triCount * 50L;
            return bytes.Length >= expected && triCount > 0;
        }

        private static ParsedStl ParseBinary(byte[] bytes)
        {
            uint triCount = BitConverter.ToUInt32(bytes, 80);
            var vertices = new Vector3[triCount * 3];

            for (int i = 0; i < triCount; i++)
            {
                int offset = 84 + i * 50 + 12; // 노멀(12바이트)은 건너뛰고 재계산
                for (int v = 0; v < 3; v++)
                {
                    int o = offset + v * 12;
                    // STL은 보통 Z-up이므로 Unity Y-up으로 축 변환 (x, z, y)
                    float x = BitConverter.ToSingle(bytes, o);
                    float y = BitConverter.ToSingle(bytes, o + 4);
                    float z = BitConverter.ToSingle(bytes, o + 8);
                    vertices[i * 3 + v] = new Vector3(x, z, y);
                }
                // 축 반전으로 인해 winding order를 뒤집는다
                (vertices[i * 3 + 1], vertices[i * 3 + 2]) = (vertices[i * 3 + 2], vertices[i * 3 + 1]);
            }

            return BuildParsed(vertices);
        }

        private static ParsedStl ParseAscii(byte[] bytes)
        {
            string text = Encoding.ASCII.GetString(bytes);
            var vertices = new List<Vector3>();

            using var reader = new StringReader(text);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (!line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase)) continue;

                string[] tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 4) continue;

                float x = float.Parse(tokens[1], CultureInfo.InvariantCulture);
                float y = float.Parse(tokens[2], CultureInfo.InvariantCulture);
                float z = float.Parse(tokens[3], CultureInfo.InvariantCulture);
                vertices.Add(new Vector3(x, z, y)); // Z-up → Y-up
            }

            // 3개 단위 winding 반전
            for (int i = 0; i + 2 < vertices.Count; i += 3)
                (vertices[i + 1], vertices[i + 2]) = (vertices[i + 2], vertices[i + 1]);

            return BuildParsed(vertices.ToArray());
        }

        private static ParsedStl BuildParsed(Vector3[] vertices)
        {
            int triVertCount = vertices.Length - vertices.Length % 3;
            var triangles = new int[triVertCount];
            for (int i = 0; i < triVertCount; i++) triangles[i] = i;

            // 바운즈 중심 기준 구면 투영 UV 생성 (STL에는 UV 정보가 없음)
            Vector3 min = vertices[0], max = vertices[0];
            foreach (var v in vertices)
            {
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }
            Vector3 center = (min + max) * 0.5f;

            var uvs = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 dir = vertices[i] - center;
                if (dir.sqrMagnitude < 1e-8f) dir = Vector3.up;
                dir.Normalize();
                float u = 0.5f + Mathf.Atan2(dir.z, dir.x) / (2f * Mathf.PI);
                float w = 0.5f + Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) / Mathf.PI;
                uvs[i] = new Vector2(u, w);
            }

            return new ParsedStl { Vertices = vertices, Triangles = triangles, Uvs = uvs };
        }
    }
}
