using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web.UI;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;

namespace genicalgofinal
{
    // 1. تعريف كلاس لحفظ بيانات الطقس (مطابق لملف weather.csv)
    public class WeatherData
    {
        // 0: date (لن نستخدمه في الحسابات)
        public double CloudCover { get; set; }     // 1
        public double Sunshine { get; set; }           // 2
        public double GlobalRadiation { get; set; } // 3
        public double MaxTemp { get; set; }            // 4 (This is the TARGET)
        public double MeanTemp { get; set; }           // 5
        public double MinTemp { get; set; }            // 6
        public double Precipitation { get; set; }      // 7
        public double Pressure { get; set; }           // 8
        public double SnowDepth { get; set; }          // 9
    }

    // 2. كلاس بيانات المخطط البياني (لم يتغير)
    public class ChartData
    {
        public List<string> generations = new List<string>();
        public List<double> bestFitness = new List<double>();
        public List<double> avgFitness = new List<double>();
    }

    public partial class _default : System.Web.UI.Page
    {
        Random rand = new Random();
        List<WeatherData> trainingData; // قاعدة البيانات من CSV

        // 3. الكروموسوم الآن 9 أوزان (8 ميزات + 1 للتقاطع)
        int chromosomeSize = 9;

        // حامل لقيمة crossoverRate لاستخدامها داخل Crossover
        double currentCrossoverRate = 0.6;

        // 4. دالة تحميل البيانات من CSV — تستخدم TryParse وتعدّ الأسطر المتخطاة
        List<WeatherData> LoadDataFromCSV(out int skippedLines)
        {
            skippedLines = 0;
            var data = new List<WeatherData>();
            string path = Server.MapPath("~/App_Data/weather.csv");

            if (!File.Exists(path))
            {
                return data;
            }

            using (var reader = new StreamReader(path))
            {
                // تخطي السطر الأول (العناوين) إن وجد
                reader.ReadLine();

                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string[] values = line.Split(',');

                    if (values.Length != 10)
                    {
                        skippedLines++;
                        continue;
                    }

                    // تأكد من عدم وجود حقول فارغة في الأعمدة المهمة
                    bool anyEmpty =
                        string.IsNullOrWhiteSpace(values[1]) ||
                        string.IsNullOrWhiteSpace(values[2]) ||
                        string.IsNullOrWhiteSpace(values[3]) ||
                        string.IsNullOrWhiteSpace(values[4]) || // target
                        string.IsNullOrWhiteSpace(values[5]) ||
                        string.IsNullOrWhiteSpace(values[6]) ||
                        string.IsNullOrWhiteSpace(values[7]) ||
                        string.IsNullOrWhiteSpace(values[8]) ||
                        string.IsNullOrWhiteSpace(values[9]);

                    if (anyEmpty)
                    {
                        skippedLines++;
                        continue;
                    }

                    // === تهيئة المتغيرات لتجنّب CS0165 ===
                    double c1 = 0.0, c2 = 0.0, c3 = 0.0, c4 = 0.0, c5 = 0.0, c6 = 0.0, c7 = 0.0, c8 = 0.0, c9 = 0.0;

                    bool ok =
                        double.TryParse(values[1], NumberStyles.Any, CultureInfo.InvariantCulture, out c1) &&
                        double.TryParse(values[2], NumberStyles.Any, CultureInfo.InvariantCulture, out c2) &&
                        double.TryParse(values[3], NumberStyles.Any, CultureInfo.InvariantCulture, out c3) &&
                        double.TryParse(values[4], NumberStyles.Any, CultureInfo.InvariantCulture, out c4) &&
                        double.TryParse(values[5], NumberStyles.Any, CultureInfo.InvariantCulture, out c5) &&
                        double.TryParse(values[6], NumberStyles.Any, CultureInfo.InvariantCulture, out c6) &&
                        double.TryParse(values[7], NumberStyles.Any, CultureInfo.InvariantCulture, out c7) &&
                        double.TryParse(values[8], NumberStyles.Any, CultureInfo.InvariantCulture, out c8) &&
                        double.TryParse(values[9], NumberStyles.Any, CultureInfo.InvariantCulture, out c9);

                    if (!ok)
                    {
                        skippedLines++;
                        continue;
                    }

                    data.Add(new WeatherData
                    {
                        CloudCover = c1,
                        Sunshine = c2,
                        GlobalRadiation = c3,
                        MaxTemp = c4,
                        MeanTemp = c5,
                        MinTemp = c6,
                        Precipitation = c7,
                        Pressure = c8,
                        SnowDepth = c9
                    });
                }
            }

