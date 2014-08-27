using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace BaseUlt2
{
    class Program
    {
        static Menu Menu;
        static bool CompatibleChamp;
        static IEnumerable<Obj_AI_Hero> OwnTeam;
        static IEnumerable<Obj_AI_Hero> EnemyTeam;
        static Vector3 EnemySpawnPos;
        public static Utility.Map.MapType Map;
        static List<PlayerInfo> PlayerInfo = new List<PlayerInfo>();
        public static Dictionary<int, int> RecallT = new Dictionary<int, int>();
        static int UltCasted;
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
            Menu.AddItem(new MenuItem("extraDelay", "Extra Delay").SetValue(new Slider(0, -2000, 2000)));
            Menu.AddItem(new MenuItem("panicKey", "Panic key (hold for disable)").SetValue(new KeyBind(32, KeyBindType.Press))); //32 == space
            Menu.AddItem(new MenuItem("regardlessKey", "No timelimit (hold)").SetValue(new KeyBind(17, KeyBindType.Press))); //17 == ctrl
            Menu.AddItem(new MenuItem("debugMode", "Debug (developer only)").SetValue(false));

            Menu TeamUlt = new Menu("Team Baseult Friends", "TeamUlt");
            Menu.AddSubMenu(TeamUlt);

            IEnumerable<Obj_AI_Hero> Champions = ObjectManager.Get<Obj_AI_Hero>();

            CompatibleChamp = Helper.IsCompatibleChamp(ObjectManager.Player.ChampionName);

            OwnTeam = Champions.Where(x => x.IsAlly);
            EnemyTeam = Champions.Where(x => x.IsEnemy);

            foreach (Obj_AI_Hero champ in OwnTeam.Where(x => !x.IsMe && Helper.IsCompatibleChamp(x.ChampionName)))
                TeamUlt.AddItem(new MenuItem(champ.ChampionName, champ.ChampionName + " friend with Baseult?").SetValue(false).DontSave());

            EnemySpawnPos = ObjectManager.Get<GameObject>().First(x => x.Type == GameObjectType.obj_SpawnPoint && x.Team != ObjectManager.Player.Team).Position;

            Map = Utility.Map.GetMap();

            PlayerInfo = EnemyTeam.Select(x => new PlayerInfo(x)).ToList();
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
                if (UltCasted == 0 || Environment.TickCount - UltCasted > 20000) //DONT change Environment.TickCount; check for draven ult return
                    HandleRecallShot(playerInfo);
            }
        }

        static void HandleRecallShot(PlayerInfo playerInfo)
        {
            bool Shoot = false;

            foreach (Obj_AI_Hero champ in OwnTeam.Where(x => x.IsValid && (x.IsMe || Helper.GetSafeMenuItem<bool>(Menu.Item(x.ChampionName))) && !x.IsDead && !x.IsStunned &&
                (x.Spellbook.CanUseSpell(SpellSlot.R) == SpellState.Ready || (x.Spellbook.GetSpell(SpellSlot.R).Level > 0 && x.Spellbook.CanUseSpell(SpellSlot.R) == SpellState.Surpressed && x.Mana >= UltInfo[x.ChampionName].ManaCost)))) //use when fixed: champ.Spellbook.GetSpell(SpellSlot.R) = Ready or champ.Spellbook.GetSpell(SpellSlot.R).ManaCost)
            {
                if (champ.ChampionName != "Ezreal" && Helper.IsCollidingWithChamps(champ, EnemySpawnPos, UltInfo[champ.ChampionName].Width))
                    continue;

                float timeneeded = Helper.GetSpellTravelTime(champ, UltInfo[champ.ChampionName].Speed, UltInfo[champ.ChampionName].Delay, EnemySpawnPos) - (Menu.Item("extraDelay").GetValue<Slider>().Value + 65); //increase timeneeded if it should arrive earlier, decrease if later

                if (timeneeded - playerInfo.GetRecallCountdown() > 100)
                    continue;

                playerInfo.incomingDamage[champ.NetworkId] = (float)Helper.GetUltDamage(champ, playerInfo.champ) * UltInfo[champ.ChampionName].DamageMultiplicator;

                if (playerInfo.GetRecallCountdown() <= timeneeded && timeneeded - playerInfo.GetRecallCountdown() < 100)
                    if (champ.IsMe)
                        Shoot = true;
            }

            float totalUltDamage = 0;

            foreach (float ultdamage in playerInfo.incomingDamage.Values)
                totalUltDamage += ultdamage;

            float targetHealth = Helper.GetTargetHealth(playerInfo);

            if (!Shoot || Menu.Item("panicKey").GetValue<KeyBind>().Active)
            {
                if (Menu.Item("debugMode").GetValue<bool>())
                    Game.PrintChat("!SHOOT/PANICKEY {0} (Health: {1} TOTAL-UltDamage: {2})", playerInfo.champ.ChampionName, targetHealth, totalUltDamage);

                return;
            }

            playerInfo.incomingDamage.Clear(); //wrong placement?

            int time = Environment.TickCount;

            if (time - playerInfo.lastSeen > 20000 && !Menu.Item("regardlessKey").GetValue<KeyBind>().Active)
            {
                if (totalUltDamage < playerInfo.champ.MaxHealth)
                {
                    if (Menu.Item("debugMode").GetValue<bool>())
                        Game.PrintChat("DONT SHOOT, TOO LONG NO VISION {0} (Health: {1} TOTAL-UltDamage: {2})", playerInfo.champ.ChampionName, targetHealth, totalUltDamage);

                    return;
                }
            }
            else if (totalUltDamage < targetHealth)
            {
                if (Menu.Item("debugMode").GetValue<bool>())
                    Game.PrintChat("DONT SHOOT {0} (Health: {1} TOTAL-UltDamage: {2})", playerInfo.champ.ChampionName, targetHealth, totalUltDamage);

                return;
            }

            if (Menu.Item("debugMode").GetValue<bool>())
                Game.PrintChat("SHOOT {0} (Health: {1} TOTAL-UltDamage: {2})", playerInfo.champ.ChampionName, targetHealth, totalUltDamage);

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
                x.GetRecallCountdown() > 0 && 
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
                Packet.S2C.Recall.Struct newRecall = Helper.RecallDecode(args.PacketData);

                PlayerInfo playerInfo = PlayerInfo.First(x => x.champ.NetworkId == newRecall.UnitNetworkId).UpdateRecall(newRecall); //Packet.S2C.Recall.Decoded(args.PacketData)

                if (Menu.Item("debugMode").GetValue<bool>())
                    Game.PrintChat(playerInfo.champ.ChampionName + ": " + playerInfo.recall.Status + " duration: " + playerInfo.recall.Duration + " guessed health: " + Helper.GetTargetHealth(playerInfo) + " lastseen: " + playerInfo.lastSeen + " health: " + playerInfo.champ.Health + " own-ultdamage: " + (float)Helper.GetUltDamage(ObjectManager.Player, playerInfo.champ) * UltInfo[ObjectManager.Player.ChampionName].DamageMultiplicator);
            }
        }
    }
}
