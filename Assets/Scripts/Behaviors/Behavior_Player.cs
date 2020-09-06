using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Behavior_Player : MonoBehaviour
{
    enum Action
    {
        Stay,
        Move_Right,
        Move_Left,
        Jump,
        Size
    }

    private struct Cluster_Data
    {
        public float hit_distance;
        public int cluster_index;
        
        public Cluster_Data(float ht, int ci)
        {
            hit_distance = ht;
            cluster_index = ci;
        }
    }

    private struct Cluster_Point
    {
        public List<Cluster_Data> cluster_data;
        public Action action;

        public Cluster_Point(List<Cluster_Data> cd, Action a)
        {
            cluster_data = cd;
            action = a;
        }
    }

    private class Cluster
    {
        public Dictionary<int, float> centroid = new Dictionary<int, float>();
        public float[] action_strengths;
        public int point_count = 0;

        public Cluster()
        {
            int action_size = (int)Action.Size;
            action_strengths = new float[action_size];
            for (int i = 0; i < action_size; ++i)
            {
                action_strengths[i] = 1f / action_size;
            }
        }

        public float GetDistance(List<Cluster_Data> cluster_data, float invalid_value)
        {
            HashSet<int> used_indexes = new HashSet<int>();

            float distance = 0f;
            foreach(Cluster_Data cd in cluster_data)
            {
                float centroid_value = invalid_value;
                if (centroid.ContainsKey(cd.cluster_index))
                {
                    centroid_value = centroid[cd.cluster_index];
                }

                distance += Mathf.Pow(centroid_value - cd.hit_distance, 2f);

                used_indexes.Add(cd.cluster_index);
            }

            foreach(int cluster_index in centroid.Keys)
            {
                if (!used_indexes.Contains(cluster_index))
                {
                    distance += Mathf.Pow(invalid_value - centroid[cluster_index], 2f);
                }
            }

            return Mathf.Sqrt(distance);
        }

        public void Add(Cluster_Point point, float new_point_weight)
        {
            foreach (Cluster_Data cd in point.cluster_data)
            {
                if (centroid.ContainsKey(cd.cluster_index))
                {
                    centroid[cd.cluster_index] = (1f - new_point_weight) * centroid[cd.cluster_index] + new_point_weight * cd.hit_distance;
                }
                else
                {
                    centroid.Add(cd.cluster_index, cd.hit_distance);
                }
            }

            AdjustActionLikelihoods((int)point.action, new_point_weight);

            ++point_count;
        }

        private void AdjustActionLikelihoods(int index, float new_point_weight)
        {
            float residue = 0f;

            for (int i = 0; i < action_strengths.Length; ++i)
            {
                if (i != index)
                {
                    float change = action_strengths[i] * new_point_weight;
                    action_strengths[i] -= change;
                    residue += change;
                }
            }

            action_strengths[index] += residue;
        }
    }

    [Header("Ray Input")]
    [SerializeField] private Transform ref_transform_eyes = null;
    [SerializeField] private int direction_check_rays = 16;
    [SerializeField] private float max_ray_distance = 1f;
    [SerializeField] private bool draw_rays = false;

    [Header("Cluster")]
    [SerializeField] private float invalid_value = 1f;
    [SerializeField] private float new_point_weight = 0.1f; // clamp between [0f, 1f]
    [SerializeField] private float new_cluster_distance = 1f;

    [Header("Random")]
    [SerializeField] private float random_action_base = 0.9f; // clamp between [0f, 1f]

    [Header("Physics")]
    [SerializeField] private Rigidbody2D ref_self_rbody = null;
    [SerializeField] private CircleCollider2D ref_self_ground_check = null;
    [SerializeField] private LayerMask layer_mask_platform = 0;
    [SerializeField] private float action_update_seconds = 1f;
    [SerializeField] private float move_speed = 1f;
    [SerializeField] private float jump_vel = 1f;

    private Dictionary<string, int> tag_to_int = new Dictionary<string, int>();
    private List<Cluster> positive_clusters = new List<Cluster>();
    private List<Cluster> negative_clusters = new List<Cluster>();
    private List<Cluster_Data> current_input = new List<Cluster_Data>();
    private Action current_action = Action.Stay;
    private float last_action_update_time = 0f;
    private int total_cluster_points = 0;

    public void AddPositive()
    {
        Cluster closest = FindClosestCluster(positive_clusters, current_input, true);
        closest.Add(new Cluster_Point(current_input, current_action), new_point_weight);
        ++total_cluster_points;
    }

    public void AddNegative()
    {
        Cluster closest = FindClosestCluster(negative_clusters, current_input, true);
        closest.Add(new Cluster_Point(current_input, current_action), new_point_weight);
        ++total_cluster_points;
    }

    private void Start()
    {
        new_point_weight = Mathf.Clamp(new_point_weight, 0f, 1f);
        random_action_base = Mathf.Clamp(random_action_base, 0f, 1f);

        current_input = GatherRayInput();
        NewAction();
    }

    private void Update()
    {
        current_input = GatherRayInput();

        float e_time = Time.time - last_action_update_time;
        if (e_time >= action_update_seconds)
        {
            NewAction();
        }
    }

    private void FixedUpdate()
    {
        switch (current_action)
        {
            case Action.Stay:
                ref_self_rbody.velocity = Vector2.zero;
                break;
            case Action.Move_Left:
                ref_self_rbody.AddForce(Vector2.right * -move_speed * Time.fixedDeltaTime);
                break;
            case Action.Move_Right:
                ref_self_rbody.AddForce(Vector2.right * move_speed * Time.fixedDeltaTime); 
                break;
            case Action.Jump:
                if (Physics2D.OverlapCircle(ref_self_ground_check.transform.position, ref_self_ground_check.radius, layer_mask_platform))
                {
                    ref_self_rbody.AddForce(Vector2.up * jump_vel);
                }
                break;
        }
    }

    private int GetTagAssignment(string tag)
    {
        if (tag_to_int.ContainsKey(tag))
        {
            return tag_to_int[tag];
        }

        int new_assignment = tag_to_int.Count;
        tag_to_int.Add(tag, new_assignment);
        return new_assignment;
    }

    private List<Cluster_Data> GatherRayInput()
    {
        List<Cluster_Data> ray_hit_dists = new List<Cluster_Data>();

        for (int i = 0; i < direction_check_rays; ++i)
        {
            float ray_dir = ((Mathf.PI * 2) / direction_check_rays) * i;
            Vector2 dir_vec = new Vector2(Mathf.Cos(ray_dir), Mathf.Sin(ray_dir));

            RaycastHit2D hit = Physics2D.Raycast(ref_transform_eyes.position, dir_vec, max_ray_distance);
            float hit_dist = hit ? hit.distance : max_ray_distance;
            if (hit)
            {
                int cluster_index = GetTagAssignment(hit.transform.tag) * direction_check_rays;
                ray_hit_dists.Add(new Cluster_Data(hit_dist, cluster_index));
            }

            if (draw_rays)
            {
                // Debug Rays only show up in Scene view
                Debug.DrawRay(ref_transform_eyes.position, dir_vec * hit_dist, hit ? Color.red : Color.yellow);
            }
        }
        
        return ray_hit_dists;
    }

    private Cluster FindClosestCluster(List<Cluster> clusters, List<Cluster_Data> input, bool create_new)
    {
        Cluster closest = null;

        float closest_distance = new_cluster_distance;
        foreach (Cluster c in clusters)
        {
            float dist = c.GetDistance(input, invalid_value);
            if (dist < closest_distance)
            {
                closest_distance = dist;
                closest = c;
            }
        }

        if (create_new && closest == null)
        {
            Debug.Log("New Cluster");
            closest = new Cluster();
            clusters.Add(closest);
        }
        return closest;
    }

    private Action DetermineAction(Cluster positive, Cluster negative)
    {
        Action new_action = Action.Stay;

        float highest_action_value = -1f;
        for (int i = 0; i < (int)Action.Size; ++i)
        {
            float positive_strength = (positive == null) ? 0f : positive.action_strengths[i];
            float negative_strength = (negative == null) ? 0f : -negative.action_strengths[i];
            float action_strength = positive_strength + negative_strength;
            if (action_strength > highest_action_value)
            {
                highest_action_value = action_strength;
                new_action = (Action)i;
            }
        }

        return new_action;
    }

    private void NewAction()
    {
        float random_action_thresh = Mathf.Pow(random_action_base, total_cluster_points);
        if (Random.value <= random_action_thresh) // Pick random action
        {
            current_action = (Action)Random.Range(0, (int)Action.Size);
            Debug.Log("Random: " + current_action);

        }
        else // Pick example action
        {
            Cluster cp = FindClosestCluster(positive_clusters, current_input, false);
            Cluster cn = FindClosestCluster(negative_clusters, current_input, false);

            current_action = DetermineAction(cp, cn);
            Debug.Log("Cluster: " + current_action);
        }

        last_action_update_time = Time.time;
    }
}
