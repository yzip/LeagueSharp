using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace BaseUlt2
{
    class Helper
    {
        public static float GetTargetHealth(PlayerInfo playerinfo, float additionalTime)
        {
            float predictedhealth = playerinfo.GetPlayer().Health + playerinfo.GetPlayer().HPRegenRate * ((float)(Environment.TickCount - playerinfo.lastSeen + additionalTime) / 1000f);
            return predictedhealth > playerinfo.GetPlayer().MaxHealth ? playerinfo.GetPlayer().MaxHealth : predictedhealth;
        }

        public static float GetSpellTravelTime(SpellDataInst ult, Vector3 targetpos)
        {
            float distance = Vector3.Distance(ObjectManager.Player.ServerPosition, targetpos);
            float missilespeed = ObjectManager.Player.ChampionName != "Jinx" ? ult.SData.MissileSpeed :
                (distance <= 1500f ? ult.SData.MissileSpeed : (1500f * ult.SData.MissileSpeed + ((distance - 1500f) * 2200f)) / distance); //1700 = missilespeed, 2200 = missilespeed after acceleration, 1500 = distance where acceleration activates

            return (distance / missilespeed + Math.Abs(ult.SData.SpellCastTime)) * 1000;
        }

        public static void DrawCircleTime(Vector2 center, float radius, float thickness, float precision, System.Drawing.Color color, int starttime, int endtime)
        {
            if (endtime <= Environment.TickCount)
                return;

            int vertices = (int)Math.Ceiling(360 * Math.Acos(2 * Math.Pow(1 - precision / radius, 2) - 1)); //instead of 2*PI -> 360? deg/rad?

            Vector2[] points = new Vector2[vertices];

            double angle = 2 * Math.PI / vertices;

            for (int i = 0; i < vertices; i++)
            {
                double x = center.X + radius * Math.Cos(i * angle);
                double y = center.Y + radius * Math.Sin(i * angle);
                points[i] = new Vector2((float)x, (float)y);
            }

            double pixels = 0;

            for (int i = 0; i < points.Length; i++) //2*pi*r faster?
                pixels += Vector2.Distance(points[i], points[(i + 1) % points.Length]);

            double pixelspersecond = pixels / ((float)(endtime - starttime) / 1000f);

            double pixelstodraw = ((float)(endtime - Environment.TickCount) / 1000f) * pixelspersecond;

            double pixelspervertice = pixels / vertices;//Vector2.Distance(points[0], points[1]);

            int verticestodraw = (int)Math.Ceiling(pixelstodraw / pixelspervertice);

            for (int i = 0; i < verticestodraw && i < points.Length; i++)
                if (OnScreen(points[i]) || OnScreen(points[(i + 1) % points.Length]))
                    Drawing.DrawLine(points[i].X, points[i].Y, points[(i + 1) % points.Length].X, points[(i + 1) % points.Length].Y, thickness, color); //% to set index to 0 in order to connect last point with first point to finish shape 
        }

        public static bool OnScreen(Vector2 screenpoint)
        {
            if (screenpoint.X >= 0 && screenpoint.Y >= 0 && screenpoint.X <= Drawing.Width && screenpoint.Y <= Drawing.Height)
                return true;

            return false;
        }

        public static Packet.S2C.Recall.Struct RecallDecode(byte[] data)
        {
            BinaryReader reader = new BinaryReader(new MemoryStream(data));
            Packet.S2C.Recall.Struct result = new Packet.S2C.Recall.Struct();

            reader.ReadByte(); //PacketId
            reader.ReadBytes(4);
            result.UnitNetworkId = BitConverter.ToInt32(reader.ReadBytes(4), 0);
            reader.ReadBytes(66);

            result.Status = Packet.S2C.Recall.RecallStatus.Unknown;

            bool teleport = false;

            if (BitConverter.ToString(reader.ReadBytes(6)) != "00-00-00-00-00-00")
            {
                if (BitConverter.ToString(reader.ReadBytes(3)) != "00-00-00")
                {
                    result.Status = Packet.S2C.Recall.RecallStatus.TeleportStart;
                    teleport = true;
                }
                else
                    result.Status = Packet.S2C.Recall.RecallStatus.RecallStarted;
            }

            reader.Close();

            Obj_AI_Hero unit = ObjectManager.GetUnitByNetworkId<Obj_AI_Hero>(result.UnitNetworkId);

            result.Duration = 0;

            if (unit != null)
            {
                if (teleport)
                    result.Duration = 3500;
                else
                {
                    string rName = unit.Spellbook.GetSpell(SpellSlot.Recall).Name;

                    switch (rName)
                    {
                        case "Recall": result.Duration = 8000; break;
                        case "RecallImproved": result.Duration = 7000; break;
                        case "OdinRecall": result.Duration = 4500; break;
                        case "OdinRecallImproved": result.Duration = 4000; break;
                    }
                }

                if (!Program.RecallT.ContainsKey(result.UnitNetworkId))
                    Program.RecallT.Add(result.UnitNetworkId, Environment.TickCount); //will result in status RecallStarted, which would be wrong if the assembly was to be loaded while somebody recalls
                else
                {
                    if (Program.RecallT[result.UnitNetworkId] == 0)
                        Program.RecallT[result.UnitNetworkId] = Environment.TickCount;
                    else
                    {
                        if (Environment.TickCount - Program.RecallT[result.UnitNetworkId] > result.Duration - 75)
                            result.Status = teleport ? Packet.S2C.Recall.RecallStatus.TeleportEnd : Packet.S2C.Recall.RecallStatus.RecallFinished;
                        else
                            result.Status = teleport ? Packet.S2C.Recall.RecallStatus.TeleportAbort : Packet.S2C.Recall.RecallStatus.RecallAborted;

                        Program.RecallT[result.UnitNetworkId] = 0; //recall aborted or finished, reset status
                    }
                }
            }

            return result;
        }
    }
}
