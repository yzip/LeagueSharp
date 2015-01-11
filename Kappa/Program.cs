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
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            Game.OnGameProcessPacket += Game_OnGameProcessPacket;

            Game.PrintChat("Loaded!");
        }

        static void Game_OnGameProcessPacket(GamePacketEventArgs args)
        {

        }
    }
}
