using System;
using LeagueSharp;
using LeagueSharp.Common;

namespace BaseUlt2
{
    class Program
    {
        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            for (int i = 0; i < 2; i++)
                Game.PrintChat("BASEULT2 IS OUTDATED, PLEASE USE BASEULT3");
        }
    }
}
