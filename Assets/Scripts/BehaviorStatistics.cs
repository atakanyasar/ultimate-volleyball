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


    private void SetRandomModel(VolleyballAgent agent)
    {
        var randomModel = modelAssets[Random.Range(0, modelAssets.Count)];
        agent.SetModel("1v1", randomModel);
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

            blueAgents.ForEach(SetRandomModel);
            purpleAgents.ForEach(SetRandomModel);
        }
    }

    public void OnStatisticEvent(VolleyballEnvController env, VolleyballAgent agent, StatisticEvent statisticEvent)
    {
        if (!keepStats) return;

        switch (statisticEvent)
        {
            case StatisticEvent.Win:
                Win(agent);
                break;
            case StatisticEvent.Lose:
                Lose(agent);
                break;
            case StatisticEvent.Tie:
                Tie(agent);
                break;
            case StatisticEvent.TouchBall:
                TouchBall(agent);
                break;
            case StatisticEvent.MissBall:
                MissBall(agent);
                break;
            case StatisticEvent.SendBall:
                SendBall(agent);
                break;
            case StatisticEvent.MakeMistake:
                MakeMistake(agent);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void Win(VolleyballAgent winner) {
        if (!keepStats) return;

        statistics[winner.GetModelName()].wins++;
        statistics[winner.GetModelName()].gamesPlayed++;

        SetRandomModel(winner);
    }

    public void Lose(VolleyballAgent loser) {
        if (!keepStats) return;

        statistics[loser.GetModelName()].losses++;
        statistics[loser.GetModelName()].gamesPlayed++;

        SetRandomModel(loser);
    }

    public void Tie(VolleyballAgent agent) {
        if (!keepStats) return;

        statistics[agent.GetModelName()].ties++;
        statistics[agent.GetModelName()].gamesPlayed++;

        SetRandomModel(agent);
    }

    public void TouchBall(VolleyballAgent agent) {
        if (!keepStats) return;

        statistics[agent.GetModelName()].touches++;

        // print agent location with behavior name to the log file named "ballTouches.txt"
        string agentLocation = agent.transform.localPosition.ToString();
        string behaviorName = agent.GetModelName();

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

        statistics[agent.GetModelName()].misses++;
    }

    private void SendBall(VolleyballAgent agent) {
        if (!keepStats) return;

        statistics[agent.GetModelName()].sendBalls++;
    }

    private void MakeMistake(VolleyballAgent agent) {
        if (!keepStats) return;

        statistics[agent.GetModelName()].mistakes++;
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

                Debug.Log($"{key}: {value.gamesPlayed} games played, win rate {winrate}%, {value.touches} touches, {missrate}% lose caused by miss, {sendrate}% successfull sends, {mistakeRate}% mistakes per touch"); 

                string logLine = $"{{'{key}': {{'gamesPlayed': {value.gamesPlayed}, 'winRate': {winrate}, 'touches': {value.touches}, 'missRate': {missrate}, 'sendRate': {sendrate}, 'mistakeRate': {mistakeRate}}}}}\n";
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