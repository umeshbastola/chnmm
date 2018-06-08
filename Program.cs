using System;
using System.Data;
using System.Collections.Generic;
using Npgsql;
using GestureRecognitionLib;
using GestureRecognitionLib.CHnMM;
using ExperimentLib;
using GestureRecognitionLib.CHnMM.Estimators;
using System.Linq;
using System.IO;

namespace MultiStrokeGestureRecognitionLib
{
    public class Program
    {
        public static void Main()
        {
            var strokeCollection = new List<KeyValuePair<String, StrokeData>>();
            var param1 = new IntParamVariation("nAreaForStrokeMap", 10, 5, 20);
            var param2 = new DoubleParamVariation("minRadiusArea", 0.01, 0.04, 0.25);
            var param3 = new DoubleParamVariation("toleranceFactorArea", 1.7, 0.2, 2.1);
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
            command.CommandText = "SELECT id FROM Tgestures";
            NpgsqlDataReader ges_read = command.ExecuteReader();
            DataTable gesture_table = new DataTable();
            gesture_table.Load(ges_read);
            ges_read.Close();
            var set = configSet[0];
            //foreach (var set in configSet)
            //{
                bool head = true;
                var file_name = "../../recognition/";
                file_name += "tol_" + set.toleranceFactorArea;
                file_name += "_dist_" + set.distEstName;
                file_name += "_nArea_" + set.nAreaForStrokeMap;
                file_name += ".csv";
                Console.WriteLine(file_name);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                CHnMMClassificationSystem cs = new CHnMMClassificationSystem(set);
                command.CommandText = "SELECT * FROM trajectories WHERE exec_num % 2 = 1";
                NpgsqlDataReader reader = command.ExecuteReader();
                DataTable dt = new DataTable();
                dt.Load(reader);
                reader.Close();
                foreach (DataRow row in dt.Rows)
                {
                    string[,] db_points = row["points"] as string[,];
                    var gestureName = row["user_id"] + "-" + row["gesture_id"] + ":" + row["stroke_seq"];
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

                for (int global_user = 1; global_user < 12; global_user++)
                {
                    foreach (DataRow gesture in gesture_table.Rows)
                    {
                        var result_matrix = new Dictionary<string, int>();
                        for (int u = 1; u < 12; u++)
                        {
                            for (int g = 1; g < 15; g++)
                            {
                                result_matrix.Add(u + "-" + g, 0);
                            }
                        }
                        result_matrix.Add("err", 0);
                        command.CommandText = "SELECT * FROM trajectories WHERE gesture_id =" + gesture["id"] + " AND user_id =" + global_user + " AND exec_num % 2 = 0 order by exec_num";
                        NpgsqlDataReader read = command.ExecuteReader();
                        DataTable dv = new DataTable();
                        dv.Load(read);
                        int maxstroke = (int)dv.Compute("MAX([stroke_seq])", "");
                        var recognized = new List<KeyValuePair<string, double>>();
                        var result = new List<KeyValuePair<string, double>>();
                        var current_exec = 1;
                        foreach (DataRow row in dv.Rows)
                        {
                            if (current_exec != Convert.ToInt32(row["exec_num"]))
                            {
                                
                                var sum = new Dictionary<string, double>();
                                recognized.Sort(CompareName);
                                foreach (var pair in recognized)
                                {
                                var adding = true;
                                for (int pre = 0; pre < maxstroke; pre++){
                                    if (recognized.Where(kvp => kvp.Key == (pair.Key.Split(':')[0] + ":" + pre)).Count() == 0)
                                    {
                                        adding = false;
                                        break;
                                    }
                                }
                                if(adding)
                                    result.Add(new KeyValuePair<string, double>(pair.Key.Split(':')[0], pair.Value));
                                }
                                var comp = result.ToLookup(pair => pair.Key, pair => pair.Value);
                                var keyset = comp.Select(g => g.Key).ToList();
                                for (int i = 0; i < keyset.Count; i++)
                                {
                                    if (comp[keyset[i]].Count() > maxstroke)
                                    {
                                        sum[keyset[i]] = 1;
                                        foreach (var x in comp[keyset[i]])
                                        {
                                            sum[keyset[i]] += x;
                                        }
                                    }
                                }
                                var ordered = sum.OrderByDescending(x => x.Value);
                                if (ordered.Count() > 0)
                                {
                                    result_matrix[ordered.First().Key] += 1;
                                }
                                else
                                    result_matrix["err"] += 1;

                                //foreach (var data in ordered)
                                //{
                                //    Console.WriteLine(data.Key + " : " + data.Value);
                                //}
                                recognized.Clear();
                                result.Clear();
                                current_exec = Convert.ToInt32(row["exec_num"]);
                            }
                            string[,] db_points = row["points"] as string[,];
                            var user = Convert.ToInt32(row["id"]);
                            var trace = Convert.ToInt32(row["stroke_seq"]);
                            var trajectory = new StrokeData(user, trace, db_points);
                            recognized.AddRange(cs.recognizeMultiStroke(trajectory));
                        }
                        string row_val = "";
                        string header = "";
                        for (int u = 1; u < 12; u++)
                        {
                            for (int g = 1; g < 15; g++)
                            {
                                header += u + "-" + g + ",";
                                row_val += result_matrix[u + "-" + g] + ",";
                            }
                        }
                        row_val += result_matrix["err"];
                        if (head)
                        {
                            sb.AppendLine(header);
                            head = false;
                        }
                        sb.AppendLine(row_val);
                        File.WriteAllText(file_name, sb.ToString());
                    }
                    strokeCollection.Clear();

                }
            }
        //}
        static int CompareName(KeyValuePair<string, double> a, KeyValuePair<string, double> b)
        {
            return a.Key.CompareTo(b.Key);
        }
    }
}
