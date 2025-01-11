using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.Sentis;
using UnityEngine;
using Random = UnityEngine.Random;

public enum StatisticEvent
{
    Win,
    TouchBall,
    MissBall,
    PassBall,
    SendBallToOwnArea,
    SendBallToOppositeArea,
    SendBallOutOfBounds,
};

public class BehaviorStatistics : MonoBehaviour
{
    public record MatchStatistics 
    {
        public int wins;
        public int touches;
        public int passes;
        public int misses;
        public int sendBallSuccess;
        public int sendBallFail;
        public int sendBallOut;
    };

    private Dictionary<string, Dictionary<string, MatchStatistics>> matchStatistics = new Dictionary<string, Dictionary<string, MatchStatistics>>();

    public bool keepStats = false;
    public string ballTouchesLogFileName = "statistics/ballTouches.txt";
    public string statisticsLogFileName = "statistics/statistics.txt";
    private FileStream ballTouchesLogFileStream;
    private FileStream statisticsLogFileStream;

    public List<ModelAsset> modelAssets;
    private List<VolleyballEnvController> volleyballAreas;

    public int matchesPerSet = 11;

    private ModelAsset SetRandomModel(VolleyballManager manager)
    {
        var randomModel = modelAssets[Random.Range(0, modelAssets.Count)];
        manager.SetModel("Manager", randomModel);
        return randomModel;
    }

    private void ResetMatchStatistics(VolleyballEnvController area)
    {
        VolleyballManager blueManager = area.blueManager.GetComponent<VolleyballManager>();
        VolleyballManager purpleManager = area.purpleManager.GetComponent<VolleyballManager>();

        var blueModel = SetRandomModel(blueManager);
        var purpleModel = SetRandomModel(purpleManager);

        blueManager.teamName = (blueModel == null ? "Default" : blueModel.name) + " (Blue)";
        purpleManager.teamName = (purpleModel == null ? "Default" : purpleModel.name) + " (Purple)";

        if (!matchStatistics.ContainsKey(blueManager.teamName))
            matchStatistics[blueManager.teamName] = new Dictionary<string, MatchStatistics>();
        if (!matchStatistics.ContainsKey(purpleManager.teamName))
            matchStatistics[purpleManager.teamName] = new Dictionary<string, MatchStatistics>();
        
        if (!matchStatistics[blueManager.teamName].ContainsKey(purpleManager.teamName))
        {
            matchStatistics[blueManager.teamName][purpleManager.teamName] = new MatchStatistics();
            matchStatistics[purpleManager.teamName][blueManager.teamName] = new MatchStatistics();
        }
        else 
        {
            ResetMatchStatistics(area);
        }
        
    }

    private void Start()
    {
        if (!keepStats) return;

        // save team names
        volleyballAreas = GetComponentsInChildren<VolleyballEnvController>().ToList();

        volleyballAreas.ForEach(area => ResetMatchStatistics(area));

        ballTouchesLogFileStream = new FileStream(ballTouchesLogFileName, FileMode.OpenOrCreate, FileAccess.Write);
        statisticsLogFileStream = new FileStream(statisticsLogFileName, FileMode.OpenOrCreate, FileAccess.Write);

    }

