using System;
using System.Data;
using System.Collections.Generic;
using Npgsql;
using GestureRecognitionLib;
using GestureRecognitionLib.CHnMM;
using ExperimentLib;
using GestureRecognitionLib.CHnMM.Estimators;
using System.Linq;

namespace MultiStrokeGestureRecognitionLib
{
    public class Program
    {
        public static void Main (){
            var strokeCollection = new List<KeyValuePair<String, StrokeData>>();
            var pointsCollection = new Dictionary<String, IEnumerable<TrajectoryPoint>>();
            var param1 = new IntParamVariation("nAreaForStrokeMap", 10, 10, 20);
            var param2 = new DoubleParamVariation("minRadiusArea", 0.01, 0.04, 0.25);
            var param3 = new DoubleParamVariation("toleranceFactorArea", 1.1, 0.4, 2.5);
            var param5 = new BoolParamVariation("useFixAreaNumber", true);
            var param6 = new BoolParamVariation("useSmallestCircle", true);
            var param7 = new BoolParamVariation("isTranslationInvariant", true);
            var param8 = new BoolParamVariation("useAdaptiveTolerance", false);
            var param9 = new DoubleParamVariation("hitProbability", 0.9);
            var param10 = new StringParamVariation("distEstName", new string[]
            {
                nameof(NaiveUniformEstimator),
                nameof(NormalEstimator)
            });

            var param11 = new BoolParamVariation("useEllipsoid", false);
            var configSet = ParameterVariation.getParameterVariations(param1, param2, param3, param5, param6, param7, param8, param9, param10, param11).Select(ps => new CHnMMParameter(ps)).ToArray();
            CHnMMClassificationSystem cs = new CHnMMClassificationSystem(configSet[0]);
            string ConnectionString = "Server=localhost; Port=5432; User Id=touchy; Password=123456;Database = touchy_data_development";
            NpgsqlConnection connection = new NpgsqlConnection(ConnectionString);
            try
            {
                connection.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM trajectories ORDER BY 5";
            NpgsqlDataReader reader = command.ExecuteReader();
            DataTable dt = new DataTable();
            dt.Load(reader);
            int maxExec = Convert.ToInt32(dt.Compute("max([exec_num])", string.Empty));


            foreach (DataRow row in dt.Rows)
            {
                string[,] db_points = row["points"] as string[,];
                var user = Convert.ToInt32(row["id"]);
                var trace = Convert.ToInt32(row["stroke_seq"]);
                var gestureName = row["user_id"] + "-" + row["gesture_id"] + "-" +row["exec_num"].ToString();
                var trajectory = StrokeData.convertPoints(db_points);
                if (pointsCollection.ContainsKey(gestureName))
                {
                    pointsCollection[gestureName] = pointsCollection[gestureName].Concat(trajectory);
                }
                else
                {
                    pointsCollection.Add(gestureName, trajectory);
                }
            }
            List<StrokeData> gestureCollection = new List<StrokeData>();
            foreach (KeyValuePair<String, IEnumerable<TrajectoryPoint>> gesture in pointsCollection)
            {
                string[] tokens = gesture.Key.Split('-');
                var trajectory = new StrokeData(Convert.ToInt32(tokens[0]), Convert.ToInt32(tokens[1]), gesture.Value.ToArray());
                gestureCollection.Add(trajectory);
            }
            cs.trainGesture("ooo", gestureCollection.Cast<BaseTrajectory>());
        }
    }
}
