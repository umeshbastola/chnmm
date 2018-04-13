using System;
using System.Data;
using System.Collections.Generic;
using Npgsql;
using GestureRecognitionLib;
using GestureRecognitionLib.CHnMM;
using ExperimentLib;
using GestureRecognitionLib.CHnMM.Estimators;
using System.Linq;
using System.Collections;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace MultiStrokeGestureRecognitionLib

{
    public class Program
    {
        public static void Main()
        {
            Task mytask = Task.Run(() =>
            {
                using (Form form = new Form())
                {
                    form.Text = "Hello its for canvas";
                    form.ShowDialog();
                }
            });
            var strokeCollection = new List<StrokeData>();
            var pointsCollection = new List<KeyValuePair<int, string[]>>();
            SortedList mySL = new SortedList();
            var param1 = new IntParamVariation("nAreaForStrokeMap", 100);
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
            command.CommandText = "SELECT * FROM trajectories WHERE user_id=1 AND gesture_id=1 AND exec_num<8 ORDER BY 5";
            NpgsqlDataReader reader = command.ExecuteReader();
            DataTable dt = new DataTable();
            dt.Load(reader);
            int maxExec = Convert.ToInt32(dt.Compute("max([exec_num])", string.Empty));


            foreach (DataRow row in dt.Rows)
            {
                string[,] db_points = row["points"] as string[,];
                for (int k = 0; k < db_points.GetLength(0); k++)
                {
                    string[] point = new string[6];
                    point[0] = db_points[k, 0];
                    point[1] = db_points[k, 1];
                    point[2] = db_points[k, 2];
                    point[3] = row["exec_num"].ToString();
                    point[4] = row["user_id"].ToString();
                    point[5] = row["gesture_id"].ToString();
                    pointsCollection.Add(new KeyValuePair<int, string[]>(Convert.ToInt32(point[2]), point));

                }
            }
            pointsCollection.Sort(SortByTime);
            var user_id = pointsCollection.First().Value[4];
            var gesture_id = pointsCollection.First().Value[5];
            for (int i = 1; i <= maxExec; i++)
            {
                IQueryable<KeyValuePair<int, string[]>> pointsQuery = pointsCollection.AsQueryable();
                var result = pointsQuery.Where(o => o.Value[3] == i.ToString());
                var trajectory = new StrokeData(Convert.ToInt32(user_id), Convert.ToInt32(result.First().Value[3]), result.ToList());
                strokeCollection.Add(trajectory);
            }
            cs.trainGesture(user_id + "_" + gesture_id, strokeCollection.Cast<BaseTrajectory>());
        }

        static int SortByTime(KeyValuePair<int, string[]> a, KeyValuePair<int, string[]> b)
        {
            return a.Key.CompareTo(b.Key);
        }
    }
}
