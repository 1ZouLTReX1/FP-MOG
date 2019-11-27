﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using UnityEngine;


public static class StopWacthTime
{
    static Stopwatch stopWatch;

    static StopWacthTime()
    {
        stopWatch = new Stopwatch();
        stopWatch.Start();
    }

    public static float Time { get => stopWatch.ElapsedMilliseconds/1000f; }
}


public class ServerLoop
{
    const int NoMoreEvents = -1;
    float tickDuration = Time.fixedDeltaTime;
    int tick = 0;
    WorldManager wm;

    float lastTickStartTime = 0;
    float speedFactor = 0.5f;

    public GameObject playerPrefab;

    public GameObject AddPlayer(int Id)
    {
        GameObject obj = GameObject.Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
        obj.name = Id.ToString();
        return obj;
    }

    public ServerLoop(GameObject playerPrefab)
    {
        this.playerPrefab = playerPrefab;
        wm = new WorldManager();
    }

    // List of rays too.
    public void TakeSnapshot(List<Player> players)
    {
        wm.TakeSnapshot(players, tick);
    }

    public byte[] GetSnapshot()
    {
        return wm.Serialize();
    }

    public void Update(List<Player> players)
    {
        /*
        Update Clients and Apply User Commands
        Tick length is Time.fixedDeltaTime

        Remove used player's User Commands
        
        Take A Snap Shot of the updated world
        */

        tick++;

        foreach (Player player in players)
            ApplyVelocity(player);

        float startTickTime = StopWacthTime.Time;
        float endTickTime = startTickTime + tickDuration;

        UnityEngine.Debug.Log("Tick Duration " + tickDuration + " Start: " + startTickTime + " End: " + endTickTime);

        float curTime = startTickTime;
        float minorJump;

        float currEventsTime;
        float nextEventsTime;

        // init the list for the events indexes = > [ true for event in every player spot, false for no event needed].
        List<bool> currBoolsUserCommands = new List<bool>(new bool[players.Count]);
        List<bool> nextBoolsUserCommands = new List<bool>(new bool[players.Count]);

        List<ServerUserCommand> currUserCommands;
        List<int> playerEventIndexes = new List<int>(new int[players.Count]);

        // Simulate Till first event
        // or Till the end Tick Time if there is no event from the clients.
        currEventsTime = GetEventsMinimalTime(players, playerEventIndexes, currBoolsUserCommands);
        // Check if empty
        if (currEventsTime == NoMoreEvents)
            minorJump = tickDuration;
        else
            minorJump = (currEventsTime - lastTickStartTime);

        Physics2D.Simulate(minorJump);
        curTime += minorJump;

        while (curTime < endTickTime)
        {
            // Get all the events with minimal time.
            currUserCommands = GetEvents(players, playerEventIndexes, currBoolsUserCommands);

            currBoolsUserCommands = nextBoolsUserCommands;
            nextBoolsUserCommands = new List<bool>(new bool[players.Count]);

            nextEventsTime = Mathf.Min(GetEventsMinimalTime(players, playerEventIndexes, nextBoolsUserCommands), endTickTime);

            minorJump = nextEventsTime - currEventsTime;
            currEventsTime = nextEventsTime;

            ApplyUserCommands(currUserCommands);
            Physics2D.Simulate(minorJump);
            curTime += minorJump;
        }

        // Delete all events according to the indexes.
        for (int i = 0; i < players.Count; i++)
            players[i].userCommandList.RemoveRange(0, playerEventIndexes[i]);

        // take and store a snapshot of the world state TODO future
        lastTickStartTime = startTickTime;

        TakeSnapshot(players);
    }

    public float GetEventsMinimalTime(List<Player> players, List<int> eventsFromIndexes, List<bool> takeEvent)
    {
        float ret = NoMoreEvents;
        ServerUserCommand curr;
        if (players.Count == 0 || takeEvent.Count == 0)
            return ret;
        
        for (int i = 0; i < eventsFromIndexes.Count; i++)
        {
            curr = players[i].userCommandList.ElementAtOrDefault(eventsFromIndexes[i]);
            if (curr != null)
            {
                if (curr.serverRecTime < ret || ret == NoMoreEvents)
                    ret = curr.serverRecTime;
            }
        }

        for (int i = 0; i < eventsFromIndexes.Count; i++)
        {
            curr = players[i].userCommandList.ElementAtOrDefault(eventsFromIndexes[i]);
            // If there is an event and it equals to the ret time then that client event needs to be played.
            if (curr != null && curr.serverRecTime == ret)
                takeEvent[i] = true;
        }

        return ret;
    }

    public List<ServerUserCommand> GetEvents(List<Player> players, List<int> eventsFromIndexes, List<bool> takeEvent)
    {
        List<ServerUserCommand> ret = new List<ServerUserCommand>();

        if (players.Count == 0 || takeEvent.Count == 0)
            return ret;

        for (int i = 0; i < eventsFromIndexes.Count; i++)
        {
            if (takeEvent[i]) 
            {
                ret.Add(players[i].userCommandList[eventsFromIndexes[i]]);
                eventsFromIndexes[i]++;
            }
        }

        return ret;
    }

    public void ApplyUserCommands(List<ServerUserCommand> commands)
    {
        foreach (ServerUserCommand cmd in commands)
            ApplyGameplay(cmd.player, cmd.ie);
    }

    public void ApplyGameplay(Player player, InputEvent ie)
    {
        float zAngle = Mathf.Repeat(ie.zAngle, 360);
        player.obj.transform.rotation = Quaternion.Euler(0, 0, zAngle);
        //ApplyVelocity(player);
    }

    public void ApplyVelocity(Player player)
    {
        float zAngle = (player.obj.transform.rotation.eulerAngles.z) * Mathf.Deg2Rad;
        player.rb.velocity = new Vector2(Mathf.Cos(zAngle) * speedFactor, Mathf.Sin(zAngle) * speedFactor);
    }
}

