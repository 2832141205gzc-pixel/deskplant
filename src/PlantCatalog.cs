using System;

internal sealed class PlantSpecies
{
    internal readonly string Id, Name, ScientificName, Introduction;
    internal readonly string[] StageNames, Resources;
    internal readonly int[] Milestones;
    internal readonly double FoliageBottom;
    internal int HarvestMinutes { get; private set; }
    internal int FruitCapacity { get; private set; }
    internal int PreviousBatchYield { get; private set; }
    internal long FruitIntervalTicks { get { return HarvestMinutes * TimeSpan.TicksPerMinute / FruitCapacity; } }
    internal bool IsCrop { get { return HarvestMinutes > 0; } }
    internal int FinalStage { get { return StageNames.Length - 1; } }

    internal PlantSpecies(string id, string name, string scientificName, string introduction,
                          string[] stageNames, int[] milestones, double foliageBottom, params string[] resources)
    {
        Id = id;
        Name = name;
        ScientificName = scientificName;
        Introduction = introduction;
        StageNames = stageNames;
        Milestones = milestones;
        FoliageBottom = foliageBottom;
        Resources = resources;
    }

    internal int StageAt(double progress)
    {
        for (var stage = FinalStage; stage > 0; stage--)
            if (progress >= Milestones[stage]) return stage;
        return 0;
    }

    internal static PlantSpecies Crop(string id, string name, string scientificName, string introduction,
                                      string[] stages, int[] milestones, double rim, int minutes, int capacity, int previousYield)
    {
        return new PlantSpecies(id, name, scientificName, introduction, stages, milestones, rim, "DeskPlant.Crop." + id + ".Stages") {
            HarvestMinutes = minutes, FruitCapacity = capacity, PreviousBatchYield = previousYield
        };
    }
}

internal static class PlantCatalog
{
    internal static readonly PlantSpecies Jade = new PlantSpecies("jade", "玉树", "Crassula ovata",
        "玉树原产南部非洲。成对的椭圆叶片肥厚多汁，可以储存水分。随着植株长大，茎会逐渐木质化，形成像小树一样的冠形。",
        new[] { "幼苗", "成长中", "成熟" }, new[] { 0, 25, 100 }, .49,
        "DeskPlant.JadeSeedling", "DeskPlant.JadeYoung", "DeskPlant.JadePlant");

    internal static readonly PlantSpecies[] All = {
        Jade,
        new PlantSpecies("snake", "虎尾兰", "Dracaena trifasciata",
            "虎尾兰的叶片直立如剑，横向深浅相间的斑纹像虎尾。新叶从基部抽出，有些栽培品种的叶缘还镶着一圈金边。",
            new[] { "幼株", "成熟" }, new[] { 0, 100 }, .64, "DeskPlant.SnakeStages"),
        new PlantSpecies("oxalis", "紫叶酢浆草", "Oxalis triangularis",
            "三枚三角形小叶组成一片紫叶，展开时像停落的蝴蝶。叶片入夜往往会合拢，明亮时重新展开，花朵通常呈淡粉色或白色。",
            new[] { "幼株", "开花" }, new[] { 0, 100 }, .64, "DeskPlant.OxalisStages"),
        PlantSpecies.Crop("passion-fruit", "鸡蛋果", "Passiflora edulis",
            "鸡蛋果也叫百香果。藤蔓用卷须攀爬，花中央有一圈醒目的丝状副花冠。紫皮品种成熟后果皮转紫，果肉芳香。",
            new[] { "幼苗", "攀藤", "开花", "结果" }, new[] { 0, 25, 62, 100 }, .61, 75, 3, 1),
        PlantSpecies.Crop("pineapple", "菠萝", "Ananas comosus",
            "菠萝的长叶从基部围成莲座，花序从中央抽出。许多小花发育后连成一枚聚花果，表面一个个果眼留下了小花的痕迹。",
            new[] { "叶丛", "抽花", "青果", "成熟" }, new[] { 0, 45, 72, 100 }, .62, 90, 3, 1),
        PlantSpecies.Crop("blueberry", "蓝莓", "Vaccinium spp.",
            "蓝莓是一类浆果灌木，小花常像倒挂的铃铛。果实由青转蓝，成熟时表面常覆一层薄薄的天然白霜，果顶还能看到宿萼。",
            new[] { "幼株", "铃花", "青果", "蓝果" }, new[] { 0, 25, 64, 100 }, .59, 60, 5, 4),
        PlantSpecies.Crop("strawberry", "草莓", "Fragaria × ananassa",
            "草莓有三出复叶，通常开白花。我们吃到的红色部分是膨大的花托，表面一个个像种子的小点是瘦果。",
            new[] { "幼苗", "展叶", "开花", "结果" }, new[] { 0, 25, 60, 100 }, .59, 45, 4, 2),
        new PlantSpecies("aloe", "芦荟", "Aloe vera",
            "芦荟的肉质叶围成莲座，叶缘有小齿，叶内的凝胶状组织能储存水分。长成的植株有时会从基部生出小侧芽。",
            new[] { "幼株", "成熟" }, new[] { 0, 100 }, .65, "DeskPlant.AloeStages"),
        new PlantSpecies("lavender", "薰衣草", "Lavandula spp.",
            "薰衣草的细长叶片和花都带香气，细小的花聚成直立花穗。常见栽培品种多开紫花，开花时也会吸引蜜蜂等访花昆虫。",
            new[] { "幼株", "花穗", "盛开" }, new[] { 0, 46, 100 }, .72, "DeskPlant.LavenderStages"),
        new PlantSpecies("mini-rose", "矮生月季", "Rosa",
            "矮生月季是株形紧凑的一类栽培月季，短枝上能开出小巧的花。不同品种的花色和花瓣层数各异，许多品种能反复开花。",
            new[] { "幼株", "展叶", "花苞", "盛开" }, new[] { 0, 25, 63, 100 }, .65, "DeskPlant.MiniRoseStages"),
        PlantSpecies.Crop("kumquat", "金桔", "Citrus japonica",
            "金桔是常绿的柑橘类植物，叶片有光泽，白色小花带香气。果实成熟后变成金黄或橙色，一枚枚挂在枝间。",
            new[] { "幼株", "开花", "结果" }, new[] { 0, 45, 100 }, .67, 70, 4, 2),
        PlantSpecies.Crop("cherry-tomato", "矮生小番茄", "Solanum lycopersicum",
            "矮生小番茄株形紧凑，开黄色的星形小花。果实常成串挂在枝间，同一串上也能同时看到青果和熟果。",
            new[] { "幼苗", "展叶", "开花", "结果" }, new[] { 0, 25, 62, 100 }, .64, 50, 5, 3)
    };

    internal static PlantSpecies Find(string id)
    {
        return Array.Find(All, plant => plant.Id == id);
    }
}
