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
    Lose,
    Tie,
    TouchBall,
    MissBall,
    SendBall,
    MakeMistake,
};

public class BehaviorStatistics : MonoBehaviour
{
    public record Statistics 
    {
        public int gamesPlayed;
        public int wins;
        public int losses;
        public int ties;
        public int touches;
        public int misses;
        public int sendBalls;
        public int mistakes;
    };

    public Dictionary<string, Statistics> statistics = new Dictionary<string, Statistics>();

    public bool keepStats = false;
    public string ballTouchesLogFileName = "statistics/ballTouches.txt";
    public string statisticsLogFileName = "statistics/statistics.txt";
    private FileStream ballTouchesLogFileStream;
    private FileStream statisticsLogFileStream;

    public List<ModelAsset> modelAssets;
    private List<VolleyballEnvController> volleyballAreas;

    private int updateCounter = 0;


    private void SetRandomModel(List<VolleyballAgent> agents)
    {
        var randomModel = modelAssets[Random.Range(0, modelAssets.Count)];
        // agents.ForEach(agent => agent.SetModel("1v1", randomModel));
    }

    private void Start()
    {
        if (!keepStats) return;

        modelAssets.ForEach(modelAsset => statistics.Add(modelAsset.name, new Statistics()));
        volleyballAreas = GetComponentsInChildren<VolleyballEnvController>().ToList();

        ballTouchesLogFileStream = new FileStream(ballTouchesLogFileName, FileMode.OpenOrCreate, FileAccess.Write);

        foreach (var volleyballArea in volleyballAreas)
        {
            var blueAgents = volleyballArea.blueManager.GetComponent<VolleyballManager>().GetAgents();
            var purpleAgents = volleyballArea.purpleManager.GetComponent<VolleyballManager>().GetAgents();

            SetRandomModel(blueAgents);
            SetRandomModel(purpleAgents);
        }
    }

    public void OnStatisticEvent(VolleyballEnvController env, List<VolleyballAgent> agents, StatisticEvent statisticEvent)
    {
        if (!keepStats) return;

        switch (statisticEvent)
        {
            case StatisticEvent.Win:
                agents.ForEach(agent => Win(agent));
                SetRandomModel(agents);
                break;
            case StatisticEvent.Lose:
                agents.ForEach(agent => Lose(agent));
                SetRandomModel(agents);
                break;
            case StatisticEvent.Tie:
                agents.ForEach(agent => Tie(agent));
                SetRandomModel(agents);
                break;
            case StatisticEvent.TouchBall:
                agents.ForEach(agent => TouchBall(agent));
                break;
            case StatisticEvent.MissBall:
                agents.ForEach(agent => MissBall(agent));
                break;
            case StatisticEvent.SendBall:
                agents.ForEach(agent => SendBall(agent));
                break;
            case StatisticEvent.MakeMistake:
                agents.ForEach(agent => MakeMistake(agent));
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void Win(VolleyballAgent winner) {
        if (!keepStats) return;

        statistics[winner.currentBehavior.GetModelName()].wins++;
        statistics[winner.currentBehavior.GetModelName()].gamesPlayed++;
    }

    private void Lose(VolleyballAgent loser) {
        if (!keepStats) return;

        statistics[loser.currentBehavior.GetModelName()].losses++;
        statistics[loser.currentBehavior.GetModelName()].gamesPlayed++;
    }

    private void Tie(VolleyballAgent agent) {
        if (!keepStats) return;

        statistics[agent.currentBehavior.GetModelName()].ties++;
        statistics[agent.currentBehavior.GetModelName()].gamesPlayed++;
    }

    private void TouchBall(VolleyballAgent agent) {
        if (!keepStats) return;

        statistics[agent.currentBehavior.GetModelName()].touches++;

        // print agent location with behavior name to the log file named "ballTouches.txt"
        string agentLocation = agent.transform.localPosition.ToString();
        string behaviorName = agent.currentBehavior.GetModelName();

        string logLine = $"{{'behaviorName': '{behaviorName}', 'location': '{agentLocation}'}}\n";

        // convert the string to a byte array
        byte[] logLineBytes = System.Text.Encoding.ASCII.GetBytes(logLine);

        // write the byte array to the file
        ballTouchesLogFileStream.Write(logLineBytes, 0, logLineBytes.Length);

        // flush the buffer to the file
        ballTouchesLogFileStream.Flush();
    }

    private void MissBall(VolleyballAgent agent) {
        if (!keepStats) return;

        statistics[agent.currentBehavior.GetModelName()].misses++;
    }

    private void SendBall(VolleyballAgent agent) {
        if (!keepStats) return;

        statistics[agent.currentBehavior.GetModelName()].sendBalls++;
    }

    private void MakeMistake(VolleyballAgent agent) {
        if (!keepStats) return;

        statistics[agent.currentBehavior.GetModelName()].mistakes++;
    }





    private void Update()
    {
        if (!keepStats) return;

        if (updateCounter % 1000 == 0)
        {
            statisticsLogFileStream = new FileStream(statisticsLogFileName, FileMode.OpenOrCreate, FileAccess.Write);

            Debug.Log("Statistics:");
            foreach (var (key, value) in statistics)
            {
                float winrate = (float)value.wins / value.gamesPlayed * 100;
                float missrate = (float)value.misses / value.losses * 100;
                float sendrate = (float)value.sendBalls / value.touches * 100;
                float mistakeRate = (float)value.mistakes / value.touches * 100;
                float touchesPerGame = (float)value.touches / value.gamesPlayed;

                Debug.Log($"{key}: {value.gamesPlayed} games played, win rate {winrate}%, {touchesPerGame} touches per game, {missrate}% lose caused by miss, {sendrate}% successful sends, {mistakeRate}% mistakes per touch"); 

                string logLine = $"{{'{key}': {{'gamesPlayed': {value.gamesPlayed}, 'winRate': {winrate}, 'touchesPerGame': {touchesPerGame}, 'missRate': {missrate}, 'sendRate': {sendrate}, 'mistakeRate': {mistakeRate}}}}}\n";
                byte[] logLineBytes = System.Text.Encoding.ASCII.GetBytes(logLine);
                statisticsLogFileStream.Write(logLineBytes, 0, logLineBytes.Length);
                statisticsLogFileStream.Flush();
            }

            statisticsLogFileStream.Close();
        }
        
        updateCounter++;

    }

    // on exit, close the log file
    private void OnApplicationQuit()
    {
        if (!keepStats) return;

        ballTouchesLogFileStream.Close();
        statisticsLogFileStream.Close();
    }



}