using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

public class SmMeshDatas
{
    public List<Vector3> Vertices;
    public List<Vector3> Normals;

    public List<Color> Colors;
    public List<Color32> Colors32;

    public List<Vector2> UV;
    public List<Vector2> UV2;
    public List<Vector2> UV3;
    public List<Vector2> UV4;
    public List<Vector4> Tangents;

    public List<BoneWeight> BoneWeights;
    public List<Matrix4x4> Bindposes;
	public List<Matrix4x4> PointsLtoW;
	public List<Vector3> VerticesWorldPos;
    public List<List<int>> SubMeshes;
	// Warning: in order to keep the bones indexes that used in skinned vertices weight, some of the item in list maybe the null!
	public List<Transform> Bones;
	public Transform RootTransform;
	public Matrix4x4 LtoW;

    public List<Material> Materials;

    public bool NormalsExists { get { return Normals != null; } }
    public bool ColorsExists { get { return Colors != null; } }
    public bool Colors32Exists { get { return Colors32 != null; } }
    public bool UVExists { get { return UV != null; } }
    public bool UV2Exists { get { return UV2 != null; } }
    public bool UV3Exists { get { return UV3 != null; } }
    public bool UV4Exists { get { return UV4 != null; } }
    public bool TangentsExists { get { return Tangents != null; } }
    public bool BoneWeightsExists { get { return BoneWeights != null; } }
    public bool MaterialsExists { get { return Materials != null; } }

