using System;
using LeagueSharp;
using LeagueSharp.Common;
1
namespace AhriSharp
{
    class Program
    {
        public static Helper Helper;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            Helper = new Helper();
            new Ahri();
        }
    }
}
