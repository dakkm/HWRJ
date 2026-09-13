using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Execution;

namespace PreProcess.Wpf.ViewModels
{
    public sealed class ResultSummaryItemViewModel
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public sealed class ResultDataSetViewModel
    {
        public string Name { get; set; }
        public string ArtifactName { get; set; }
        public string SourcePath { get; set; }
        public int RowCount { get; set; }
        public DataView Rows { get; set; }
        public string DisplayName { get { return Name + "（" + RowCount.ToString(CultureInfo.InvariantCulture) + " 行）"; } }
    }

    public sealed class ResultBrowserStore
    {
        private readonly IDictionary<string, ResultBrowserViewModel> items = new Dictionary<string, ResultBrowserViewModel>();

        public void Remember(string moduleKey, ResultBrowserViewModel view)
        {
            if (String.IsNullOrWhiteSpace(moduleKey)) throw new ArgumentException("模块键不能为空。", "moduleKey");
            if (view == null) throw new ArgumentNullException("view");
            items[moduleKey] = view;
        }

        public ResultBrowserViewModel Select(string moduleKey, string moduleTitle)
        {
            ResultBrowserViewModel view;
            return items.TryGetValue(moduleKey, out view) ? view : ResultBrowserViewModel.Empty(moduleTitle);
        }
    }

    public sealed class ResultBrowserViewModel : ObservableObject
    {
        private ResultDataSetViewModel selectedDataSet;
        private PointImageViewModel selectedPointImage;

        private ResultBrowserViewModel()
        {
            Summary = new ObservableCollection<ResultSummaryItemViewModel>();
            Issues = new ObservableCollection<string>();
            DataSets = new ObservableCollection<ResultDataSetViewModel>();
            PointImages = new ObservableCollection<PointImageViewModel>();
            TemperatureChart = new ChartViewModel { XAxisTitle = "Time / s", YAxisTitle = "Temperature / K" };
            TrajectoryDiagram = new TrajectoryDiagramViewModel();
        }

        public string ModuleTitle { get; private set; }
        public string RunStatus { get; private set; }
        public string RunId { get; private set; }
        public string OutputDirectory { get; private set; }
        public string AvailabilityMessage { get; private set; }
        public string EmptyMessage { get; private set; }
        public bool IsLoading { get; private set; }
        public bool HasResult { get; private set; }
        public bool HasIssues { get { return Issues.Count > 0; } }
        public bool HasSummary { get { return Summary.Count > 0; } }
        public bool HasDataSets { get { return DataSets.Count > 0; } }
        public bool HasTemperatureChart { get { return TemperatureChart != null && TemperatureChart.HasData; } }
        public bool HasTrajectoryDiagram { get { return TrajectoryDiagram != null && TrajectoryDiagram.HasData; } }
        public bool HasPointImages { get { return PointImages.Count > 0; } }
        public ObservableCollection<ResultSummaryItemViewModel> Summary { get; private set; }
        public ObservableCollection<string> Issues { get; private set; }
        public ObservableCollection<ResultDataSetViewModel> DataSets { get; private set; }
        public ObservableCollection<PointImageViewModel> PointImages { get; private set; }
        public ChartViewModel TemperatureChart { get; private set; }
        public TrajectoryDiagramViewModel TrajectoryDiagram { get; private set; }
        public string TemperatureEmptyMessage { get; private set; }
        public string TrajectoryEmptyMessage { get; private set; }
        public string ImageEmptyMessage { get; private set; }

        public ResultDataSetViewModel SelectedDataSet
        {
            get { return selectedDataSet; }
            set { if (ReferenceEquals(selectedDataSet, value)) return; selectedDataSet = value; Notify(); }
        }

        public PointImageViewModel SelectedPointImage
        {
            get { return selectedPointImage; }
            set { if (ReferenceEquals(selectedPointImage, value)) return; selectedPointImage = value; Notify(); }
        }

        public static ResultBrowserViewModel Empty(string moduleTitle)
        {
            return new ResultBrowserViewModel
            {
                ModuleTitle = moduleTitle,
                RunStatus = "尚未运行",
                AvailabilityMessage = "当前模块尚无结果。",
                EmptyMessage = "运行当前模块后，可在此查看结果概要和数据表。"
                , TemperatureEmptyMessage = "暂无温度曲线数据。"
                , TrajectoryEmptyMessage = "暂无轨迹数据。"
                , ImageEmptyMessage = "暂无可重建的红外图像数据。"
            };
        }

