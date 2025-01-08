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
    public int ballSpawnSide = 0;

    public VolleyballSettings volleyballSettings;
    BehaviorStatistics behaviorStatistics;
    
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

    public VolleyballAgent LastHitterAgent { get => lastHitterAgent; set => lastHitterAgent = value; }

    void Start()
    {

        // Used to control agent & ball starting positions
        ballRb = ball.GetComponent<Rigidbody>();

        // Starting ball spawn side
        // -1 = spawn blue side, 1 = spawn purple side
        var spawnSideList = new List<int> { -1, 1 };
        if (ballSpawnSide == 0) {
            ballSpawnSide = spawnSideList[Random.Range(0, 2)];
        }

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
        // double touching the ball
        if (LastHitterAgent == agent) {
            if (LastHitterAgent.BehaviorNameEquals("SendBallTo") && volleyballSettings.trainingModeName == "SendBallTo") {
                LastHitterAgent.currentBehavior.AddReward(-0.75f);    
                EndAllEpisodes();
                return;
            }

            GetTeamManager(team).AddReward(-0.2f);
        }

        // update last hitter
        lastHitterTeam = team;
        LastHitterAgent = agent;

        // TouchBall event
        behaviorStatistics.OnStatisticEvent(this, new List<VolleyballAgent> { agent }, StatisticEvent.TouchBall);

        ball.GetComponent<VolleyballController>().highestPoint = 0;

        GetTeamManager(team).AddReward(0.1f);

        if (LastHitterAgent.BehaviorNameEquals("1v1")) {
            agent.currentBehavior.AddReward(0.01f);
        }
        else if (LastHitterAgent.BehaviorNameEquals("MoveToBall")) {
            agent.currentBehavior.AddReward(1.0f);

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

    public VolleyballManager GetTeamManager(Team team)
    {
        if (team == Team.Blue)
        {
            return blueManager.GetComponent<VolleyballManager>();
        }
        else if (team == Team.Purple)
        {
            return purpleManager.GetComponent<VolleyballManager>();
        }
        else
        {
            return null;
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
        if (winner == Team.Default) {
            behaviorStatistics.OnStatisticEvent(this, blueAgents, StatisticEvent.Tie);
            behaviorStatistics.OnStatisticEvent(this, purpleAgents, StatisticEvent.Tie);
        }
        else {
            behaviorStatistics.OnStatisticEvent(this, GetTeamPlayers(winner), StatisticEvent.Win);
            behaviorStatistics.OnStatisticEvent(this, GetTeamPlayers(GetOpponentTeam(winner)), StatisticEvent.Lose);

            // apply reward to winner team manager
            GetTeamManager(winner).AddReward(1f);

            // apply penalty to loser team manager
            GetTeamManager(GetOpponentTeam(winner)).AddReward(-1f);
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
                if (LastHitterAgent != null && LastHitterAgent.BehaviorNameEquals("1v1")) {
                    LastHitterAgent.currentBehavior.AddReward(-0.009f);
                }

                if (LastHitterAgent != null && LastHitterAgent.BehaviorNameEquals("SendBallTo")) {
                    LastHitterAgent.currentBehavior.AddReward(-0.5f);
                }

                if (LastHitterAgent != null) {
                    behaviorStatistics.OnStatisticEvent(this, new List<VolleyballAgent> {LastHitterAgent}, StatisticEvent.MakeMistake);
                }

                UpdateWinLoseStatistics(GetOpponentTeam(lastHitterTeam));

                // end episode
                EndAllEpisodes();
                break;

            case Event.HitBlueGoal:
                // blue wins
                ResolveHitIntoGoal(Team.Blue);

                // turn floor blue
                StartCoroutine(GoalScoredSwapGroundMaterial(volleyballSettings.blueGoalMaterial, RenderersList, .5f));

                // end episode
                EndAllEpisodes();
                break;

            case Event.HitPurpleGoal:
                // purple wins
                ResolveHitIntoGoal(Team.Purple);

                // turn floor purple
                StartCoroutine(GoalScoredSwapGroundMaterial(volleyballSettings.purpleGoalMaterial, RenderersList, .5f));

                // end episode
                EndAllEpisodes();

                break;

            case Event.HitIntoBlueArea:
                if (lastHitterTeam == Team.Purple)
                {
                    GetTeamManager(Team.Purple).AddReward(0.25f);

                    if (LastHitterAgent.BehaviorNameEquals("1v1")) {
                        LastHitterAgent.currentBehavior.AddReward(0.1f);
                    }

                    behaviorStatistics.OnStatisticEvent(this, new List<VolleyballAgent> {LastHitterAgent}, StatisticEvent.SendBall);
                }
                break;

            case Event.HitIntoPurpleArea:
                if (lastHitterTeam == Team.Blue)
                {
                    GetTeamManager(Team.Blue).AddReward(0.25f);

                    if (LastHitterAgent.BehaviorNameEquals("1v1")) {
                        LastHitterAgent.currentBehavior.AddReward(0.1f);
                    }

                    behaviorStatistics.OnStatisticEvent(this, new List<VolleyballAgent> {LastHitterAgent}, StatisticEvent.SendBall);
                }
                break;
        }
    }

    private void ResolveHitIntoGoal(Team winnerTeam) {
        Team loserTeam = GetOpponentTeam(winnerTeam);

        // loser team misses ball
        if (lastHitterTeam != loserTeam) {
            GetTeamPlayers(loserTeam).ForEach(agent => {

                if (agent.BehaviorNameEquals("1v1")) {
                    agent.currentBehavior.AddReward(-1f);
                }

                if (agent.BehaviorNameEquals("MoveToBall")) {
                    // penalty according to distance from ball
                    agent.currentBehavior.AddReward(-0.1f * Vector3.Distance(agent.transform.position, ball.transform.position));
                }

                if (agent.BehaviorNameEquals("SendBallTo")) {
                    // penalty according to distance from target
                    agent.currentBehavior.AddReward(-1f);
                }
            });
            
            behaviorStatistics.OnStatisticEvent(this, GetTeamPlayers(loserTeam), StatisticEvent.MissBall);

        }

        // last hitter agent scores into goal with successfull hit
        if (lastHitterTeam == winnerTeam)
        {
            if (LastHitterAgent.BehaviorNameEquals("1v1")) {
                LastHitterAgent.currentBehavior.AddReward(1f);
            }
        }

        // last hitter agent makes mistake and scores into opponents goal
        if (lastHitterTeam == loserTeam)
        {
            behaviorStatistics.OnStatisticEvent(this, new List<VolleyballAgent> {LastHitterAgent}, StatisticEvent.MakeMistake);
            
            if (LastHitterAgent.BehaviorNameEquals("1v1")) {
                LastHitterAgent.currentBehavior.AddReward(-0.009f);
            }
        }

        // reward for last hitter agent who tries to send ball to target
        if (LastHitterAgent != null && LastHitterAgent.BehaviorNameEquals("SendBallTo")) {
            float distanceToTarget = Vector3.Distance(ball.transform.position, LastHitterAgent.ManagerTarget);
            if (distanceToTarget < 1.5f) {
                LastHitterAgent.currentBehavior.AddReward(1f);
            }
            else {
                LastHitterAgent.currentBehavior.AddReward(-0.05f * distanceToTarget);
            }
            LastHitterAgent.currentBehavior.AddReward(ball.GetComponent<VolleyballController>().highestPoint * 0.025f);
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
                blueAgents[i].currentBehavior.EpisodeInterrupted();
            }

            for (int i = 0; i < purpleAgents.Count; i++)
            {
                purpleAgents[i].currentBehavior.EpisodeInterrupted();
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
            blueAgents[i].currentBehavior.EndEpisode();
        }

        for (int i = 0; i < purpleAgents.Count; i++)
        {
            purpleAgents[i].currentBehavior.EndEpisode();
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
        LastHitterAgent = null;

        foreach (var agent in blueAgents)
        {
            // randomise starting positions and rotations
            var randomPosX = Random.Range(-5f, 5f);
            var randomPosZ = Random.Range(-5f, 5f);
            var randomPosY = Random.Range(0.5f, 1f); // depends on jump height
            var randomRot = Random.Range(-45f, 45f);

            agent.currentBehavior.transform.localPosition = new Vector3(randomPosX, randomPosY, randomPosZ);
            agent.currentBehavior.transform.eulerAngles = new Vector3(0, randomRot, 0);

            agent.currentBehavior.GetComponent<Rigidbody>().velocity = default(Vector3);
        }

        foreach (var agent in purpleAgents)
        {

            // randomise starting positions and rotations
            var randomPosX = Random.Range(-5f, 5f);
            var randomPosZ = Random.Range(-5f, 5f);
            var randomPosY = Random.Range(0.5f, 1f); // depends on jump height
            var randomRot = Random.Range(-45f, 45f);

            agent.currentBehavior.transform.localPosition = new Vector3(randomPosX, randomPosY, randomPosZ);
            agent.currentBehavior.transform.eulerAngles = new Vector3(0, randomRot, 0);

            agent.currentBehavior.GetComponent<Rigidbody>().velocity = default(Vector3);
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
        var randomPosY = Random.Range(5f, 8f);

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

        if (volleyballSettings.trainingModeName == "SendBallTo") {
            // revert ball spawn side
            ballSpawnSide = -1 * ballSpawnSide; 

            randomPosZ = Random.Range(-volleyballSettings.ballResetMaxLocation, volleyballSettings.ballResetMaxLocation);

            randomPosX = (ballSpawnSide == -1 ? blueAgents[0].transform.position.x : purpleAgents[0].transform.position.x) + randomPosX;
            randomPosZ = (ballSpawnSide == -1 ? blueAgents[0].transform.position.z : purpleAgents[0].transform.position.z) + randomPosZ;

            ball.transform.position = new Vector3(randomPosX, randomPosY, randomPosZ);

            blueAgents[0].GetComponent<Rigidbody>().velocity = new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));
            purpleAgents[0].GetComponent<Rigidbody>().velocity = new Vector3(Random.Range(-3f, 3f), 0, Random.Range(-3f, 3f));

            blueAgents[0].ActiveTarget = false;
            purpleAgents[0].ActiveTarget = false;
            blueAgents[0].ManagerTargetPlane.SetActive(false);
            purpleAgents[0].ManagerTargetPlane.SetActive(false);
            blueAgents[0].ManagerTarget = blueAgents[0].transform.position;
            purpleAgents[0].ManagerTarget = purpleAgents[0].transform.position;
        }

        ballRb.angularVelocity = Vector3.zero;
        ballRb.velocity = new Vector3(randomVelX, randomVelY, randomVelZ);

        ballRb.GetComponent<VolleyballController>().highestPoint = 0;
    }
}
