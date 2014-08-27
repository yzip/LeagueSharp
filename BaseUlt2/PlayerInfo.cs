using System;
using System.Collections.Generic;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace BaseUlt2
{
    class PlayerInfo
    {
        public Obj_AI_Hero champ;
        public Dictionary<int, float> incomingDamage;
        public int lastSeen;
        public Packet.S2C.Recall.Struct recall;

        public PlayerInfo(Obj_AI_Hero champ)
        {
            this.champ = champ;
            this.recall = new Packet.S2C.Recall.Struct(champ.NetworkId, Packet.S2C.Recall.RecallStatus.Unknown, Packet.S2C.Recall.ObjectType.Player, 0);
            this.incomingDamage = new Dictionary<int, float>();
        }

        public PlayerInfo UpdateRecall(Packet.S2C.Recall.Struct newRecall)
        {
            this.recall = newRecall;
            return this;
        }

        public int GetRecallStart()
        {
            switch ((int)recall.Status)
            {
                case (int)Packet.S2C.Recall.RecallStatus.RecallStarted:
                case (int)Packet.S2C.Recall.RecallStatus.TeleportStart:
                    return Program.RecallT[recall.UnitNetworkId];

                default:
                    return 0;
            }
        }

        public int GetRecallEnd()
        {
            return GetRecallStart() + recall.Duration;
        }

        public int GetRecallCountdown()
        {
            int countdown = GetRecallEnd() - Environment.TickCount;
            return countdown < 0 ? 0 : countdown;
        }

        override public string ToString()
        {
            string drawtext = champ.ChampionName + ": " + recall.Status; //change to better string

            float countdown = (float)GetRecallCountdown() / 1000f;

            if (countdown > 0)
                drawtext += " (" + countdown.ToString("0.00") + "s)";

            return drawtext;
        }
    }
}
