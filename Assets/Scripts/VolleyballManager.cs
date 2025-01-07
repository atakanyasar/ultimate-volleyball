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
    MoveToPosition,
    SendBallToTarget,
}

public class VolleyballManager : Agent
{
    // get script from parent object
    private VolleyballEnvController envController;
    private List<VolleyballAgent> agents = new();
    public Team teamId;
    private int teamRot;

    public ModelAsset MoveToBallModel;
    public ModelAsset SendBallToModel;

    public void Start()
    {
        envController = GetComponentInParent<VolleyballEnvController>();

        // iterate all child objects
        foreach (Transform child in transform)
        {
            VolleyballAgent agent = child.GetComponent<VolleyballAgent>();
            agents.Add(agent);
        }

        if (teamId == Team.Blue)
        {
            teamRot = -1;
        }
        else
        {
            teamRot = 1;
        }
    }

    public List<VolleyballAgent> GetAgents()
    {
        return agents;
    }

    private void GiveTask(VolleyballAgent agent, SubTask task) {
        if (task == SubTask.MoveToBall) {
            agent.SetModel("MoveToBall", MoveToBallModel);
        }

        if (task == SubTask.SendBallToTarget) {
            agent.SetModel("SendBallTo", SendBallToModel);
        } 

    }

    public override void OnActionReceived(ActionBuffers actions) {

        for (int i = 0; i < agents.Count; i++) {
            var agent = agents[i];
            var action = actions.DiscreteActions[i];
            var x = actions.ContinuousActions[i * 2];
            var z = actions.ContinuousActions[i * 2 + 1];

            if (action == 0) {
                agent.ActiveTarget = false;
                agent.ManagerTargetPlane.SetActive(false);

                GiveTask(agent, SubTask.MoveToBall);
            }
            else if (action == 1) {
                agent.ActiveTarget = true;
                agent.ManagerTarget = GetComponentInParent<VolleyballEnvController>().transform.position + new Vector3(x * 6.0f * teamRot, 0.5f, z * 13.0f * teamRot);
                agent.ManagerTargetPlane.SetActive(true);

                GiveTask(agent, SubTask.SendBallToTarget);
            }
            else {
                GiveTask(agent, SubTask.Idle);
            }
        }
            

        foreach (VolleyballAgent agent in agents) {
            if (agent.BehaviorNameEquals("SendBallTo")) {
                if (agent.ActiveTarget) {
                    agent.ManagerTargetPlane.transform.position = agent.ManagerTarget;
                }
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor) {

        foreach (var agent in agents) {           
            // agent position (3 floats)
            sensor.AddObservation(agent.transform.localPosition.x * teamRot);
            sensor.AddObservation(agent.transform.localPosition.y);
            sensor.AddObservation(agent.transform.localPosition.z * teamRot);

            // is agent last hitter (1 float)
            sensor.AddObservation(envController.LastHitterAgent == agent);
        }

        foreach (var agent in envController.GetTeamPlayers(envController.GetOpponentTeam(teamId))) {
            // opponent agent position (3 floats)
            sensor.AddObservation(agent.transform.localPosition.x * teamRot);
            sensor.AddObservation(agent.transform.localPosition.y);
            sensor.AddObservation(agent.transform.localPosition.z * teamRot);
        }

        // ball position (3 floats)
        sensor.AddObservation(envController.ball.transform.localPosition.x * teamRot);
        sensor.AddObservation(envController.ball.transform.localPosition.y);
        sensor.AddObservation(envController.ball.transform.localPosition.z * teamRot);

        // ball velocity (3 floats)
        sensor.AddObservation(envController.ball.GetComponent<Rigidbody>().velocity.x * teamRot);
        sensor.AddObservation(envController.ball.GetComponent<Rigidbody>().velocity.y);
        sensor.AddObservation(envController.ball.GetComponent<Rigidbody>().velocity.z * teamRot);




    }

    public override void Heuristic(in ActionBuffers actionsOut) {

        if (envController.volleyballSettings.trainingModeName == "Manager") {
            
            actionsOut.ContinuousActions.Array[0] = 0;
            actionsOut.ContinuousActions.Array[1] = 0.5f;
            actionsOut.ContinuousActions.Array[2] = 0;
            actionsOut.ContinuousActions.Array[3] = 0.5f;

            if (Input.GetKey(KeyCode.Keypad0)) {
                actionsOut.DiscreteActions.Array[0] = 0; // move to ball
                actionsOut.DiscreteActions.Array[1] = 0; // move to ball
            }
            else if (Input.GetKey(KeyCode.Keypad1)) {
                actionsOut.DiscreteActions.Array[0] = 0; // move to ball
                actionsOut.DiscreteActions.Array[1] = 1; // send ball to target
            }
            else if (Input.GetKey(KeyCode.Keypad2)) {
                actionsOut.DiscreteActions.Array[0] = 1; // send ball to target
                actionsOut.DiscreteActions.Array[1] = 0; // move to ball
            }
            else {
                actionsOut.DiscreteActions.Array[0] = 1; // send ball to target
                actionsOut.DiscreteActions.Array[1] = 1; // send ball to target
            }
            return;
        }

        actionsOut.DiscreteActions.Array[0] = 2; // idle
        actionsOut.DiscreteActions.Array[1] = 2; // idle

        foreach (VolleyballAgent agent in agents) {
            if (agent.BehaviorNameEquals("SendBallTo")) {
                if (agent.ActiveTarget == false) {
                    agent.ActiveTarget = true;
                    agent.ManagerTarget = GetComponentInParent<VolleyballEnvController>().transform.position + new Vector3(Random.Range(-6.0f, 6.0f), 0.5f, Random.Range(-13.0f, 13.0f));
                    agent.ManagerTargetPlane.SetActive(true);
                } 
            }
            if (agent.BehaviorNameEquals("MoveToPosition")) {
                if (agent.ActiveTarget == false) {
                    agent.ActiveTarget = true;
                    agent.ManagerTarget = transform.position + new Vector3(Random.Range(-2.0f, 2.0f), 0.5f, Random.Range(-2.0f, 2.0f));
                    agent.ManagerTargetPlane.SetActive(true);
                }
            }
        }
    }


}
