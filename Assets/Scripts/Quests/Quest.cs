using System;

[Serializable]
public class Quest
{
    public string Id;
    public string Name;
    public string Description;
    public int HonorReward;

    public Quest(string id, string name, string description, int honorReward)
    {
        Id = id;
        Name = name;
        Description = description;
        HonorReward = honorReward;
    }
}
