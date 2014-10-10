using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace KarthusSharp
{
    class Program
    {
        static void Main(string[] args)
        {
            Game.OnGameStart += Game_OnGameStart;

            if (Game.Mode == GameMode.Running)
                Game_OnGameStart(new EventArgs());
        }

        private static void Game_OnGameStart(EventArgs args)
        {
            if (ObjectManager.Player.ChampionName != "Karthus")
                return;

            Game.PrintChat("KarthusSharp: Not yet keppo");
        }
    }
}
