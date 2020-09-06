using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Camera_Follow : MonoBehaviour
{
    [SerializeField] private string tag_to_follow = "";
    [SerializeField] private float deadband_x = 0f;
    [SerializeField] private float deadband_y = 0f;
    [SerializeField] private float pan_speed_x = 0f; // 0 means instant
    [SerializeField] private float pan_speed_y = 0f; // 0 means instant
    [SerializeField] private bool lock_on_start = true;

    private GameObject ref_to_follow = null;

    private void Start()
    {
        if (lock_on_start)
        {
            ref_to_follow = GameObject.FindGameObjectWithTag(tag_to_follow);

            float targ_x = ref_to_follow.transform.position.x;
            float targ_y = ref_to_follow.transform.position.y;

            transform.position = new Vector3(targ_x, targ_y, transform.position.z);
        }
    }

    private void FixedUpdate()
    {
        if (ref_to_follow == null)
        {
            ref_to_follow = GameObject.FindGameObjectWithTag(tag_to_follow);
        }

        float targ_x = ref_to_follow.transform.position.x;
        float targ_y = ref_to_follow.transform.position.y;

        float diff_x = transform.position.x - targ_x;
        float diff_y = transform.position.y - targ_y;

        if (Mathf.Abs(diff_x) > deadband_x)
        {
            if (pan_speed_x == 0)
            {
                transform.position = new Vector3(targ_x, transform.position.y, transform.position.z);
            }
            else
            {
                transform.position = new Vector3(Mathf.MoveTowards(transform.position.x, targ_x, pan_speed_x * Time.fixedDeltaTime), transform.position.y, transform.position.z);
            }
        }

        if (Mathf.Abs(diff_y) > deadband_y)
        {
            if (pan_speed_y == 0)
            {
                transform.position = new Vector3(transform.position.x, targ_y, transform.position.z);
            }
            else
            {
                transform.position = new Vector3(transform.position.x, Mathf.MoveTowards(transform.position.y, targ_y, pan_speed_y * Time.fixedDeltaTime), transform.position.z);
            }
        }
    }
}
