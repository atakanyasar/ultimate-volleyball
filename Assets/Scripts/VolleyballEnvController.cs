using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.Sentis;
using UnityEngine;
using Random = UnityEngine.Random;

public enum Team
{
    Blue = 0,
    Purple = 1,
    Default = 2
}

public enum Event
{
    HitPurpleGoal = 0,
    HitBlueGoal = 1,
    HitOutOfBounds = 2,
    HitIntoBlueArea = 3,
    HitIntoPurpleArea = 4
}

public class VolleyballEnvController : MonoBehaviour
{
    int ballSpawnSide;

    VolleyballSettings volleyballSettings;

    public List<string> behaviors;
    public List<ModelAsset> modelAssets;
    
    public GameObject blueManager;
    public GameObject purpleManager;

    private List<VolleyballAgent> blueAgents;
    private List<VolleyballAgent> purpleAgents;

    List<Renderer> RenderersList = new List<Renderer>();

    public GameObject ball;
    Rigidbody ballRb;

    public GameObject blueGoal;
    public GameObject purpleGoal;

    Renderer blueGoalRenderer;

    Renderer purpleGoalRenderer;

    Team lastHitterTeam;
    VolleyballAgent lastHitterAgent;

    private int resetTimer;
    public int MaxEnvironmentSteps;

    void Start()
    {

        // Used to control agent & ball starting positions
        ballRb = ball.GetComponent<Rigidbody>();

        // Starting ball spawn side
        // -1 = spawn blue side, 1 = spawn purple side
        var spawnSideList = new List<int> { -1, 1 };
        ballSpawnSide = spawnSideList[Random.Range(0, 2)];

        // Render ground to visualise which agent scored
        blueGoalRenderer = blueGoal.GetComponent<Renderer>();
        purpleGoalRenderer = purpleGoal.GetComponent<Renderer>();
        RenderersList.Add(blueGoalRenderer);
        RenderersList.Add(purpleGoalRenderer);

        volleyballSettings = FindFirstObjectByType<VolleyballSettings>();

        blueAgents = blueManager.GetComponent<VolleyballManager>().GetAgents();
        purpleAgents = purpleManager.GetComponent<VolleyballManager>().GetAgents();

        ResetScene();
    }

    /// <summary>
    /// Tracks which agent last had control of the ball
    /// </summary>
    public void UpdateLastHitter(Team team, VolleyballAgent agent)
    {
        lastHitterTeam = team;
        lastHitterAgent = agent;

        if (lastHitterAgent.BehaviorNameEquals("MoveToBall")) {
            agent.AddReward(1.0f);
        }

        if (lastHitterAgent.BehaviorNameEquals("1v1")) {
            agent.AddReward(0.01f);
        }

    }

