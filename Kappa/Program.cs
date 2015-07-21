using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace masterys
{
    class Program
    {
        private static void Main(string[] args)
        {
            try
            {
                //Game.OnUpdate += Game_OnUpdate;
                Console.WriteLine("AttackDelay: " + ObjectManager.Player.AttackDelay);
                Console.WriteLine("Calc: " + ((int)ObjectManager.Player.AttackDelay * 1000));
                Console.WriteLine("Calc2: " + (ObjectManager.Player.AttackDelay * 1000));
                Console.WriteLine("Calc3: " + (ObjectManager.Player.AttackDelay * 1000f));

                Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe)
            {
                Console.WriteLine("kek: {0}", args.SData.OverrideCastTime);
            }
        }

        private static void Game_OnUpdate(EventArgs args)
        {
            Console.WriteLine("AttackDelay: " + ObjectManager.Player.AttackDelay);
            Console.WriteLine("Calc: " + ((int)ObjectManager.Player.AttackDelay * 1000));
            Console.WriteLine("Calc2: " + (ObjectManager.Player.AttackDelay * 1000));
            Console.WriteLine("Calc3: " + (ObjectManager.Player.AttackDelay * 1000f));
        }
    }
}