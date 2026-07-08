using System;

[Serializable]
public class Quest
{
    public string Id;
    public string Name;
    public string Description;
    public int HonorReward;
    public string ObjectiveStatKey;
    public int ObjectiveTarget;

    public Quest(string id, string name, string description, int honorReward,
                 string objectiveStatKey = null, int objectiveTarget = 0)
    {
        Id = id;
        Name = name;
        Description = description;
        HonorReward = honorReward;
        ObjectiveStatKey = objectiveStatKey;
        ObjectiveTarget = objectiveTarget;
    }

    public bool HasObjective => !string.IsNullOrEmpty(ObjectiveStatKey) && ObjectiveTarget > 0;
}
