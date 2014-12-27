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
            if(args.PacketData[0] == Packet.S2C.Teleport.Header)
            {
                /*var packet = new GamePacket(args.PacketData);

                Utility.DumpPacket(packet);

                int UnitNetworkId = packet.ReadInteger(54);
                var gameObject = ObjectManager.GetUnitByNetworkId<Obj_AI_Hero>(UnitNetworkId);

                if (gameObject == null)
                    return;

                string typeAsString = packet.ReadString(6);
                string recallName = packet.ReadString(30);

                Game.PrintChat("recalling: " + gameObject.ChampionName + " Type: " + typeAsString + " RecallName: " + recallName);*/

                //var recall = Packet.S2C.Teleport.Decoded(args.PacketData);

                //Game.PrintChat("Recall: " + ObjectManager.GetUnitByNetworkId<Obj_AI_Hero>(recall.UnitNetworkId).ChampionName + " Type: " + recall.Type + " Status: " + recall.Status + " Duration: " + recall.Duration);
            }
        }
    }
}