	public List<int> posPoints, negPoints, posTriangles, negTriangles;
	/// <summary>
	/// Record the many of two vertices index to indicate edges that cross the section edges, 
	/// and one cross edge indicate one new section vertice
	/// </summary>
	public List<IndexVector> slicedCrossEdges;
	/// <summary>
	/// Record the many of two slicedCrossEdges index to indicate section edges,
	/// and slicedCrossEdges index will give the new vertice index later 
	/// </summary>
	public List<IndexVector> slicedSectionEdgeIndexes;
	/// <summary>
	/// Record both side of section triangles, use both slicedCrossEdges index and vertices index
	/// </summary>
	public List<List<SectionTriangle>> sectionPosTriangles, sectionNegTriangles;
    public SmMeshDatas(Mesh initMesh, Transform newRoot = null, Material[] newMaterials = null, Transform[] newBones = null)
    {
		if (initMesh != null)
		{
			Materials = newMaterials != null ? newMaterials.ToList() : null;
			RootTransform = newRoot;
			int vertCount = initMesh.vertexCount / 3;
			Bindposes = initMesh.bindposes.ToList();
			if (Bindposes.Count == 0)
			{
				Bindposes = null;
			}
			else
			{
				Bones = newBones.ToList();		
			}	

			Vertices = new List<Vector3>(vertCount);
			Normals = new List<Vector3>(vertCount);
			Colors = new List<Color>();
			Colors32 = new List<Color32>();
			UV = new List<Vector2>(vertCount);
			UV2 = new List<Vector2>();
			UV3 = new List<Vector2>();
			UV4 = new List<Vector2>();
			Tangents = new List<Vector4>();
			BoneWeights = new List<BoneWeight>(Bindposes == null ? 0 : vertCount);

			initMesh.GetVertices(Vertices);
			initMesh.GetNormals(Normals);
			initMesh.GetColors(Colors);
			initMesh.GetColors(Colors32);
			initMesh.GetUVs(0, UV);
			initMesh.GetUVs(1, UV2);
			initMesh.GetUVs(2, UV3);
			initMesh.GetUVs(3, UV4);
			initMesh.GetTangents(Tangents);
			initMesh.GetBoneWeights(BoneWeights);

			SubMeshes = new List<List<int>>();

			for (int subMeshIndex = 0; subMeshIndex < initMesh.subMeshCount; ++subMeshIndex)
				SubMeshes.Add(initMesh.GetTriangles(subMeshIndex).ToList());

			if (Normals.Count == 0)		Normals = null;
			if (Colors.Count == 0)		Colors = null;
			if (Colors32.Count == 0)	Colors32 = null;
			if (UV.Count == 0)			UV = null;
			if (UV2.Count == 0)			UV2 = null;
			if (UV3.Count == 0)			UV3 = null;
			if (UV4.Count == 0)			UV4 = null;
			if (Tangents.Count == 0)	Tangents = null;
			if (BoneWeights.Count == 0)	BoneWeights = null;	
		}
    }
    public Mesh GenerateMesh()
    {
        Mesh mesh = new Mesh();
        
        mesh.SetVertices(Vertices);
        if (NormalsExists)
            mesh.SetNormals(Normals);

        if (ColorsExists)
            mesh.SetColors(Colors);
        if (Colors32Exists)
            mesh.SetColors(Colors32);

        if (UVExists)
            mesh.SetUVs(0, UV);
        if (UV2Exists)
            mesh.SetUVs(1, UV2);
        if (UV3Exists)
            mesh.SetUVs(2, UV3);
        if (UV4Exists)
            mesh.SetUVs(3, UV4);

        if (TangentsExists)
            mesh.SetTangents(Tangents);

        if (BoneWeightsExists)
        {
            mesh.boneWeights = BoneWeights.ToArray();
            mesh.bindposes = Bindposes.ToArray();
        }
        
        mesh.subMeshCount = SubMeshes.Count;
        for (int subMeshIndex = 0; subMeshIndex < SubMeshes.Count; ++subMeshIndex)
            mesh.SetTriangles(SubMeshes[subMeshIndex], subMeshIndex);

        return mesh;
    }
    public Mesh GeneratePartialMesh(List<int> verticeIndexes, List<int> triangleIndexes)
    {
        Mesh mesh = new Mesh();
		var partialVertices = new List<Vector3>();
		var partialNormals = new List<Vector3>();
		var partialColors = new List<Color>();
		var partialColors32 = new List<Color32>();
		var partialUV = new List<Vector2>();
		var partialUV2 = new List<Vector2>();
		var partialUV3 = new List<Vector2>();
		var partialUV4 = new List<Vector2>();
		var partialTangents = new List<Vector4>();
		var partialBoneWeights = new List<BoneWeight>();
		var mapedVertices = new Dictionary<int, int>();

		foreach (int i in verticeIndexes)
		{
			mapedVertices.Add(i, partialVertices.Count);	
			partialVertices.Add(Vertices[i]);
			if (NormalsExists)
				partialNormals.Add(Normals[i]);
			if (ColorsExists)
				partialColors.Add(Colors[i]);
			if (Colors32Exists)
				partialColors32.Add(Colors32[i]);
			if (UVExists)
				partialUV.Add(UV[i]);
			if (UV2Exists)
				partialUV2.Add(UV2[i]);
			if (UV3Exists)
				partialUV3.Add(UV3[i]);
			if (UV4Exists)
				partialUV4.Add(UV4[i]);
			if (TangentsExists)
				partialTangents.Add(Tangents[i]);
			if (BoneWeightsExists)
				partialBoneWeights.Add(BoneWeights[i]);		
		}

        mesh.SetVertices(partialVertices);
        if (NormalsExists)
            mesh.SetNormals(partialNormals);

        if (ColorsExists)
            mesh.SetColors(partialColors);
        if (Colors32Exists)
            mesh.SetColors(partialColors32);

        if (UVExists)
            mesh.SetUVs(0, partialUV);
        if (UV2Exists)
            mesh.SetUVs(1, partialUV2);
        if (UV3Exists)
            mesh.SetUVs(2, partialUV3);
        if (UV4Exists)
            mesh.SetUVs(3, partialUV4);

        if (TangentsExists)
            mesh.SetTangents(partialTangents);

        if (BoneWeightsExists)
        {
            mesh.boneWeights = partialBoneWeights.ToArray();
            mesh.bindposes = Bindposes.ToArray();
        }
        
		var partialTriangleIndexes = new int[triangleIndexes.Count];
		for (int i = 0; i < triangleIndexes.Count; i++)
			partialTriangleIndexes[i] = mapedVertices[triangleIndexes[i]];
        mesh.subMeshCount = 1;
        mesh.SetTriangles(partialTriangleIndexes, 0);		
		return mesh;
	}
	
