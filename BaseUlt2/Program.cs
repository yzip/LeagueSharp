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
        static bool CompatibleChamp;

        static Vector3 EnemySpawnPos;

        public static bool IsDominion;

        static Menu Menu;

        static List<PlayerInfo> Players = new List<PlayerInfo>();

        public static Dictionary<int, int> RecallT = new Dictionary<int, int>();

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
            }

            foreach (GameObject spawn in ObjectManager.Get<GameObject>())
            {
                if (spawn.Type == GameObjectType.obj_SpawnPoint)
                {
                    if (spawn.Team != ObjectManager.Player.Team)
                    {
                        EnemySpawnPos = spawn.Position;
                        break;
                    }
                }
            }

            IsDominion = Utility.Map.GetMap() == Utility.Map.MapType.CrystalScar;

            foreach (Obj_AI_Hero hero in ObjectManager.Get<Obj_AI_Hero>()) //preadd everyone, because otherwise lastSeen might not be correctly updated
                AddRecall(new Packet.S2C.Recall.Struct(hero.NetworkId, Packet.S2C.Recall.RecallStatus.Unknown, Packet.S2C.Recall.ObjectType.Player, 0));
            AddRecall(new Packet.S2C.Recall.Struct(ObjectManager.Player.NetworkId, Packet.S2C.Recall.RecallStatus.Unknown, Packet.S2C.Recall.ObjectType.Player, 0));

            Game.OnGameProcessPacket += Game_OnGameProcessPacket; //put handlers before Menu init, because LeagueSharp.Common fucks them up. when fixed, put them after and remove the Menu checks
            Game.OnGameUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;

            Menu = new Menu("BaseUlt", "BaseUlt", true);
            Menu.AddToMainMenu();
            Menu.AddItem(new MenuItem("showRecalls", "Show Recalls").SetValue(true));
            Menu.AddItem(new MenuItem("baseUlt", "Base Ult").SetValue(true));
            Menu.AddItem(new MenuItem("panicKey", "Panic key (hold for disable)").SetValue(new KeyBind(32, KeyBindType.Press))); //32 == space
            Menu.AddItem(new MenuItem("debugMode", "Debug (developer only)").SetValue(false));
        }

        static void Drawing_OnDraw(EventArgs args)
        {
            if (Menu == null || !Menu.Item("showRecalls").GetValue<bool>()) return;

            int index = -1;

            foreach (PlayerInfo playerinfo in from playerinfo in Players
                                              where
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
            if (!CompatibleChamp) return;

            int time = Environment.TickCount;

            foreach (PlayerInfo playerinfo in from playerinfo in Players
                                              where
                                                  playerinfo.GetPlayer() != null &&
                                                  playerinfo.GetPlayer().IsVisible
                                              select playerinfo)
            {
                playerinfo.lastSeen = time;
            }

            if (Menu == null || !Menu.Item("baseUlt").GetValue<bool>()) return;

            if (Menu.Item("panicKey").GetValue<KeyBind>().Active || ObjectManager.Player.IsDead || ObjectManager.Player.Spellbook.CanUseSpell(SpellSlot.R) != SpellState.Ready)
                return;

            foreach (PlayerInfo playerinfo in from playerinfo in Players
                                              where
                                                  playerinfo.GetPlayer() != null &&
                                                  playerinfo.GetPlayer().IsValid &&
                                                  !playerinfo.GetPlayer().IsDead &&
                                                  playerinfo.GetPlayer().Team != ObjectManager.Player.Team &&
                                                  playerinfo.recall.Status == Packet.S2C.Recall.RecallStatus.RecallStarted &&
                                                  time - playerinfo.lastSeen < 22500
                                              orderby playerinfo.GetTime()
                                              select playerinfo)
            {
                HandleRecallShot(playerinfo);
            }
        }

        static void HandleRecallShot(PlayerInfo playerinfo)
        {
            Obj_AI_Hero player = playerinfo.GetPlayer();

            DamageLib.StageType stageType = DamageLib.StageType.Default;

            if (ObjectManager.Player.ChampionName == "Ezreal")
                stageType = DamageLib.StageType.FirstDamage;
            else if (ObjectManager.Player.ChampionName == "Draven")
                stageType = DamageLib.StageType.ThirdDamage;

            float ultdamage = (float)DamageLib.getDmg(player, DamageLib.SpellType.R, stageType);
            float timeneeded = Helper.GetSpellTravelTime(ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R), EnemySpawnPos) + Game.Ping - 170; //increase timeneeded if it should arrive earlier, decrease if later

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
                if(Helper.CheckNoCollision(EnemySpawnPos.To2D(), player.NetworkId))
                {
                    if (Menu != null && Menu.Item("debugMode").GetValue<bool>())
                        Game.PrintChat("SHOOT {0} (Health: {1} UltDamage: {2} ReactionTime: {3})", playerinfo.GetPlayer().ChampionName, Helper.GetTargetHealth(playerinfo, timeneeded), ultdamage, (timeneeded - countdown));

                    ObjectManager.Player.Spellbook.CastSpell(SpellSlot.R, EnemySpawnPos);
                }
                else
                    if (Menu != null && Menu.Item("debugMode").GetValue<bool>())
                        Game.PrintChat("DONT SHOOT COLLISION {0} (Health: {1} UltDamage: {2})", playerinfo.GetPlayer().ChampionName, Helper.GetTargetHealth(playerinfo, timeneeded), ultdamage);
            }
        }

        static PlayerInfo AddRecall(Packet.S2C.Recall.Struct newrecall)
        {
            foreach (PlayerInfo playerinfo in Players)
            {
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
