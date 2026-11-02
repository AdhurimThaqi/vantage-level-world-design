namespace Vantage
{
    /// <summary>
    /// Every sound event by name. The names are also the WAV file names in Assets/Audio (EVENT.wav or EVENT_NN.wav),
    /// which is how Vantage → Audio → Refresh Sound Bank fills the bank. Change a name here and in the files together.
    /// </summary>
    public static class VantageSounds
    {
        public const string AmbExterior = "AMB_Exterior_Dusk";
        public const string AmbL1 = "AMB_L1_Interior";
        public const string AmbL2 = "AMB_L2_Armory";
        public const string AmbL3 = "AMB_L3_Destroyed";
        public const string AmbHelipad = "AMB_Helipad";
        public const string AmbDebris = "AMB_Debris";
        public const string FsConcrete = "FS_Concrete";
        public const string FsMetal = "FS_Metal";
        public const string FsDirt = "FS_Dirt";
        public const string AlarmSiren = "ALARM_Siren";
        public const string ShutterSlam = "SHUTTER_Slam";
        public const string ShutterRoll = "SHUTTER_Roll";
        public const string KeyPickup = "KEY_Pickup";
        public const string KeySlotTurn = "KEYSLOT_Turn";
        public const string SentryGlint = "SENTRY_Glint";
        public const string SentryCrack = "SENTRY_Crack";
        public const string SentryWarningCall = "SENTRY_WarningCall";
        public const string VoContact = "VO_Contact";
        public const string RobotDeflect = "ROBOT_Deflect";
        public const string RobotServo = "ROBOT_Servo";
        public const string RobotTelegraph = "ROBOT_Telegraph";
        public const string DroneHum = "DRONE_Hum";
        public const string HeliRotor = "HELI_Rotor";
        public const string UiMove = "UI_Move";
        public const string UiSelect = "UI_Select";
        public const string CoinCollect = "COIN_Collect";
        public const string PickupWeapon = "PICKUP_Weapon";
        public const string MusExplore = "MUS_Explore";
        public const string MusCombat = "MUS_Combat";
        public const string StgLockdown = "STG_Lockdown";
        public const string StgCleared = "STG_Cleared";
        public const string StgExtraction = "STG_Extraction";

        public static readonly string[] All =
        {
            AmbExterior, AmbL1, AmbL2, AmbL3, AmbHelipad, AmbDebris, FsConcrete, FsMetal, FsDirt, AlarmSiren, ShutterSlam,
            ShutterRoll, KeyPickup, KeySlotTurn, SentryGlint, SentryCrack, SentryWarningCall, VoContact, RobotDeflect,
            RobotServo, RobotTelegraph, DroneHum, HeliRotor, UiMove, UiSelect, CoinCollect, PickupWeapon, MusExplore,
            MusCombat, StgLockdown, StgCleared, StgExtraction,
        };
    }
}