        public static ResultBrowserViewModel Loading(string moduleTitle)
        {
            return new ResultBrowserViewModel
            {
                ModuleTitle = moduleTitle,
                RunStatus = "运行中",
                AvailabilityMessage = "后端正在运行。",
                EmptyMessage = "结果生成后将自动读取。",
                IsLoading = true
                , TemperatureEmptyMessage = "结果生成后将自动显示温度曲线。"
                , TrajectoryEmptyMessage = "结果生成后将自动显示轨迹示意图。"
                , ImageEmptyMessage = "结果生成后将自动显示红外图像。"
            };
        }

        public static ResultBrowserViewModel FromRecord(RunRecord record, string moduleTitle)
        {
            if (record == null) throw new ArgumentNullException("record");
            var view = new ResultBrowserViewModel
            {
                ModuleTitle = moduleTitle,
                RunStatus = TranslateState(record.State.ToString()),
                RunId = record.RunId,
                OutputDirectory = record.ResultDirectory ?? record.RunDirectory,
                AvailabilityMessage = record.Message,
                EmptyMessage = record.State.ToString() == "Completed"
                    ? "本次运行未产生可显示的数据。"
                    : "本次运行未完成，没有可显示的结果数据。",
                TemperatureEmptyMessage = "本次运行没有可显示的温度曲线。",
                TrajectoryEmptyMessage = "本次运行没有可显示的轨迹数据。",
                ImageEmptyMessage = "本次运行没有可显示的红外图像。"
            };
            AddRecordSummary(view, record);
            if (!String.IsNullOrWhiteSpace(record.Diagnostic)) view.Issues.Add(record.Diagnostic);
            return view;
        }

