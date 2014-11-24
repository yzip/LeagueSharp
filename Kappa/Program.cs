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

        static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {

        }

        private static void Game_OnGameLoad(EventArgs args)
        {

        }

        static void Game_OnGameUpdate(EventArgs args)
        {
            
        }
    }
}
