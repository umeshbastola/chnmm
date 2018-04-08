using System;
using System.Collections.Generic;
using System.Linq;
using GestureRecognitionLib;

namespace MultiStrokeGestureRecognitionLib
{
    public class StrokeData : BaseTrajectory
    {
        public int UserID { get; }
        public int TraceID { get; }
        public override TrajectoryPoint[] TrajectoryPoints { get; }

        public StrokeData(int user, int trace, List<KeyValuePair<int, string[]>> points){
            UserID = user;
            TraceID = trace;
            TrajectoryPoints = convertPoints(points,trace).ToArray();
        }

        public static IEnumerable<TrajectoryPoint> convertPoints(List<KeyValuePair<int, string[]>> points, int a)
        {
            foreach (KeyValuePair<int, string[]> point in points)
            {
                int x = Convert.ToInt32(point.Value[0]);
                int y = Convert.ToInt32(point.Value[1]);
                long t = Convert.ToInt64(point.Value[2]);
                double dx = x;
                double dy = y;
                yield return new TrajectoryPoint(dx, dy, t);
            }
        }
    }
}
