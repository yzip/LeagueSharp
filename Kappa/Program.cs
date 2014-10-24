using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LeagueSharp;
using SharpDX;
using LeagueSharp.Common;

namespace Kappa
{
    class Program
    {
        static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
            //Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
        }

        static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if(sender.Type == GameObjectType.obj_AI_Hero)
            {
                var hero = (Obj_AI_Hero)sender;
                Game.PrintChat("sender: " + hero.ChampionName + " target: " + args.Target.NetworkId);
            }
        }

        private static Obj_AI_Base UnitUnderCursor()
        {
            return ObjectManager
                .Get<Obj_AI_Base>()
                .Where(x => x.IsValid && !x.IsDead)
                .FirstOrDefault(unit => Vector2.Distance(Game.CursorPos.To2D(), unit.ServerPosition.To2D()) < 300);
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            //var unit = UnitUnderCursor();

            /*foreach (var ally in ObjectManager.Get<Obj_AI_Hero>().Where(h => !h.IsMe))
            {
                    Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.Q)).Send();
                    Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.W)).Send();
                    Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.E)).Send();
                    Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.R)).Send();
                    Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.Item1)).Send();
                    Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.Item2)).Send();
                    Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.Item3)).Send();
                    Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.Item4)).Send();
                    Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.Item5)).Send();
                    Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.Item6)).Send();
                    Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.Recall)).Send();
                    Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.Trinket)).Send();
                    Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.Unknown)).Send();
               
                    Game.PrintChat("sent");
            }*/

            /*foreach (var hero in ObjectManager.Get<Obj_AI_Hero>())
            {
                Console.WriteLine("" + hero.ChampionName + ":");

                for (var i = 0; i < 59; i++)
                {
                    var spell = hero.Spellbook.GetSpell((SpellSlot)i);
                    if (spell != null && spell.Name != null && spell.Name != "nospelldata" && spell.Name != "BaseSpell" && i > 11)
                    {
                        Console.WriteLine(i + " " + spell.Name);
                    }
                }
            }*/

            foreach (var ally in ObjectManager.Get<Obj_AI_Hero>().Where(h => !h.IsMe))// && h.IsAlly))
            {
                    Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.W)).Send();
                    Game.PrintChat("sent");
            }

            //freeze game
           //Game.OnGameUpdate += Game_OnGameUpdate;
        }

        static void Game_OnGameUpdate(EventArgs args)
        {
            foreach (var hero in ObjectManager.Get<Obj_AI_Base>())
            {
                Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(hero.NetworkId, SpellSlot.Q)).Send();
            }
        }
    }
}