    public void AddNewPoint(int from, int to, float ratioIn)
    {
		{ // Vertex
			Vector3 vFrom = Vertices[from];
			Vector3 vTo = Vertices[to];
			Vector3 vNew = Vector3.Lerp(vFrom, vTo, ratioIn);
			Vertices.Add(vNew);
		}

		if (NormalsExists)
		{
			Vector3 nFrom = Normals[from];
			Vector3 nTo = Normals[to];
			Vector3 nNew = Vector3.Slerp(nFrom, nTo, ratioIn);
			Normals.Add(nNew);
		}

		if (ColorsExists)
		{
			Color colorFrom = Colors[from];
			Color colorTo = Colors[to];
			Color colorNew = Color.Lerp(colorFrom, colorTo, ratioIn);
			Colors.Add(colorNew);
		}
		if (Colors32Exists)
		{
			Color32 colorFrom = Colors32[from];
			Color32 colorTo = Colors32[to];
			Color32 colorNew = Color32.Lerp(colorFrom, colorTo, ratioIn);
			Colors32.Add(colorNew);
		}

		if (UVExists)
		{
			AddValue(UV, from, to, ratioIn);
		}
		if (UV2Exists)
		{
			AddValue(UV2, from, to, ratioIn);
		}
		if (UV3Exists)
		{
			AddValue(UV3, from, to, ratioIn);
		}
		if (UV4Exists)
		{
			AddValue(UV4, from, to, ratioIn);
		}

		if (TangentsExists)
		{
			Vector4 vFrom = Tangents[from];
			Vector4 vTo = Tangents[to];
			Vector4 vNew = Vector4.Lerp(vFrom, vTo, ratioIn);
			Tangents.Add(vNew);
		}

		if (BoneWeightsExists)
		{
			var w1 = BoneWeights[from];
			var w2 = BoneWeights[to];

			var ws = new Dictionary<int, float>();
			float ratioOut = 1 - ratioIn;

			if (w1.weight0 != 0) ws.Add(w1.boneIndex0, w1.weight0 * ratioIn);
			if (w1.weight1 != 0) ws.Add(w1.boneIndex1, w1.weight1 * ratioIn);
			if (w1.weight2 != 0) ws.Add(w1.boneIndex2, w1.weight2 * ratioIn);
			if (w1.weight3 != 0) ws.Add(w1.boneIndex3, w1.weight3 * ratioIn);

			if (w2.weight0 != 0)
			{
				float fA;
				ws.TryGetValue(w2.boneIndex0, out fA);
				ws[w2.boneIndex0] = fA + w2.weight0 * ratioOut;
			}
			if (w2.weight1 != 0)
			{
				float fA;
				ws.TryGetValue(w2.boneIndex1, out fA);
				ws[w2.boneIndex1] = fA + w2.weight1 * ratioOut;
			}
			if (w2.weight2 != 0)
			{
				float fA;
				ws.TryGetValue(w2.boneIndex2, out fA);
				ws[w2.boneIndex2] = fA + w2.weight2 * ratioOut;
			}
			if (w2.weight3 != 0)
			{
				float fA;
				ws.TryGetValue(w2.boneIndex3, out fA);
				ws[w2.boneIndex3] = fA + w2.weight3 * ratioOut;
			}

			var newBoneWeight = new BoneWeight();
			var wsArr = ws
				.Where(v => v.Value != 0)
				.Take(4)
				.OrderByDescending(v => v.Value)
				.ToArray();
			KeyValuePair<int, float>[] wsArr4 = new KeyValuePair<int, float>[4];
			Array.Copy(wsArr, wsArr4, wsArr.Length);
			
			float weightSum = 0;
			weightSum += wsArr4[0].Value;
			weightSum += wsArr4[1].Value;
			weightSum += wsArr4[2].Value;
			weightSum += wsArr4[3].Value;

			float weightRatio = 1 / weightSum;

			if (wsArr.Length > 0)
			{
				newBoneWeight.boneIndex0 = wsArr[0].Key;
				newBoneWeight.weight0 = wsArr[0].Value * weightRatio;
			}
			if (wsArr.Length > 1)
			{
				newBoneWeight.boneIndex1 = wsArr[1].Key;
				newBoneWeight.weight1 = wsArr[1].Value * weightRatio;
			}
			if (wsArr.Length > 2)
			{
				newBoneWeight.boneIndex2 = wsArr[2].Key;
				newBoneWeight.weight2 = wsArr[2].Value * weightRatio;
			}
			if (wsArr.Length > 3)
			{
				newBoneWeight.boneIndex3 = wsArr[3].Key;
				newBoneWeight.weight3 = wsArr[3].Value * weightRatio;
			}

			BoneWeights.Add(newBoneWeight);
		}
		
		VerticesWorldPos.Add(GetVerticeWorldPos(VerticesWorldPos.Count));
    }
	public void AddNewSubmeshs(List<List<int>> addTriangles, Material capMaterial)
	{
		List<int> newTriangles = new List<int>();
		foreach (var triangles in addTriangles)
		{
			newTriangles.AddRange(triangles);
		}
		SubMeshes.Add(newTriangles);
		
		if (MaterialsExists)
			Materials.Add(capMaterial);			
	}
	public void AddNewBones(Transform[] oldBones, Transform[] newBones, Dictionary<Transform, int> mapedBones)
	{
		for (int i = 0; i < newBones.Length; i++)
		{
			int oldIndex = Bones.IndexOf(oldBones[i]);
			if (oldIndex >= 0)
			{
				var newIndex = Bones.Count;
				Bones.Add(newBones[i]);
				Bindposes.Add(Bindposes[oldIndex]);
				mapedBones.Add(oldBones[i], newIndex);
			}
		}
	}
	public int AddNewBone(Transform newBone, Transform oldBone)
	{
		int index = -1;
		int oldIndex = Bones.IndexOf(oldBone);
		if (oldIndex >= 0)
		{
			index = Bones.Count;
			Bones.Add(newBone);
			Bindposes.Add(Bindposes[oldIndex]);
		}
		return index;
	}
	public List<int> DuplicateVerticesData(List<int> verticeIndexes)
	{
		List<int> newVerticeIndexes = new List<int>();
		foreach (int i in verticeIndexes)
		{
			newVerticeIndexes.Add(Vertices.Count);
			Vertices.Add(Vertices[i]);
			VerticesWorldPos.Add(VerticesWorldPos[i]);
			if (NormalsExists)
				Normals.Add(Normals[i]);
			if (ColorsExists)
				Colors.Add(Colors[i]);
			if (Colors32Exists)
				Colors32.Add(Colors32[i]);
			if (UVExists)
				UV.Add(UV[i]);
			if (UV2Exists)
				UV2.Add(UV2[i]);
			if (UV3Exists)
				UV3.Add(UV3[i]);
			if (UV4Exists)
				UV4.Add(UV4[i]);
			if (TangentsExists)
				Tangents.Add(Tangents[i]);
			if (BoneWeightsExists)
				BoneWeights.Add(BoneWeights[i]);
		}		
		return newVerticeIndexes;
	}
	public Tuple<List<List<int>>, int> DuplicateVerticesData(List<List<int>> verticeIndexes)
	{
		int allCount = 0;
		List<List<int>> newVerticeIndexes = new List<List<int>>();
		for (int iV = 0; iV < verticeIndexes.Count; iV++)
		{
			allCount += verticeIndexes[iV].Count;
			List<int> newIndexes = new List<int>();
			foreach (int i in verticeIndexes[iV])
			{
				newIndexes.Add(Vertices.Count);
				Vertices.Add(Vertices[i]);
				VerticesWorldPos.Add(VerticesWorldPos[i]);
				if (NormalsExists)
					Normals.Add(Normals[i]);
				if (ColorsExists)
					Colors.Add(Colors[i]);
				if (Colors32Exists)
					Colors32.Add(Colors32[i]);
				if (UVExists)
					UV.Add(UV[i]);
				if (UV2Exists)
					UV2.Add(UV2[i]);
				if (UV3Exists)
					UV3.Add(UV3[i]);
				if (UV4Exists)
					UV4.Add(UV4[i]);
				if (TangentsExists)
					Tangents.Add(Tangents[i]);
				if (BoneWeightsExists)
					BoneWeights.Add(BoneWeights[i]);
			}
			newVerticeIndexes.Add(newIndexes);
		}	
		return Tuple.Create(newVerticeIndexes, allCount);
	}
	public void SetVerticesNormalsAndUVs(List<List<int>> verticeIndexes, List<Vector3> newNormals, List<List<Vector2>> capsUVList)
	{
		for (int iV = 0; iV < verticeIndexes.Count; iV++)
		{
			for (int i = 0; i < verticeIndexes[iV].Count; i++)
			{
				int iP = verticeIndexes[iV][i];
				if (NormalsExists && newNormals[iV].magnitude != 0f)
					Normals[iP] = newNormals[iV];
				if (UVExists)
					UV[iP] = capsUVList[iV][i];
			}		
		}		
	}
	public void DuplicateTriangles(List<List<int>> newTriangleIndexes, int indexShift)
	{
		for (int iT = 0; iT < newTriangleIndexes.Count; iT++)
		{
			for (int i = 0; i < newTriangleIndexes[iT].Count; i++)
				newTriangleIndexes[iT][i] += indexShift;
		}
	}
	public Vector3 GetVerticeWorldPos(int index)
	{
		Vector3 pointWorldPosition = Vector3.zero;
		Vector3 position = Vertices[index];
		if (BoneWeightsExists)
		{
			BoneWeight boneWeight = BoneWeights[index];
			if (boneWeight.weight0 > 0f)
				pointWorldPosition += PointsLtoW[boneWeight.boneIndex0].MultiplyPoint3x4(position) * boneWeight.weight0;
			if (boneWeight.weight1 > 0f)
				pointWorldPosition += PointsLtoW[boneWeight.boneIndex1].MultiplyPoint3x4(position) * boneWeight.weight1;
			if (boneWeight.weight2 > 0f)
				pointWorldPosition += PointsLtoW[boneWeight.boneIndex2].MultiplyPoint3x4(position) * boneWeight.weight2;
			if (boneWeight.weight3 > 0f)
				pointWorldPosition += PointsLtoW[boneWeight.boneIndex3].MultiplyPoint3x4(position) * boneWeight.weight3;
		}
		else
			pointWorldPosition = LtoW.MultiplyPoint3x4(position);
		return pointWorldPosition;
	}
	public void SetLtoWMatrix()
	{
		VerticesWorldPos = new List<Vector3>();
		if (BoneWeightsExists)
		{
			PointsLtoW = new List<Matrix4x4>();
			for (int i = 0; i < Bones.Count; i++)
			{
				Transform bone = Bones[i];
				PointsLtoW.Add(bone != null ? bone.localToWorldMatrix * Bindposes[i] : new Matrix4x4());				
			}
		}
		else
			LtoW = RootTransform.localToWorldMatrix;

		for (int i = 0; i < Vertices.Count; i++)
			VerticesWorldPos.Add(GetVerticeWorldPos(i));
	}
    private static void AddValue(List<Vector2> vectors, int from, int to, float ratioL)
    {
        Vector2 nFrom = vectors[from];
        Vector2 nTo = vectors[to];
        Vector2 nNew = Vector2.Lerp(nFrom, nTo, ratioL);
        vectors.Add(nNew);
    }
}
