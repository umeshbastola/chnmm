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
        public static void Main()
        {
            var strokeCollection = new List<KeyValuePair<String, StrokeData>>();
            var param1 = new IntParamVariation("nAreaForStrokeMap", 10, 5, 20);
            var param2 = new DoubleParamVariation("minRadiusArea", 0.01, 0.04, 0.25);
            var param3 = new DoubleParamVariation("toleranceFactorArea", 1.7, 0.4, 2.5);
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

            string ConnectionString = "Server=localhost; Port=5432; User Id=macbook; Database = touchy_data_development";
            string ConnectionString_heroku = "Database=dcbpejtem8e4qu; Server=ec2-54-75-239-237.eu-west-1.compute.amazonaws.com; Port=5432; User Id=pbcgcsyjsmpeds; Password=323743a3eec80c0a49dcee493617af7b94fee458a6a89a671dc3acaad0c3f437; Sslmode=Require;Trust Server Certificate=true";
            NpgsqlConnection connection = new NpgsqlConnection(ConnectionString);
            NpgsqlConnection connection_1 = new NpgsqlConnection(ConnectionString);
            try
            {
                connection.Open();
                connection_1.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            NpgsqlCommand waiting = connection_1.CreateCommand();
            waiting.CommandText = "SELECT * FROM geslogs WHERE verified=0";
            NpgsqlDataReader wList = waiting.ExecuteReader();
            while (wList.Read())
            {
                
                NpgsqlCommand command = connection.CreateCommand();
                command.CommandText = "SELECT * FROM trajectories WHERE user_id=" + wList.GetValue(1) + " AND gesture_id=0";
                NpgsqlDataReader reader = command.ExecuteReader();
                DataTable dt = new DataTable();
                dt.Load(reader);
                var passwordSet = dt.Select("is_password=1");
                foreach (DataRow row in passwordSet)
                {

                    string[,] db_points = row["points"] as string[,];
                    var gestureName = row["user_id"] + "-" + row["gesture_id"] + "-" + row["stroke_seq"];
                    var user = Convert.ToInt32(row["id"]);
                    var trace = Convert.ToInt32(row["stroke_seq"]);
                    var trajectory = new StrokeData(user, trace, db_points);
                    strokeCollection.Add(new KeyValuePair<String, StrokeData>(gestureName, trajectory));
                }

                var lookup = strokeCollection.ToLookup(kvp => kvp.Key, kvp => kvp.Value);
                var keys = lookup.Select(g => g.Key).ToList();

                for (int i = 0; i < keys.Count; i++)
                {
                    List<StrokeData> fingerCollection = new List<StrokeData>();
                    foreach (StrokeData x in lookup[keys[i]])
                    {
                        fingerCollection.Add(x);
                    }
                    cs.trainGesture(keys[i], fingerCollection.Cast<BaseTrajectory>());
                }

                var ckeckSet = dt.Select("is_password=2");
                var result = new double[ckeckSet.Count()];
                int c = 0;
                foreach (DataRow row in ckeckSet)
                {
                    string[,] db_points = row["points"] as string[,];
                    var gestureName = row["user_id"] + "-" + row["gesture_id"] + "-" + row["stroke_seq"];
                    var user = Convert.ToInt32(row["id"]);
                    var trace = Convert.ToInt32(row["stroke_seq"]);
                    var trajectory = new StrokeData(user, trace, db_points);
                    result[c] = cs.getSimilarity(gestureName, trajectory);
                    c++;
                }

                bool ok = false;
                foreach (var prob in result)
                {
                    Console.WriteLine(prob);
                    if (prob <= 0)
                    {
                        ok = false;
                        break;
                    }
                    ok = true;
                }
                NpgsqlCommand insertion = connection.CreateCommand();

                if (ok)
                {
                    Console.WriteLine(" Gesture matches!");
                    insertion.CommandText = "UPDATE geslogs SET verified=1 WHERE user_id=" + wList.GetValue(1);
                    insertion.ExecuteNonQuery();
                }
                else
                {
                    Console.WriteLine(" Gesture does not match!");
                    insertion.CommandText = "UPDATE geslogs SET verified=2 WHERE user_id=" + wList.GetValue(1);
                    insertion.ExecuteNonQuery();
                }
            }
        }
    }
}
