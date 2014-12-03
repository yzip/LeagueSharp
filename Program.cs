using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LeagueSharp;
using LeagueSharp.Common;

namespace Kappa
{
    class Program
    {
        static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
        }

        static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if(sender.Type == GameObjectType.obj_AI_Hero)
            {
                var hero = (Obj_AI_Hero)sender;
                Game.PrintChat("sender: " + hero.ChampionName + " target: " + args.Target.NetworkId);
            }

            
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            foreach (var ally in ObjectManager.Get<Obj_AI_Hero>().Where(h => !h.IsMe))
            {
                Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.Q)).Send();
                Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.W)).Send();
                Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.E)).Send();
                Packet.C2S.Cast.Encoded(new Packet.C2S.Cast.Struct(ally.NetworkId, SpellSlot.R)).Send();
                Game.PrintChat("sent");
            }
        }
    }
}
