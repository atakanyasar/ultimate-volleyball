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
    BehaviorStatistics behaviorStatistics;

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
        behaviorStatistics = FindFirstObjectByType<BehaviorStatistics>();

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

        behaviorStatistics.OnStatisticEvent(this, agent, StatisticEvent.TouchBall);

        if (lastHitterAgent.BehaviorNameEquals("1v1")) {
            agent.AddReward(0.01f);
        }
        
        if (lastHitterAgent.BehaviorNameEquals("MoveToBall")) {
            agent.AddReward(1.0f);

            if (volleyballSettings.trainingModeName == "MoveToBall") {
                EndAllEpisodes();
            }
        }


    }

    public Team GetOpponentTeam(Team team)
    {
        if (team == Team.Blue)
        {
            return Team.Purple;
        }
        else if (team == Team.Purple)
        {
            return Team.Blue;
        }
        else
        {
            return Team.Default;
        }
    }

    public List<VolleyballAgent> GetTeamPlayers(Team team)
    {
        if (team == Team.Blue)
        {
            return blueAgents;
        }
        else if (team == Team.Purple)
        {
            return purpleAgents;
        }
        else
        {
            return null;
        }
    }

    private void UpdateWinLoseStatistics(Team winner)
    {
        // if (blueAgents[0].BehaviorNameEquals("1v1") && purpleAgents[0].BehaviorNameEquals("1v1")) {
        //     if (blueAgents[0].GetModelName() == purpleAgents[0].GetModelName()) {
        //         return;
        //     }
        // }

        if (winner == Team.Blue)
        {
            blueAgents.ForEach(agent => behaviorStatistics.Win(agent));
            purpleAgents.ForEach(agent => behaviorStatistics.Lose(agent));
        }
        else if (winner == Team.Purple)
        {
            blueAgents.ForEach(agent => behaviorStatistics.Lose(agent));
            purpleAgents.ForEach(agent => behaviorStatistics.Win(agent));
        }
        else
        {
            blueAgents.ForEach(agent => behaviorStatistics.Tie(agent));
            purpleAgents.ForEach(agent => behaviorStatistics.Tie(agent));
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

                if (lastHitterAgent != null) {
                    behaviorStatistics.OnStatisticEvent(this, lastHitterAgent, StatisticEvent.MakeMistake);
                }

                UpdateWinLoseStatistics(GetOpponentTeam(lastHitterTeam));

                // end episode
                EndAllEpisodes();
                break;

            case Event.HitBlueGoal:
                // blue wins
                ResolveHitIntoGoal(Team.Blue);

                blueManager.GetComponent<VolleyballManager>().AddReward(1f);
                purpleManager.GetComponent<VolleyballManager>().AddReward(-1f);

                // turn floor blue
                StartCoroutine(GoalScoredSwapGroundMaterial(volleyballSettings.blueGoalMaterial, RenderersList, .5f));

                // end episode
                EndAllEpisodes();
                break;

            case Event.HitPurpleGoal:
                // purple wins
                ResolveHitIntoGoal(Team.Purple);

                purpleManager.GetComponent<VolleyballManager>().AddReward(1f);
                blueManager.GetComponent<VolleyballManager>().AddReward(-1f);

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

                    behaviorStatistics.OnStatisticEvent(this, lastHitterAgent, StatisticEvent.SendBall);
                }
                break;

            case Event.HitIntoPurpleArea:
                if (lastHitterTeam == Team.Blue)
                {
                    blueManager.GetComponent<VolleyballManager>().AddReward(1f);

                    if (lastHitterAgent.BehaviorNameEquals("1v1")) {
                        lastHitterAgent.AddReward(0.1f);
                    }

                    behaviorStatistics.OnStatisticEvent(this, lastHitterAgent, StatisticEvent.SendBall);
                }
                break;
        }
    }

    private void ResolveHitIntoGoal(Team winnerTeam) {
        Team loserTeam = GetOpponentTeam(winnerTeam);

        // loser team misses ball
        if (lastHitterTeam != loserTeam) {
            GetTeamPlayers(loserTeam).ForEach(agent => {
                behaviorStatistics.OnStatisticEvent(this, agent, StatisticEvent.MissBall);

                if (agent.BehaviorNameEquals("1v1")) {
                    agent.AddReward(-1f);
                }

                if (agent.BehaviorNameEquals("MoveToBall")) {
                    // penalty according to distance from ball
                    agent.AddReward(-0.1f * Vector3.Distance(agent.transform.position, ball.transform.position));
                }
            });
        }

        // last hitter agent scores into goal with successfull hit
        if (lastHitterTeam == winnerTeam)
        {
            if (lastHitterAgent.BehaviorNameEquals("1v1")) {
                lastHitterAgent.AddReward(1f);
            }
        }

        // last hitter agent makes mistake and scores into opponents goal
        if (lastHitterTeam == loserTeam)
        {
            behaviorStatistics.OnStatisticEvent(this, lastHitterAgent, StatisticEvent.MakeMistake);
            
            if (lastHitterAgent.BehaviorNameEquals("1v1")) {
                lastHitterAgent.AddReward(-0.009f);
            }
        }

        UpdateWinLoseStatistics(winnerTeam);

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
        if (volleyballSettings.trainingModeName == "MoveTo") {
            return;
        }
        
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
            // randomise starting positions and rotations
            var randomPosX = Random.Range(-5f, 5f);
            var randomPosZ = Random.Range(-5f, 5f);
            var randomPosY = Random.Range(0.5f, 3.75f); // depends on jump height
            var randomRot = Random.Range(-45f, 45f);

            agent.transform.localPosition = new Vector3(randomPosX, randomPosY, randomPosZ);
            agent.transform.eulerAngles = new Vector3(0, randomRot, 0);

            agent.GetComponent<Rigidbody>().velocity = default(Vector3);
        }

        foreach (var agent in purpleAgents)
        {

            // randomise starting positions and rotations
            var randomPosX = Random.Range(-5f, 5f);
            var randomPosZ = Random.Range(-5f, 5f);
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
        var randomPosX = Random.Range(-volleyballSettings.ballResetMaxLocation, volleyballSettings.ballResetMaxLocation);
        var randomPosZ = Random.Range(7f - volleyballSettings.ballResetMaxLocation, 7f + volleyballSettings.ballResetMaxLocation);
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
