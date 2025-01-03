using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.Sentis;
using UnityEngine;
using Random = UnityEngine.Random;

public class BehaviorStatistics : MonoBehaviour
{
    public record Statistics 
    {
        public int gamesPlayed;
        public int wins;
        public int losses;
        public int ties;
    };

    public Dictionary<string, Statistics> statistics = new Dictionary<string, Statistics>();

    public bool keepStats = false;

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

        foreach (var volleyballArea in volleyballAreas)
        {
            var blueAgents = volleyballArea.blueManager.GetComponent<VolleyballManager>().GetAgents();
            var purpleAgents = volleyballArea.purpleManager.GetComponent<VolleyballManager>().GetAgents();

            blueAgents.ForEach(SetRandomModel);
            purpleAgents.ForEach(SetRandomModel);
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

    private void Update()
    {
        if (!keepStats) return;

        if (updateCounter % 1000 == 0)
        {
            Debug.Log("Statistics:");
            foreach (var (key, value) in statistics)
            {
                float winrate = (float)value.wins / value.gamesPlayed * 100;
                Debug.Log($"{key}: {value.gamesPlayed} games played, win rate {winrate}%, {value.wins} wins, {value.losses} losses, {value.ties} ties");
            }
        }
        
        updateCounter++;

    }




}