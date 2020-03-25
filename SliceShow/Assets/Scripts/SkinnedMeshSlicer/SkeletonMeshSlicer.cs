using System;
using System.Linq;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using MeshGenerator;

public class SkeletonMeshSlicer : MonoBehaviour
{
    public bool Asynchronously = true;
    public enum CapMode {FORCE_ADD, FORCE_NOT_ADD, ONLY_ADD_LOOP}
    public CapMode nowCapMode = CapMode.ONLY_ADD_LOOP;
    public Material capMaterial;
    /// <summary>
    /// The new collider mesh will being abandoned if it have one or more bounds edge length is under this value 
    /// </summary>
    public float minBoundsLength = 0.05f;
    /// <summary>
    /// The parent for all new separated bones
    /// </summary>
    Transform cutOffParts;
    /// <summary>
    /// The plane for slice
    /// </summary>
    Plane _slicePlane;
    SkinnedMeshRenderer[] sMRenders;
    public SliceTarget[] sliceTargets{get; private set;}
    bool[] isInCut;
    Action SliceFinishAction;
    bool canSliceFinish;
    public class SliceTarget
    {
        SkinnedMeshRenderer _sMRender;
        SkeletonMeshSlicer _sMSlicer;
        /// <summary>
        /// Use for store one skinned mesh's data
        /// </summary>
        public SmMeshDatas _sMDataBase;
        public List<Transform> sectionsBones, posNoneSectionBones, negNoneSectionBones, posInvisibleBones, negInvisibleBones, allRootBones = new List<Transform>();
        /// <summary>
        /// Map old bones to new bones indexes
        /// </summary>
        public Dictionary<Transform, int> mapedBones;
        public List<SliceThread> sliceThreads = new List<SliceThread>();
        int targetIndex, boneSliceIndex;
        /// <summary>
        /// True if we use old bone to weight vertices on the positive side of slice plane
        /// </summary>
        bool posGoesOld;
        public class SliceThread
        {
            public SliceTarget sliceTarget;
            public bool sliceFinish = false;
            public void SliceMesh(object obj)
            {
                sliceTarget = (SliceTarget)obj;
                sliceTarget.DistinguishBothSideIndexes(sliceTarget._sMDataBase);
                // Process the less number side vertices
                sliceTarget.posGoesOld = sliceTarget._sMDataBase.posPoints.Count > sliceTarget._sMDataBase.negPoints.Count;
                var points =  sliceTarget.posGoesOld ? sliceTarget._sMDataBase.negPoints : sliceTarget._sMDataBase.posPoints;
                sliceTarget.SeparateVerticesWeights(points);
                sliceTarget.GetAllInvisableBones(sliceTarget._sMDataBase.posPoints, sliceTarget.posInvisibleBones, true);
                sliceTarget.GetAllInvisableBones(sliceTarget._sMDataBase.negPoints, sliceTarget.negInvisibleBones, false);
                sliceFinish = true;               
            }
            public virtual void OnSliceFinish()
            {
                // Create new skinned mesh
                sliceTarget._sMRender.bones = sliceTarget._sMDataBase.Bones.ToArray();
                sliceTarget._sMRender.sharedMaterials = sliceTarget._sMDataBase.Materials.ToArray();
                sliceTarget._sMRender.sharedMesh = sliceTarget._sMDataBase.GenerateMesh();              
            }
        }
        public class SliceColliderHelper : SliceThread
        {
            public List<SmMeshDatas> colMeshDatas = new List<SmMeshDatas>();
            public Collider[] oldCols;
            public Transform sectionBone;
            bool[] allColSliceFinish;
            public void SliceMesh(SliceTarget nowSliceTarget)
            {
                sliceTarget = nowSliceTarget;
                allColSliceFinish = new bool[colMeshDatas.Count];
                if (sliceTarget._sMSlicer.Asynchronously)
                {
                    for (int i = 0; i < colMeshDatas.Count; i++)                    
                        ThreadPool.QueueUserWorkItem(new WaitCallback(SliceOneColMesh), i);                    
                }
                else
                {
                    for (int i = 0; i < colMeshDatas.Count; i++)
                        SliceOneColMesh(i);
                }       
            }
            public void SliceOneColMesh(object obj)
            {
                var i = (int)obj;
                var colMeshData = colMeshDatas[i];
                sliceTarget.DistinguishBothSideIndexes(colMeshData);
                allColSliceFinish[i] = true;
                if (allColSliceFinish.All(isFinished => isFinished))
                    sliceFinish = true;
            }
            public override void OnSliceFinish()
            {
                var otherSideSectionBone = sliceTarget._sMDataBase.Bones[sliceTarget.mapedBones[sectionBone]];
                var otherOldCols = otherSideSectionBone.GetComponents<Collider>();
                // Create the new colliders for each side of section bone, the new colliders mesh is create by slice the old colliders mesh
                for (int i = 0; i < colMeshDatas.Count; i++)
                {
                    var colMeshData = colMeshDatas[i];
                    Mesh posMesh = colMeshData.GeneratePartialMesh(colMeshData.posPoints, colMeshData.posTriangles);
                    Mesh negMesh = colMeshData.GeneratePartialMesh(colMeshData.negPoints, colMeshData.negTriangles);

                    Mesh oldBoneMesh;
                    Mesh newBoneMesh;
                    if (sliceTarget.posGoesOld)
                    {
                        oldBoneMesh = posMesh;
                        newBoneMesh = negMesh;
                    }
                    else
                    {
                        oldBoneMesh = negMesh;
                        newBoneMesh = posMesh;
                    }

                    var minBoundsLength = sliceTarget._sMSlicer.minBoundsLength;
                    var boundsLength = oldBoneMesh.bounds.max - oldBoneMesh.bounds.min;
                    if (boundsLength.x > minBoundsLength && boundsLength.y > minBoundsLength && boundsLength.z > minBoundsLength)
                        ChangeMeshOnBone(sectionBone, oldBoneMesh, oldCols[i]);
                    boundsLength = newBoneMesh.bounds.max - newBoneMesh.bounds.min; 
                    if (boundsLength.x > minBoundsLength && boundsLength.y > minBoundsLength && boundsLength.z > minBoundsLength)    
                        ChangeMeshOnBone(otherSideSectionBone, newBoneMesh, otherOldCols[i]);
                }
            } 
            void ChangeMeshOnBone(Transform bone, Mesh newMeshm, Collider oldCol)
            {
                var phyMaterial = oldCol.sharedMaterial;
                Destroy(oldCol);
                var meshCol = bone.gameObject.AddComponent<MeshCollider>();
                meshCol.sharedMesh = newMeshm;
                meshCol.sharedMaterial = phyMaterial;
                // MeshCollider.convex set active for simulate physics rigidbody
                meshCol.convex = true;                
            }
        } 
        public SliceTarget(SkeletonMeshSlicer sMSlicer, SkinnedMeshRenderer sMRender, int i)
        {
            _sMSlicer = sMSlicer;
            _sMRender = sMRender;
            _sMDataBase = new SmMeshDatas(_sMRender.sharedMesh, _sMRender.rootBone.parent, _sMRender.sharedMaterials, _sMRender.bones); 
            targetIndex = i;
            allRootBones.Add(_sMRender.rootBone);
        }
        public void SliceTargetByPlane()
        {
            sectionsBones = new List<Transform>();
            posNoneSectionBones = new List<Transform>();
            negNoneSectionBones = new List<Transform>();
            mapedBones = new Dictionary<Transform, int>();
            posInvisibleBones = new List<Transform>();
            negInvisibleBones = new List<Transform>();
            GetSectionsBones();
            // Check if any bone is get cut, then separate those bones and weight the mesh to follow the correct bone
            if (sectionsBones.Count > 0)
            {
                _sMSlicer.isInCut[targetIndex] = true;
                SeparateBones();

                var sliceThread = new SliceThread();
                _sMDataBase.SetLtoWMatrix();

                if (_sMSlicer.Asynchronously)
                    ThreadPool.QueueUserWorkItem(new WaitCallback(sliceThread.SliceMesh), this);
                else
                    sliceThread.SliceMesh(this);
                sliceThreads.Add(sliceThread);

                SliceBonesColliders();
            }
        }
        void GetSectionsBones()
        {
            // Get the bones that get cut by plane
            foreach (Transform bone in _sMDataBase.Bones)
            {
                if (bone != null)
                {
                    PlaneCrossCheck(bone);
                }
            }                
        }
		void PlaneCrossCheck(Transform bone)
		{
			//    a1              b1
			//     /^^^^^^^^^^^^^/|
			//    /  |          / |
			// c1/           d1/  |
			//   --------------   |
			//   |   |        |   |
			//   |            |   |
			//   |   |        |   |
			//   |            |   |
			//   | a2 -  -  - | - /b2
			//   |  /         |  /
			//   |            | /
			// c2|/___________|/d2

            bool canCut = false;
            Collider col;
            if (bone.TryGetComponent<Collider>(out col))
            {
                var b1 = col.bounds.max;
                var c2 = col.bounds.min;
                var a2 = new Vector3(c2.x, c2.y, b1.z);
                var b2 = new Vector3(b1.x, c2.y, b1.z);
                var d2 = new Vector3(b1.x, c2.y, c2.z);
                var a1 = new Vector3(c2.x, b1.y, b1.z);
                var c1 = new Vector3(c2.x, b1.y, c2.z);
                var d1 = new Vector3(b1.x, b1.y, c2.z);

                var vertices = new Vector3[7]{c2, a2, b2, d2, a1, c1, d1};
                bool bonePosSide = _sMSlicer._slicePlane.GetSide(b1);
                foreach (var v in vertices)
                {
                    if (bonePosSide != _sMSlicer._slicePlane.GetSide(v))
                    {
                        canCut = true;
                        break;
                    }
                }

                // Add section bone
                if (canCut)
                    AddBoneToList(sectionsBones, bone);
                else
                    AddBoneToList(bonePosSide, posNoneSectionBones, negNoneSectionBones, bone);
            }
			else
                PlaneCrossBoneCheck(bone);
		}
        void PlaneCrossBoneCheck(Transform bone)
        {
            bool canCut = false;
            bool bonePosSide = _sMSlicer._slicePlane.GetSide(bone.position);
            var parentBone = bone.parent;
            if (parentBone != null && _sMDataBase.Bones.Contains(parentBone))
            {
                bool parentBonePosSide =  _sMSlicer._slicePlane.GetSide(parentBone.position);
                canCut = bonePosSide != parentBonePosSide;

                // Add section bone
                if (canCut)
                    AddBoneToList(sectionsBones, parentBone);
                else
                    AddBoneToList(parentBonePosSide, posNoneSectionBones, negNoneSectionBones, parentBone);
            }            
        }
        void AddBoneToList(List<Transform> boneList, Transform bone)
        {
            if (!boneList.Contains(bone))
                boneList.Add(bone);            
        }
        void AddBoneToList(bool isPos, List<Transform> posBoneList, List<Transform> negBoneList, Transform bone)
        {
            if (isPos)
            {
                if (!posBoneList.Contains(bone))
                    posBoneList.Add(bone);
            }
            else if (!negBoneList.Contains(bone))
                negBoneList.Add(bone);            
        }
        void SeparateBones()
        {
            boneSliceIndex = _sMDataBase.Bones.Count;
            // Separate the bones by duplicate all bones first
            for (int i = allRootBones.Count - 1; i >= 0 ; i--)
            {
                var oldRootBone = allRootBones[i];
                Transform[] oldChilds = oldRootBone.GetComponentsInChildren<Transform>();
                Transform newRootBone = Instantiate(oldRootBone, oldRootBone.parent, true);
                Transform[] newChilds = newRootBone.GetComponentsInChildren<Transform>();
                _sMDataBase.AddNewBones(oldChilds, newChilds, mapedBones);
                allRootBones.Add(newRootBone);
            }
        }
        /// <summary>
        /// Slice the colliders of the bone that get cuted
        /// </summary>
        void SliceBonesColliders()
        {
            foreach (var bone in sectionsBones)
            {
                Collider[] cols = bone.GetComponents<Collider>();
                if (cols.Length > 0)
                {   
                    var sliceColHelper = new SliceColliderHelper();
                    sliceColHelper.oldCols = cols;
                    sliceColHelper.sectionBone = bone;
                    
                    foreach (var col in cols)
                    {
                        var colliderB = col as BoxCollider;
                        var colliderS = col as SphereCollider;
                        var colliderC = col as CapsuleCollider;
                        var colliderM = col as MeshCollider; 
                        Mesh colMesh;
                        if (colliderB != null)
                            colMesh = Cube.Create(colliderB.size, colliderB.center);
                        else if (colliderS != null)
                            colMesh = IcoSphere.Create(colliderS.radius, colliderS.center);
                        else if (colliderC != null)
                            colMesh = Capsule.Create(colliderC.radius, colliderC.height, colliderC.direction, colliderC.center);
                        else if (colliderM != null)
                            colMesh = Instantiate(colliderM.sharedMesh);
                        else
                            continue;

                        var colMeshData = new SmMeshDatas(colMesh, bone);
                        colMeshData.SetLtoWMatrix();
                        sliceColHelper.colMeshDatas.Add(colMeshData);    
                    }
                    sliceThreads.Add(sliceColHelper);
                    sliceColHelper.SliceMesh(this);
                } 
            }
        }
        void DistinguishBothSideIndexes(SmMeshDatas nowDataBase)
        {
            // Two side vertices indexes
            nowDataBase.posPoints = new List<int>();
            nowDataBase.negPoints = new List<int>();
            // Two side triangles data
            nowDataBase.posTriangles = new List<int>();
            nowDataBase.negTriangles = new List<int>();
            List<bool> verticesSides = new List<bool>();
            for (int verticesIndex = 0; verticesIndex < nowDataBase.Vertices.Count; verticesIndex++)
            {
                bool side = _sMSlicer._slicePlane.GetSide(nowDataBase.VerticesWorldPos[verticesIndex]);
                verticesSides.Add(side);
                if (side)
                    nowDataBase.posPoints.Add(verticesIndex);
                else
                    nowDataBase.negPoints.Add(verticesIndex);
            }

            if (nowDataBase.posPoints.Count > 0 && nowDataBase.negPoints.Count > 0)
            {
                nowDataBase.slicedCrossEdges = new List<IndexVector>();
                nowDataBase.slicedSectionEdgeIndexes = new List<IndexVector>();
                nowDataBase.sectionPosTriangles = new List<List<SectionTriangle>>();
                nowDataBase.sectionNegTriangles = new List<List<SectionTriangle>>();
                for (int subMeshIndex = 0; subMeshIndex < nowDataBase.SubMeshes.Count; ++subMeshIndex)
                {
                    List<int> subTriangles = nowDataBase.SubMeshes[subMeshIndex];
                    nowDataBase.sectionPosTriangles.Add(new List<SectionTriangle>());
                    nowDataBase.sectionNegTriangles.Add(new List<SectionTriangle>());
                    for (int trangleIndex = subTriangles.Count - 3; trangleIndex >= 0; trangleIndex -= 3)
                    {
                        var sTriangle = new SliceableTriangle(
                            subTriangles[trangleIndex],
                            subTriangles[trangleIndex + 1],
                            subTriangles[trangleIndex + 2]);  

                        bool side1 = verticesSides[sTriangle.i1];
                        bool side2 = verticesSides[sTriangle.i2];
                        bool side3 = verticesSides[sTriangle.i3];

                        bool allPosSide = side1 && side2 && side3;
					    bool allNegSide = !side1 && !side2 && !side3;
                        if (allPosSide)
                        {
                            nowDataBase.posTriangles.Add(sTriangle.i1);
                            nowDataBase.posTriangles.Add(sTriangle.i2);
                            nowDataBase.posTriangles.Add(sTriangle.i3);
                        }
                        else if (allNegSide)
                        {
                            nowDataBase.negTriangles.Add(sTriangle.i1);
                            nowDataBase.negTriangles.Add(sTriangle.i2);
                            nowDataBase.negTriangles.Add(sTriangle.i3);                            
                        }
                        else
                        {
                            sTriangle.DivideByPlane(nowDataBase, subMeshIndex, side1, side2, side3);
                            subTriangles.RemoveRange(trangleIndex, 3);
                        }                    
                    }
                }
                // Get section vertices data, and assign it the corresponding list
                List<int> posSectionPoints = new List<int>();
                List<int> negSectionPoints = new List<int>();
                foreach (var iV in nowDataBase.slicedCrossEdges)
                    posSectionPoints.Add(AddNewSectionVertices(nowDataBase, iV.From, iV.To));
                negSectionPoints = nowDataBase.DuplicateVerticesData(posSectionPoints);
                
                AddSectionTrianglesInMesh(posSectionPoints, nowDataBase.sectionPosTriangles, nowDataBase.posTriangles, nowDataBase);
                AddSectionTrianglesInMesh(negSectionPoints, nowDataBase.sectionNegTriangles, nowDataBase.negTriangles, nowDataBase);

                List<IndexVector> sectionEdges = new List<IndexVector>();
                foreach (var iV in nowDataBase.slicedSectionEdgeIndexes)
                    sectionEdges.Add(new IndexVector(posSectionPoints[iV.From], posSectionPoints[iV.To]));

                nowDataBase.posPoints.AddRange(posSectionPoints);
                nowDataBase.negPoints.AddRange(negSectionPoints);

                if (_sMSlicer.nowCapMode != CapMode.FORCE_NOT_ADD)
                {
                    // Find caps edges, create caps vertices, then build caps triangles & UVs & Normals to create caps faces, and assign it the corresponding list
                    List<List<int>> posOrderedCapEdges = new List<List<int>>(), posCapsTriangles = new List<List<int>>(), 
                    negOrderedCapEdges = new List<List<int>>(), negCapsTriangles = new List<List<int>>();
                    DivideDifferentEdge(sectionEdges, posOrderedCapEdges, nowDataBase);
                    posOrderedCapEdges = nowDataBase.DuplicateVerticesData(posOrderedCapEdges).Item1;
                    List<List<Vector2>> capsUVList = CreateAllCaps(posOrderedCapEdges, posCapsTriangles, negCapsTriangles, nowDataBase);

                    var tupleData = nowDataBase.DuplicateVerticesData(posOrderedCapEdges);
                    negOrderedCapEdges = tupleData.Item1;
                    nowDataBase.DuplicateTriangles(negCapsTriangles, tupleData.Item2);
                    nowDataBase.AddNewSubmeshs(posCapsTriangles, _sMSlicer.capMaterial);
                    nowDataBase.AddNewSubmeshs(negCapsTriangles, _sMSlicer.capMaterial);

                    List<Vector3> posCapNormal = new List<Vector3>(), negCapNormal = new List<Vector3>();
                    CalculateCapNormalsAndSwitchCapSide(posCapsTriangles, negCapsTriangles, posCapNormal, negCapNormal, posOrderedCapEdges, negOrderedCapEdges, nowDataBase);

                    nowDataBase.SetVerticesNormalsAndUVs(posOrderedCapEdges, posCapNormal, capsUVList);
                    nowDataBase.SetVerticesNormalsAndUVs(negOrderedCapEdges, negCapNormal, capsUVList);

                    foreach (var capEdge in posOrderedCapEdges)
                        nowDataBase.posPoints.AddRange(capEdge);
                    foreach (var capEdge in negOrderedCapEdges)
                        nowDataBase.negPoints.AddRange(capEdge);

                    foreach (var capTriangles in posCapsTriangles)
                        nowDataBase.posTriangles.AddRange(capTriangles);
                    foreach (var capTriangles in negCapsTriangles)
                        nowDataBase.negTriangles.AddRange(capTriangles);
                }
            }            
        }
        void AddSectionTrianglesInMesh(List<int> nowSectionPoints, List<List<SectionTriangle>> nowDiffSectionTriangles, List<int> nowSideTriangles, SmMeshDatas nowDataBase)
        {
            for (int subMeshIndex = 0; subMeshIndex < nowDiffSectionTriangles.Count; ++subMeshIndex)
            {
                foreach (var sectionTriangle in nowDiffSectionTriangles[subMeshIndex])
                {
                    int[] indexes = new int[3];
                    foreach (int i in sectionTriangle.dynamicIndexes)
                    {
                        indexes[i] = nowSectionPoints[sectionTriangle.indexes[i]];
                    }
                    foreach (int i in sectionTriangle.fixedIndexes)
                    {
                        indexes[i] = sectionTriangle.indexes[i];
                    }
                    nowDataBase.SubMeshes[subMeshIndex].AddRange(indexes);
                    nowSideTriangles.AddRange(indexes);
                }
            }
        }
        public int AddNewSectionVertices(SmMeshDatas nowDataBase, int from, int to)
        {
            int newIndex = nowDataBase.Vertices.Count;

			var vOldOrigin = nowDataBase.VerticesWorldPos[from];
			var vOldDir = nowDataBase.VerticesWorldPos[to] - vOldOrigin;

			float enter;
			_sMSlicer._slicePlane.Raycast(new Ray(vOldOrigin, vOldDir), out enter);

			float ratioIn = enter / vOldDir.magnitude;
            nowDataBase.AddNewPoint(from, to, ratioIn);

            return newIndex;
        }
        List<List<Vector2>> CreateAllCaps(List<List<int>> posOrderedCapEdges, List<List<int>> posCapsTriangles, List<List<int>> negCapsTriangles, SmMeshDatas nowDataBase)
        {
            List<List<Vector2>> capsUVList = new List<List<Vector2>>(), capsVertices2D = new List<List<Vector2>>();
            foreach (var edge in posOrderedCapEdges)
            {
                var capVertices2D = ConvertModalV3ToPlaneV2(edge, nowDataBase);
                var newTriangles1 = FillCapByTriangles(new List<int>(edge), new List<Vector2>(capVertices2D), true);
                var newTriangles2 = FillCapByTriangles(new List<int>(edge), new List<Vector2>(capVertices2D), false);
                // Since we are not sure about loop direction we record is clockWise or not, so we do both, choose the cap that fully filled with triangles
                var capTriangles = newTriangles1.Count > newTriangles2.Count ? newTriangles1 : newTriangles2;
                posCapsTriangles.Add(capTriangles);
                capsVertices2D.Add(capVertices2D);
            }
            // Create other side of cap mesh's triangles, the winding order of triangle need be reversed
            foreach (var capTriangles in posCapsTriangles)
            {
                var newCapTriangles = new List<int>();
                for (int i = 0; i < capTriangles.Count; i += 3)
                {
                    newCapTriangles.Add(capTriangles[i]);
                    newCapTriangles.Add(capTriangles[i + 2]);
                    newCapTriangles.Add(capTriangles[i + 1]);
                }
                negCapsTriangles.Add(newCapTriangles);
            }
            // Calculate the UV for all caps vertices by use the vertices 2D position get from above, shift cap's edge on the UV axis, then scale it to 1;
            foreach (var capVertice2D in capsVertices2D)
            {
                List<Vector2> capUV = new List<Vector2>();  
                float wMax, wMin, hMax, hMin;
                wMax = hMax = Mathf.Infinity;
                wMin = hMin = Mathf.NegativeInfinity;
                foreach (var v in capVertice2D)
                {
                    if (v.x > wMax) wMax = v.x;
                    if (v.x < wMin) wMin = v.x;
                    if (v.y > hMax) hMax = v.y;
                    if (v.y < hMin) hMin = v.y;
                }
                float sizeX = wMax - wMin;
                float sizeY = hMax - hMin;
                float scale = Mathf.Max(sizeX, sizeY);

                Vector2 vShift = new Vector2(wMin + (sizeX - scale) / 2, hMin + (sizeY - scale) / 2);

                foreach (var v in capVertice2D)
                    capUV.Add((v - vShift) / scale);

                capsUVList.Add(capUV);
            }             
            return capsUVList;
        }
		/// <summary>
		/// Transfom vertices from modal space to plane space and convert them to List<Vector2>
		/// </summary>
		List<Vector2> ConvertModalV3ToPlaneV2(List<int> pointsIndexes, SmMeshDatas nowDataBase)
		{
            var v2s = new List<Vector2>();
            var rotation = Quaternion.FromToRotation(_sMSlicer._slicePlane.normal, Vector3.forward);

            foreach (int i in pointsIndexes)
                v2s.Add(rotation * nowDataBase.VerticesWorldPos[i]);
			return v2s;
		}
        /// <summary>
        /// From one chosen vertice on the cap edge try to create triangles circly along the edge, create while delete those vertice in new triangles,
        /// if can't create triangle on some vertice, then switch that chosen vertice to this new vertice, 
        /// then continue loop around until the cap is filled with triangles
        /// </summary>
        /// <param name="capEdge">Edge of cap</param>
        /// <param name="capVertices2D">Vertices position in 2D space</param>
        /// <param name="clockWise">The direction for create triangles</param>
        /// <returns>Triangles been create</returns>
        List<int> FillCapByTriangles(List<int> capEdge, List<Vector2> capVertices2D, bool clockWise)
        {
            List<int> newTriangles = new List<int>(); 
            int nowIndex = 0;
            int loopCounter = 0;
            while (capEdge.Count > 2 && loopCounter < capEdge.Count)
            {
                loopCounter++;
                int counterSub1 = nowIndex - 2 < 0 ? capEdge.Count + nowIndex - 2 : nowIndex - 2;
                int counterIndex = nowIndex - 1 < 0 ? capEdge.Count - 1 : nowIndex - 1;
                while (counterSub1 != nowIndex && capEdge.Count > 2)
                { 
                    Vector2 v1 = capVertices2D[counterSub1];
                    Vector2 v2 = capVertices2D[counterIndex];

                    Vector3 vA = v1 - capVertices2D[nowIndex];
                    Vector3 vB = v2 - capVertices2D[nowIndex];
                    Vector3 vC = Vector3.Cross(vA, vB);

                    if (clockWise ? vC.z >= 0f : vC.z <= 0f)
                    {
                        newTriangles.Add(capEdge[nowIndex]);
                        newTriangles.Add(capEdge[counterSub1]);
                        newTriangles.Add(capEdge[counterIndex]);                            
                        capEdge.RemoveAt(counterIndex);
                        capVertices2D.RemoveAt(counterIndex);
                        if (counterIndex < nowIndex)
                            nowIndex--;
                        if (counterSub1 == capEdge.Count)
                            counterSub1--;
                        loopCounter = 0;
                    }
                    else
                    {
                        nowIndex = counterIndex;
                        break;
                    }
                    counterIndex = counterSub1;
                    counterSub1--;
                    if (counterSub1 < 0)
                        counterSub1 = capEdge.Count - 1;                       
                }
            }
            return newTriangles;
        }
        void DivideDifferentEdge(List<IndexVector> sectionEdges, List<List<int>> orderedCapEdges, SmMeshDatas nowDataBase)
        {
            // Search for find all loop edges
            while (sectionEdges.Count > 0)
            {
                List<int> nowOrderedCapEdge = new List<int>();
                
                IndexVector nowEdge = sectionEdges[0];
                nowOrderedCapEdge.Add(nowEdge.From);
                nowOrderedCapEdge.Add(nowEdge.To);
                sectionEdges.RemoveAt(0);
                // Search for find single loop edge by find two point edge that linked with current two point edge, skip the two point edge with same direction
                int i = 0;
                while (i < sectionEdges.Count)
                {
                    if (Mathf.Approximately((nowDataBase.Vertices[nowEdge.From] - nowDataBase.Vertices[sectionEdges[i].From]).magnitude, 0f))
                    {
                        nowEdge.From = sectionEdges[i].To;
                        nowOrderedCapEdge.Insert(0, nowEdge.From);
                    }
                    else if (Mathf.Approximately((nowDataBase.Vertices[nowEdge.To] - nowDataBase.Vertices[sectionEdges[i].To]).magnitude, 0f))
                    {
                        nowEdge.To = sectionEdges[i].From;
                        nowOrderedCapEdge.Add(nowEdge.To);
                    }
                    else if (Mathf.Approximately((nowDataBase.Vertices[nowEdge.From] - nowDataBase.Vertices[sectionEdges[i].To]).magnitude, 0f))
                    {
                        nowEdge.From = sectionEdges[i].From;
                        nowOrderedCapEdge.Insert(0, nowEdge.From);
                    }
                    else if (Mathf.Approximately((nowDataBase.Vertices[nowEdge.To] - nowDataBase.Vertices[sectionEdges[i].From]).magnitude, 0f))
                    {
                        nowEdge.To = sectionEdges[i].To; 
                        nowOrderedCapEdge.Add(nowEdge.To);
                    }                   
                    else
                    {
                        i++;
                        continue;
                    }
                    sectionEdges.RemoveAt(i);
                    i = 0;
                }
                if (nowOrderedCapEdge.Count > 2 && (Mathf.Approximately((nowDataBase.Vertices[nowEdge.To] - nowDataBase.Vertices[nowEdge.From]).magnitude, 0f) || _sMSlicer.nowCapMode == CapMode.FORCE_ADD))
                    orderedCapEdges.Add(nowOrderedCapEdge);
            }
        }
        void CalculateCapNormalsAndSwitchCapSide(List<List<int>> posCapsTriangles, List<List<int>> negCapsTriangles, List<Vector3> posCapNormals, List<Vector3> negCapNormals, List<List<int>> posOrderedCapEdges, List<List<int>> negOrderedCapEdges, SmMeshDatas nowDataBase)
        {
            for (int iC = 0; iC < posCapsTriangles.Count; iC++)
            {
                var triangles = posCapsTriangles[iC];
                Vector3 posCapNormal = Vector3.zero;
                if (triangles.Count > 2)
                {     
                    // Winding order of triangle is clockwise order correspond to triangle's front face
                    var v1 = nowDataBase.Vertices[triangles[0]];
                    var v2 = nowDataBase.Vertices[triangles[1]];
                    var v3 = nowDataBase.Vertices[triangles[2]];
                    var dir1 = v2 - v1;
                    var dir2 = v3 - v1;
                    posCapNormal = Vector3.Cross(dir1, dir2).normalized;

                    v1 = nowDataBase.VerticesWorldPos[triangles[0]];
                    v2 = nowDataBase.VerticesWorldPos[triangles[1]];
                    v3 = nowDataBase.VerticesWorldPos[triangles[2]];
                    dir1 = v2 - v1;
                    dir2 = v3 - v1;
                    // The neg side cap need face the same direction as slice plane's normal, when direction don't fit then switch the cap side
                    if (Vector3.Dot(Vector3.Cross(dir1, dir2).normalized, _sMSlicer._slicePlane.normal) > 0f)
                    {
                        posCapNormal = -posCapNormal;

                        var tmp = new List<int>(posOrderedCapEdges[iC]);
                        posOrderedCapEdges[iC] = negOrderedCapEdges[iC];
                        negOrderedCapEdges[iC] = tmp;
                        tmp = new List<int>(posCapsTriangles[iC]);
                        posCapsTriangles[iC] = negCapsTriangles[iC];
                        negCapsTriangles[iC] = tmp;                    
                    }
                }
                posCapNormals.Add(posCapNormal);
                negCapNormals.Add(-posCapNormal);
            }
        }
        /// <summary>
        /// Map the different side vertices bone weight to new bone that been created early
        /// </summary>
        void SeparateVerticesWeights(List<int> sideVertices)
        {
            foreach (int verticeIndex in sideVertices)
            {
                BoneWeight boneWeight = _sMDataBase.BoneWeights[verticeIndex];

                boneWeight.boneIndex0 = mapedBones[_sMDataBase.Bones[boneWeight.boneIndex0]];
                if (boneWeight.weight1 > 0f)
                    boneWeight.boneIndex1 = mapedBones[_sMDataBase.Bones[boneWeight.boneIndex1]];
                if (boneWeight.weight2 > 0f)
                    boneWeight.boneIndex2 = mapedBones[_sMDataBase.Bones[boneWeight.boneIndex2]];
                if (boneWeight.weight3 > 0f)
                    boneWeight.boneIndex3 = mapedBones[_sMDataBase.Bones[boneWeight.boneIndex3]];

                _sMDataBase.BoneWeights[verticeIndex] = boneWeight;
            }
        }
        /// <summary>
        /// Find bones have no weight influences for provided vertices
        /// </summary>
        /// <param name="sideVertices"> Vertices on one side of slice plane</param>
        /// <param name="isPos"> If the provided vertices is on the postive side of the slice plane </param>
        void GetAllInvisableBones(List<int> sideVertices, List<Transform> invisibleBones, bool isPos)
        {
            var visibleBoneIndexes = new HashSet<int>();
            foreach (int verticeIndex in sideVertices)
            {
                BoneWeight boneWeight = _sMDataBase.BoneWeights[verticeIndex];

                visibleBoneIndexes.Add(boneWeight.boneIndex0);
                if (boneWeight.weight1 > 0f)
                    visibleBoneIndexes.Add(boneWeight.boneIndex1);
                if (boneWeight.weight2 > 0f)
                    visibleBoneIndexes.Add(boneWeight.boneIndex2);
                if (boneWeight.weight3 > 0f)
                    visibleBoneIndexes.Add(boneWeight.boneIndex3);
            }
            
            var sideBonesIndexRange = isPos == posGoesOld ? new int[2]{0, boneSliceIndex} : new int[2]{boneSliceIndex, _sMDataBase.Bones.Count};
            for (int i = sideBonesIndexRange[0]; i < sideBonesIndexRange[1]; i++)
            {
                if (_sMDataBase.Bones[i] != null && !visibleBoneIndexes.Contains(i))
                    invisibleBones.Add(_sMDataBase.Bones[i]);
            }
        }
        public void AfterProcess()
        {
            // Finish all threads and clean it for next run
            while (sliceThreads.Count > 0)
            {
                sliceThreads[0].OnSliceFinish();
                sliceThreads.RemoveAt(0);                
            }      

            // Find new root bones and its childs, separate them, and delete useless invisible bones and its childs
            var invisibleBones = new List<Transform>(posInvisibleBones);
            invisibleBones.AddRange(negInvisibleBones);
            foreach (var bone in invisibleBones)
                bone.gameObject.SetActive(false);
            for (int i = allRootBones.Count - 1; i >= 0 ; i--)
                FindNewRootInChilds(allRootBones[i]);
            for (int i = 1; i < allRootBones.Count; i++)
                allRootBones[i].SetParent(_sMSlicer.cutOffParts);
            foreach (var bone in invisibleBones)
            {
                int i = _sMDataBase.Bones.IndexOf(bone);
                _sMDataBase.Bones[i] = null;
                
                i = allRootBones.IndexOf(bone);
                if (i >= 0)
                    allRootBones.RemoveAt(i);

                Destroy(bone.gameObject);
            }
            // Delete excess but useless physical components for both old bones and new bones 
            List<Transform> oldHideBones;
            List<Transform> newHideBones;
            if (posGoesOld)
            {
                oldHideBones = negNoneSectionBones;
                newHideBones = posNoneSectionBones;
            }
            else
            {
                oldHideBones = posNoneSectionBones;
                newHideBones = negNoneSectionBones;
            }
            for (int i = 0; i < newHideBones.Count; i++)
                newHideBones[i] = _sMDataBase.Bones[mapedBones[newHideBones[i]]];
            DisableBonesPhysical(oldHideBones);
            DisableBonesPhysical(newHideBones);
            // Delete the character joint in all bones that no longer useful
            foreach (var bone in _sMDataBase.Bones)
            {
                CharacterJoint cj;
                if (bone != null && bone.TryGetComponent<CharacterJoint>(out cj))
                {
                    if (cj.connectedBody == null || allRootBones.Contains(bone))
                        Destroy(cj);
                }
            }
        }
        void DisableBonesPhysical(List<Transform> bones)
        {
            foreach (var bone in bones)
            {
                if (bone != null && !allRootBones.Contains(bone))
                {      
                    CharacterJoint cj;
                    if (bone.TryGetComponent<CharacterJoint>(out cj))
                        Destroy (cj);
                    Collider col;
                    if (bone.TryGetComponent<Collider>(out col))
                        Destroy (col);    
                    Rigidbody rg;
                    if (bone.TryGetComponent<Rigidbody>(out rg))
                        Destroy(rg);
                }
            }
        }
        void FindNewRootInChilds(Transform parentBone)
        {
            bool parentAcitve = parentBone.gameObject.activeSelf;
            foreach (Transform bone in parentBone)
            {
                if (!parentAcitve && bone.gameObject.activeSelf && _sMDataBase.Bones.Contains(bone))
                    allRootBones.Add(bone);
                FindNewRootInChilds(bone);
            }
        }      
    }
    public class SliceableTriangle
    {
		public readonly int i1;
		public readonly int i2;
		public readonly int i3;

