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
using PreProcess.Wpf.Views;

namespace ResultViewTest
{
    internal static class Program
    {
        // 保存该组件运行所需的配置或中间状态。
        private static int passed;

        [STAThread]
        private static int Main(string[] args)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (args.Length != 1) { Console.Error.WriteLine("Usage: ResultViewTest <project-root>"); return 2; }
            string root = Path.GetFullPath(args[0]);
            // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
            try
            {
                TestStates();
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                TestSimilarityInputValidation();
                ResultBrowserViewModel forward = TestForward(root);
                ResultBrowserViewModel prediction = TestPrediction(root);
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                TestSimilarity(root);
                TestScene(root);
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                TestModuleSwitch(forward, prediction);
                TestXaml(root);
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                Console.WriteLine("P5.3 ResultViewTest: " + passed + " assertions passed.");
                return 0;
            }
            // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }

        private static void TestStates()
        {
            ResultBrowserViewModel empty = ResultBrowserViewModel.Empty("正向计算");
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            Check(empty.RunStatus == "尚未运行" && !empty.HasDataSets, "no-result prompt");
            ResultBrowserViewModel loading = ResultBrowserViewModel.Loading("正向计算");
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            Check(loading.IsLoading && loading.RunStatus == "运行中", "running state");
            var failedRecord = new RunRecord { Module = "01", RunId = "run_failed", State = ProcessRunState.Failed, Message = "后端失败", Diagnostic = "fixture diagnostic" };
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            ResultBrowserViewModel failed = ResultBrowserViewModel.FromRecord(failedRecord, "正向计算");
            Check(failed.RunStatus == "失败" && failed.HasIssues && !failed.HasDataSets, "failed run state");
            ResultBrowserViewModel parseFailure = ResultBrowserViewModel.ReadFailed(failedRecord, "正向计算", new InvalidDataException("bad csv"));
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            Check(parseFailure.RunStatus == "结果读取失败" && parseFailure.Issues.Any(x => x.Contains("bad csv")), "parse failure display");
        }

        private static void TestSimilarityInputValidation()
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var rule = new SimilarityPercentageValidationRule();
            Check(!rule.Validate("5", System.Globalization.CultureInfo.InvariantCulture).IsValid, "partial similarity input rejected without model setter");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(rule.Validate("50", System.Globalization.CultureInfo.InvariantCulture).IsValid, "minimum similarity input accepted");
            Check(rule.Validate("92.5", System.Globalization.CultureInfo.InvariantCulture).IsValid, "decimal similarity input accepted");
            Check(!rule.Validate("101", System.Globalization.CultureInfo.InvariantCulture).IsValid, "out-of-range similarity input rejected");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static ResultBrowserViewModel TestForward(string root)
        {
            string output = FindRunFile(root, "run_20260911_131206_54dd28bb", "temperature_history.csv").DirectoryName;
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            var record = Completed("01", "run_20260911_131206_54dd28bb", output, Directory.GetParent(output).FullName);
            ResultBrowserViewModel view = ResultBrowserViewModel.FromResult(new ResultReader().Read(record), "正向计算", "正向计算完成。");
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            Check(view.RunStatus == "已完成" && view.HasResult, "forward completed state");
            Check(view.DataSets.Count == 3, "forward three data entries");
            Check(view.DataSets.Sum(x => x.RowCount) == 284, "forward displayed row counts");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(view.SelectedDataSet != null && view.SelectedDataSet.Rows.Count == 78, "forward default table");
            Check(view.SelectedDataSet.Rows.Table.Columns.Cast<System.Data.DataColumn>().Any(x => x.ColumnName == "目标1温度（K）"), "Chinese temperature column header");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(AllHeadersAreChinese(view), "forward table headers localized");
            Check(view.Summary.Any(x => x.Name == "run_id" && x.Value == record.RunId), "run summary");
            // 返回当前步骤生成的结果，并结束本次调用。
            return view;
        }

