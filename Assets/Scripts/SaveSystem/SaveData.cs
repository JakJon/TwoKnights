using System;
using System.Collections.Generic;

[Serializable]
public class SaveData
{
    public int version = 4;
    public int gold = 0;
    public int furthestWave = 0;
    public int honorPoints = 0;
    public int knightRank = 1;
    public List<QuestCompletion> completedQuests = new List<QuestCompletion>();
    public List<StatEntry> stats = new List<StatEntry>();
}

[Serializable]
public class QuestCompletion
{
    public string questId;
    public string completedDate;
}

[Serializable]
public class StatEntry
{
    public string key;
    public int value;
}
