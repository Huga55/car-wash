using UnityEngine;

namespace LittleGarage
{
    /// <summary>Everything that makes one vehicle unique. Authored as a ScriptableObject
    /// so adding a car never changes system code.</summary>
    [CreateAssetMenu(fileName = "VehicleDef", menuName = "LittleGarage/Vehicle")]
    public sealed class VehicleDef : ScriptableObject
    {
        public string id;
        public string displayNameRu;
        public VehicleKind kind;
        public Color bodyColor = Color.blue;
        public Color accentColor = Color.white;
        public float length = 4.4f;
        public float width = 1.85f;
        public float height = 1.45f;
        public float wheelRadius = 0.36f;
        [Range(0f, 1f)] public float dirt = 0.9f;
        public Color[] colors;
        public string[] rimStyles;
        public string[] decals;
        public PartKind[] serviceableParts;
        public float cameraDistance = 7.5f;
    }

    [CreateAssetMenu(fileName = "ToolDef", menuName = "LittleGarage/Tool")]
    public sealed class ToolDef : ScriptableObject
    {
        public WashTool tool;
        public string labelRu;
        public string hintRu;
        public string soundKey;
        public float radius = 0.075f;
        public Color color = Color.white;
    }

    /// <summary>Plain serializable step inside a job.</summary>
    [System.Serializable]
    public sealed class JobTaskDef
    {
        public TaskKind kind;
        public string titleRu;
        public string instructionRu;
        public WashTool[] tools;
        public PartKind part;
        public Color paintColor = Color.red;
        public bool optionalCustomization;
    }

    [CreateAssetMenu(fileName = "JobDef", menuName = "LittleGarage/Job")]
    public sealed class JobDef : ScriptableObject
    {
        public string id;
        public string titleRu;
        [TextArea] public string descriptionRu;
        public string vehicleId;
        public WorkZone zone;
        public JobTaskDef[] tasks;
        public int reward = 100;
    }

    /// <summary>Root content catalog. CreateDefault makes the game playable without
    /// hand-authored assets; designers can replace it with an asset in Resources.</summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "LittleGarage/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        public VehicleDef[] vehicles;
        public JobDef[] jobs;
        public ToolDef[] tools;

        public VehicleDef Vehicle(string id)
        {
            if (vehicles == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < vehicles.Length; i++)
                if (vehicles[i] != null && vehicles[i].id == id) return vehicles[i];
            return null;
        }

        public JobDef Job(string id)
        {
            if (jobs == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < jobs.Length; i++)
                if (jobs[i] != null && jobs[i].id == id) return jobs[i];
            return null;
        }

        public ToolDef Tool(WashTool tool)
        {
            if (tools == null) return null;
            for (int i = 0; i < tools.Length; i++)
                if (tools[i] != null && tools[i].tool == tool) return tools[i];
            return null;
        }

        private static Color C(byte r, byte g, byte b)
        {
            return new Color32(r, g, b, 255);
        }

        private static Color[] Palette(params Color[] colors) { return colors; }

        private static VehicleDef Vehicle(string id, string nameRu, VehicleKind kind,
            Color body, Color accent, float length, float width, float height,
            float wheel, float dirt, PartKind[] parts, float cam, Color[] colors)
        {
            VehicleDef v = CreateInstance<VehicleDef>();
            v.id = id; v.displayNameRu = nameRu; v.kind = kind;
            v.bodyColor = body; v.accentColor = accent;
            v.length = length; v.width = width; v.height = height;
            v.wheelRadius = wheel; v.dirt = dirt;
            v.serviceableParts = parts;
            v.cameraDistance = cam;
            v.colors = colors;
            v.rimStyles = new[] { "Стандарт", "Спортивные", "Хромированные" };
            v.decals = new[] { "Без наклейки", "Гоночная полоса", "Звезда" };
            return v;
        }

        private static JobTaskDef Wash(string title, string hint, params WashTool[] tools)
        {
            return new JobTaskDef
            {
                kind = TaskKind.Wash, titleRu = title, instructionRu = hint, tools = tools
            };
        }

        private static JobTaskDef Repair(PartKind part, string hint)
        {
            return new JobTaskDef
            {
                kind = TaskKind.Repair, part = part,
                titleRu = "Ремонт: " + part.Ru(), instructionRu = hint
            };
        }

        private static JobTaskDef Paint(Color color, string hint)
        {
            return new JobTaskDef
            {
                kind = TaskKind.Paint, paintColor = color,
                titleRu = "Покраска кузова", instructionRu = hint
            };
        }

        private static JobTaskDef Customize(string hint)
        {
            return new JobTaskDef
            {
                kind = TaskKind.Customize, titleRu = "Кастомизация", instructionRu = hint,
                optionalCustomization = true
            };
        }

