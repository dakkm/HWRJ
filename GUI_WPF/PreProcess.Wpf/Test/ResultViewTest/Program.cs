using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Execution;
using PreProcess.Wpf.Services.Process;
using PreProcess.Wpf.Services.Results;
using PreProcess.Wpf.ViewModels;

namespace ResultViewTest
{
    internal static class Program
    {
        private static int passed;

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length != 1) { Console.Error.WriteLine("Usage: ResultViewTest <project-root>"); return 2; }
            string root = Path.GetFullPath(args[0]);
            try
            {
                TestStates();
                ResultBrowserViewModel forward = TestForward(root);
                ResultBrowserViewModel prediction = TestPrediction(root);
                TestSimilarity(root);
                TestScene(root);
                TestModuleSwitch(forward, prediction);
                TestXaml(root);
                Console.WriteLine("P5.3 ResultViewTest: " + passed + " assertions passed.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }

        private static void TestStates()
        {
            ResultBrowserViewModel empty = ResultBrowserViewModel.Empty("正向计算");
            Check(empty.RunStatus == "尚未运行" && !empty.HasDataSets, "no-result prompt");
            ResultBrowserViewModel loading = ResultBrowserViewModel.Loading("正向计算");
            Check(loading.IsLoading && loading.RunStatus == "运行中", "running state");
            var failedRecord = new RunRecord { Module = "01", RunId = "run_failed", State = ProcessRunState.Failed, Message = "后端失败", Diagnostic = "fixture diagnostic" };
            ResultBrowserViewModel failed = ResultBrowserViewModel.FromRecord(failedRecord, "正向计算");
            Check(failed.RunStatus == "失败" && failed.HasIssues && !failed.HasDataSets, "failed run state");
            ResultBrowserViewModel parseFailure = ResultBrowserViewModel.ReadFailed(failedRecord, "正向计算", new InvalidDataException("bad csv"));
            Check(parseFailure.RunStatus == "结果读取失败" && parseFailure.Issues.Any(x => x.Contains("bad csv")), "parse failure display");
        }

        private static ResultBrowserViewModel TestForward(string root)
        {
            string output = FindRunFile(root, "run_20260911_131206_54dd28bb", "temperature_history.csv").DirectoryName;
            var record = Completed("01", "run_20260911_131206_54dd28bb", output, Directory.GetParent(output).FullName);
            ResultBrowserViewModel view = ResultBrowserViewModel.FromResult(new ResultReader().Read(record), "正向计算", "正向计算完成。");
            Check(view.RunStatus == "已完成" && view.HasResult, "forward completed state");
            Check(view.DataSets.Count == 3, "forward three data entries");
            Check(view.DataSets.Sum(x => x.RowCount) == 1454, "forward table row counts");
            Check(view.SelectedDataSet != null && view.SelectedDataSet.Rows.Count == 78, "forward default table");
            Check(view.SelectedDataSet.Rows.Table.Columns.Cast<System.Data.DataColumn>().Any(x => x.ColumnName == "T_1"), "temperature column header");
            Check(view.Summary.Any(x => x.Name == "run_id" && x.Value == record.RunId), "run summary");
            return view;
        }

        private static ResultBrowserViewModel TestPrediction(string root)
        {
            string output = FindRunFile(root, "run_20260910_205238_9608c9ba", "prediction_summary.json").DirectoryName;
            ResultBrowserViewModel view = ResultBrowserViewModel.FromResult(
                new ResultReader().Read(Completed("02", "run_20260910_205238_9608c9ba", output)), "智能预测");
            Check(view.DataSets.Count == 3, "prediction data entries");
            Check(view.DataSets.Any(x => x.RowCount == 1616), "prediction gzip table exposed");
            Check(view.Summary.Any(x => x.Name == "prediction.mode" && x.Value == "both"), "prediction summary exposed");
            return view;
        }

        private static void TestSimilarity(string root)
        {
            string output = FindRunFile(root, "run_20260912_044041_2f565756", "evaluation_status.json").DirectoryName;
            ResultBrowserViewModel view = ResultBrowserViewModel.FromResult(
                new ResultReader().Read(Completed("03", "run_20260912_044041_2f565756", output)), "相似度评估");
            Check(view.DataSets.Count == 7 && view.DataSets[0].RowCount > 0, "similarity and feature tables exposed");
            Check(view.HasTemperatureChart && view.TemperatureChart.Series.Count == 2, "similarity dual curves exposed");
            Check(view.Summary.Any(x => x.Name.Contains("temperature_similarity_percent") &&
                Math.Abs(Double.Parse(x.Value, System.Globalization.CultureInfo.InvariantCulture) - 100.0) < 1e-12), "similarity overview");
        }

        private static void TestScene(string root)
        {
            string output = FindRunFile(root, "run_20260911_151927_cfafd7eb", "scene_search_summary.json").DirectoryName;
            ResultBrowserViewModel view = ResultBrowserViewModel.FromResult(
                new ResultReader().Read(Completed("04", "run_20260911_151927_cfafd7eb", output)), "红外场景构建");
            Check(view.DataSets.Count == 3, "scene data entries");
            Check(view.DataSets.Count(x => x.RowCount == 0) == 2 && view.DataSets.Any(x => x.RowCount == 5), "scene empty tables retained");
            Check(view.Summary.Any(x => x.Value == "ProxyOnlyCompleted"), "scene status summary");
        }

        private static void TestModuleSwitch(ResultBrowserViewModel forward, ResultBrowserViewModel prediction)
        {
            var cache = new ResultBrowserStore();
            cache.Remember("01", forward);
            cache.Remember("02", prediction);
            Check(ReferenceEquals(cache.Select("02", "智能预测"), prediction), "switch to prediction result");
            Check(ReferenceEquals(cache.Select("01", "正向计算"), forward), "switch back to forward result");
            ResultBrowserViewModel empty = cache.Select("04", "红外场景构建");
            Check(!empty.HasDataSets && empty.RunStatus == "尚未运行", "switch to module without result");
        }

        private static void TestXaml(string root)
        {
            string project = Path.Combine(root, "GUI_WPF", "PreProcess.Wpf", "PreProcess.Wpf");
            string browserPath = Path.Combine(project, "Views", "ResultBrowserView.xaml");
            string mainPath = Path.Combine(project, "MainWindow.xaml");
            XDocument.Parse(File.ReadAllText(browserPath));
            XDocument.Parse(File.ReadAllText(mainPath));
            string browser = File.ReadAllText(browserPath);
            string main = File.ReadAllText(mainPath);
            Check(browser.Contains("结果概要") && browser.Contains("结果数据表"), "status summary and data controls");
            Check(browser.Contains("EnableRowVirtualization=\"True\"") && browser.Contains("EnableColumnVirtualization=\"True\""), "data-grid virtualization");
            Check(main.Contains("<views:ResultBrowserView DataContext=\"{Binding Execution.ResultBrowser}\"/>") && !main.Contains("曲线显示区域。暂无结果。"), "right region replaced");
            string runViewModel = File.ReadAllText(Path.Combine(project, "ViewModels", "ForwardRunViewModel.cs"));
            Check(runViewModel.Contains("await Task.Run(() => ResultBrowserViewModel.FromResult"), "result conversion off UI thread");
        }

        private static RunRecord Completed(string module, string runId, string output, string runDirectory = null)
        {
            return new RunRecord { Module = module, RunId = runId, ResultDirectory = output, RunDirectory = runDirectory ?? output,
                State = ProcessRunState.Completed, StartedAt = DateTimeOffset.Now.AddSeconds(-2), EndedAt = DateTimeOffset.Now, Message = "完成" };
        }

        private static FileInfo FindRunFile(string root, string runId, string fileName)
        {
            FileInfo match = new DirectoryInfo(Path.Combine(root, "coreprogram")).EnumerateFiles(fileName, SearchOption.AllDirectories)
                .FirstOrDefault(x => x.FullName.IndexOf(runId, StringComparison.Ordinal) >= 0);
            if (match == null) match = new DirectoryInfo(Path.Combine(root, "GUI_WPF", "Verification")).EnumerateFiles(fileName, SearchOption.AllDirectories)
                .FirstOrDefault(x => x.FullName.IndexOf(runId, StringComparison.Ordinal) >= 0);
            if (match == null) throw new FileNotFoundException("Fixture not found: " + runId + "/" + fileName);
            return match;
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAILED: " + name);
            passed++; Console.WriteLine("PASS " + name);
        }
    }
}