            return data;
        }

        // 6. دالة اللياقة (Fitness) — محمية ضد trainingData فارغ
        double CalculateFitness(double[] weights)
        {
            if (trainingData == null || trainingData.Count == 0) return 0.0;

            double totalSquaredError = 0.0;

            foreach (var day in trainingData)
            {
                double predictedValue =
                    weights[0] * day.CloudCover +
                    weights[1] * day.Sunshine +
                    weights[2] * day.GlobalRadiation +
                    weights[3] * day.MeanTemp +
                    weights[4] * day.MinTemp +
                    weights[5] * day.Precipitation +
                    weights[6] * day.Pressure +
                    weights[7] * day.SnowDepth +
                    weights[8]; // intercept

                double error = predictedValue - day.MaxTemp;
                totalSquaredError += error * error;
            }

            double mse = totalSquaredError / trainingData.Count;
            return 1.0 / (1.0 + mse);
        }

        // --- مساعدات GA: Selection / Crossover / Mutation ---

        // Roulette-wheel selection (يعيد Clone)
        double[] RouletteWheelSelection(List<double[]> population, List<double> fitness)
        {
            double sum = fitness.Sum();
            if (sum <= 0.0)
            {
                int idx = rand.Next(population.Count);
                return (double[])population[idx].Clone();
            }

            double r = rand.NextDouble() * sum;
            double cum = 0.0;
            for (int i = 0; i < fitness.Count; i++)
            {
                cum += fitness[i];
                if (r <= cum)
                {
                    return (double[])population[i].Clone();
                }
            }

            return (double[])population[population.Count - 1].Clone();
        }

        // Single-point crossover — يستخدم currentCrossoverRate
        void Crossover(double[] parent1, double[] parent2, out double[] child1, out double[] child2)
        {
            child1 = (double[])parent1.Clone();
            child2 = (double[])parent2.Clone();

            if (rand.NextDouble() > currentCrossoverRate) return;

            int point = rand.Next(1, chromosomeSize - 1);
            for (int i = point; i < chromosomeSize; i++)
            {
                double t = child1[i];
                child1[i] = child2[i];
                child2[i] = t;
            }
        }

        // Mutation: gaussian perturbation لكل جين
        void Mutation(double[] chromosome, double mutationRate)
        {
            const double sigma = 0.1;

            for (int i = 0; i < chromosome.Length; i++)
            {
                if (rand.NextDouble() < mutationRate)
                {
                    chromosome[i] += NextGaussian() * sigma;
                    if (double.IsNaN(chromosome[i]) || double.IsInfinity(chromosome[i]))
                        chromosome[i] = 0.0;
                }
            }
        }

        // Box-Muller Gaussian(0,1)
        double NextGaussian()
        {
            double u1 = 1.0 - rand.NextDouble();
            double u2 = 1.0 - rand.NextDouble();
            double r = Math.Sqrt(-2.0 * Math.Log(u1));
            double theta = 2.0 * Math.PI * u2;
            return r * Math.Cos(theta);
        }

