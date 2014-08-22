using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace BaseUlt2
{
    class PlayerInfo
    {
        public Obj_AI_Hero champ;
        public Dictionary<int, float> incomingDamage;
        public int lastSeen;
        public Packet.S2C.Recall.Struct recall;

        public PlayerInfo(Obj_AI_Hero champ)
        {
            this.champ = champ;
            this.recall = new Packet.S2C.Recall.Struct(champ.NetworkId, Packet.S2C.Recall.RecallStatus.Unknown, Packet.S2C.Recall.ObjectType.Player, 0);
            this.incomingDamage = new Dictionary<int, float>();
        }

        public PlayerInfo UpdateRecall(Packet.S2C.Recall.Struct newRecall)
        {
            this.recall = newRecall;
            return this;
        }

        public int GetRecallStart()
        {
            switch ((int)recall.Status)
            {
                case (int)Packet.S2C.Recall.RecallStatus.RecallStarted:
                case (int)Packet.S2C.Recall.RecallStatus.TeleportStart:
                    return Program.RecallT[recall.UnitNetworkId];

                default:
                    return 0;
            }
        }

        public int GetRecallEnd()
        {
            return GetRecallStart() + recall.Duration;
        }

        public int GetRecallCountdown()
        {
            int countdown = GetRecallEnd() - Environment.TickCount;
            return countdown < 0 ? 0 : countdown;
        }

        override public string ToString()
        {
            string drawtext = champ.ChampionName + ": " + recall.Status; //change to better string

            float countdown = (float)GetRecallCountdown() / 1000f;

            if (countdown > 0)
                drawtext += " (" + countdown.ToString("0.00") + "s)";

            return drawtext;
        }
    }

    class Program
    {
        static Menu Menu;
        static bool CompatibleChamp;
        static IEnumerable<Obj_AI_Hero> OwnTeam;
        static IEnumerable<Obj_AI_Hero> EnemyTeam;
        static Vector3 EnemySpawnPos;
        static Utility.Map.MapType Map;
        static List<PlayerInfo> PlayerInfo = new List<PlayerInfo>();
        public static Dictionary<int, int> RecallT = new Dictionary<int, int>();
        static int UltCasted = 0;
        static Spell Ult;

        struct UltData
        {
            public DamageLib.StageType StageType;
            public float ManaCost;
            public float DamageMultiplicator;
            public float Width, Delay, Speed, Range;
        }

        static Dictionary<String, UltData> UltInfo = new Dictionary<string, UltData>()
        {
            {"Jinx", new UltData {StageType = DamageLib.StageType.Default, ManaCost = 100f, DamageMultiplicator = 1f, Width = 140f, Delay = 600f / 1000f, Speed = 1700f, Range = 20000f}},
            {"Ashe", new UltData {StageType = DamageLib.StageType.Default, ManaCost = 100f, DamageMultiplicator = 1f, Width = 130f, Delay = 250f / 1000f, Speed = 1600f, Range = 20000f}},
            {"Draven", new UltData {StageType = DamageLib.StageType.FirstDamage, ManaCost = 120f, DamageMultiplicator = 0.7f, Width = 160f, Delay = 400f / 1000f, Speed = 2000f, Range = 20000f}},
            {"Ezreal", new UltData {StageType = DamageLib.StageType.Default, ManaCost = 100f, DamageMultiplicator = 0.7f, Width = 160f, Delay = 1000f / 1000f, Speed = 2000f, Range = 20000f}}
        };

        static void Main(string[] args)
        {
            Game.OnGameStart += Game_OnGameStart;

            if (Game.Mode == GameMode.Running)
                Game_OnGameStart(new EventArgs());
        }

        static void Game_OnGameStart(EventArgs args)
        {
            Menu = new Menu("BaseUlt2", "BaseUlt", true);
            Menu.AddToMainMenu();
            Menu.AddItem(new MenuItem("showRecalls", "Show Recalls").SetValue(true));
            Menu.AddItem(new MenuItem("baseUlt", "Base Ult").SetValue(true));
            Menu.AddItem(new MenuItem("panicKey", "Panic key (hold for disable)").SetValue(new KeyBind(32, KeyBindType.Press))); //32 == space
            Menu.AddItem(new MenuItem("extraDelay", "Extra Delay").SetValue(new Slider(0, -2000, 2000)));
            Menu.AddItem(new MenuItem("debugMode", "Debug (developer only)").SetValue(false));
            //add buffer

            Menu TeamUlt = new Menu("Team Baseult Champs", "TeamUlt");
            Menu.AddSubMenu(TeamUlt);

            IEnumerable<Obj_AI_Hero> Champions = ObjectManager.Get<Obj_AI_Hero>();

            CompatibleChamp = IsCompatibleChamp(ObjectManager.Player.ChampionName);

            OwnTeam = Champions.Where(x => x.IsAlly);
            EnemyTeam = Champions.Where(x => x.IsEnemy);

            foreach (Obj_AI_Hero champ in OwnTeam.Where(x => !x.IsMe && IsCompatibleChamp(x.ChampionName)))
                TeamUlt.AddItem(new MenuItem(champ.ChampionName, champ.ChampionName + " friend with Baseult?").SetValue(false).DontSave());

            EnemySpawnPos = ObjectManager.Get<GameObject>().First(x => x.Type == GameObjectType.obj_SpawnPoint && x.Team != ObjectManager.Player.Team).Position;

            Map = Utility.Map.GetMap();

            foreach (Obj_AI_Hero champ in EnemyTeam)
                PlayerInfo.Add(new PlayerInfo(champ));

            PlayerInfo.Add(new PlayerInfo(ObjectManager.Player));

            Ult = new Spell(SpellSlot.R, 20000f);

            Game.OnGameProcessPacket += Game_OnGameProcessPacket;
            Drawing.OnDraw += Drawing_OnDraw;

            if (CompatibleChamp)
                Game.OnGameUpdate += Game_OnGameUpdate;

            Game.PrintChat("<font color=\"#1eff00\">BaseUlt2 -</font> <font color=\"#00BFFF\">Loaded (compatible champ: " + (CompatibleChamp ? "Yes" : "No") + ")</font>");
        }

        static void Game_OnGameUpdate(EventArgs args)
        {
            int time = Environment.TickCount;

            foreach (PlayerInfo playerInfo in PlayerInfo.Where(x => x.champ.IsVisible))
                playerInfo.lastSeen = time;

            if (!Menu.Item("baseUlt").GetValue<bool>()) return;

            foreach (PlayerInfo playerInfo in PlayerInfo.Where(x =>
                x.champ.IsValid &&
                !x.champ.IsDead &&
                x.champ.IsEnemy &&
                x.recall.Status == Packet.S2C.Recall.RecallStatus.RecallStarted).OrderBy(x => x.GetRecallEnd()))
            {
                if (UltCasted == 0 || Environment.TickCount - UltCasted > 15000) //DONT change Environment.TickCount; check for draven ult return
                    HandleRecallShot(playerInfo);
            }
        }

        static void HandleRecallShot(PlayerInfo playerInfo)
        {
            bool Shoot = false;

            foreach (Obj_AI_Hero champ in OwnTeam.Where(x => x.IsValid && (x.IsMe || Menu.Item(x.ChampionName).GetValue<bool>()) && !x.IsDead && !x.IsStunned &&
                (x.Spellbook.CanUseSpell(SpellSlot.R) == SpellState.Ready || (x.Spellbook.GetSpell(SpellSlot.R).Level > 0 && x.Spellbook.CanUseSpell(SpellSlot.R) == SpellState.Surpressed && x.Mana >= UltInfo[x.ChampionName].ManaCost)))) //use when fixed: champ.Spellbook.GetSpell(SpellSlot.R) = Ready or champ.Spellbook.GetSpell(SpellSlot.R).ManaCost)
            {
                //Add Champ collision, check only current enemy champ pos
                if (champ.ChampionName != "Ezreal" && !IsCollidingWithChamps(champ.ServerPosition.To2D(), EnemySpawnPos.To2D(), playerInfo.champ.NetworkId, UltInfo[champ.ChampionName].Width, UltInfo[champ.ChampionName].Delay, UltInfo[champ.ChampionName].Speed, UltInfo[champ.ChampionName].Range))
                    continue;

                float timeneeded = GetSpellTravelTime(champ, UltInfo[champ.ChampionName].Speed, UltInfo[champ.ChampionName].Delay, EnemySpawnPos) - (Menu.Item("extraDelay").GetValue<Slider>().Value + 65); //increase timeneeded if it should arrive earlier, decrease if later

                if (timeneeded - playerInfo.GetRecallCountdown() > 100)
                    continue;

                playerInfo.incomingDamage[champ.NetworkId] = (float)GetUltDamage(champ, playerInfo.champ) * UltInfo[champ.ChampionName].DamageMultiplicator;

                if (playerInfo.GetRecallCountdown() <= timeneeded && timeneeded - playerInfo.GetRecallCountdown() < 100)
                    if (champ.IsMe)
                        Shoot = true;
            }

            if (!Shoot || Menu.Item("panicKey").GetValue<KeyBind>().Active)
                return;

            float totalUltDamage = 0;

            foreach (float ultdamage in playerInfo.incomingDamage.Values)
                totalUltDamage += ultdamage;

            playerInfo.incomingDamage.Clear(); //wrong placement?

            float targetHealth = GetTargetHealth(playerInfo);

            int time = Environment.TickCount;

            if (time - playerInfo.lastSeen > 15000)
            {
                if (totalUltDamage < playerInfo.champ.MaxHealth)
                {
                    Game.PrintChat("DONT SHOOT, TOO LONG NO VISION {0} (Health: {1} UltDamage: {2})", playerInfo.champ.ChampionName, targetHealth, totalUltDamage);
                    return;
                }
            }
            else if (totalUltDamage < targetHealth)
            {
                Game.PrintChat("DONT SHOOT {0} (Health: {1} UltDamage: {2})", playerInfo.champ.ChampionName, targetHealth, totalUltDamage);
                return;
            }

            if (Menu.Item("debugMode").GetValue<bool>())
                Game.PrintChat("SHOOT {0} (Health: {1} UltDamage: {2})", playerInfo.champ.ChampionName, targetHealth, totalUltDamage);

            Ult.Cast(EnemySpawnPos, true);
            UltCasted = time;
        }

        static void Drawing_OnDraw(EventArgs args)
        {
            if (!Menu.Item("showRecalls").GetValue<bool>()) return;

            int index = -1;

            foreach (PlayerInfo playerInfo in PlayerInfo.Where(x =>
                (x.recall.Status == Packet.S2C.Recall.RecallStatus.RecallStarted || x.recall.Status == Packet.S2C.Recall.RecallStatus.TeleportStart) &&
                x.champ.IsValid &&
                !x.champ.IsDead &&
                (x.champ.IsEnemy || Menu.Item("debugMode").GetValue<bool>())).OrderBy(x => x.GetRecallEnd()))
            {
                index++;

                //draw progress bar
                //show circle on minimap on recall

                Drawing.DrawText((float)Drawing.Width * 0.73f, (float)Drawing.Height * 0.88f + ((float)index * 15f), System.Drawing.Color.Red, playerInfo.ToString());
            }
        }

        static void Game_OnGameProcessPacket(GamePacketEventArgs args)
        {
            if (args.PacketData[0] == Packet.S2C.Recall.Header)
            {
                Packet.S2C.Recall.Struct newRecall = RecallDecode(args.PacketData);

                PlayerInfo playerInfo = PlayerInfo.Where(x => x.champ.NetworkId == newRecall.UnitNetworkId).FirstOrDefault().UpdateRecall(newRecall); //Packet.S2C.Recall.Decoded(args.PacketData)

                if (Menu.Item("debugMode").GetValue<bool>())
                    Game.PrintChat(playerInfo.champ.ChampionName + ": " + playerInfo.recall.Status + " duration: " + playerInfo.recall.Duration + " guessed health: " + GetTargetHealth(playerInfo) + " lastseen: " + playerInfo.lastSeen + " health: " + playerInfo.champ.Health);
            }
        }

        public static float GetTargetHealth(PlayerInfo playerInfo)
        {
            if (playerInfo.champ.IsVisible)
                return playerInfo.champ.Health;

            float predictedhealth = playerInfo.champ.Health + playerInfo.champ.HPRegenRate * ((float)(Environment.TickCount - playerInfo.lastSeen + playerInfo.GetRecallCountdown()) / 1000f);

            return predictedhealth > playerInfo.champ.MaxHealth ? playerInfo.champ.MaxHealth : predictedhealth;
        }

        public static float GetSpellTravelTime(Obj_AI_Hero source, float speed, float delay, Vector3 targetpos)
        {
            float distance = Vector3.Distance(source.ServerPosition, targetpos);
            float missilespeed = source.ChampionName != "Jinx" ? speed :
                (distance <= 1500f ? speed : (1500f * speed + ((distance - 1500f) * 2200f)) / distance); //1700 = missilespeed, 2200 = missilespeed after acceleration, 1350 acceleration starts, 1500 = fully acceleration

            return (distance / missilespeed + delay) * 1000;
        }

        public static bool IsCollidingWithChamps(Vector2 frompos, Vector2 targetpos, int targetnetid, float width, float delay, float speed, float range)
        {
            return !Prediction.GetCollision(frompos, new List<Vector2>() {targetpos}, Prediction.SkillshotType.SkillshotLine, width, delay, speed * 10000, range).Any(); //x => x.NetworkId != targetnetid
        }

        public static List<Obj_AI_Base> GetChampCollision(Vector2 from, List<Vector2> To, Prediction.SkillshotType stype, float width, float delay, float speed)
        {
            var result = new List<Obj_AI_Base>();
            delay -= 0.07f + Game.Ping / 1000;

            foreach (var TestPosition in To)
            {
                foreach (var collisionObject in ObjectManager.Get<Obj_AI_Hero>().Where(x => x.IsValidTarget(float.MaxValue, true)))
                {
                    var objectPrediction = Prediction.GetBestPosition(collisionObject, delay, width, speed, from.To3D(), float.MaxValue, false, stype, @from.To3D());

                    if (objectPrediction.Position.To2D().Distance(from, TestPosition, true, true) <= Math.Pow((width + 15 + collisionObject.BoundingRadius), 2))
                        result.Add(collisionObject);
                }
            }

            result = result.Distinct().ToList();
            return result;
        }

        public static Packet.S2C.Recall.Struct RecallDecode(byte[] data)
        {
            BinaryReader reader = new BinaryReader(new MemoryStream(data));
            Packet.S2C.Recall.Struct recall = new Packet.S2C.Recall.Struct();

            reader.ReadByte(); //PacketId
            reader.ReadInt32();
            recall.UnitNetworkId = reader.ReadInt32();
            reader.ReadBytes(66);

            recall.Status = Packet.S2C.Recall.RecallStatus.Unknown;

            bool teleport = false;

            if (BitConverter.ToString(reader.ReadBytes(6)) != "00-00-00-00-00-00")
            {
                if (BitConverter.ToString(reader.ReadBytes(3)) != "00-00-00")
                {
                    recall.Status = Packet.S2C.Recall.RecallStatus.TeleportStart;
                    teleport = true;
                }
                else
                    recall.Status = Packet.S2C.Recall.RecallStatus.RecallStarted;
            }

            reader.Close();

            Obj_AI_Hero champ = ObjectManager.GetUnitByNetworkId<Obj_AI_Hero>(recall.UnitNetworkId);

            recall.Duration = 0;

            if (champ != null)
            {
                if (teleport)
                    recall.Duration = 3500;
                else //use masteries to detect recall duration, because spelldata is not initialized yet when enemy has not been seen
                {
                    recall.Duration = Map == Utility.Map.MapType.CrystalScar ? 4500 : 8000;

                    if (champ.Masteries.Any(x => x.Page == MasteryPage.Utility && x.Id == 65 && x.Points == 1))
                        recall.Duration -= Map == Utility.Map.MapType.CrystalScar ? 500 : 1000; //phasewalker mastery
                }

                int time = Environment.TickCount - Game.Ping;

                if (!RecallT.ContainsKey(recall.UnitNetworkId))
                    RecallT.Add(recall.UnitNetworkId, time); //will result in status RecallStarted, which would be wrong if the assembly was to be loaded while somebody recalls
                else
                {
                    if (RecallT[recall.UnitNetworkId] == 0)
                        RecallT[recall.UnitNetworkId] = time;
                    else
                    {
                        if (time - RecallT[recall.UnitNetworkId] > recall.Duration - 75)
                            recall.Status = teleport ? Packet.S2C.Recall.RecallStatus.TeleportEnd : Packet.S2C.Recall.RecallStatus.RecallFinished;
                        else
                            recall.Status = teleport ? Packet.S2C.Recall.RecallStatus.TeleportAbort : Packet.S2C.Recall.RecallStatus.RecallAborted;

                        Program.RecallT[recall.UnitNetworkId] = 0; //recall aborted or finished, reset status
                    }
                }
            }

            return recall;
        }

        static bool IsCompatibleChamp(String championName)
        {
            switch (championName)
            {
                case "Ashe":
                case "Ezreal":
                case "Draven":
                case "Jinx":
                    return true;

                default:
                    return false;
            }
        }

        static double GetUltDamage(Obj_AI_Hero source, Obj_AI_Hero enemy)
        {
            switch (source.ChampionName)
            {
                case "Ashe":
                    return CalcMagicDmg((75 + (source.Spellbook.GetSpell(SpellSlot.R).Level * 175)) + (1.0 * source.FlatMagicDamageMod), source, enemy);
                case "Draven":
                    return CalcPhysicalDmg((75 + (source.Spellbook.GetSpell(SpellSlot.R).Level * 100)) + (1.1 * source.FlatPhysicalDamageMod), source, enemy); // way to enemy
                case "Jinx":
                    var percentage = CalcPhysicalDmg(((enemy.MaxHealth - enemy.Health) / 100) * (20 + (5 * source.Spellbook.GetSpell(SpellSlot.R).Level)), source, enemy);
                    return percentage + CalcPhysicalDmg((150 + (source.Spellbook.GetSpell(SpellSlot.R).Level * 100)) + (1.0 * source.FlatPhysicalDamageMod), source, enemy);
                case "Ezreal":
                    return CalcMagicDmg((200 + (source.Spellbook.GetSpell(SpellSlot.R).Level * 150)) +
                        (1.0 * (source.FlatPhysicalDamageMod + source.BaseAttackDamage)) +
                        (0.9 * source.FlatMagicDamageMod), source, enemy);
                default:
                    return 0;
            }
        }

        public static double CalcPhysicalDmg(double dmg, Obj_AI_Hero source, Obj_AI_Base enemy)
        {
            bool doubleedgedsword = false, havoc = false, arcaneblade = false, butcher = false;
            int executioner = 0;

            foreach (var mastery in source.Masteries)
            {
                if (mastery.Page == MasteryPage.Offense)
                {
                    switch (mastery.Id)
                    {
                        case 65:
                            doubleedgedsword = (mastery.Points == 1);
                            break;
                        case 146:
                            havoc = (mastery.Points == 1);
                            break;
                        case 132:
                            arcaneblade = (mastery.Points == 1);
                            break;
                        case 100:
                            executioner = mastery.Points;
                            break;
                        case 68:
                            butcher = (mastery.Points == 1);
                            break;
                    }
                }
            }

            double additionaldmg = 0;
            if (doubleedgedsword)
            {
                if (ObjectManager.Player.CombatType == GameObjectCombatType.Melee)
                {
                    additionaldmg += dmg * 0.02;
                }
                else
                {
                    additionaldmg += dmg * 0.015;
                }
            }

            if (havoc)
            {
                additionaldmg += dmg * 0.03;
            }

            if (executioner > 0)
            {
                if (executioner == 1)
                {
                    if ((enemy.Health / enemy.MaxHealth) * 100 < 20)
                    {
                        additionaldmg += dmg * 0.05;
                    }
                }
                else if (executioner == 2)
                {
                    if ((enemy.Health / enemy.MaxHealth) * 100 < 35)
                    {
                        additionaldmg += dmg * 0.05;
                    }
                }
                else if (executioner == 3)
                {
                    if ((enemy.Health / enemy.MaxHealth) * 100 < 50)
                    {
                        additionaldmg += dmg * 0.05;
                    }
                }
            }

            double newarmor = enemy.Armor * ObjectManager.Player.PercentArmorPenetrationMod;
            var dmgreduction = 100 / (100 + newarmor - ObjectManager.Player.FlatArmorPenetrationMod);
            return (((dmg + additionaldmg) * dmgreduction));
        }

        public static double CalcMagicDmg(double dmg, Obj_AI_Hero source, Obj_AI_Base enemy)
        {
            bool doubleedgedsword = false, havoc = false, arcaneblade = false, butcher = false;
            int executioner = 0;

            foreach (var mastery in source.Masteries)
            {
                if (mastery.Page == MasteryPage.Offense)
                {
                    switch (mastery.Id)
                    {
                        case 65:
                            doubleedgedsword = (mastery.Points == 1);
                            break;
                        case 146:
                            havoc = (mastery.Points == 1);
                            break;
                        case 132:
                            arcaneblade = (mastery.Points == 1);
                            break;
                        case 100:
                            executioner = mastery.Points;
                            break;
                        case 68:
                            butcher = (mastery.Points == 1);
                            break;
                    }
                }
            }

            double additionaldmg = 0;
            if (doubleedgedsword)
            {
                if (ObjectManager.Player.CombatType == GameObjectCombatType.Melee)
                {
                    additionaldmg = dmg * 0.02;
                }
                else
                {
                    additionaldmg = dmg * 0.015;
                }
            }
            if (havoc)
            {
                additionaldmg += dmg * 0.03;
            }
            if (executioner > 0)
            {
                if (executioner == 1)
                {
                    if ((enemy.Health / enemy.MaxHealth) * 100 < 20)
                    {
                        additionaldmg += dmg * 0.05;
                    }
                }
                else if (executioner == 2)
                {
                    if ((enemy.Health / enemy.MaxHealth) * 100 < 35)
                    {
                        additionaldmg += dmg * 0.05;
                    }
                }
                else if (executioner == 3)
                {
                    if ((enemy.Health / enemy.MaxHealth) * 100 < 50)
                    {
                        additionaldmg += dmg * 0.05;
                    }
                }
            }

            double newspellblock = enemy.SpellBlock * source.PercentMagicPenetrationMod;
            var dmgreduction = 100 / (100 + newspellblock - source.FlatMagicPenetrationMod);
            return (((dmg + additionaldmg) * dmgreduction));
        }
    }
}
