using System;
using System.Data;
using System.Collections.Generic;
using Npgsql;
using GestureRecognitionLib;
using GestureRecognitionLib.CHnMM;
using ExperimentLib;
using GestureRecognitionLib.CHnMM.Estimators;
using System.Linq;
using System.Xml;
using System.IO;

namespace MultiStrokeGestureRecognitionLib
{
    public class Program
    {
        private static Dictionary<string, int> result_matrix;
        private static int total_unclass, total_misclass;
        public static void Main()
        {
            var param1 = new IntParamVariation("nAreaForStrokeMap", 10, 5, 20);
            var param2 = new DoubleParamVariation("minRadiusArea", 0.01);
            var param3 = new DoubleParamVariation("toleranceFactorArea", 1.1, 0.2, 2.5);
            var param5 = new BoolParamVariation("useFixAreaNumber", true);
            var param6 = new BoolParamVariation("useSmallestCircle", true);
            var param7 = new BoolParamVariation("isTranslationInvariant", false);
            var param8 = new BoolParamVariation("useAdaptiveTolerance", false);
            var param9 = new DoubleParamVariation("hitProbability", 0.9);
            var param10 = new StringParamVariation("distEstName", new string[]
            {
                nameof(NaiveUniformEstimator),
                nameof(NormalEstimator)
            });

            var param11 = new BoolParamVariation("useEllipsoid", false);
            var configSet = ParameterVariation.getParameterVariations(param1, param2, param3, param5, param6, param7, param8, param9, param10, param11).Select(ps => new CHnMMParameter(ps)).ToArray();
            string ConnectionString = "Server=localhost; Port=5432; User Id=macbook; Database = touchy_data_development";
            string ConnectionString_heroku = "Database=dcbpejtem8e4qu; Server=ec2-54-75-239-237.eu-west-1.compute.amazonaws.com; Port=5432; User Id=pbcgcsyjsmpeds; Password=323743a3eec80c0a49dcee493617af7b94fee458a6a89a671dc3acaad0c3f437; Sslmode=Require;Trust Server Certificate=true";
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
            //#########################################################
            //RECOGNITION TASK//
            //#########################################################

            command.CommandText = "SELECT id FROM Tgestures";
            NpgsqlDataReader ges_read = command.ExecuteReader();
            DataTable gesture_table = new DataTable();
            gesture_table.Load(ges_read);
            ges_read.Close();
            //var set = configSet[0];
            foreach (var set in configSet)
            {
                total_unclass = 0;
                total_misclass = 0;
                bool head = true;
                var file_name = "../../single_model_rec/";
                file_name += "tol_" + set.toleranceFactorArea;
                file_name += "_dist_" + set.distEstName;
                file_name += "_nArea_" + set.nAreaForStrokeMap;
                file_name += ".csv";
                Console.Write(file_name);
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                CHnMMClassificationSystem cs = new CHnMMClassificationSystem(set);
                for (int global_user = 1; global_user < 12; global_user++)
                {
                    foreach (DataRow gesture in gesture_table.Rows)
                    {
                        List<StrokeData> gestureStrokes = new List<StrokeData>();
                        var accumulate = new List<int[]>();
                        command.CommandText = "SELECT * FROM trajectories WHERE user_id = " + global_user + " AND gesture_id=" + gesture["id"] + " AND exec_num % 2 = 1 ORDER BY exec_num, stroke_seq;";
                        NpgsqlDataReader reader = command.ExecuteReader();
                        DataTable dt = new DataTable();
                        dt.Load(reader);
                        reader.Close();
                        var prev_exec = 1;
                        var prev_stroke = 0;
                        var time_lapse = 0;
                        foreach (DataRow row in dt.Rows)
                        {
                            if (prev_exec != Convert.ToInt32(row["exec_num"]))
                            {
                                // when all the strokes from a gesture are collected 
                                var trajectory = new StrokeData(global_user, accumulate);
                                gestureStrokes.Add(trajectory);
                                prev_exec = Convert.ToInt32(row["exec_num"]);
                                time_lapse = 0;
                                accumulate.Clear();
                            }

                            string[,] db_points = row["points"] as string[,];
                            int trace = Convert.ToInt32(row["stroke_seq"]);
                            int rowLength = db_points.GetLength(0);
                            if (prev_stroke != trace)
                            {
                                time_lapse = accumulate.Last()[2];
                            }
                            for (int i = 0; i < rowLength; i++)
                            {
                                int[] single_pt = new int[4];
                                single_pt[0] = Convert.ToInt32(db_points[i, 0]);
                                single_pt[1] = Convert.ToInt32(db_points[i, 1]);
                                single_pt[2] = Convert.ToInt32(db_points[i, 2]) + time_lapse;
                                single_pt[3] = trace;
                                accumulate.Add(single_pt);
                            }
                        }
                        if (accumulate.Count > 0)
                        {
                            // add very last stroke to gesture
                            var last_trajectory = new StrokeData(global_user, accumulate);
                            gestureStrokes.Add(last_trajectory);
                        }
                        var gestureName = global_user + "-" + gesture["id"];
                        cs.trainGesture(gestureName, gestureStrokes.Cast<BaseTrajectory>());
                    }

                }
                //=================================TRAINING COMPLETE============================ 
                //==============================================================================
                //=================================RECOGNITION START============================

                for (int global_user = 1; global_user < 12; global_user++)
                {
                    foreach (DataRow gesture in gesture_table.Rows)
                    {
                        // For every gesture from every users
                        result_matrix = new Dictionary<string, int>(); // final confusion matrix initialization
                        for (int u = 1; u < 12; u++)
                        {
                            for (int g = 1; g < 15; g++)
                            {
                                result_matrix.Add(u + "-" + g, 0);
                            }
                        }
                        result_matrix.Add("err", 0);

                        command.CommandText = "SELECT * FROM trajectories WHERE user_id = " + global_user + " AND gesture_id=" + gesture["id"] + " AND exec_num % 2 = 0 ORDER BY exec_num";
                        NpgsqlDataReader reader = command.ExecuteReader();
                        DataTable dt = new DataTable();
                        dt.Load(reader);
                        reader.Close();
                        int maxstroke = (int)dt.Compute("MAX([stroke_seq])", "") + 1;
                        List<int[]>[] allStrokes = new List<int[]>[maxstroke];
                        var prev_exec = (int)dt.Rows[0]["exec_num"];

                        foreach (DataRow row in dt.Rows)
                        {
                            var accumulate = new List<int[]>();
                            if (prev_exec != Convert.ToInt32(row["exec_num"]))
                            {
                                //if all the strokes of a gesture are finished then enter
                                calculate_result(allStrokes, global_user, Convert.ToInt32(gesture["id"]), set, cs);
                                prev_exec = Convert.ToInt32(row["exec_num"]);
                                allStrokes = new List<int[]>[maxstroke];
                            }
                            string[,] db_points = row["points"] as string[,];
                            int trace = Convert.ToInt32(row["stroke_seq"]);
                            int rowLength = db_points.GetLength(0);

                            for (int i = 0; i < rowLength; i++)
                            {
                                //reformat the trajectory points intoa a list of arrays
                                int[] single_pt = new int[4];
                                single_pt[0] = Convert.ToInt32(db_points[i, 0]);
                                single_pt[1] = Convert.ToInt32(db_points[i, 1]);
                                single_pt[2] = Convert.ToInt32(db_points[i, 2]);
                                single_pt[3] = trace;
                                accumulate.Add(single_pt);
                            }
                            allStrokes[trace] = accumulate;
                        }
                        if (allStrokes[0] != null)
                        {
                            // add very last stroke to gesture and calculate its recognition result 
                            calculate_result(allStrokes, global_user, Convert.ToInt32(gesture["id"]), set, cs);
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
                }
                Console.Write("----" + total_misclass + "--------" + total_unclass);
                Console.WriteLine("");
            }
        }
        static IEnumerable<IEnumerable<T>> GetPermutations<T>(IEnumerable<T> list, int length)
        {
            if (length == 1) return list.Select(t => new T[] { t });
            return GetPermutations(list, length - 1)
                .SelectMany(t => list.Where(o => !t.Contains(o)),
                    (t1, t2) => t1.Concat(new T[] { t2 }));
        }

        static public void calculate_result(List<int[]>[] allStrokes, int global_user, int gesture, CHnMMParameter set, CHnMMClassificationSystem cs)
        {
            int[] match_length = Enumerable.Range(0, allStrokes.Length).ToArray();
            var time_lapse = 0;
            List<List<int>> possible_combinations = new List<List<int>>();
            for (int i = 0; i < allStrokes.Length; i++)
            {
                var trajectory = new StrokeData(global_user, allStrokes[i]);
                var current_length = cs.checkFeasibility(0, trajectory, set.nAreaForStrokeMap);
                if (current_length > 0)
                {
                    possible_combinations.Add(new List<int> { i });
                }
            }

            // finding incremental best combination
            if (possible_combinations.Count > 0)
            {
                for (int comb = 0; comb < possible_combinations.Count; comb++)
                {
                    for (int i = 0; i < allStrokes.Length; i++)
                    {
                        List<int[]> best_combination = new List<int[]>();
                        var strech = new List<int>();
                        for (int p = 0; p < possible_combinations[comb].Count; p++)
                        {
                            best_combination.AddRange(allStrokes[possible_combinations[comb][p]]);
                            strech.Add(possible_combinations[comb][p]);
                        }
                        if (!possible_combinations[comb].Contains(i))
                        {
                            best_combination.AddRange(allStrokes[i]);
                            var trajectory = new StrokeData(global_user, best_combination);
                            var current_length = cs.checkFeasibility(possible_combinations[comb].Count, trajectory, set.nAreaForStrokeMap);
                            if (current_length > possible_combinations[comb].Count)
                            {
                                strech.Add(i);
                                possible_combinations.Add(strech);
                            }

                        }
                    }
                }
            }

            // get probabilities for all resulting combinations
            List<KeyValuePair<string, double>> comb_results = new List<KeyValuePair<string, double>>();
            foreach (var comb in possible_combinations)
            {
                if (comb.Count == allStrokes.Length)
                {
                    List<int[]> best_combination = new List<int[]>();
                    time_lapse = 0;
                    foreach (var s in comb)
                    {
                        foreach (var point in allStrokes[s])
                        {
                            point[2] += time_lapse;
                        }
                        time_lapse = allStrokes[s].Last()[2];
                        best_combination.AddRange(allStrokes[s]);
                    }
                    var trajectory = new StrokeData(global_user, best_combination);
                    var result = cs.recognizeGesture(trajectory);
                    if (result != null)
                        comb_results.Add(new KeyValuePair<string, double>(result.Split(':')[0], Convert.ToDouble(result.Split(':')[1])));
                }
            }

            // get the first result with maximum similarity and evaluate it
            if (comb_results.Count > 0)
            {
                var ordered = comb_results.OrderByDescending(x => x.Value);
                if (ordered.First().Key != global_user + "-" + gesture)
                {
                    result_matrix[ordered.First().Key] += 1;
                    total_misclass++;
                }
            }
            else
            {
                result_matrix["err"] += 1;
                total_unclass++;
            }
        }
    }
}