        // 9. دالة تشغيل الخوارزمية (عند الضغط على الزر)
        protected void Button1_Click(object sender, EventArgs e)
        {
            int skipped;
            trainingData = LoadDataFromCSV(out skipped);
            if (trainingData == null || trainingData.Count == 0)
            {
                lblBestModel.Text = "خطأ: لم يتم العثور على ملف weather.csv في مجلد App_Data، أو أن الملف فارغ/تالف.";
                return;
            }

            // قراءة المعاملات من الواجهة
            int popSize = int.Parse(TextBox1.Text);
            double crossoverRate = double.Parse(TextBox3.Text) / 100.0;
            double mutationRate = double.Parse(TextBox4.Text) / 100.0;
            int maxGen = int.Parse(TextBox5.Text);

            // خزّن القيمة لاستخدامها داخل Crossover
            currentCrossoverRate = crossoverRate;

            ChartData chartData = new ChartData();

            //تهيئة السكان
            List<double[]> population = new List<double[]>();
            List<double> fitness = new List<double>();

            for (int i = 0; i < popSize; i++)
            {
                double[] chromosome = new double[chromosomeSize];
                for (int j = 0; j < chromosomeSize; j++)
                    chromosome[j] = (rand.NextDouble() * 2.0) - 1.0;

                population.Add(chromosome);
                fitness.Add(CalculateFitness(chromosome));
            }

            double bestOverallFitness = 0.0;
            double[] bestOverallChromosome = null;

            for (int gen = 0; gen < maxGen; gen++)
            {
                List<double[]> newPopulation = new List<double[]>();
                List<double> newFitness = new List<double>();

                // elitism — clone قبل الإضافة
                int bestIndex = fitness.IndexOf(fitness.Max());
                newPopulation.Add((double[])population[bestIndex].Clone());
                newFitness.Add(fitness[bestIndex]);

                if (fitness[bestIndex] > bestOverallFitness)
                {
                    bestOverallFitness = fitness[bestIndex];
                    bestOverallChromosome = (double[])population[bestIndex].Clone();
                }

                while (newPopulation.Count < popSize)
                {
                    double[] parent1 = RouletteWheelSelection(population, fitness);
                    double[] parent2 = RouletteWheelSelection(population, fitness);
                    double[] child1, child2;
                    Crossover(parent1, parent2, out child1, out child2);
                    Mutation(child1, mutationRate);
                    Mutation(child2, mutationRate);

                    if (newPopulation.Count < popSize)
                    {
                        newPopulation.Add(child1);
                        newFitness.Add(CalculateFitness(child1));
                    }
                    if (newPopulation.Count < popSize)
                    {
                        newPopulation.Add(child2);
                        newFitness.Add(CalculateFitness(child2));
                    }
                }

                population = newPopulation;
                fitness = newFitness;

                chartData.generations.Add((gen + 1).ToString());
                chartData.bestFitness.Add(fitness.Max());
                chartData.avgFitness.Add(fitness.Average());
            }

            // نتائج نهائية
            double bestFinalFitness = fitness.Max();
            double[] bestFinalChromosome = population[fitness.IndexOf(bestFinalFitness)];

            if (bestOverallFitness > bestFinalFitness && bestOverallChromosome != null)
            {
                bestFinalFitness = bestOverallFitness;
                bestFinalChromosome = bestOverallChromosome;
            }

            double bestMSE = (1.0 / bestFinalFitness) - 1.0;

            // احفظ بيانات المخطط في الحقل المخفي بصيغة JSON ليقرأها الجافاسكربت
            try
            {
                var serializer = new JavaScriptSerializer();
                hfChartData.Value = serializer.Serialize(chartData);
            }
            catch
            {
                hfChartData.Value = "";
            }

            lblBestFitness.Text = bestFinalFitness.ToString("F6");
            lblBestMSE.Text = bestMSE.ToString("F2");

            lblBestModel.Text =
                $"Cloud: {bestFinalChromosome[0]:F4}, Sun: {bestFinalChromosome[1]:F4}, Rad: {bestFinalChromosome[2]:F4}, " +
                $"Mean: {bestFinalChromosome[3]:F4}, Min: {bestFinalChromosome[4]:F4}, Prec: {bestFinalChromosome[5]:F4}, " +
                $"Pres: {bestFinalChromosome[6]:F4}, Snow: {bestFinalChromosome[7]:F4}, Intercept: {bestFinalChromosome[8]:F4}. " +
                $"(Rows used: {trainingData.Count}, Skipped: {skipped})";

            // أمثلة سريعة للمطابقة
            try
            {
                int examples = Math.Min(3, trainingData.Count);
                var sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine("أمثلة: Predicted => Actual");
                for (int i = 0; i < examples; i++)
                {
                    var d = trainingData[i];
                    double pred =
                        bestFinalChromosome[0] * d.CloudCover +
                        bestFinalChromosome[1] * d.Sunshine +
                        bestFinalChromosome[2] * d.GlobalRadiation +
                        bestFinalChromosome[3] * d.MeanTemp +
                        bestFinalChromosome[4] * d.MinTemp +
                        bestFinalChromosome[5] * d.Precipitation +
                        bestFinalChromosome[6] * d.Pressure +
                        bestFinalChromosome[7] * d.SnowDepth +
                        bestFinalChromosome[8];
                    sb.AppendLine($"{pred:F2} => {d.MaxTemp:F2}");
                }
                lblBestModel.Text += sb.ToString();
            }
            catch
            {
                // تجاهل أخطاء العرض
            }
        }
    }
}