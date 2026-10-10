using UnityEngine;

public enum eGameState { title, howToPlay, dayIntro, playing, paused, dayResults, shop, victory }

// The design knobs for one workday. Edit these on the GameManager in the Inspector.
[System.Serializable]
public class DaySettings {
    public string   title = "Day 1";
    [TextArea( 2, 5 )]
    public string   intro;
    [Tooltip( "Money needed by closing time to pass the day" )]
    public int      goal = 80;
    [Tooltip( "What customers can order today" )]
    public eItem[]  menu = { eItem.bread, eItem.cookies };
    [Tooltip( "Average seconds between customers (shorter during the lunch rush)" )]
    public float    spawnInterval = 12f;
    [Range( 0, 1 )]
    public float    doubleOrderChance;
    [Range( 0, 1 )]
    public float    hurriedChance;
    [Range( 0, 1 )]
    public float    grandmaChance = 0.15f;
    public bool     hasCritic;
    public bool     berriesUnlocked;
}

// What happened during one day, shown on the results screen
[System.Serializable]
public class DayStats {
    public int  earnings;
    public int  tips;
    public int  served;
    public int  lost;
    public int  ordersTaken;
    public int  perfectBakes;
    public int  burnt;
    public int  wrongItems;
    public int  bestStreak;
    public int  catPets;
    public int  criticReview;   // 0 = the critic didn't come
}

public enum eUpgrade { turboOven, thirdOven, comfyChairs, quickWhisk, runningShoes, tipJar }

// Something to buy in the shop between days
[System.Serializable]
public class UpgradeDef {
    public eUpgrade id;
    public string   displayName;
    [TextArea( 2, 3 )]
    public string   description;
    public int      price;
    public Sprite   icon;
}
