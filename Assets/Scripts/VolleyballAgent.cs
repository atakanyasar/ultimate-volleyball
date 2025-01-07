using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using System;
using Unity.Sentis;

public class VolleyballAgent : Agent
{
    public GameObject area;
    public Rigidbody agentRb;
    private BehaviorParameters behaviorParameters;
    public Team teamId;

    // To get ball's location for observations
    public GameObject ball;
    Rigidbody ballRb;

    VolleyballSettings volleyballSettings;
    VolleyballEnvController envController;

    // Controls jump behavior
    float jumpingTime;
    Vector3 jumpTargetPos;
    Vector3 jumpStartingPos;
    float agentRot;

    public Collider[] hitGroundColliders = new Collider[3];
    EnvironmentParameters resetParams;

    private Vector3 managerTarget = new(-1.0f, 0.5f, -1.0f);
    private bool activeTarget = false;
    public GameObject ManagerTargetPlane;

    public Vector3 ManagerTarget { get => managerTarget; set => managerTarget = value; }
    public bool ActiveTarget { get => activeTarget; set => activeTarget = value; }
    public BehaviorParameters BehaviorParameters { get => behaviorParameters; set => behaviorParameters = value; }

    void Start()
    {
        envController = area.GetComponent<VolleyballEnvController>();
    }

    public override void Initialize()
    {
        volleyballSettings = FindFirstObjectByType<VolleyballSettings>();
        BehaviorParameters = gameObject.GetComponent<BehaviorParameters>();

        agentRb = GetComponent<Rigidbody>();
        ballRb = ball.GetComponent<Rigidbody>();
        
        // for symmetry between player side
        if (teamId == Team.Blue)
        {
            agentRot = -1;
        }
        else
        {
            agentRot = 1;
        }

        resetParams = Academy.Instance.EnvironmentParameters;
    }

    public string GetModelName()
    {
        if (BehaviorParameters.Model == null)
        {
            return "None";
        }
        return BehaviorParameters.Model.name;
    }

    public bool BehaviorNameEquals(string name)
    {
        return BehaviorParameters.BehaviorName == name;
    }

    /// <summary>
    /// Moves  a rigidbody towards a position smoothly.
    /// </summary>
    /// <param name="targetPos">Target position.</param>
    /// <param name="rb">The rigidbody to be moved.</param>
    /// <param name="targetVel">The velocity to target during the
    ///  motion.</param>
    /// <param name="maxVel">The maximum velocity posible.</param>
    void MoveTowards(
        Vector3 targetPos, Rigidbody rb, float targetVel, float maxVel)
    {
        var moveToPos = targetPos - rb.worldCenterOfMass;
        var velocityTarget = Time.fixedDeltaTime * targetVel * moveToPos;
        if (float.IsNaN(velocityTarget.x) == false)
        {
            rb.velocity = Vector3.MoveTowards(
                rb.velocity, velocityTarget, maxVel);
        }
    }

    /// <summary>
    /// Check if agent is on the ground to enable/disable jumping
    /// </summary>
    public bool CheckIfGrounded()
    {
        hitGroundColliders = new Collider[3];
        var o = gameObject;
        Physics.OverlapBoxNonAlloc(
            o.transform.localPosition + new Vector3(0, -0.05f, 0),
            new Vector3(0.95f / 2f, 0.5f, 0.95f / 2f),
            hitGroundColliders,
            o.transform.rotation);
        var grounded = false;
        foreach (var col in hitGroundColliders)
        {
            if (col != null && col.transform != transform &&
                (col.CompareTag("walkableSurface") ||
                 col.CompareTag("purpleGoal") ||
                 col.CompareTag("blueGoal")))
            {
                grounded = true; //then we're grounded
                break;
            }
        }
        return grounded;
    }

    /// <summary>
    /// Called when agent collides with the ball
    /// </summary>
    void OnCollisionEnter(Collision c)
    {
        if (c.gameObject.CompareTag("ball"))
        {
            envController.UpdateLastHitter(teamId, this);
        }
    }