        public static ResultBrowserViewModel FromResult(RunResult result, string moduleTitle, string completionMessage = null)
        {
            if (result == null) throw new ArgumentNullException("result");
            var view = new ResultBrowserViewModel
            {
                ModuleTitle = moduleTitle,
                RunStatus = TranslateState(result.RunState),
                RunId = result.RunId,
                OutputDirectory = result.OutputDirectory,
                AvailabilityMessage = completionMessage ?? result.AvailabilityMessage,
                HasResult = result.ResultExists
            };

            Add(view.Summary, "模块", result.ModuleCode);
            Add(view.Summary, "run_id", result.RunId);
            Add(view.Summary, "运行状态", view.RunStatus);
            Add(view.Summary, "输入请求", result.InputRequestPath);
            Add(view.Summary, "输出目录", result.OutputDirectory);
            if (result.StartedAt.HasValue) Add(view.Summary, "开始时间", result.StartedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            if (result.EndedAt.HasValue) Add(view.Summary, "结束时间", result.EndedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            foreach (KeyValuePair<string, string> item in result.Summary) Add(view.Summary, item.Key, item.Value);
            foreach (string issue in result.Issues) if (!String.IsNullOrWhiteSpace(issue)) view.Issues.Add(issue);

            AddArtifacts(view, result.Temperatures);
            AddArtifacts(view, result.Trajectories);
            AddArtifacts(view, result.InfraredResponses);
            AddArtifacts(view, result.Similarities);
            AddArtifacts(view, result.Scenes);
            view.TemperatureChart = ChartViewModel.From(result.Temperatures);
            view.TrajectoryDiagram = TrajectoryDiagramViewModel.From(result.Trajectories);
            foreach (PointImageResult image in result.PointImages)
            {
                PointImageViewModel converted = PointImageViewModel.From(image);
                if (converted != null) view.PointImages.Add(converted);
            }
            if (view.DataSets.Count > 0) view.SelectedDataSet = view.DataSets[0];
            if (view.PointImages.Count > 0) view.SelectedPointImage = view.PointImages[0];
            view.EmptyMessage = view.DataSets.Count == 0
                ? (result.AvailabilityMessage ?? "结果目录中没有可显示的数据表。")
                : String.Empty;
            view.TemperatureEmptyMessage = view.HasTemperatureChart ? String.Empty : "当前结果不包含可显示的温度曲线。";
            view.TrajectoryEmptyMessage = view.HasTrajectoryDiagram ? String.Empty : "当前结果不包含可显示的轨迹数据。";
            view.ImageEmptyMessage = view.HasPointImages ? String.Empty : "当前结果格式不支持红外图像重建；接口已保留。";
            return view;
        }

        public static ResultBrowserViewModel ReadFailed(RunRecord record, string moduleTitle, Exception error)
        {
            ResultBrowserViewModel view = FromRecord(record, moduleTitle);
            view.RunStatus = "结果读取失败";
            view.AvailabilityMessage = "后端运行记录已保留，但结果无法显示。";
            view.EmptyMessage = "请检查结果文件和警告信息。";
            view.TemperatureEmptyMessage = view.EmptyMessage;
            view.TrajectoryEmptyMessage = view.EmptyMessage;
            view.ImageEmptyMessage = view.EmptyMessage;
            view.Issues.Add(error == null ? "结果读取失败。" : error.Message);
            return view;
        }

        private static void AddRecordSummary(ResultBrowserViewModel view, RunRecord record)
        {
            Add(view.Summary, "模块", record.Module);
            Add(view.Summary, "run_id", record.RunId);
            Add(view.Summary, "运行状态", view.RunStatus);
            Add(view.Summary, "输入请求", record.RequestPath);
            Add(view.Summary, "结果目录", record.ResultDirectory ?? record.RunDirectory);
            if (record.StartedAt != default(DateTimeOffset)) Add(view.Summary, "开始时间", record.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            if (record.EndedAt != default(DateTimeOffset)) Add(view.Summary, "结束时间", record.EndedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            if (record.ExitCode.HasValue) Add(view.Summary, "退出码", record.ExitCode.Value.ToString(CultureInfo.InvariantCulture));
            Add(view.Summary, "后端状态", record.BackendStatus);
        }

        private static void AddArtifacts<T>(ResultBrowserViewModel view, IEnumerable<T> artifacts) where T : ResultArtifact
        {
            foreach (T artifact in artifacts)
            {
                foreach (KeyValuePair<string, string> item in artifact.Summary)
                    Add(view.Summary, artifact.Name + "." + item.Key, item.Value);
                foreach (ResultTable table in artifact.Tables)
                    if (!view.DataSets.Any(x => String.Equals(x.SourcePath, NormalizeDisplayPath(table.SourcePath), StringComparison.OrdinalIgnoreCase) && x.Name == (table.Name ?? artifact.Name)))
                        view.DataSets.Add(ToDataSet(artifact.Name, table));
            }
        }

        private static ResultDataSetViewModel ToDataSet(string artifactName, ResultTable source)
        {
            var table = new DataTable(source.Name ?? artifactName) { Locale = CultureInfo.InvariantCulture };
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ResultColumn column in source.Columns)
            {
                string baseName = column.DisplayName ?? column.Key;
                if (!String.IsNullOrWhiteSpace(column.Unit)) baseName += " [" + column.Unit + "]";
                string name = baseName;
                for (int suffix = 2; !names.Add(name); suffix++) name = baseName + " (" + suffix.ToString(CultureInfo.InvariantCulture) + ")";
                DataColumn dataColumn = table.Columns.Add(name, typeof(object));
                dataColumn.ExtendedProperties["ResultKey"] = column.Key;
            }
            foreach (ResultRow sourceRow in source.Rows)
            {
                DataRow row = table.NewRow();
                for (int index = 0; index < source.Columns.Count; index++)
                {
                    object value;
                    row[index] = sourceRow.Values.TryGetValue(source.Columns[index].Key, out value) && value != null ? value : DBNull.Value;
                }
                table.Rows.Add(row);
            }
            return new ResultDataSetViewModel
            {
                Name = source.Name ?? artifactName,
                ArtifactName = artifactName,
                SourcePath = NormalizeDisplayPath(source.SourcePath),
                RowCount = table.Rows.Count,
                Rows = table.DefaultView
            };
        }

        private static string NormalizeDisplayPath(string path)
        {
            return String.IsNullOrWhiteSpace(path) ? "未记录源文件" : Path.GetFullPath(path);
        }

        private static void Add(ICollection<ResultSummaryItemViewModel> target, string name, string value)
        {
            if (!String.IsNullOrWhiteSpace(value)) target.Add(new ResultSummaryItemViewModel { Name = name, Value = value });
        }

        private static string TranslateState(string state)
        {
            switch (state ?? String.Empty)
            {
                case "Preparing": return "准备中";
                case "Running": return "运行中";
                case "Stopping": return "停止中";
                case "Completed": return "已完成";
                case "Failed": return "失败";
                case "Cancelled": return "已停止";
                default: return String.IsNullOrWhiteSpace(state) ? "未知" : state;
            }
        }
    }
}
