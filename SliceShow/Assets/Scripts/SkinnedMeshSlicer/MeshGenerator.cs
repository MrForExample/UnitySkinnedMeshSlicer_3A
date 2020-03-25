using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MeshGenerator
{
	// http://wiki.unity3d.com/index.php/ProceduralPrimitives#C.23_-_Box
	public static class Cube
	{
		public static Mesh Create(Vector3 size, Vector3 center)
		{
			float length = size.x;
			float width = size.y;
			float height = size.z;

			Mesh mesh = new Mesh();

			#region Vertices
			Vector3 p0 = new Vector3(-length * .5f, -width * .5f, height * .5f);
			Vector3 p1 = new Vector3(length * .5f, -width * .5f, height * .5f);
			Vector3 p2 = new Vector3(length * .5f, -width * .5f, -height * .5f);
			Vector3 p3 = new Vector3(-length * .5f, -width * .5f, -height * .5f);

			Vector3 p4 = new Vector3(-length * .5f, width * .5f, height * .5f);
			Vector3 p5 = new Vector3(length * .5f, width * .5f, height * .5f);
			Vector3 p6 = new Vector3(length * .5f, width * .5f, -height * .5f);
			Vector3 p7 = new Vector3(-length * .5f, width * .5f, -height * .5f);

			Vector3[] vertices = new Vector3[]
			{
				// Bottom
				p0, p1, p2, p3,
 
				// Left
				p7, p4, p0, p3,
 
				// Front
				p4, p5, p1, p0,
 
				// Back
				p6, p7, p3, p2,
 
				// Right
				p5, p6, p2, p1,
 
				// Top
				p7, p6, p5, p4
			};
			#endregion

			#region Normales
			Vector3 up = Vector3.up;
			Vector3 down = Vector3.down;
			Vector3 front = Vector3.forward;
			Vector3 back = Vector3.back;
			Vector3 left = Vector3.left;
			Vector3 right = Vector3.right;

			Vector3[] normales = new Vector3[]
			{
				// Bottom
				down, down, down, down,
 
				// Left
				left, left, left, left,
 
				// Front
				front, front, front, front,
 
				// Back
				back, back, back, back,
 
				// Right
				right, right, right, right,
 
				// Top
				up, up, up, up
			};
			#endregion

			#region UVs
			Vector2 _00 = new Vector2(0f, 0f);
			Vector2 _10 = new Vector2(1f, 0f);
			Vector2 _01 = new Vector2(0f, 1f);
			Vector2 _11 = new Vector2(1f, 1f);

			Vector2[] uvs = new Vector2[]
			{
				// Bottom
				_11, _01, _00, _10,
 
				// Left
				_11, _01, _00, _10,
 
				// Front
				_11, _01, _00, _10,
 
				// Back
				_11, _01, _00, _10,
 
				// Right
				_11, _01, _00, _10,
 
				// Top
				_11, _01, _00, _10,
			};
			#endregion

			#region Triangles
			int[] triangles = new int[]
			{
				// Bottom
				3, 1, 0,
				3, 2, 1,			
 
				// Left
				3 + 4 * 1, 1 + 4 * 1, 0 + 4 * 1,
				3 + 4 * 1, 2 + 4 * 1, 1 + 4 * 1,
 
				// Front
				3 + 4 * 2, 1 + 4 * 2, 0 + 4 * 2,
				3 + 4 * 2, 2 + 4 * 2, 1 + 4 * 2,
 
				// Back
				3 + 4 * 3, 1 + 4 * 3, 0 + 4 * 3,
				3 + 4 * 3, 2 + 4 * 3, 1 + 4 * 3,
 
				// Right
				3 + 4 * 4, 1 + 4 * 4, 0 + 4 * 4,
				3 + 4 * 4, 2 + 4 * 4, 1 + 4 * 4,
 
				// Top
				3 + 4 * 5, 1 + 4 * 5, 0 + 4 * 5,
				3 + 4 * 5, 2 + 4 * 5, 1 + 4 * 5,
			};
			#endregion

			#region Shift vertices
			if (Math.Abs(center.sqrMagnitude) > Mathf.Epsilon)
				for (int i = 0; i < vertices.Length; i++)
				{
					vertices[i] = vertices[i] + center;
				}
			#endregion

			mesh.vertices = vertices;
			mesh.normals = normales;
			mesh.uv = uvs;
			mesh.triangles = triangles;

			mesh.RecalculateBounds();
			;

			return mesh;
		}
	}
	public static class Capsule
	{
		/// <param name="nbLong">Longitude</param>
		/// <param name="nbLat">Latitude</param>
		public static Mesh Create(float radius, float height, int direction, Vector3 center, int nbLong = 12, int nbLat = 4)
		{
			Mesh mesh = new Mesh();

			#region Vertices
			Vector3[] vertices = new Vector3[nbLong * nbLat * 2 + 2];
			height = Mathf.Max(height / 2f - radius, 0f);
			const float _2pi = Mathf.PI * 2f;

			var shift = Vector3.up * height;
			vertices[0] = Vector3.up * radius + shift;
			for (int lat = 0; lat < nbLat; lat++)
			{
				float a1 = Mathf.PI * (lat + 1) / (nbLat) / 2;
				float sin1 = Mathf.Sin(a1);
				float cos1 = Mathf.Cos(a1);

				for (int lon = 0; lon < nbLong; lon++)
				{
					float a2 = _2pi * lon / nbLong;
					float sin2 = Mathf.Sin(a2);
					float cos2 = Mathf.Cos(a2);

					vertices[
						1 +             // up vertex
						lat * nbLong +  // lat shift
						lon             // lon shift
						] = shift + new Vector3(sin1 * cos2, cos1, sin1 * sin2) * radius;
				}
			}

			shift = Vector3.up * -height;
			for (int lat = 0; lat < nbLat; lat++)
			{
				float a1 = Mathf.PI / 2f + Mathf.PI * (lat) / (nbLat) / 2;
				float sin1 = Mathf.Sin(a1);
				float cos1 = Mathf.Cos(a1);

				for (int lon = 0; lon < nbLong; lon++)
				{
					float a2 = _2pi * lon / nbLong;
					float sin2 = Mathf.Sin(a2);
					float cos2 = Mathf.Cos(a2);

					vertices[
						nbLong * nbLat +
						1 +             // up vertex
						lat * nbLong +  // lat shift
						lon             // lon shift
						] = shift + new Vector3(sin1 * cos2, cos1, sin1 * sin2) * radius;
				}
			}

			vertices[vertices.Length - 1] = Vector3.up * -radius + shift;
			#endregion

			//#region Normales		
			//Vector3[] normales = new Vector3[vertices.Length];
			//for (int n = 0; n < vertices.Length; n++)
			//	normales[n] = vertices[n].normalized;
			//#endregion

			//#region UVs
			//Vector2[] uvs = new Vector2[vertices.Length];
			//uvs[0] = Vector2.up;
			//uvs[uvs.Length - 1] = Vector2.zero;
			//for (int lat = 0; lat < nbLat; lat++)
			//	for (int lon = 0; lon <= nbLong; lon++)
			//		uvs[lon + lat * (nbLong + 1) + 1] = new Vector2((float)lon / nbLong, 1f - (float)(lat + 1) / (nbLat + 1));
			//#endregion

			#region Triangles
			int caps = nbLong * 2;
			int middle = ((nbLat - 1) * 2 + 1) * nbLong * 2;

			int[] triangles = new int[(caps + middle) * 3];

			//Top Cap
			int i = 0;
			for (int lon = 0; lon < nbLong - 1; lon++)
			{
				triangles[i++] = lon + 2;
				triangles[i++] = lon + 1;
				triangles[i++] = 0;
			}
			triangles[i++] = 1;
			triangles[i++] = nbLong;
			triangles[i++] = 0;

			//Middle
			for (int lat = 0; lat < nbLat * 2 - 1; lat++)
			{
				for (int lon = 0; lon < nbLong - 1; lon++)
				{
					int current = lon + lat * nbLong + 1;
					int next = current + nbLong;

					triangles[i++] = current;
					triangles[i++] = current + 1;
					triangles[i++] = next + 1;

					triangles[i++] = current;
					triangles[i++] = next + 1;
					triangles[i++] = next;
				}

				triangles[i++] = nbLong * (lat + 1);
				triangles[i++] = nbLong * lat + 1;
				triangles[i++] = nbLong * (lat + 1) + 1;

				triangles[i++] = nbLong * (lat + 1);
				triangles[i++] = nbLong * (lat + 1) + 1;
				triangles[i++] = nbLong * (lat + 2);
			}

			//Bottom Cap
			for (int lon = 0; lon < nbLong - 1; lon++)
			{
				triangles[i++] = vertices.Length - 1;
				triangles[i++] = vertices.Length - (lon + 2) - 1;
				triangles[i++] = vertices.Length - (lon + 1) - 1;
			}
			triangles[i++] = vertices.Length - 1;
			triangles[i++] = vertices.Length - 1 - 1;
			triangles[i++] = vertices.Length - (nbLong) - 1;
			#endregion

			#region Rotate
			if (direction != 1)
				for (int ii = 0; ii < vertices.Length; ii++)
				{
					var v = vertices[ii];
					if (direction == 0)
						v = new Vector3(v.y, v.z, v.x);
					if (direction == 2)
						v = new Vector3(v.z, v.x, v.y);
					vertices[ii] = v;
				}
			#endregion

			#region Shift vertices
			if (Math.Abs(center.sqrMagnitude) > Mathf.Epsilon)
				for (int ii = 0; ii < vertices.Length; ii++)
				{
					vertices[ii] = vertices[ii] + center;
				}
			#endregion

			mesh.vertices = vertices;
			//mesh.normals = normales;
			//mesh.uv = uvs;
			mesh.triangles = triangles;

			mesh.RecalculateBounds();
			;

			return mesh;
		}
	}
	// http://wiki.unity3d.com/index.php/ProceduralPrimitives#C.23_-_Sphere
	public static class IcoSphere
	{
		private struct TriangleIndices
		{
			public readonly int V1;
			public readonly int V2;
			public readonly int V3;

			public TriangleIndices(int v1, int v2, int v3)
			{
				V1 = v1;
				V2 = v2;
				V3 = v3;
			}
		}

		public static Mesh Create(float radius, Vector3 center, int recursionLevel = 1)
		{
			Mesh mesh = new Mesh();
			mesh.Clear();

			List<Vector3> vertList = new List<Vector3>();
			Dictionary<long, int> middlePointIndexCache = new Dictionary<long, int>();

			// create 12 vertices of a icosahedron
			float t = (1f + Mathf.Sqrt(5f)) / 2f;

			vertList.Add(new Vector3(-1f, t, 0f).normalized * radius);
			vertList.Add(new Vector3(1f, t, 0f).normalized * radius);
			vertList.Add(new Vector3(-1f, -t, 0f).normalized * radius);
			vertList.Add(new Vector3(1f, -t, 0f).normalized * radius);

			vertList.Add(new Vector3(0f, -1f, t).normalized * radius);
			vertList.Add(new Vector3(0f, 1f, t).normalized * radius);
			vertList.Add(new Vector3(0f, -1f, -t).normalized * radius);
			vertList.Add(new Vector3(0f, 1f, -t).normalized * radius);

			vertList.Add(new Vector3(t, 0f, -1f).normalized * radius);
			vertList.Add(new Vector3(t, 0f, 1f).normalized * radius);
			vertList.Add(new Vector3(-t, 0f, -1f).normalized * radius);
			vertList.Add(new Vector3(-t, 0f, 1f).normalized * radius);


			// create 20 triangles of the icosahedron
			List<TriangleIndices> faces = new List<TriangleIndices>();

			// 5 faces around point 0
			faces.Add(new TriangleIndices(0, 11, 5));
			faces.Add(new TriangleIndices(0, 5, 1));
			faces.Add(new TriangleIndices(0, 1, 7));
			faces.Add(new TriangleIndices(0, 7, 10));
			faces.Add(new TriangleIndices(0, 10, 11));

			// 5 adjacent faces 
			faces.Add(new TriangleIndices(1, 5, 9));
			faces.Add(new TriangleIndices(5, 11, 4));
			faces.Add(new TriangleIndices(11, 10, 2));
			faces.Add(new TriangleIndices(10, 7, 6));
			faces.Add(new TriangleIndices(7, 1, 8));

			// 5 faces around point 3
			faces.Add(new TriangleIndices(3, 9, 4));
			faces.Add(new TriangleIndices(3, 4, 2));
			faces.Add(new TriangleIndices(3, 2, 6));
			faces.Add(new TriangleIndices(3, 6, 8));
			faces.Add(new TriangleIndices(3, 8, 9));

			// 5 adjacent faces 
			faces.Add(new TriangleIndices(4, 9, 5));
			faces.Add(new TriangleIndices(2, 4, 11));
			faces.Add(new TriangleIndices(6, 2, 10));
			faces.Add(new TriangleIndices(8, 6, 7));
			faces.Add(new TriangleIndices(9, 8, 1));


			// refine triangles
			for (int i = 0; i < recursionLevel; i++)
			{
				List<TriangleIndices> faces2 = new List<TriangleIndices>();
				foreach (var tri in faces)
				{
					// replace triangle by 4 triangles
					int a = GetMiddlePoint(tri.V1, tri.V2, ref vertList, ref middlePointIndexCache, radius);
					int b = GetMiddlePoint(tri.V2, tri.V3, ref vertList, ref middlePointIndexCache, radius);
					int c = GetMiddlePoint(tri.V3, tri.V1, ref vertList, ref middlePointIndexCache, radius);

					faces2.Add(new TriangleIndices(tri.V1, a, c));
					faces2.Add(new TriangleIndices(tri.V2, b, a));
					faces2.Add(new TriangleIndices(tri.V3, c, b));
					faces2.Add(new TriangleIndices(a, b, c));
				}
				faces = faces2;
			}

			#region Shift vertices
			if (Math.Abs(center.sqrMagnitude) > Mathf.Epsilon)
				for (int i = 0; i < vertList.Count; i++)
				{
					vertList[i] = vertList[i] + center;
				}
			#endregion

			mesh.vertices = vertList.ToArray();

			List<int> triList = new List<int>();
			for (int i = 0; i < faces.Count; i++)
			{
				triList.Add(faces[i].V1);
				triList.Add(faces[i].V2);
				triList.Add(faces[i].V3);
			}
			mesh.triangles = triList.ToArray();
			mesh.uv = new Vector2[vertList.Count];

			Vector3[] normales = new Vector3[vertList.Count];
			for (int i = 0; i < normales.Length; i++)
				normales[i] = vertList[i].normalized;


			mesh.normals = normales;

			mesh.RecalculateBounds();
			;

			return mesh;
		}

		// return index of point in the middle of p1 and p2
		private static int GetMiddlePoint(int p1, int p2, ref List<Vector3> vertices, ref Dictionary<long, int> cache, float radius)
		{
			// first check if we have it already
			bool firstIsSmaller = p1 < p2;
			long smallerIndex = firstIsSmaller ? p1 : p2;
			long greaterIndex = firstIsSmaller ? p2 : p1;
			long key = (smallerIndex << 32) + greaterIndex;

			int ret;
			if (cache.TryGetValue(key, out ret))
			{
				return ret;
			}

			// not in cache, calculate it
			Vector3 point1 = vertices[p1];
			Vector3 point2 = vertices[p2];
			Vector3 middle = new Vector3
			(
				(point1.x + point2.x) / 2f,
				(point1.y + point2.y) / 2f,
				(point1.z + point2.z) / 2f
			);

			// add vertex makes sure point is on unit sphere
			int i = vertices.Count;
			vertices.Add(middle.normalized * radius);

			// store it, return index
			cache.Add(key, i);

			return i;
		}
	}
}