		/// <summary>
		/// Create triangle from 3 indexes of a mesh
		/// </summary>
		public SliceableTriangle(int i1, int i2, int i3)
		{
			this.i1 = i1;
			this.i2 = i2;
			this.i3 = i3;
		}
		/// <summary>
		/// Store the data about how plane cut this trianle
		/// </summary>
		public void DivideByPlane(SmMeshDatas nowDataBase, int subMeshIndex, bool _side1, bool _side2, bool _side3)
		{
			if (!_side1 && _side2 && _side3)
			{
				CalculateWithOneNegTriangle(i1, i2, i3, nowDataBase, subMeshIndex);
			}
			else if (_side1 && !_side2 && _side3)
			{
				CalculateWithOneNegTriangle(i2, i3, i1, nowDataBase, subMeshIndex);
			}
			else if (_side1 && _side2 && !_side3)
			{
				CalculateWithOneNegTriangle(i3, i1, i2, nowDataBase, subMeshIndex);
			}
			
			else if (_side1 && !_side2 && !_side3)
			{
				CalculateWithTwoNegTriangle(i1, i2, i3, nowDataBase, subMeshIndex);
			}
			else if (!_side1 && _side2 && !_side3)
			{
				CalculateWithTwoNegTriangle(i2, i3, i1, nowDataBase, subMeshIndex);
			}
			else if (!_side1 && !_side2 && _side3)
			{
				CalculateWithTwoNegTriangle(i3, i1, i2, nowDataBase, subMeshIndex);
			}
			else
				throw new InvalidOperationException();
		}

