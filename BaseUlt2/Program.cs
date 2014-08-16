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
        static List<PlayerInfo> Players = new List<PlayerInfo>();
        public static Dictionary<int, int> RecallT = new Dictionary<int, int>();

        static bool CompatibleChamp;

        static Vector3 EnemySpawnPos;
        public static bool IsDominion;

        static Spell Ult;
        static DamageLib.StageType StageType = DamageLib.StageType.Default;
        static float UltWidth, UltDelay, UltSpeed, UltRange;

        static float UltDamageReductionMultiplicator = 1f;

        static int UltCasted;

        static void Main(string[] args)
        {
            Game.OnGameStart += Game_OnGameStart;

            if (Game.Mode == GameMode.Running)
                Game_OnGameStart(null);
        }

        static void Game_OnGameStart(EventArgs args)
        {
            if (ObjectManager.Player.ChampionName == "Ashe" ||
                ObjectManager.Player.ChampionName == "Ezreal" ||
                ObjectManager.Player.ChampionName == "Draven" ||
                ObjectManager.Player.ChampionName == "Jinx")
            {
                CompatibleChamp = true;
                Ult = new Spell(SpellSlot.R, 20000f);

                foreach (GameObject spawn in ObjectManager.Get<GameObject>())
                {
                    if (spawn == null) continue;

                    if (spawn.Type == GameObjectType.obj_SpawnPoint)
                    {
                        if (spawn.Team != ObjectManager.Player.Team)
                        {
                            EnemySpawnPos = spawn.Position;
                            break;
                        }
                    }
                }
            }

            IsDominion = Utility.Map.GetMap() == Utility.Map.MapType.CrystalScar;

            foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>()) //preadd everyone, because otherwise lastSeen might not be correctly updated
            {
                if (hero == null) continue;

                AddRecall(new Packet.S2C.Recall.Struct(hero.NetworkId, Packet.S2C.Recall.RecallStatus.Unknown, Packet.S2C.Recall.ObjectType.Player, 0));
            }

            AddRecall(new Packet.S2C.Recall.Struct(ObjectManager.Player.NetworkId, Packet.S2C.Recall.RecallStatus.Unknown, Packet.S2C.Recall.ObjectType.Player, 0));

            Game.OnGameProcessPacket += Game_OnGameProcessPacket; //put handlers before Menu init, because LeagueSharp.Common fucks them up. when fixed, put them after and remove the Menu checks
            Drawing.OnDraw += Drawing_OnDraw;

            if (CompatibleChamp)
                Game.OnGameUpdate += Game_OnGameUpdate;

            Menu = new Menu("BaseUlt", "BaseUlt", true);
            Menu.AddToMainMenu();
            Menu.AddItem(new MenuItem("showRecalls", "Show Recalls").SetValue(true));
            Menu.AddItem(new MenuItem("baseUlt", "Base Ult").SetValue(true));
            Menu.AddItem(new MenuItem("panicKey", "Panic key (hold for disable)").SetValue(new KeyBind(32, KeyBindType.Press))); //32 == space
            Menu.AddItem(new MenuItem("minUltDamage", "Calc minimum ult dmg (Ez, Draven)").SetValue(false));
            Menu.AddItem(new MenuItem("debugMode", "Debug (developer only)").SetValue(false));

            if(CompatibleChamp)
            {
                switch (ObjectManager.Player.ChampionName)
                {
                    case "Jinx":
                        UltWidth = 140f;
                        UltDelay = 600f / 1000f;
                        UltSpeed = 1700f;
                        UltRange = 20000f;
                        break;
                    case "Ashe":
                        UltWidth = 130f;
                        UltDelay = 250f / 1000f;
                        UltSpeed = 1600;
                        UltRange = 20000f;
                        break;
                    case "Draven":
                        UltWidth = 160f;
                        UltDelay = 400f / 1000f;
                        UltSpeed = 2000f;
                        UltRange = 20000f;
                        UltDamageReductionMultiplicator = Menu.Item("minUltDamage").GetValue<bool>() ? 1f : 0.7f;
                        StageType = Menu.Item("minUltDamage").GetValue<bool>() ? DamageLib.StageType.ThirdDamage : DamageLib.StageType.FirstDamage;
                        break;
                    case "Ezreal":
                        UltWidth = 160f;
                        UltDelay = 1000f / 1000f;
                        UltSpeed = 2000f;
                        UltRange = 20000f;
                        UltDamageReductionMultiplicator = Menu.Item("minUltDamage").GetValue<bool>() ? 1f : 0.7f;
                        StageType = Menu.Item("minUltDamage").GetValue<bool>() ? DamageLib.StageType.FirstDamage : DamageLib.StageType.Default;
                        break;
                }
            }

            Game.PrintChat("<font color=\"#1eff00\">BaseUlt2 -</font> <font color=\"#00BFFF\">Loaded (compatible champ: " + (CompatibleChamp ? "Yes" : "No") + ")</font>");
        }

        static void Drawing_OnDraw(EventArgs args)
        {
            if (Menu == null || !Menu.Item("showRecalls").GetValue<bool>()) return;

            int index = -1;

            foreach (PlayerInfo playerinfo in from playerinfo in Players
                                              where
                                                  playerinfo != null &&
                                                  (playerinfo.recall.Status == Packet.S2C.Recall.RecallStatus.RecallStarted || playerinfo.recall.Status == Packet.S2C.Recall.RecallStatus.TeleportStart) &&
                                                  playerinfo.GetPlayer() != null &&
                                                  playerinfo.GetPlayer().IsValid &&
                                                  !playerinfo.GetPlayer().IsDead &&
                                                  (playerinfo.GetPlayer().Team != ObjectManager.Player.Team || Menu.Item("debugMode").GetValue<bool>())
                                              orderby playerinfo.GetTime()
                                              select playerinfo)
            {
                index++;

                //draw progress bar
                //show circle on minimap on recall

                Drawing.DrawText((float)Drawing.Width * 0.73f, (float)Drawing.Height * 0.88f + ((float)index * 15f), System.Drawing.Color.Red, playerinfo.ToString());
            }
        }

        static void Game_OnGameProcessPacket(GamePacketEventArgs args)
        {
            if (args.PacketData[0] == Packet.S2C.Recall.Header)
            {
                PlayerInfo playerinfo = AddRecall(Helper.RecallDecode(args.PacketData)); //AddRecall(Packet.S2C.Recall.Decoded(args.PacketData));

                if (Menu != null && Menu.Item("debugMode").GetValue<bool>())
                    Game.PrintChat(playerinfo.GetPlayer().ChampionName + ": " + playerinfo.recall.Status);
            }
        }

        static void Game_OnGameUpdate(EventArgs args)
        {
            int time = Environment.TickCount;

            foreach (PlayerInfo playerinfo in from playerinfo in Players
                                              where
                                                  playerinfo != null &&
                                                  playerinfo.GetPlayer() != null &&
                                                  playerinfo.GetPlayer().IsVisible
                                              select playerinfo)
            {
                playerinfo.lastSeen = time;
            }

            if (Menu == null || !Menu.Item("baseUlt").GetValue<bool>()) return;

            if (Menu.Item("panicKey").GetValue<KeyBind>().Active || ObjectManager.Player.IsDead || !Ult.IsReady())
                return;

            foreach (PlayerInfo playerinfo in from playerinfo in Players
                                              where
                                                  playerinfo != null &&
                                                  playerinfo.GetPlayer() != null &&
                                                  playerinfo.GetPlayer().IsValid &&
                                                  !playerinfo.GetPlayer().IsDead &&
                                                  playerinfo.GetPlayer().Team != ObjectManager.Player.Team &&
                                                  playerinfo.recall.Status == Packet.S2C.Recall.RecallStatus.RecallStarted &&
                                                  time - playerinfo.lastSeen < 22500
                                              orderby playerinfo.GetTime()
                                              select playerinfo)
            {
                if (UltCasted == 0 || Environment.TickCount - UltCasted > 15000) //check for draven ult return
                    HandleRecallShot(playerinfo);
            }
        }

        static void HandleRecallShot(PlayerInfo playerinfo)
        {
            Obj_AI_Hero player = playerinfo.GetPlayer();

            float ultdamage = Ult.GetDamage(player, StageType) * UltDamageReductionMultiplicator;
            float timeneeded = Helper.GetSpellTravelTime(UltSpeed, UltDelay, EnemySpawnPos) + Game.Ping - 65; //increase timeneeded if it should arrive earlier, decrease if later

            if (Helper.GetTargetHealth(playerinfo, timeneeded) > ultdamage)
            {
                if (Menu != null && Menu.Item("debugMode").GetValue<bool>())
                    Game.PrintChat("DONT SHOOT {0} (Health: {1} UltDamage: {2})", playerinfo.GetPlayer().ChampionName, Helper.GetTargetHealth(playerinfo, timeneeded), ultdamage);

                return;
            }

            float distance = Vector3.Distance(ObjectManager.Player.ServerPosition, EnemySpawnPos);
            float countdown = playerinfo.GetTime() + playerinfo.recall.Duration - Environment.TickCount;

            if (countdown <= timeneeded && !(timeneeded - countdown > 100))
            {
                if(ObjectManager.Player.ChampionName == "Ezreal" || Helper.CheckNoCollision(EnemySpawnPos.To2D(), player.NetworkId, UltWidth, UltDelay, UltSpeed, UltRange)) //dont check champ collision for Ezreal
                {
                    if (Menu != null && Menu.Item("debugMode").GetValue<bool>())
                        Game.PrintChat("SHOOT {0} (Health: {1} UltDamage: {2} ReactionTime: {3})", playerinfo.GetPlayer().ChampionName, Helper.GetTargetHealth(playerinfo, timeneeded), ultdamage, (timeneeded - countdown));

                    Ult.Cast(EnemySpawnPos, true);
                    UltCasted = Environment.TickCount;
                }
                else if (Menu != null && Menu.Item("debugMode").GetValue<bool>())
                        Game.PrintChat("DONT SHOOT COLLISION {0} (Health: {1} UltDamage: {2})", playerinfo.GetPlayer().ChampionName, Helper.GetTargetHealth(playerinfo, timeneeded), ultdamage);
            }
        }

        static PlayerInfo AddRecall(Packet.S2C.Recall.Struct newrecall)
        {
            foreach (PlayerInfo playerinfo in Players)
            {
                if (playerinfo == null) continue;

                if (playerinfo.recall.UnitNetworkId == newrecall.UnitNetworkId) //update info if already existing
                {
                    playerinfo.recall = newrecall;
                    return playerinfo;
                }
            }

            PlayerInfo newplayeirnfo = new PlayerInfo(newrecall);

            Players.Add(newplayeirnfo);

            return newplayeirnfo;
        }
    }
}
