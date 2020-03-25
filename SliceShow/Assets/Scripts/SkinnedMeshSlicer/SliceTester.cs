using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SliceTester : MonoBehaviour
{
    public bool isDebug = false;
    public SkeletonMeshSlicer slicer;
    public string legTag = "Leg";
    public Transform rootBone;
    Animator animator;
    void Start()
    {
        animator = slicer.GetComponent<Animator>();
    }
    void Update() 
    {
        if (Input.GetButtonDown("Attack") && isDebug)
            slicer.SliceByMeshPlane(transform.up, transform.position);
    }
    /// <summary>
    /// It's important to set the normal vertical to the plane for slice, e.g, we set to transform.forward here
    /// </summary>
    /// <param name="isSliceWithMesh"> Only cut the bone that intersect with mesh of your knife, or cut bone with endless plane</param>
    public void TrySlice()
    {
        //var nowTime = Time.realtimeSinceStartup; 
        slicer.SliceByMeshPlane(transform.forward, transform.position, ActiveRagdollWhenNeeded);        
        //Debug.Log(Time.realtimeSinceStartup - nowTime);
    } 
    void ActiveRagdollWhenNeeded()
    {
        var brokenBones = new List<Transform>();
        var separatedBones = new List<Transform>();
        foreach (var sliceTarget in slicer.sliceTargets)
        {
            brokenBones.AddRange(sliceTarget.sectionsBones);
            separatedBones.AddRange(sliceTarget.allRootBones);
        }
        // Active ragdoll of original root bone when one bone with tag of leg is get cut
        foreach (var bone in brokenBones)
        {
            if (bone.gameObject.tag == legTag)
            {
                ActiveRagdollInHierarchy(rootBone);
                animator.enabled = false;
                break;
            }
        }
        // Active ragdoll for bone been create and separated after cut
        foreach (var bone in separatedBones)
        {
            if (bone != rootBone)
                ActiveRagdollInHierarchy(bone);
        }
    }
    void ActiveRagdollInHierarchy(Transform newRootBone)
    {
        var rgBodys = newRootBone.GetComponentsInChildren<Rigidbody>();
        foreach (var rg in rgBodys)
            rg.isKinematic = false;
        var colBodys = newRootBone.GetComponentsInChildren<Collider>();
        foreach (var col in colBodys)
            col.isTrigger = false;    
    }
}
