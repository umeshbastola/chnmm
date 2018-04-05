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

        public StrokeData(int user, int trace, string[,] points){
            UserID = user;
            TraceID = trace;
            TrajectoryPoints = convertPoints(points).ToArray();
        }

        public static IEnumerable<TrajectoryPoint> convertPoints(string[,] points)
        {
            int rowLength = points.GetLength(0);
            for (int i = 0; i < rowLength; i++)
            {
                int x = Convert.ToInt32(points[i,0]);
                int y = Convert.ToInt32(points[i,1]);
                long t = Convert.ToInt64(points[i,2]);
                double dx = x;
                double dy = y;

                yield return new TrajectoryPoint(dx, dy, t);
            }
        }
    }
}