		/// <summary>
		/// Calculate when two point right. The result is one triangle
		/// </summary>
		static void CalculateWithOneNegTriangle(int neg, int posA, int posB, SmMeshDatas nowDataBase, int subMeshIndex)
		{
			//            pos
			//  posA\.^^^^^^^^^^^^/posB
			//       \   .       /
			//        \      .  /
			//      -----------.----
			//      iA  \     /  iB
			//           \   /
			//            \ /
			//            neg

            IndexVector iV = new IndexVector(neg, posA);
            int iA = iV.GetEqualsIndex(nowDataBase.slicedCrossEdges);
            if (iA < 0)
            {
                iA = nowDataBase.slicedCrossEdges.Count;
                nowDataBase.slicedCrossEdges.Add(iV);
            }

            iV = new IndexVector(neg, posB);
            int iB = iV.GetEqualsIndex(nowDataBase.slicedCrossEdges);
            if (iB < 0)
            {
                iB = nowDataBase.slicedCrossEdges.Count;
                nowDataBase.slicedCrossEdges.Add(iV);
            }

            nowDataBase.sectionNegTriangles[subMeshIndex].Add(new SectionTriangle(neg, iA, iB, new int[2]{1, 2}, new int[1]{0}));
            nowDataBase.sectionPosTriangles[subMeshIndex].Add(new SectionTriangle(posA, iB, iA, new int[2]{1, 2}, new int[1]{0}));
            nowDataBase.sectionPosTriangles[subMeshIndex].Add(new SectionTriangle(posA, posB, iB, new int[1]{2}, new int[2]{0, 1}));

            nowDataBase.slicedSectionEdgeIndexes.Add(new IndexVector(iA, iB));
		}

