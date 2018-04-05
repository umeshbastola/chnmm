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
        private LinkedList<TrajectoryModel> knownTrajectories = new LinkedList<TrajectoryModel>();

        public static void Main (){
            var strokeCollection = new List<KeyValuePair<String, StrokeData>>();
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
            command.CommandText = "SELECT * FROM trajectories";
            NpgsqlDataReader reader = command.ExecuteReader();

            DataTable dt = new DataTable();
            dt.Load(reader);
            var gestureName = "default";
            int maxLevel = Convert.ToInt32(dt.Compute("max([stroke_seq])", string.Empty));
            List<string[]> firstFromEach = new List<string[]>();
            int count = 0;
            foreach (DataRow row in dt.Rows)
            {
                string[] temp = new string[3];
                string[,] db_points = row["points"] as string[,];
                var user = Convert.ToInt32(row["id"]);
                var trace = Convert.ToInt32("2");
                var trajectory = new StrokeData(user, trace, db_points);
                strokeCollection.Add(new KeyValuePair<String, StrokeData>(row["stroke_seq"].ToString(), trajectory));
                count++;
            }
            var lookup = strokeCollection.ToLookup(kvp => kvp.Key, kvp => kvp.Value);
            for (int i = 0; i <= maxLevel; i++)
            {
                List<StrokeData> fingerCollection = new List<StrokeData>();
                foreach (StrokeData x in lookup[i.ToString()])
                {
                    fingerCollection.Add(x);
                }
                cs.trainGesture(gestureName, fingerCollection.Cast<BaseTrajectory>());
            }
        }
    }
}
