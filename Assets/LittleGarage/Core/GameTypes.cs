namespace LittleGarage
{
    /// <summary>Vehicle families built by VehicleFactory.</summary>
    public enum VehicleKind
    {
        Sedan, Sports, SUV, Pickup, Police, FireTruck, Ambulance, Truck, Bus, DumpTruck
    }

    /// <summary>Individually serviceable parts. Wheel positions are explicit.</summary>
    public enum PartKind
    {
        WheelFL, WheelFR, WheelRL, WheelRR,
        Engine, Battery, Windshield, HeadlightL, HeadlightR, MirrorL, MirrorR, Bumper
    }

    /// <summary>Six real washing tools with distinct effects and sounds.</summary>
    public enum WashTool { Pressure, Foam, Sponge, Rinse, Dryer, Polisher }

    public enum WorkZone { WashBay, RepairBay, PaintBay }

    public enum TaskKind { Wash, Repair, Paint, Customize }

    /// <summary>State-map channels. RGBA of SurfaceStateSystem.Map.</summary>
    public enum SurfaceChannel { Dirty = 0, Wet = 1, Foam = 2, Polish = 3 }

    public enum ScreenId { Home, Garage, Play, Complete }

    /// <summary>How a repair part is interacted with.</summary>
    public enum RepairGesture
    {
        Unscrew,      // click/drag on fasteners until they come off
        LiftOff,      // drag the broken part to the recycling tray
        SlideIn,      // drag a new part from the shelf onto the mount
        Wipe,         // scrub the mounting surface with a cloth
        Toggle        // open/close hood or press a latch
    }

    public static class GameTypes
    {
        public static bool IsWheel(PartKind kind)
        {
            return kind == PartKind.WheelFL || kind == PartKind.WheelFR ||
                   kind == PartKind.WheelRL || kind == PartKind.WheelRR;
        }

        public static string Ru(this PartKind kind)
        {
            switch (kind)
            {
                case PartKind.WheelFL: return "Переднее левое колесо";
                case PartKind.WheelFR: return "Переднее правое колесо";
                case PartKind.WheelRL: return "Заднее левое колесо";
                case PartKind.WheelRR: return "Заднее правое колесо";
                case PartKind.Engine: return "Двигатель";
                case PartKind.Battery: return "Аккумулятор";
                case PartKind.Windshield: return "Лобовое стекло";
                case PartKind.HeadlightL: return "Левая фара";
                case PartKind.HeadlightR: return "Правая фара";
                case PartKind.MirrorL: return "Левое зеркало";
                case PartKind.MirrorR: return "Правое зеркало";
                case PartKind.Bumper: return "Бампер";
                default: return "Деталь";
            }
        }

        public static string Ru(this WorkZone zone)
        {
            switch (zone)
            {
                case WorkZone.WashBay: return "Моечный пост";
                case WorkZone.RepairBay: return "Ремонтный пост";
                default: return "Покрасочно-тюнинговая зона";
            }
        }
    }
}