		/// <summary>
		/// Calculate when one point right. The result is two triangles
		/// </summary>
		static void CalculateWithTwoNegTriangle(int pos, int negA, int negB, SmMeshDatas nowDataBase, int subMeshIndex)
		{
			//            pos
			//            / \
			//           /   \
			//      iB  /     \  iA
			//      ---.-----------
			//        /   .     \
			//       /       .   \
			//  negB/____________.\negA
			//           neg

            IndexVector iV = new IndexVector(negA, pos);
            int iA = iV.GetEqualsIndex(nowDataBase.slicedCrossEdges);
            if (iA < 0)
            {
                iA = nowDataBase.slicedCrossEdges.Count;
                nowDataBase.slicedCrossEdges.Add(iV);
            }
                
            iV = new IndexVector(negB, pos);
            int iB = iV.GetEqualsIndex(nowDataBase.slicedCrossEdges);
            if (iB < 0)
            {
                iB = nowDataBase.slicedCrossEdges.Count;
                nowDataBase.slicedCrossEdges.Add(iV);
            }

            nowDataBase.sectionNegTriangles[subMeshIndex].Add(new SectionTriangle(negA, negB, iB, new int[1]{2}, new int[2]{0, 1}));
            nowDataBase.sectionNegTriangles[subMeshIndex].Add(new SectionTriangle(negA, iB, iA, new int[2]{1, 2}, new int[1]{0}));
            nowDataBase.sectionPosTriangles[subMeshIndex].Add(new SectionTriangle(pos, iA, iB, new int[2]{1, 2}, new int[1]{0}));

            nowDataBase.slicedSectionEdgeIndexes.Add(new IndexVector(iB, iA));
		}
        public SliceableTriangle FindEdgeSharedTriangle(List<SliceableTriangle> slicedTrangles)
        {
            SliceableTriangle adjacentTriangle = null;
            foreach (SliceableTriangle newTriangle in slicedTrangles)
            {
                int pointEqualNum = Convert.ToInt32(IsSharePoint(newTriangle.i1)) + 
                                    Convert.ToInt32(IsSharePoint(newTriangle.i2)) + 
                                    Convert.ToInt32(IsSharePoint(newTriangle.i3));
                if (pointEqualNum == 2)
                {
                    adjacentTriangle = newTriangle;
                    break;
                }
            }
            return adjacentTriangle;
        }
        bool IsSharePoint(int i)
        {
            return i1 == i || i2 == i || i3 == i;
        }         
    }
    void Start() 
    {
        cutOffParts = new GameObject ("_cutOffParts").transform;

        sMRenders = GetComponentsInChildren<SkinnedMeshRenderer>();
        sliceTargets = new SliceTarget[sMRenders.Length];
        isInCut = new bool[sMRenders.Length];
        for (int i = 0; i < sMRenders.Length; i++)
            sliceTargets[i] = new SliceTarget(this, sMRenders[i], i);
    }
    void Update()
    {
        for (int iT = 0; iT < sliceTargets.Length; iT++)
        {
            SliceTarget sliceTarget = sliceTargets[iT];
            if (isInCut[iT])
            {
                bool allThreadFinished = true;
                foreach (var sliceThread in sliceTarget.sliceThreads)
                {
                    if (!sliceThread.sliceFinish)
                    {
                        allThreadFinished=  false;
                        break;
                    }
                }
                if (allThreadFinished)
                    // Slice finished for single mesh corresponding to single SkinnedMeshRenderer
                    isInCut[iT] = false;
            }     
        } 
        if (canSliceFinish && !isInCut.Any(inCut => inCut))
        {
            // Slice finished for all meshs
            canSliceFinish = false;
            foreach (var sliceTarget in sliceTargets)
                sliceTarget.AfterProcess();
            if (SliceFinishAction != null)
                SliceFinishAction(); 
            Debug.Log("Slice finished!");            
        }    
    }
    /// <summary>
    /// Slice all character's mesh by plane
    /// </summary>
    /// <param name="planeNormal"> Plane normal in world space </param>
    /// <param name="planePosition"> Plane position in world space </param>
    /// <param name="whenSliceFinish"> Function will be called when slice is finish </param>
    public void SliceByMeshPlane(Vector3 planeNormal, Vector3 planePosition, Action whenSliceFinish = null)
    {
        if (!isInCut.Any(inCut => inCut))
        {
            Debug.Log("Begin to try slice!");
            _slicePlane = new Plane(planeNormal, planePosition);
            SliceFinishAction = whenSliceFinish;

            for (int iT = 0; iT < sliceTargets.Length; iT++)
                sliceTargets[iT].SliceTargetByPlane();
            canSliceFinish = isInCut.Any(inCut => inCut);
        }
    }
}
public class SectionTriangle
{
    public int[] indexes = new int[3];
    public int[] dynamicIndexes;
    public int[] fixedIndexes;
    public SectionTriangle(int i1, int i2, int i3, int[] nowDynamicIndexes, int[] nowFixedIndexes)
    {
        indexes[0] = i1;
        indexes[1] = i2;
        indexes[2] = i3;
        dynamicIndexes = nowDynamicIndexes;
        fixedIndexes = nowFixedIndexes;
    }
}
/// <summary>
/// Vector from 'From' to 'To'
/// </summary>
public struct IndexVector : IEquatable<IndexVector>
{
    public IndexVector(int from, int to)
    {
        From = from;
        To = to;
    }
    public int From;
    public int To;

    public bool Equals(IndexVector obj)
    {
        return (From == obj.From && To == obj.To) || (From == obj.To && To == obj.From);
    }
    public bool ExactEquals(IndexVector obj)
    {
        return From == obj.From & To == obj.To;
    }
    public bool PartalEquals(IndexVector obj)
    {
        return From == obj.To || To == obj.From || From == obj.From || To == obj.To;
    }
    public int GetEqualsIndex(List<IndexVector> indexVectors)
    {
        int index = -1;
        for (int i = 0; i < indexVectors.Count; i++)
        {
            if (Equals(indexVectors[i]))
            {
                index = i;
                break;
            }
        }                
        return index;
    }

    public override string ToString()
    {
        return "(" + From.ToString() + " -> " + To.ToString() + ")";
    }
}