    public void OnStatisticEvent(VolleyballEnvController env, VolleyballManager team, VolleyballManager opponentTeam, StatisticEvent statisticEvent)
    {
        if (!keepStats) return;

        string teamName = team.teamName;
        string opponentTeamName = (opponentTeam != null ? opponentTeam.teamName : "");

        if (teamName == opponentTeamName) {
            opponentTeamName = teamName + " (1)";
        }

        switch (statisticEvent)
        {
            case StatisticEvent.Win: 
                matchStatistics[teamName][opponentTeamName].wins++;
                handleMatchEnd(env, teamName, opponentTeamName);
                break;
            case StatisticEvent.TouchBall:
                TouchBall(env, teamName, opponentTeamName);
                break;
            case StatisticEvent.PassBall:
                matchStatistics[teamName][opponentTeamName].passes++;
                break;
            case StatisticEvent.MissBall:
                matchStatistics[teamName][opponentTeamName].misses++;
                break;
            case StatisticEvent.SendBallToOppositeArea:
                matchStatistics[teamName][opponentTeamName].sendBallSuccess++;
                break;
            case StatisticEvent.SendBallToOwnArea:
                matchStatistics[teamName][opponentTeamName].sendBallFail++;
                break;
            case StatisticEvent.SendBallOutOfBounds:
                matchStatistics[teamName][opponentTeamName].sendBallOut++;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void handleMatchEnd(VolleyballEnvController envController, string teamName, string opponentTeamName)
    {
        if (matchStatistics[teamName][opponentTeamName].wins == matchesPerSet || matchStatistics[opponentTeamName][teamName].wins == matchesPerSet)
        {
            PrintMatchStatistics(teamName, opponentTeamName);

            matchStatistics[teamName].Remove(opponentTeamName);
            matchStatistics[opponentTeamName].Remove(teamName);

            ResetMatchStatistics(envController);
        }
    }

    private void TouchBall(VolleyballEnvController volleyballEnv, string teamName, string opponentTeamName) {
        if (!keepStats) return;

        matchStatistics[teamName][opponentTeamName].touches++;

        // print agent location with behavior name to the log file named "ballTouches.txt"
        string ballTouchLocation = volleyballEnv.ball.transform.localPosition.ToString();

        string logLine = $"{{'teamName': '{teamName}', 'location': '{ballTouchLocation}'}}\n";

        // convert the string to a byte array
        byte[] logLineBytes = System.Text.Encoding.ASCII.GetBytes(logLine);

        // write the byte array to the file
        ballTouchesLogFileStream.Write(logLineBytes, 0, logLineBytes.Length);

        // flush the buffer to the file
        ballTouchesLogFileStream.Flush();
    }

    private void PrintMatchStatistics(string teamName, string opponentTeamName)
    {
        string printLine = $"{{'{teamName} vs {opponentTeamName}': {{'wins': {matchStatistics[teamName][opponentTeamName].wins}, " +
                   $"'touches': {matchStatistics[teamName][opponentTeamName].touches}, " +
                   $"'passes': {matchStatistics[teamName][opponentTeamName].passes}, " +
                   $"'misses': {matchStatistics[teamName][opponentTeamName].misses}, " +
                   $"'sendBallSuccess': {matchStatistics[teamName][opponentTeamName].sendBallSuccess}, " +
                   $"'sendBallFail': {matchStatistics[teamName][opponentTeamName].sendBallFail}, " +
                   $"'sendBallOut': {matchStatistics[teamName][opponentTeamName].sendBallOut}}}}}\n";
        printLine += $"{{'{opponentTeamName} vs {teamName}': {{'wins': {matchStatistics[opponentTeamName][teamName].wins}, " +
                     $"'touches': {matchStatistics[opponentTeamName][teamName].touches}, " +
                     $"'passes': {matchStatistics[opponentTeamName][teamName].passes}, " +
                     $"'misses': {matchStatistics[opponentTeamName][teamName].misses}, " +
                     $"'sendBallSuccess': {matchStatistics[opponentTeamName][teamName].sendBallSuccess}, " +
                     $"'sendBallFail': {matchStatistics[opponentTeamName][teamName].sendBallFail}, " +
                     $"'sendBallOut': {matchStatistics[opponentTeamName][teamName].sendBallOut}}}}}\n";
        byte[] printLineBytes = System.Text.Encoding.ASCII.GetBytes(printLine);
        statisticsLogFileStream.Write(printLineBytes, 0, printLineBytes.Length);
        statisticsLogFileStream.Flush();

        Debug.Log(printLine);
        
    }

    // on exit, close the log file
    private void OnApplicationQuit()
    {
        if (!keepStats) return;

        ballTouchesLogFileStream.Close();
        statisticsLogFileStream.Close();
    }



}