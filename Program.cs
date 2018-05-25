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
            var param1 = new IntParamVariation("nAreaForStrokeMap", 10);
            var param2 = new DoubleParamVariation("minRadiusArea", 0.01);
            var param3 = new DoubleParamVariation("toleranceFactorArea", 1.3);
            var param5 = new BoolParamVariation("useFixAreaNumber", true);
            var param6 = new BoolParamVariation("useSmallestCircle", true);
            var param7 = new BoolParamVariation("isTranslationInvariant", false);
            var param8 = new BoolParamVariation("useAdaptiveTolerance", false);
            var param9 = new DoubleParamVariation("hitProbability", 0.9);
            var param10 = new StringParamVariation("distEstName", new string[]
            {
                nameof(NaiveUniformEstimator)
                //nameof(NormalEstimator)
            });

            var param11 = new BoolParamVariation("useEllipsoid", false);
            var configSet = ParameterVariation.getParameterVariations(param1, param2, param3, param5, param6, param7, param8, param9, param10, param11).Select(ps => new CHnMMParameter(ps)).ToArray();
            CHnMMClassificationSystem cs = new CHnMMClassificationSystem(configSet[0]);

            string ConnectionString = "Server=localhost; Port=5432; User Id=touchy; Password=123456;Database = touchy_data_development";
            string ConnectionString_heroku = "Database=dcbpejtem8e4qu; Server=ec2-54-75-239-237.eu-west-1.compute.amazonaws.com; Port=5432; User Id=pbcgcsyjsmpeds; Password=323743a3eec80c0a49dcee493617af7b94fee458a6a89a671dc3acaad0c3f437; Sslmode=Require;Trust Server Certificate=true";
            NpgsqlConnection connection = new NpgsqlConnection(ConnectionString_heroku);
            try
            {
                connection.Open();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            NpgsqlCommand command = connection.CreateCommand();
            var global_user = 1;
            command.CommandText = "SELECT * FROM trajectories WHERE user_id="+global_user+ "AND exec_num % 2 = 1";
            NpgsqlDataReader reader = command.ExecuteReader();
            DataTable dt = new DataTable();
            dt.Load(reader);
            var trainingSet = dt.Select("exec_num < 21");
            foreach (DataRow row in trainingSet)
            {
                string[,] db_points = row["points"] as string[,];
                var gestureName = row["gesture_id"] + "-" + row["stroke_seq"];
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
            command.CommandText = "SELECT * FROM trajectories";
            NpgsqlDataReader read = command.ExecuteReader();
            DataTable dv = new DataTable();
            dv.Load(read);
            var testSet = dv.Select("exec_num >= 1");
            var result = new List<KeyValuePair<int, string>>();
            int c = 0;
            foreach (DataRow row in testSet)
            {
                string[,] db_points = row["points"] as string[,];
                var gestureName = row["gesture_id"] + "-" + row["stroke_seq"];
                var user = Convert.ToInt32(row["id"]);
                var trace = Convert.ToInt32(row["stroke_seq"]);
                var trajectory = new StrokeData(user, trace, db_points);

                var ges_id = row["gesture_id"].ToString();
                var exec_num = row["exec_num"].ToString();
                if(ges_id.Length == 1){
                    ges_id = "0" + ges_id;
                }
                if (exec_num.Length == 1)
                {
                    exec_num = "0" + exec_num;
                }
                var full_name = int.Parse(row["user_id"].ToString() + ges_id + exec_num + row["stroke_seq"].ToString());
                result.Add(new KeyValuePair<int, string>(full_name, row["user_id"] +"-"+ ges_id +"-"+ exec_num +"-"+ row["stroke_seq"] + ":" + cs.getSimilarity(gestureName, trajectory).ToString()));
                c++;
            }
            result  = result.OrderBy(x => x.Key).ThenBy(x => x.Value).ToList();
            var matching = "";
            int stroke_match = -1;
            var total_missed = 0;
            var ges_miss = 0;
            var false_user = 0;
            var prev_ges = "";
            for (int i = 1; i < result.Count; i++)
            {
                var current = result[i].Value.Split('-');
                var previous = result[i-1].Value.Split('-');
                var current_exec = current[2];
                var prev_exec = previous[2];
                var current_prob = current[3].Split(':')[1];
                var current_user = current[0];

                if (current_exec != prev_exec)
                {
                    var total_strokes = (Convert.ToInt32(previous[3].Split(':')[0]));

                    if (current_user == global_user.ToString() && stroke_match < total_strokes)
                    {
                        ges_miss++;
                        total_missed++;
                        Console.WriteLine(prev_exec);
                    }else if(current_user != global_user.ToString() && stroke_match == total_strokes){
                        false_user++;
                    }
                    if (stroke_match == total_strokes)
                    {
                        Console.WriteLine(matching);
                    }
                    stroke_match = -1;
                    matching = "";
                    prev_exec = current_exec;
                }
                if (current_prob != "0")
                {
                    stroke_match++;
                    matching = result[i].Value;
                }
                if (current_user == global_user.ToString() && current[1] != prev_ges)
                {
                    Console.WriteLine("############################################" + previous[1] + ":" + ges_miss);
                    prev_ges = current[1];
                    ges_miss = 0;
                }
            }
            Console.WriteLine("############################################" + ":" + ges_miss);
            Console.WriteLine(total_missed+ " -"+ false_user);

    //        var strokeCollection = new List<KeyValuePair<String, StrokeData>>();
    //        var param1 = new IntParamVariation("nAreaForStrokeMap", 10);
    //        var param2 = new DoubleParamVariation("minRadiusArea", 0.01);
    //        var param3 = new DoubleParamVariation("toleranceFactorArea", 1.7);
    //        var param5 = new BoolParamVariation("useFixAreaNumber", true);
    //        var param6 = new BoolParamVariation("useSmallestCircle", true);
    //        var param7 = new BoolParamVariation("isTranslationInvariant", false);
    //        var param8 = new BoolParamVariation("useAdaptiveTolerance", false);
    //        var param9 = new DoubleParamVariation("hitProbability", 0.9);
    //        var param10 = new StringParamVariation("distEstName", new string[]
    //        {
    //            nameof(NaiveUniformEstimator)
    //            //nameof(NormalEstimator)
    //        });

    //        var param11 = new BoolParamVariation("useEllipsoid", false);
    //        var configSet = ParameterVariation.getParameterVariations(param1, param2, param3, param5, param6, param7, param8, param9, param10, param11).Select(ps => new CHnMMParameter(ps)).ToArray();
    //        CHnMMClassificationSystem cs = new CHnMMClassificationSystem(configSet[0]);

    //        string ConnectionString = "Server=localhost; Port=5432; User Id=touchy; Password=123456;Database = touchy_data_development";
    //        string ConnectionString_heroku = "Database=dcbpejtem8e4qu; Server=ec2-54-75-239-237.eu-west-1.compute.amazonaws.com; Port=5432; User Id=pbcgcsyjsmpeds; Password=323743a3eec80c0a49dcee493617af7b94fee458a6a89a671dc3acaad0c3f437; Sslmode=Require;Trust Server Certificate=true";
    //        NpgsqlConnection connection = new NpgsqlConnection(ConnectionString_heroku);
    //        try
    //        {
    //            connection.Open();
    //        }
    //        catch (Exception ex)
    //        {
    //            Console.WriteLine(ex.ToString());
    //        }
    //        NpgsqlCommand command = connection.CreateCommand();
    //        command.CommandText = "SELECT * FROM trajectories WHERE gesture_id =1 AND exec_num % 2 = 1";
    //        NpgsqlDataReader reader = command.ExecuteReader();
    //        DataTable dt = new DataTable();
    //        dt.Load(reader);
    //        var trainingSet = dt.Select("exec_num < 21");

    //        foreach (DataRow row in trainingSet)
    //        {
    //            string[,] db_points = row["points"] as string[,];
    //            var gestureName = row["user_id"]+ "-"+ row["gesture_id"] + ":" + row["stroke_seq"];
    //            var user = Convert.ToInt32(row["id"]);
    //            var trace = Convert.ToInt32(row["stroke_seq"]);
    //            var trajectory = new StrokeData(user, trace, db_points);
    //            strokeCollection.Add(new KeyValuePair<String, StrokeData>(gestureName, trajectory));
    //        }

    //        var lookup = strokeCollection.ToLookup(kvp => kvp.Key, kvp => kvp.Value);
    //        var keys = lookup.Select(g => g.Key).ToList();
    //        for (int i = 0; i < keys.Count; i++)
    //        {
    //            List<StrokeData> fingerCollection = new List<StrokeData>();
    //            foreach (StrokeData x in lookup[keys[i]])
    //            {
    //                fingerCollection.Add(x);
    //            }
    //            cs.trainGesture(keys[i], fingerCollection.Cast<BaseTrajectory>());
    //        }
    //        command.CommandText = "SELECT * FROM trajectories WHERE gesture_id = 1 AND user_id = 9 AND exec_num % 2 = 0";
    //        NpgsqlDataReader read = command.ExecuteReader();
    //        DataTable dv = new DataTable();
    //        dv.Load(read);
    //        int maxstroke = (int)dv.Compute("MAX([stroke_seq])", ""); 
    //        var testSet = dv.Select("exec_num >= 1");
    //        var recognized = new List<KeyValuePair<string, double>>();
    //        var result = new List<KeyValuePair<string, double>>();
    //        var current_exec = 1;
    //        var match = false;
    //        foreach (DataRow row in testSet)
    //        {
    //            if(current_exec != Convert.ToInt32(row["exec_num"])){
    //                var sum = new Dictionary<string, double>();
    //                recognized.Sort(CompareName);
    //                foreach (var pair in recognized)
    //                {
    //                    result.Add(new KeyValuePair<string, double>(pair.Key.Split(':')[0],pair.Value));
    //                }
    //                var comp  = result.ToLookup(pair => pair.Key, pair => pair.Value);
    //                var keyset = comp.Select(g => g.Key).ToList();
    //                for (int i = 0; i < keyset.Count; i++)
    //                {
    //                    if(comp[keyset[i]].Count() > maxstroke){
    //                        sum[keyset[i]] = 0;
    //                        foreach (var x in comp[keyset[i]])
    //                        {
    //                            sum[keyset[i]] += x;
    //                        }
    //                    }
    //                    match = true;
    //                }
    //                var ordered = sum.OrderBy(x => x.Value);
    //                foreach(var data in sum){
    //                    Console.WriteLine(data.Key+ " : " +data.Value);
    //                }
    //                Console.WriteLine("=================="+Convert.ToInt32(row["exec_num"]));
    //                recognized.Clear();
    //                result.Clear();
    //                current_exec = Convert.ToInt32(row["exec_num"]);
    //            }
    //            if (!match)
    //                Console.WriteLine("No match found");
    //            string[,] db_points = row["points"] as string[,];
    //            var user = Convert.ToInt32(row["id"]);
    //            var trace = Convert.ToInt32(row["stroke_seq"]);
    //            var trajectory = new StrokeData(user, trace, db_points);
    //            recognized.AddRange(cs.recognizeMultiStroke(trajectory));
    //        }

    //    }
    //    static int CompareName(KeyValuePair<string, double> a, KeyValuePair<string, double> b)
    //    {
    //        return a.Key.CompareTo(b.Key);
    //    }

        
    }
}