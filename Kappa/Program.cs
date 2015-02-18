using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

using LeagueSharp;
using SharpDX;
using LeagueSharp.Common;

namespace Kappa
{
    [ServiceContract]
    public interface IBaseUlt3_API
    {
        [OperationContract]
        bool IsRecalling(Obj_AI_Hero hero);

        [OperationContract]
        int GetRecallCountdown(Obj_AI_Hero hero);
    };

    public class BaseUlt3_API : IBaseUlt3_API
    {
        public bool IsRecalling(Obj_AI_Hero hero)
        {
            return true;
        }

        public int GetRecallCountdown(Obj_AI_Hero hero)
        {
            return 12034234;
        }
    };

    class Program
    {
        static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            Game.PrintChat("BaseUlt loaded!");

            Shared.ShareInterface<BaseUlt3_API>();
        }
    }
}
