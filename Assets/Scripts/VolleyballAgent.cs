using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolleyballAgent : MonoBehaviour
{
    List<VolleyballAgentBehavior> behaviors = new List<VolleyballAgentBehavior>();
    public VolleyballAgentBehavior currentBehavior;

    private Vector3 managerTarget = new(-1.0f, 0.5f, -1.0f);
    private bool activeTarget = false;
    public GameObject ManagerTargetPlane;

    public Vector3 ManagerTarget { get => managerTarget; set => managerTarget = value; }
    public bool ActiveTarget { get => activeTarget; set => activeTarget = value; }
    
    void Start()
    {
        foreach (Transform child in transform)
        {
            if (child.TryGetComponent(out VolleyballAgentBehavior behavior))
            {
                behaviors.Add(behavior);
                if (currentBehavior != behavior)
                {
                    behavior.gameObject.SetActive(false);
                }
            }
        }
    }

    public void EnableBehavior(string behaviorName)
    {

        foreach (VolleyballAgentBehavior behavior in behaviors)
        {
            if (behavior.name.EndsWith(behaviorName))
            {
                // if the behavior is already enabled, do nothing
                if (currentBehavior == behavior)
                {
                    return;
                }

                behavior.transform.position = currentBehavior.transform.position;
                behavior.transform.rotation = currentBehavior.transform.rotation;
                behavior.GetComponent<Rigidbody>().velocity = currentBehavior.GetComponent<Rigidbody>().velocity;
                behavior.GetComponent<Rigidbody>().angularVelocity = currentBehavior.GetComponent<Rigidbody>().angularVelocity;

                currentBehavior.gameObject.SetActive(false);
                behavior.gameObject.SetActive(true);
                currentBehavior = behavior;
                break;
            }
        }
    }

    public string GetModelName()
    {
        if (currentBehavior.BehaviorParameters.Model == null)
        {
            return "None";
        }
        return currentBehavior.BehaviorParameters.Model.name;
    }

    public bool BehaviorNameEquals(string name)
    {
        return currentBehavior.BehaviorParameters.BehaviorName == name;
    }

}
