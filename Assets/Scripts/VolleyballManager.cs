using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.Sentis;
using UnityEngine;
using Random = UnityEngine.Random;

public enum SubTask
{
    Idle = 0,
    SinglePlayer,
    MoveToBall,
    HitBall,
    BlockBall,
    MoveToPosition
}

public class VolleyballManager : Agent
{
    // get script from parent object
    private VolleyballEnvController envController;
    private List<VolleyballAgent> agents = new();
    public Team teamId;
    public int numSquares = 5; // number of squares to divide the area into

    public void Start()
    {
        envController = GetComponentInParent<VolleyballEnvController>();

        // iterate all child objects
        foreach (Transform child in transform)
        {
            VolleyballAgent agent = child.GetComponent<VolleyballAgent>();
            agents.Add(agent);
        }
    }

    public List<VolleyballAgent> GetAgents()
    {
        return agents;
    }

    private void GiveTask(VolleyballAgent agent, SubTask task) {
        // string behavior = envController.behaviors[(int)task];
        // ModelAsset model = envController.modelAssets[(int)task];
        // agent.SetModel(behavior, model);
        if (task == SubTask.SinglePlayer) {

        }

        if (task == SubTask.MoveToPosition) {
            if (agent.ActiveTarget == false) {
                agent.ActiveTarget = true;
                agent.MoveToTarget = transform.position + new Vector3(Random.Range(-2.0f, 2.0f), 0.5f, Random.Range(-2.0f, 2.0f));
                agent.MoveToTargetPlane.SetActive(true);
                agent.MoveToTargetPlane.transform.localPosition = agent.MoveToTarget;
            } 
        }
        if (task == SubTask.MoveToBall) {

        }

    }

    public override void OnActionReceived(ActionBuffers actions) {
        // apply actions to each agent
        // foreach (VolleyballAgent agent in agents) {
        //     GiveTask(agent, (SubTask)actions.DiscreteActions[0]);
        // }
    }

    public override void CollectObservations(VectorSensor sensor) {
        // get the size of each square
        float squareSize = 1.0f / numSquares;

        // for each square, check if it contains the player
        /*
        for (int i = 0; i < numSquares; i++) {
            for (int j = 0; j < numSquares; j++) {
                List<int> playerInSquare = new List<int>();
                foreach (VolleyballAgent agent in agents) {
                    Vector3 agentPos = agent.transform.position;
                    if (agentPos.x > i * squareSize && agentPos.x < (i + 1) * squareSize &&
                        agentPos.z > j * squareSize && agentPos.z < (j + 1) * squareSize) {
                        playerInSquare.Add(1);
                        sensor.AddObservation(1);
                    }
                    else {
                        playerInSquare.Add(0);
                        sensor.AddObservation(0);
                    }
                }
            }
        }
        */

        // add agent-specific observations for each agent
        // for (int i = 0; i < agents.Count; i++) {
        //     VolleyballAgent agent = agents[i];
        //     agent.CollectObservations(sensor);
        // }

    }

    public override void Heuristic(in ActionBuffers actionsOut) {
        foreach (VolleyballAgent agent in agents) {
            GiveTask(agent, SubTask.SinglePlayer);   
        }
    }


}