    /// <summary>
    /// Resolves scenarios when ball enters a trigger and assigns rewards.
    /// Example reward functions are shown below.
    /// To enable Self-Play: Set either Purple or Blue Agent's Team ID to 1.
    /// </summary>
    public void ResolveEvent(Event triggerEvent)
    {
        switch (triggerEvent)
        {
            case Event.HitOutOfBounds:
                // apply penalty to agent
                if (lastHitterAgent != null && lastHitterAgent.BehaviorNameEquals("1v1")) {
                    lastHitterAgent.AddReward(-0.009f);
                }

                // end episode
                EndAllEpisodes();
                break;

            case Event.HitBlueGoal:
                // blue wins
                blueManager.GetComponent<VolleyballManager>().AddReward(1f);
                purpleManager.GetComponent<VolleyballManager>().AddReward(-1f);

                // penalty to purple agent
                foreach (var agent in purpleAgents)
                {
                    if (agent.BehaviorNameEquals("1v1")) {
                        if (lastHitterTeam == Team.Blue) {
                            agent.AddReward(-1f);
                        }
                        else {
                            agent.AddReward(-0.009f);
                        }
                    }
                }

                // reward for blue agent
                if (lastHitterTeam == Team.Blue)
                {
                    if (lastHitterAgent.BehaviorNameEquals("1v1")) {
                        lastHitterAgent.AddReward(1f);
                    }
                }

                // turn floor blue
                StartCoroutine(GoalScoredSwapGroundMaterial(volleyballSettings.blueGoalMaterial, RenderersList, .5f));

                // end episode
                EndAllEpisodes();
                break;

            case Event.HitPurpleGoal:
                // purple wins
                purpleManager.GetComponent<VolleyballManager>().AddReward(1f);
                blueManager.GetComponent<VolleyballManager>().AddReward(-1f);

                // penalty to blue agent
                foreach (var agent in blueAgents)
                {
                    if (agent.BehaviorNameEquals("1v1")) {
                        if (lastHitterTeam == Team.Purple) {          
                            agent.AddReward(-1f);
                        }
                        else {
                            agent.AddReward(-0.009f);
                        }
                    }
                }

                // reward for purple agent
                if (lastHitterTeam == Team.Purple)
                {
                    if (lastHitterAgent.BehaviorNameEquals("1v1")) {
                        lastHitterAgent.AddReward(1f);
                    }
                }

                // turn floor purple
                StartCoroutine(GoalScoredSwapGroundMaterial(volleyballSettings.purpleGoalMaterial, RenderersList, .5f));

                // end episode
                EndAllEpisodes();
                break;

            case Event.HitIntoBlueArea:
                if (lastHitterTeam == Team.Purple)
                {
                    purpleManager.GetComponent<VolleyballManager>().AddReward(1f);

                    if (lastHitterAgent.BehaviorNameEquals("1v1")) {
                        lastHitterAgent.AddReward(0.1f);
                    }
                }
                break;

            case Event.HitIntoPurpleArea:
                if (lastHitterTeam == Team.Blue)
                {
                    blueManager.GetComponent<VolleyballManager>().AddReward(1f);

                    if (lastHitterAgent.BehaviorNameEquals("1v1")) {
                        lastHitterAgent.AddReward(0.1f);
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Changes the color of the ground for a moment.
    /// </summary>
    /// <returns>The Enumerator to be used in a Coroutine.</returns>
    /// <param name="mat">The material to be swapped.</param>
    /// <param name="time">The time the material will remain.</param>
    IEnumerator GoalScoredSwapGroundMaterial(Material mat, List<Renderer> rendererList, float time)
    {
        foreach (var renderer in rendererList)
        {
            renderer.material = mat;
        }

        yield return new WaitForSeconds(time); // wait for 2 sec

        foreach (var renderer in rendererList)
        {
            renderer.material = volleyballSettings.defaultMaterial;
        }

    }

    /// <summary>
    /// Called every step. Control max env steps.
    /// </summary>
    void FixedUpdate()
    {
        resetTimer += 1;
        if (resetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0)
        {
            for (int i = 0; i < blueAgents.Count; i++)
            {
                blueAgents[i].EpisodeInterrupted();
            }

            for (int i = 0; i < purpleAgents.Count; i++)
            {
                purpleAgents[i].EpisodeInterrupted();
            }

            ResetScene();
        }
    }

    public void EndAllEpisodes()
    {
        // return; // For Player MoveTo training mode
        
        for (int i = 0; i < blueAgents.Count; i++)
        {
            blueAgents[i].EndEpisode();
        }

        for (int i = 0; i < purpleAgents.Count; i++)
        {
            purpleAgents[i].EndEpisode();
        }
        
        ResetScene();
    }

    /// <summary>
    /// Reset agent and ball spawn conditions.
    /// </summary>
    public void ResetScene()
    {
        resetTimer = 0;

        lastHitterTeam = Team.Default; // reset last hitter
        lastHitterAgent = null;

        foreach (var agent in blueAgents)
        {
            agent.EpisodeInterrupted();

            // randomise starting positions and rotations
            var randomPosX = Random.Range(-2f, 2f);
            var randomPosZ = Random.Range(-2f, 2f);
            var randomPosY = Random.Range(0.5f, 3.75f); // depends on jump height
            var randomRot = Random.Range(-45f, 45f);

            agent.transform.localPosition = new Vector3(randomPosX, randomPosY, randomPosZ);
            agent.transform.eulerAngles = new Vector3(0, randomRot, 0);

            agent.GetComponent<Rigidbody>().velocity = default(Vector3);
        }

        foreach (var agent in purpleAgents)
        {
            agent.EpisodeInterrupted();

            // randomise starting positions and rotations
            var randomPosX = Random.Range(-2f, 2f);
            var randomPosZ = Random.Range(-2f, 2f);
            var randomPosY = Random.Range(0.5f, 3.75f); // depends on jump height
            var randomRot = Random.Range(-45f, 45f);

            agent.transform.localPosition = new Vector3(randomPosX, randomPosY, randomPosZ);
            agent.transform.eulerAngles = new Vector3(0, randomRot, 0);

            agent.GetComponent<Rigidbody>().velocity = default(Vector3);
        }

        // reset ball to starting conditions
        ResetBall();
    }

    /// <summary>
    /// Reset ball spawn conditions
    /// </summary>
    void ResetBall()
    {
        var randomPosX = Random.Range(-2f, 2f);
        var randomPosZ = Random.Range(6f, 10f);
        var randomPosY = Random.Range(6f, 8f);

        var randomVelX = Random.Range(-volleyballSettings.ballResetMaxVelocity, volleyballSettings.ballResetMaxVelocity);
        var randomVelY = Random.Range(-volleyballSettings.ballResetMaxVelocity, volleyballSettings.ballResetMaxVelocity);
        var randomVelZ = Random.Range(-volleyballSettings.ballResetMaxVelocity, volleyballSettings.ballResetMaxVelocity);

        // alternate ball spawn side
        // -1 = spawn blue side, 1 = spawn purple side
        ballSpawnSide = -1 * ballSpawnSide;

        if (ballSpawnSide == -1)
        {
            ball.transform.localPosition = new Vector3(randomPosX, randomPosY, randomPosZ);
        }
        else if (ballSpawnSide == 1)
        {
            ball.transform.localPosition = new Vector3(randomPosX, randomPosY, -1 * randomPosZ);
        }

        ballRb.angularVelocity = Vector3.zero;
        ballRb.velocity = new Vector3(randomVelX, randomVelY, randomVelZ);
    }
}
