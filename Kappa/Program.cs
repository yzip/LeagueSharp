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

            Game.PrintChat("kappaHD");
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            try
            {
                if (Activator.CreateInstance(null, "Kappa." + ObjectManager.Player.ChampionName) != null)
                {
                    Game.PrintChat("Ok!");
                }
            }
            catch(Exception e)
            {
                Game.PrintChat("Error!");
            }
        }
    }
    
    class Champion
    {
        public Champion()
        {

        }
    }

    class Ahri : Champion
    {
        public Ahri()
        {
            Game.PrintChat("Loaded lel");
        }
    }
}