    /// <summary>
    /// Starts the jump sequence
    /// </summary>
    public void Jump()
    {
        jumpingTime = 0.2f;
        jumpStartingPos = agentRb.position;
    }

    /// <summary>
    /// Resolves the agent movement
    /// </summary>
    public void MoveAgent(ActionSegment<int> act)
    {
        var grounded = CheckIfGrounded();
        var dirToGo = Vector3.zero;
        var rotateDir = Vector3.zero;
        var dirToGoForwardAction = act[0];
        var rotateDirAction = act[1];
        var dirToGoSideAction = act[2];
        var jumpAction = act[3];

        if (dirToGoForwardAction == 1)
            dirToGo = (grounded ? 1f : 0.5f) * transform.forward * 1f;
        else if (dirToGoForwardAction == 2)
            dirToGo = (grounded ? 1f : 0.5f) * transform.forward * volleyballSettings.speedReductionFactor * -1f;

        if (rotateDirAction == 1)
            rotateDir = transform.up * -1f;
        else if (rotateDirAction == 2)
            rotateDir = transform.up * 1f;

        if (dirToGoSideAction == 1)
            dirToGo = (grounded ? 1f : 0.5f) * transform.right * volleyballSettings.speedReductionFactor * -1f;
        else if (dirToGoSideAction == 2)
            dirToGo = (grounded ? 1f : 0.5f) * transform.right * volleyballSettings.speedReductionFactor;

        if (jumpAction == 1) {

            // Penalty for unecessary jumping
            if (BehaviorNameEquals("MoveTo") || BehaviorNameEquals("MoveToBall")) {
                AddReward(-0.2f);
            }
            else {
                AddReward(-0.0001f);
            }

            if (((jumpingTime <= 0f) && grounded))
            {
                Jump();
            }

        }

        transform.Rotate(rotateDir, Time.fixedDeltaTime * 200f);
        agentRb.AddForce(agentRot * dirToGo * volleyballSettings.agentRunSpeed,
            ForceMode.VelocityChange);

        if (jumpingTime > 0f)
        {
            jumpTargetPos =
                new Vector3(agentRb.position.x,
                    jumpStartingPos.y + volleyballSettings.agentJumpHeight,
                    agentRb.position.z) + agentRot*dirToGo;

            MoveTowards(jumpTargetPos, agentRb, volleyballSettings.agentJumpVelocity,
                volleyballSettings.agentJumpVelocityMaxChange);
        }

        if (!(jumpingTime > 0f) && !grounded)
        {
            agentRb.AddForce(
                Vector3.down * volleyballSettings.fallingForce, ForceMode.Acceleration);
        }

        if (jumpingTime > 0f)
        {
            jumpingTime -= Time.fixedDeltaTime;
        }
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        var previousDistance = Vector3.Distance(this.transform.position, this.ManagerTarget);
        MoveAgent(actionBuffers.DiscreteActions);
        var currentDistance = Vector3.Distance(this.transform.position, this.ManagerTarget);

        if (BehaviorNameEquals("MoveTo")) {
            if (this.ActiveTarget) {
                // relative to manager
                this.ManagerTargetPlane.transform.position = this.ManagerTarget;

                // If moved towards the target, reward the agent
                AddReward((previousDistance - currentDistance) * 0.001f);

                // If agent reaches the target
                if (currentDistance < 0.75f)
                {
                    AddReward(1.0f);
                    this.ActiveTarget = false;
                    this.ManagerTarget = new Vector3(-1.0f, 0.5f, -1.0f);
                    this.ManagerTargetPlane.SetActive(false);
                }
            }
        }

    }

