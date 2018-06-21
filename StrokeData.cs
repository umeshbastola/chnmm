using System;
using System.Collections.Generic;
using System.Linq;
using GestureRecognitionLib;

namespace MultiStrokeGestureRecognitionLib
{
    public class StrokeData : BaseTrajectory
    {
        public int UserID { get; }
        public override TrajectoryPoint[] TrajectoryPoints { get; }

        public StrokeData(int user, List<int[]> points)
        {
            UserID = user;
            TrajectoryPoints = convertPoints(points).ToArray();
        }

        public static IEnumerable<TrajectoryPoint> convertPoints(List<int[]> points)
        {
            foreach (var point in points)
            {
                int x = point[0];
                int y = point[1];
                long t = point[2];
                int seq = point[3];
                double dx = x;
                double dy = y;
                yield return new TrajectoryPoint(dx, dy, t, seq);
            }
        }
    }
}