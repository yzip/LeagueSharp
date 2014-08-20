using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace BaseUlt2
{
    class PlayerInfo
    {
        public Packet.S2C.Recall.Struct recall;
        public int lastSeen;
        public Dictionary<String,float> ultDamage;

        Obj_AI_Hero player;

        public PlayerInfo(Packet.S2C.Recall.Struct recall)
        {
            this.recall = recall;
            ultDamage = new Dictionary<string, float>();
        }

        public Obj_AI_Hero GetPlayer()
        {
            if (player == null)
                player = ObjectManager.GetUnitByNetworkId<Obj_AI_Hero>(recall.UnitNetworkId);

            return player;
        }

        public int GetTime()
        {
            int ping = Game.Ping;

            switch ((int)recall.Status)
            {
                //case (int)Packet.S2C.Recall.RecallStatus.RecallAborted:
                //case (int)Packet.S2C.Recall.RecallStatus.RecallFinished:
                case (int)Packet.S2C.Recall.RecallStatus.RecallStarted:
                    return Program.RecallT[recall.UnitNetworkId] - ping; //Packet.S2C.Recall.RecallT[recall.UnitNetworkId] - ping;

                //case (int)Packet.S2C.Recall.RecallStatus.TeleportAbort:
                //case (int)Packet.S2C.Recall.RecallStatus.TeleportEnd:
                case (int)Packet.S2C.Recall.RecallStatus.TeleportStart:
                    return Program.RecallT[recall.UnitNetworkId] - ping; //Packet.S2C.Recall.TPT[recall.UnitNetworkId] - ping;

                default:
                    return 0;
            }
        }

        override public string ToString()
        {
            string drawtext = GetPlayer().ChampionName + ": " + recall.Status; //change to better string

            float countdown = (float)(GetTime() + recall.Duration - Environment.TickCount) / 1000f;

            if (countdown > 0)
                drawtext += " (" + countdown.ToString("0.00") + "s)";

            return drawtext;
        }
    }
}
