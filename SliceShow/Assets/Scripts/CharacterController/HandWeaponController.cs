using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandWeaponController : MonoBehaviour
{
    public float defenseMoveSpeed = 1f;
    public SliceTester knife;
    Animator weaponAnimator;
    Vector2 attackDirection, defenseDirection;
    bool attackOnce = true;
    // Start is called before the first frame update
    void Start()
    {
        weaponAnimator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        var nowMoveVector = new Vector2();
        nowMoveVector.x = Input.GetAxis("Mouse X");
        nowMoveVector.y = Input.GetAxis("Mouse Y");

        if (Input.GetButtonDown("Attack"))
        {
            attackDirection = Vector2.zero;
        }
        else if (Input.GetButton("Attack"))
        {
            attackDirection += nowMoveVector;
        }
        else if (Input.GetButtonUp("Attack"))
        {
            attackDirection = Mathf.Approximately(attackDirection.magnitude, 0f) ? new Vector2(0f, 1f) : attackDirection.normalized;
            weaponAnimator.SetFloat("Attack_RL", attackDirection.x);
            weaponAnimator.SetFloat("Attack_UD", attackDirection.y);
            weaponAnimator.SetBool("IsInAttack", true);
            attackOnce = true;
        }

        if (Input.GetButtonDown("Defense"))
        {
            weaponAnimator.SetBool("IsInDefense", true);
            weaponAnimator.SetFloat("Defense_RL", 0f);
            weaponAnimator.SetFloat("Defense_UD", 0f);
            defenseDirection = Vector2.zero;       
        }
        else if (Input.GetButton("Defense"))
        {
            defenseDirection += nowMoveVector * defenseMoveSpeed;
            if (defenseDirection.magnitude > 1f)
                defenseDirection = defenseDirection.normalized;
            weaponAnimator.SetFloat("Defense_RL", defenseDirection.x);
            weaponAnimator.SetFloat("Defense_UD", defenseDirection.y);
        }
        else if (Input.GetButtonUp("Defense"))
        {
            weaponAnimator.SetBool("IsInDefense", false);  
        }
    }
    public void AttackFinish()
    {
        if (attackOnce)
        {
            attackOnce = false;
            var newKnifeAngles = knife.transform.localEulerAngles;
            newKnifeAngles.x = Vector3.SignedAngle(attackDirection, Vector3.up, Vector3.forward);
            knife.transform.localEulerAngles = newKnifeAngles;
            weaponAnimator.SetBool("IsInAttack", false);
            knife.TrySlice();            
        }
    }
}