        private static ResultBrowserViewModel TestPrediction(string root)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string output = FindRunFile(root, "run_20260910_205238_9608c9ba", "prediction_summary.json").DirectoryName;
            ResultBrowserViewModel view = ResultBrowserViewModel.FromResult(
                new ResultReader().Read(Completed("02", "run_20260910_205238_9608c9ba", output)), "智能预测");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(view.DataSets.Count == 2, "prediction forward-format data entries");
            Check(view.DataSets.Any(x => x.RowCount == 1616), "prediction gzip table exposed");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(view.DataSets[0].Rows.Table.Columns.Cast<System.Data.DataColumn>().Select(x => x.ColumnName).SequenceEqual(new[] { "帧序号", "时间（s）", "目标1温度（K）" }), "prediction temperature display matches forward format");
            Check(view.DataSets[1].Rows.Table.Columns.Cast<System.Data.DataColumn>().Any(x => x.ColumnName == "探测器辐照度（W·m⁻²）"), "prediction infrared display matches forward format");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(AllHeadersAreChinese(view), "prediction table headers localized");
            Check(view.Summary.Any(x => x.Name == "prediction.mode" && x.Value == "both"), "prediction summary exposed");
            return view;
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void TestSimilarity(string root)
        {
            string output = FindRunFile(root, "run_20260912_044041_2f565756", "evaluation_status.json").DirectoryName;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            ResultBrowserViewModel view = ResultBrowserViewModel.FromResult(
                new ResultReader().Read(Completed("03", "run_20260912_044041_2f565756", output)), "相似度评估");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(view.DataSets.Count == 8 && view.DataSets[0].Name == "相似度结果" && view.DataSets[0].RowCount == 3,
                "similarity percentage overview is default table");
            Check(view.DataSets[0].Rows.Table.Columns.Cast<System.Data.DataColumn>().Select(x => x.ColumnName).SequenceEqual(
                // 继续处理当前业务步骤，保持上下文状态一致。
                new[] { "评价类型", "相似度", "限制特征" }), "similarity overview headers");
            Check(view.DataSets[0].Rows.Cast<System.Data.DataRowView>().All(x => x[1].ToString().EndsWith("%")),
                // 继续处理当前业务步骤，保持上下文状态一致。
                "similarity overview values formatted");
            Check(AllHeadersAreChinese(view), "similarity table headers localized");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(view.HasTemperatureChart && view.TemperatureChart.Series.Count == 2, "similarity dual curves exposed");
            Check(view.Summary.Any(x => x.Name.Contains("temperature_similarity_percent") &&
                Math.Abs(Double.Parse(x.Value, System.Globalization.CultureInfo.InvariantCulture) - 100.0) < 1e-12), "similarity overview");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void TestScene(string root)
        {
            string output = FindRunFile(root, "run_20260911_151927_cfafd7eb", "scene_search_summary.json").DirectoryName;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            ResultBrowserViewModel view = ResultBrowserViewModel.FromResult(
                new ResultReader().Read(Completed("04", "run_20260911_151927_cfafd7eb", output)), "红外场景构建");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(view.DataSets.Count == 4, "scene summary and detailed data entries");
            Check(view.DataSets[0].Name == "候选相似度结果" && view.DataSets[0].RowCount == 1, "scene similarity summary is default table");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(view.DataSets[0].Rows.Table.Columns.Cast<System.Data.DataColumn>().Select(x => x.ColumnName).SequenceEqual(
                new[] { "候选", "代理预测相似度", "正向复核相似度", "单次复核耗时" }), "scene similarity summary headers");
            Check(view.DataSets[0].Rows[0][1].ToString().EndsWith("%"), "scene similarity values formatted");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(view.DataSets.Count(x => x.RowCount == 0) == 2 && view.DataSets.Any(x => x.RowCount == 5), "scene empty detailed tables retained");
            Check(AllHeadersAreChinese(view), "scene table headers localized");
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            Check(view.Summary.Any(x => x.Value == "ProxyOnlyCompleted"), "scene status summary");
        }

        private static void TestModuleSwitch(ResultBrowserViewModel forward, ResultBrowserViewModel prediction)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var cache = new ResultBrowserStore();
            cache.Remember("01", forward);
            cache.Remember("02", prediction);
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(ReferenceEquals(cache.Select("02", "智能预测"), prediction), "switch to prediction result");
            Check(ReferenceEquals(cache.Select("01", "正向计算"), forward), "switch back to forward result");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            ResultBrowserViewModel empty = cache.Select("04", "红外场景构建");
            Check(!empty.HasDataSets && empty.RunStatus == "尚未运行", "switch to module without result");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void TestXaml(string root)
        {
            string project = Path.Combine(root, "GUI_WPF", "PreProcess.Wpf", "PreProcess.Wpf");
            string browserPath = Path.Combine(project, "Views", "ResultBrowserView.xaml");
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string mainPath = Path.Combine(project, "MainWindow.xaml");
            XDocument.Parse(File.ReadAllText(browserPath));
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            XDocument.Parse(File.ReadAllText(mainPath));
            string browser = File.ReadAllText(browserPath);
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string main = File.ReadAllText(mainPath);
            Check(browser.Contains("结果概要") && browser.Contains("结果数据表"), "status summary and data controls");
            Check(browser.Contains("EnableRowVirtualization=\"True\"") && browser.Contains("EnableColumnVirtualization=\"True\""), "data-grid virtualization");
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(main.Contains("<views:ResultBrowserView DataContext=\"{Binding Execution.ResultBrowser}\"/>") && !main.Contains("曲线显示区域。暂无结果。"), "right region replaced");
            string targetSettings = File.ReadAllText(Path.Combine(project, "Views", "TargetSettingsView.xaml"));
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Check(targetSettings.Contains("UpdateSourceTrigger=\"LostFocus\"") && targetSettings.Contains("SimilarityPercentageValidationRule"),
                "similarity input commits after complete editing with non-throwing validation");
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string runViewModel = File.ReadAllText(Path.Combine(project, "ViewModels", "ForwardRunViewModel.cs"));
            Check(runViewModel.Contains("await Task.Run(() => ResultBrowserViewModel.FromResult"), "result conversion off UI thread");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static RunRecord Completed(string module, string runId, string output, string runDirectory = null)
        {
            return new RunRecord { Module = module, RunId = runId, ResultDirectory = output, RunDirectory = runDirectory ?? output,
                State = ProcessRunState.Completed, StartedAt = DateTimeOffset.Now.AddSeconds(-2), EndedAt = DateTimeOffset.Now, Message = "完成" };
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static FileInfo FindRunFile(string root, string runId, string fileName)
        {
            FileInfo match = new DirectoryInfo(Path.Combine(root, "coreprogram")).EnumerateFiles(fileName, SearchOption.AllDirectories)
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                .FirstOrDefault(x => x.FullName.IndexOf(runId, StringComparison.Ordinal) >= 0);
            if (match == null) match = new DirectoryInfo(Path.Combine(root, "GUI_WPF", "Verification")).EnumerateFiles(fileName, SearchOption.AllDirectories)
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                .FirstOrDefault(x => x.FullName.IndexOf(runId, StringComparison.Ordinal) >= 0);
            if (match == null) throw new FileNotFoundException("Fixture not found: " + runId + "/" + fileName);
            return match;
        }

        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("FAILED: " + name);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            passed++; Console.WriteLine("PASS " + name);
        }

        private static bool AllHeadersAreChinese(ResultBrowserViewModel view)
        {
            // 返回当前步骤生成的结果，并结束本次调用。
            return view.DataSets.SelectMany(dataSet => dataSet.Rows.Table.Columns.Cast<System.Data.DataColumn>())
                .All(column => column.ColumnName.Any(ch => ch >= '\u4e00' && ch <= '\u9fff'));
        }
    }
}