    public override void CollectObservations(VectorSensor sensor)
    {

        if (BehaviorParameters.BehaviorName == "MoveTo") {
            // agent observations (9 floats)
            CollectAgentObservations(sensor);

            // target observations (4 floats)
            CollectTargetObservations(sensor);

            return;
        }

        if (BehaviorNameEquals("SendBallTo")) {
            // agent observations (9 floats)
            CollectAgentObservations(sensor);

            // ball observations (8 floats)
            CollectBallObservations(sensor);

            // target observations (4 floats)
            CollectTargetObservations(sensor);

            return;
        }

        // Default behavior

        // Agent rotation (1 float)
        sensor.AddObservation(this.transform.rotation.y);

        // Vector from agent to ball (direction to ball) (3 floats)
        Vector3 toBall = new Vector3((ballRb.transform.position.x - this.transform.position.x)*agentRot, 
        (ballRb.transform.position.y - this.transform.position.y),
        (ballRb.transform.position.z - this.transform.position.z)*agentRot);

        sensor.AddObservation(toBall.normalized);

        // Distance from the ball (1 float)
        sensor.AddObservation(toBall.magnitude);

        // Agent velocity (3 floats)
        sensor.AddObservation(agentRb.velocity);

        // Ball velocity (3 floats)
        sensor.AddObservation(ballRb.velocity.y);
        sensor.AddObservation(ballRb.velocity.z*agentRot);
        sensor.AddObservation(ballRb.velocity.x*agentRot); 

    }

    /// <summary>
    /// agent observations (9 floats)
    /// </summary>
    /// <param name="sensor"></param>
    public void CollectAgentObservations(VectorSensor sensor)
    {
        // Agent rotation (1 float)
        sensor.AddObservation(this.transform.rotation.y * agentRot);

        // Agent position (3 floats)
        sensor.AddObservation(this.transform.localPosition.x * agentRot);
        sensor.AddObservation(this.transform.localPosition.y);
        sensor.AddObservation(this.transform.localPosition.z * agentRot);

        // Agent velocity (3 floats)
        sensor.AddObservation(agentRb.velocity.x * agentRot);
        sensor.AddObservation(agentRb.velocity.y);
        sensor.AddObservation(agentRb.velocity.z * agentRot);

        // Z distance to net (1 float)
        sensor.AddObservation((envController.transform.position.z - this.transform.position.z) * agentRot);

        // Agent team (1 float)
        sensor.AddObservation(agentRot);
    }

    /// <summary>
    /// ball observations (8 floats)
    /// </summary>
    /// <param name="sensor"></param>
    public void CollectBallObservations(VectorSensor sensor)
    {
        // Vector from agent to ball (direction to ball) (3 floats)
        Vector3 toBall = new Vector3(
            (ballRb.transform.position.x - this.transform.position.x)*agentRot, 
            (ballRb.transform.position.y - this.transform.position.y),
            (ballRb.transform.position.z - this.transform.position.z)*agentRot
        );

        sensor.AddObservation(toBall.normalized);

        // Distance from the ball (1 float)
        sensor.AddObservation(toBall.magnitude);

        // Ball velocity (3 floats)
        sensor.AddObservation(ballRb.velocity.y);
        sensor.AddObservation(ballRb.velocity.z*agentRot);
        sensor.AddObservation(ballRb.velocity.x*agentRot);

        // Is agent last hitter (1 float)
        sensor.AddObservation(envController.LastHitterAgent == this);
    }

    /// <summary>
    /// target observations (4 floats)
    /// </summary>
    /// <param name="sensor"></param>
    public void CollectTargetObservations(VectorSensor sensor)
    {
        Vector3 toTarget = new Vector3(
            (ManagerTarget.x - this.transform.position.x)*agentRot,
            (ManagerTarget.y - this.transform.position.y),
            (ManagerTarget.z - this.transform.position.z)*agentRot
        );
        
        // To Target position (3 floats)
        sensor.AddObservation(toTarget.normalized);

        // Distance to Target (1 float)
        sensor.AddObservation(toTarget.magnitude);
    }
        

    // For human controller
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        if (Input.GetKey(KeyCode.D))
        {
            // rotate right
            discreteActionsOut[1] = 2;
        }
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            // forward
            discreteActionsOut[0] = 1;
        }
        if (Input.GetKey(KeyCode.A))
        {
            // rotate left
            discreteActionsOut[1] = 1;
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            // backward
            discreteActionsOut[0] = 2;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            // move left
            discreteActionsOut[2] = 1;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            // move right
            discreteActionsOut[2] = 2;
        }
        discreteActionsOut[3] = Input.GetKey(KeyCode.Space) ? 1 : 0;
    }
}