        /// <summary>Builds the complete default catalog. Deterministic, allocation-light
        /// and independent of Resources so Play Mode always has content.</summary>
        public static GameConfig CreateDefault()
        {
            GameConfig cfg = CreateInstance<GameConfig>();

            Color[] bright = Palette(C(72, 156, 255), C(232, 66, 62), C(255, 204, 64),
                C(96, 200, 130), C(186, 118, 230), C(250, 250, 250));
            Color[] service = Palette(C(250, 250, 250), C(74, 118, 220), C(232, 66, 62),
                C(255, 190, 48), C(48, 52, 60));

            VehicleDef[] vs =
            {
                Vehicle("sedan", "Маленький седан", VehicleKind.Sedan,
                    C(72,156,255), C(40,92,170), 4.3f, 1.82f, 1.44f, 0.34f, 1f,
                    new[]{PartKind.WheelFL, PartKind.Engine, PartKind.Windshield, PartKind.HeadlightL}, 7.2f, bright),
                Vehicle("sports", "Спортивная машина", VehicleKind.Sports,
                    C(232,66,62), C(255,232,120), 4.5f, 1.94f, 1.22f, 0.36f, 0.8f,
                    new[]{PartKind.WheelFR, PartKind.Battery, PartKind.MirrorL}, 7.4f, bright),
                Vehicle("police", "Полицейская машина", VehicleKind.Police,
                    C(74,118,220), C(250,250,250), 4.7f, 1.88f, 1.46f, 0.36f, 0.85f,
                    new[]{PartKind.WheelFL, PartKind.HeadlightR, PartKind.Bumper}, 7.6f, service),
                Vehicle("suv", "Внедорожник", VehicleKind.SUV,
                    C(96,170,110), C(44,78,52), 4.9f, 2.0f, 1.78f, 0.42f, 1f,
                    new[]{PartKind.WheelRR, PartKind.Battery, PartKind.MirrorR}, 8.0f, bright),
                Vehicle("fire", "Пожарная машина", VehicleKind.FireTruck,
                    C(232,66,62), C(255,238,210), 6.4f, 2.3f, 2.6f, 0.48f, 0.9f,
                    new[]{PartKind.WheelRL, PartKind.HeadlightL, PartKind.Engine}, 9.6f, service),
                Vehicle("ambulance", "Скорая помощь", VehicleKind.Ambulance,
                    C(250,250,250), C(236,78,78), 5.4f, 2.05f, 2.1f, 0.4f, 0.8f,
                    new[]{PartKind.Windshield, PartKind.Battery, PartKind.MirrorL}, 8.7f, service),
                Vehicle("pickup", "Пикап", VehicleKind.Pickup,
                    C(255,150,56), C(120,92,64), 5.2f, 2.05f, 1.7f, 0.41f, 1f,
                    new[]{PartKind.WheelRR, PartKind.Bumper, PartKind.Engine}, 8.4f, bright),
                Vehicle("truck", "Грузовик", VehicleKind.Truck,
                    C(96,170,210), C(240,240,240), 7.6f, 2.5f, 3.0f, 0.52f, 1f,
                    new[]{PartKind.WheelFL, PartKind.Bumper, PartKind.Battery}, 10.8f, service),
                Vehicle("bus", "Автобус", VehicleKind.Bus,
                    C(255,176,48), C(62,110,190), 9.5f, 2.55f, 3.1f, 0.5f, 0.95f,
                    new[]{PartKind.Engine, PartKind.MirrorR, PartKind.HeadlightL}, 12.0f, service),
                Vehicle("dump", "Самосвал", VehicleKind.DumpTruck,
                    C(255,190,48), C(110,92,60), 7.0f, 2.5f, 2.9f, 0.52f, 1f,
                    new[]{PartKind.WheelRL, PartKind.MirrorL, PartKind.Engine}, 10.2f, bright)
            };
            cfg.vehicles = vs;

            cfg.tools = new[]
            {
                Tool(WashTool.Pressure, "Мойка высокого давления", "Веди струю по кузову, чтобы сбить грязь", "water", 0.14f, C(120,205,245)),
                Tool(WashTool.Foam, "Пеногенератор", "Покрой кузов густой пеной", "foam", 0.15f, C(245,250,255)),
                Tool(WashTool.Sponge, "Губка", "Протри кузов круговыми движениями", "sponge", 0.12f, C(255,220,140)),
                Tool(WashTool.Rinse, "Смывание", "Смой пену чистой водой", "rinse", 0.16f, C(160,225,255)),
                Tool(WashTool.Dryer, "Воздушная сушка", "Обсуши кузов тёплым воздухом", "air", 0.17f, C(255,245,210)),
                Tool(WashTool.Polisher, "Полировальная машинка", "Веди машинку по кузову до блеска", "polish", 0.14f, C(210,230,255))
            };
            cfg.jobs = new[]
            {
                Job("job01", "Грязный седан", "Машинка вся в грязи. Помоем её!", "sedan", WorkZone.WashBay, 100,
                    Wash("Смочить кузов", "Нажми и веди струю по кузову", WashTool.Pressure),
                    Wash("Нанести пену", "Покрой весь кузов пеной", WashTool.Foam),
                    Wash("Потереть губкой", "Протри грязные места", WashTool.Sponge),
                    Wash("Смыть пену", "Смой всю пену водой", WashTool.Rinse),
                    Wash("Высушить", "Обсуши кузов", WashTool.Dryer),
                    Wash("Полировать", "Веди полировку до блеска", WashTool.Polisher)),
                Job("job02", "Спортивная машина", "Быстрая машина заслуживает блеска", "sports", WorkZone.WashBay, 130,
                    Wash("Смочить", "Нажми и веди по кузову", WashTool.Pressure),
                    Wash("Пена", "Покрой кузов пеной", WashTool.Foam),
                    Wash("Смыть", "Смой пену", WashTool.Rinse),
                    Wash("Сушка", "Обсуши машину", WashTool.Dryer),
                    Wash("Полировка", "Полируй до блеска", WashTool.Polisher)),
                Job("job03", "Патруль с проколотым колесом", "Помоем полицейскую машину и сменим колесо", "police", WorkZone.RepairBay, 160,
                    Wash("Мойка", "Смочить и помыть кузов", WashTool.Pressure, WashTool.Foam, WashTool.Rinse, WashTool.Dryer),
                    Repair(PartKind.WheelFL, "Открути крепления, сними колесо и поставь новое")),
                Job("job04", "Внедорожник после дачи", "Много грязи и разряжен аккумулятор", "suv", WorkZone.WashBay, 180,
                    Wash("Полная мойка", "Промой кузов от грязи", WashTool.Pressure, WashTool.Foam, WashTool.Sponge, WashTool.Rinse, WashTool.Dryer),
                    Repair(PartKind.Battery, "Сними старый аккумулятор и поставь новый")),
                Job("job05", "Пожарная машина", "Помоем, починим фару и перекрасим", "fire", WorkZone.PaintBay, 220,
                    Wash("Мойка", "Помой большой кузов", WashTool.Pressure, WashTool.Foam, WashTool.Rinse, WashTool.Dryer),
                    Repair(PartKind.HeadlightL, "Сними разбитую фару и установи новую"),
                    Paint(C(232,66,62), "Выбери яркий цвет и покрась кузов")),
                Job("job06", "Скорая помощь", "Треснуло лобовое стекло", "ambulance", WorkZone.RepairBay, 200,
                    Wash("Мойка", "Помой кузов", WashTool.Pressure, WashTool.Foam, WashTool.Rinse, WashTool.Dryer),
                    Repair(PartKind.Windshield, "Удали старое стекло, очисти раму и вставь новое")),
                Job("job07", "Пикап на тюнинг", "Колесо, бампер и новый стиль", "pickup", WorkZone.PaintBay, 240,
                    Wash("Мойка", "Помой пикап", WashTool.Pressure, WashTool.Foam, WashTool.Sponge, WashTool.Rinse, WashTool.Dryer),
                    Repair(PartKind.WheelRR, "Замени повреждённое колесо"),
                    Customize("Выбери цвет, диски и наклейку")),
                Job("job08", "Грузовик на ремонте", "Сбитый бампер и покраска", "truck", WorkZone.PaintBay, 260,
                    Wash("Мойка", "Помой грузовик", WashTool.Pressure, WashTool.Foam, WashTool.Rinse, WashTool.Dryer),
                    Repair(PartKind.Bumper, "Сними старый бампер и установи новый"),
                    Paint(C(96,170,210), "Покрась кузов в новый цвет")),
                Job("job09", "Автобус после рейса", "Двигатель и полировка", "bus", WorkZone.RepairBay, 280,
                    Wash("Мойка", "Помой автобус", WashTool.Pressure, WashTool.Foam, WashTool.Sponge, WashTool.Rinse, WashTool.Dryer),
                    Repair(PartKind.Engine, "Открой капот, замени деталь двигателя и закрой капот"),
                    Wash("Финальный блеск", "Полируй кузов", WashTool.Polisher)),
                Job("job10", "Самосвал: финальный заказ", "Колесо, зеркало и полный тюнинг", "dump", WorkZone.PaintBay, 350,
                    Wash("Мойка", "Помой самосвал", WashTool.Pressure, WashTool.Foam, WashTool.Sponge, WashTool.Rinse, WashTool.Dryer),
                    Repair(PartKind.WheelRL, "Замени колесо"),
                    Repair(PartKind.MirrorL, "Замени зеркало"),
                    Customize("Собери свой неповторимый вид"))
            };
            return cfg;
        }

        private static ToolDef Tool(WashTool t, string label, string hint, string sound, float radius, Color color)
        {
            ToolDef d = CreateInstance<ToolDef>();
            d.tool = t; d.labelRu = label; d.hintRu = hint;
            d.soundKey = sound; d.radius = radius; d.color = color;
            return d;
        }

        private static JobDef Job(string id, string title, string desc, string vehicle,
            WorkZone zone, int reward, params JobTaskDef[] tasks)
        {
            JobDef j = CreateInstance<JobDef>();
            j.id = id; j.titleRu = title; j.descriptionRu = desc;
            j.vehicleId = vehicle; j.zone = zone; j.reward = reward; j.tasks = tasks;
            return j;
        }
    }
}
