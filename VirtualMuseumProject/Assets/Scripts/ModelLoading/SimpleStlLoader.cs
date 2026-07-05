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
    ///
    /// 대용량(수십만 삼각형) 파일 대응:
    /// - 파싱/정점 웰딩/노멀/UV 계산을 전부 백그라운드 스레드에서 수행 (메인 스레드는 Mesh 조립만)
    /// - 정점 웰딩(동일 좌표 병합)으로 메모리 절감 + 스무스 셰이딩 노멀 확보
    ///   (STL은 삼각형별 독립 정점이라 웰딩 없이는 각진 플랫 셰이딩 + 3배 메모리)
    /// - STL에는 UV가 없으므로 발굴 마스크 페인팅용 구면 투영 UV를 생성
    /// </summary>
    public static class SimpleStlLoader
    {
        private class ParsedStl
        {
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector2[] Uvs;
            public int[] Triangles;
        }

        public static async Task<Mesh> LoadMeshAsync(string filePath)
        {
            byte[] bytes = await Task.Run(() => File.ReadAllBytes(filePath));
            ParsedStl parsed = await Task.Run(() => Parse(bytes));

            if (parsed == null || parsed.Vertices.Length == 0)
                return null;

            // Mesh API는 메인 스레드 전용 — 여기서는 조립만 한다 (노멀은 이미 백그라운드에서 계산됨)
            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(filePath) };
            if (parsed.Vertices.Length > 65535)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.vertices = parsed.Vertices;
            mesh.normals = parsed.Normals;
            mesh.uv = parsed.Uvs;
            mesh.triangles = parsed.Triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------------------------------------------------------- 파싱 (백그라운드)

        private static ParsedStl Parse(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 15) return null;
            Vector3[] rawTriangleSoup = IsBinary(bytes) ? ReadBinarySoup(bytes) : ReadAsciiSoup(bytes);
            if (rawTriangleSoup == null || rawTriangleSoup.Length < 3) return null;
            return WeldAndBuild(rawTriangleSoup);
        }

        private static bool IsBinary(byte[] bytes)
        {
            // 바이너리 STL도 "solid"로 시작할 수 있으므로 파일 크기 일치 여부로 판별한다.
            if (bytes.Length < 84) return false;
            uint triCount = BitConverter.ToUInt32(bytes, 80);
            long expected = 84L + triCount * 50L;
            return bytes.Length >= expected && triCount > 0;
        }

        private static Vector3[] ReadBinarySoup(byte[] bytes)
        {
            uint triCount = BitConverter.ToUInt32(bytes, 80);
            var vertices = new Vector3[triCount * 3];

            for (int i = 0; i < triCount; i++)
            {
                int offset = 84 + i * 50 + 12; // 노멀(12바이트)은 건너뛰고 웰딩 후 재계산
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

            return vertices;
        }

        private static Vector3[] ReadAsciiSoup(byte[] bytes)
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

            return vertices.ToArray();
        }

        // ---------------------------------------------------------------- 웰딩 + 노멀/UV (백그라운드)

        private static ParsedStl WeldAndBuild(Vector3[] soup)
        {
            int triVertCount = soup.Length - soup.Length % 3;

            // 1) 정점 웰딩: 완전히 같은 좌표는 하나로 병합
            //    (Vector3의 IEquatable.Equals/GetHashCode는 근사가 아닌 정확 비교이므로 Dictionary 키로 안전)
            var indexOf = new Dictionary<Vector3, int>(triVertCount / 2);
            var unique = new List<Vector3>(triVertCount / 2);
            var triangles = new int[triVertCount];

            for (int i = 0; i < triVertCount; i++)
            {
                Vector3 v = soup[i];
                if (!indexOf.TryGetValue(v, out int idx))
                {
                    idx = unique.Count;
                    indexOf.Add(v, idx);
                    unique.Add(v);
                }
                triangles[i] = idx;
            }

            var vertices = unique.ToArray();

            // 2) 스무스 노멀: 면적 가중(교차곱 크기 비례) 누적 후 정규화
            var normals = new Vector3[vertices.Length];
            for (int i = 0; i + 2 < triVertCount; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                Vector3 faceNormal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                normals[a] += faceNormal;
                normals[b] += faceNormal;
                normals[c] += faceNormal;
            }
            for (int i = 0; i < normals.Length; i++)
                normals[i] = normals[i].sqrMagnitude > 1e-12f ? normals[i].normalized : Vector3.up;

            // 3) 바운즈 중심 기준 구면 투영 UV (STL에는 UV 정보가 없음)
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

            return new ParsedStl { Vertices = vertices, Normals = normals, Uvs = uvs, Triangles = triangles };
        }
    }
}
