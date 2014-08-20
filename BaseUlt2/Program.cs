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

        static int UltCasted;

        static Dictionary<String, UltData> UltInfo = new Dictionary<string, UltData>()
        {
            {"Jinx", new UltData {StageType = DamageLib.StageType.Default, ManaCost = 100f, DamageReductionMultiplicator = 1f, Width = 140f, Delay = 600f / 1000f, Speed = 1700f, Range = 20000f}},
            {"Ashe", new UltData {StageType = DamageLib.StageType.Default, ManaCost = 100f, DamageReductionMultiplicator = 1f, Width = 130f, Delay = 250f / 1000f, Speed = 1600f, Range = 20000f}},
            {"Draven", new UltData {StageType = DamageLib.StageType.FirstDamage, ManaCost = 120f, DamageReductionMultiplicator = 0.7f, Width = 160f, Delay = 400f / 1000f, Speed = 2000f, Range = 20000f}},
            {"Ezreal", new UltData {StageType = DamageLib.StageType.Default, ManaCost = 100f, DamageReductionMultiplicator = 0.7f, Width = 160f, Delay = 1000f / 1000f, Speed = 2000f, Range = 20000f}}
        };

        struct UltData
        {
            public DamageLib.StageType StageType;
            public float ManaCost;
            public float DamageReductionMultiplicator;
            public float Width, Delay, Speed, Range;
        }

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
            Menu.AddItem(new MenuItem("debugMode", "Debug (developer only)").SetValue(false));

            var TeamUlt = new Menu("Team Baseult Champs", "TeamUlt");
            Menu.AddSubMenu(TeamUlt);
            
            foreach(Obj_AI_Hero champ in from champ in ObjectManager.Get<Obj_AI_Hero>()
                                         where
                                            champ.IsAlly && 
                                            !champ.IsMe &&
                                            (champ.ChampionName == "Ezreal" || champ.ChampionName == "Jinx" || champ.ChampionName == "Ashe" || champ.ChampionName == "Draven")
                                         select champ
                                             )
            {
                TeamUlt.AddItem(new MenuItem(champ.ChampionName, champ.ChampionName + " friend?").SetValue(false).DontSave()); 
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

        static double UltDamage(Obj_AI_Hero source, Obj_AI_Hero enemy)
        {
            switch(source.ChampionName)
            {
                case "Ashe":
                    return CalcMagicDmg((75 + (ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Level * 175)) + (1.0 * ObjectManager.Player.FlatMagicDamageMod), source, enemy);
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

            foreach (PlayerInfo playerinfo in from playerinfo in Players
                                              where
                                                  playerinfo != null &&
                                                  playerinfo.GetPlayer() != null &&
                                                  playerinfo.GetPlayer().IsValid &&
                                                  !playerinfo.GetPlayer().IsDead &&
                                                  playerinfo.GetPlayer().IsEnemy &&
                                                  playerinfo.recall.Status == Packet.S2C.Recall.RecallStatus.RecallStarted
                                              orderby playerinfo.GetTime()
                                              select playerinfo)
            {
                if (UltCasted == 0 || Environment.TickCount - UltCasted > 15000) //DONT change Environment.TickCount; check for draven ult return
                    HandleTeamUltShot(playerinfo);
            }
        }

        static void HandleTeamUltShot(PlayerInfo playerinfo)
        {
            Obj_AI_Hero target = playerinfo.GetPlayer();

            float countdown = playerinfo.GetTime() + playerinfo.recall.Duration - Environment.TickCount;

            bool Shoot = false;            

            foreach (Obj_AI_Hero champ in from champ in ObjectManager.Get<Obj_AI_Hero>()
                                          where
                                             champ.IsValid &&
                                             champ.IsAlly &&
                                             (champ.IsMe || (Menu != null && Menu.Item(champ.ChampionName).GetValue<bool>())) &&
                                             !champ.IsDead &&
                                             !champ.IsStunned &&
                                             (champ.Spellbook.CanUseSpell(SpellSlot.R) == SpellState.Ready || (champ.Spellbook.GetSpell(SpellSlot.R).Level > 0 && champ.Spellbook.CanUseSpell(SpellSlot.R) == SpellState.Surpressed && champ.Mana >= UltInfo[champ.ChampionName].ManaCost)) //use when fixed: champ.Spellbook.GetSpell(SpellSlot.R).Ready or champ.Spellbook.GetSpell(SpellSlot.R).ManaCost)
                                          select champ
                                 )
            {
                if (champ.ChampionName != "Ezreal" && !Helper.CheckNoCollision(champ.ServerPosition.To2D(), EnemySpawnPos.To2D(), target.NetworkId, UltInfo[champ.ChampionName].Width, UltInfo[champ.ChampionName].Delay, UltInfo[champ.ChampionName].Speed, UltInfo[champ.ChampionName].Range))
                    continue;

                float timeneeded = Helper.GetSpellTravelTime(champ, UltInfo[champ.ChampionName].Speed, UltInfo[champ.ChampionName].Delay, EnemySpawnPos) + Game.Ping - (70 + champ.ChampionName == "Jinx" ? 10 : 0); //increase timeneeded if it should arrive earlier, decrease if later | let jinx shoot later so ult does more damage, keppo (calculate that, needs custom health param in DamageLib)

                if (timeneeded - countdown > 100)
                    continue;

                playerinfo.ultDamage[champ.ChampionName] = (float)UltDamage(champ, target) * UltInfo[champ.ChampionName].DamageReductionMultiplicator;

                if (countdown <= timeneeded && timeneeded - countdown < 100)
                    if (champ.IsMe)
                        Shoot = true;
            }

            float totalUltDamage = 0;

            foreach (float ultdamage in playerinfo.ultDamage.Values)
                totalUltDamage += ultdamage;

            if (!Shoot || Menu.Item("panicKey").GetValue<KeyBind>().Active)
                return;

            playerinfo.ultDamage.Clear(); //wrong placement?

            float targetHealth = Helper.GetTargetHealth(playerinfo, countdown);

            if(Environment.TickCount - playerinfo.lastSeen > 22500)
            {
                if (totalUltDamage < target.MaxHealth)
                {
                    Game.PrintChat("DONT SHOOT, TOO LONG NO VISION {0} (Health: {1} UltDamage: {2})", playerinfo.GetPlayer().ChampionName, targetHealth, totalUltDamage);
                    return;
                }
            }
            else if (totalUltDamage < targetHealth)
            {
                Game.PrintChat("DONT SHOOT {0} (Health: {1} UltDamage: {2})", playerinfo.GetPlayer().ChampionName, targetHealth, totalUltDamage);
                return;
            }

            if (Menu != null && Menu.Item("debugMode").GetValue<bool>())
                Game.PrintChat("SHOOT {0} (Health: {1} UltDamage: {2})", playerinfo.GetPlayer().ChampionName, targetHealth, totalUltDamage);

            Ult.Cast(EnemySpawnPos, true);
            UltCasted = Environment.TickCount;
        }

        static PlayerInfo AddRecall(Packet.S2C.Recall.Struct newrecall)
        {
            foreach (PlayerInfo playerinfo in Players)
            {
                if (playerinfo == null) continue;

                if (playerinfo.recall.UnitNetworkId == newrecall.UnitNetworkId) //update info if already existing
                {
                    if(newrecall.Status == Packet.S2C.Recall.RecallStatus.RecallStarted)
                        playerinfo.ultDamage.Clear();

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
