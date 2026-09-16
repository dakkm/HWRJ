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
        public bool HasPointImages { get { return PointImages.Count > 0; } }
        public bool IsTrajectoryResult { get; private set; }
        // Similarity and scene-build results are evaluated artifacts, not primary visual outputs.
        public bool HidePhysicalVisualizations { get { return IsSimilarityOrSceneTitle(ModuleTitle); } }
        public string ImageTabTitle { get { return IsTrajectoryResult ? "轨迹图" : "红外图像"; } }
        public ObservableCollection<ResultSummaryItemViewModel> Summary { get; private set; }
        public ObservableCollection<string> Issues { get; private set; }
        public ObservableCollection<ResultDataSetViewModel> DataSets { get; private set; }
        public ObservableCollection<PointImageViewModel> PointImages { get; private set; }
        public ChartViewModel TemperatureChart { get; private set; }
        public string TemperatureEmptyMessage { get; private set; }
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
                IsTrajectoryResult = IsTrajectoryTitle(moduleTitle),
                RunStatus = "尚未运行",
                AvailabilityMessage = "当前模块尚无结果。",
                EmptyMessage = "运行当前模块后，可在此查看结果概要和数据表。"
                , TemperatureEmptyMessage = "暂无温度曲线数据。"
                , ImageEmptyMessage = IsTrajectoryTitle(moduleTitle) ? "暂无可显示的轨迹图。" : "暂无可重建的红外图像数据。"
            };
        }

        public static ResultBrowserViewModel Loading(string moduleTitle)
        {
            return new ResultBrowserViewModel
            {
                ModuleTitle = moduleTitle,
                IsTrajectoryResult = IsTrajectoryTitle(moduleTitle),
                RunStatus = "运行中",
                AvailabilityMessage = "后端正在运行。",
                EmptyMessage = "结果生成后将自动读取。",
                IsLoading = true
                , TemperatureEmptyMessage = "结果生成后将自动显示温度曲线。"
                , ImageEmptyMessage = IsTrajectoryTitle(moduleTitle) ? "后处理完成后将自动显示轨迹图。" : "结果生成后将自动显示红外图像。"
            };
        }

        public static ResultBrowserViewModel FromRecord(RunRecord record, string moduleTitle)
        {
            if (record == null) throw new ArgumentNullException("record");
            var view = new ResultBrowserViewModel
            {
                ModuleTitle = moduleTitle,
                IsTrajectoryResult = IsTrajectoryTitle(moduleTitle),
                RunStatus = TranslateState(record.State.ToString()),
                RunId = record.RunId,
                OutputDirectory = record.ResultDirectory ?? record.RunDirectory,
                AvailabilityMessage = record.Message,
                EmptyMessage = record.State.ToString() == "Completed"
                    ? "本次运行未产生可显示的数据。"
                    : "本次运行未完成，没有可显示的结果数据。",
                TemperatureEmptyMessage = "本次运行没有可显示的温度曲线。",
                ImageEmptyMessage = IsTrajectoryTitle(moduleTitle) ? "本次运行没有可显示的轨迹图。" : "本次运行没有可显示的红外图像。"
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
                IsTrajectoryResult = result.ModuleType == ResultModuleType.Trajectory,
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

            // A completed forward run also contains the feature-extraction CSVs.
            // On that page the data selector is a feature selector, so expose the
            // six business features instead of the unrelated physical source tables.
            bool addedForwardFeatures = result.ModuleType == ResultModuleType.Forward
                && AddForwardFeatureDataSets(view, result);
            if (!addedForwardFeatures)
            {
                AddArtifacts(view, result.Temperatures);
                AddArtifacts(view, result.Trajectories);
                AddArtifacts(view, result.InfraredResponses);
                AddArtifacts(view, result.Similarities);
                AddArtifacts(view, result.Scenes);
            }
            view.TemperatureChart = ChartViewModel.From(result.Temperatures);
            foreach (PointImageResult image in result.PointImages)
            {
                PointImageViewModel converted = PointImageViewModel.From(image);
                if (converted != null) view.PointImages.Add(converted);
            }
            if (view.IsTrajectoryResult)
            {
                string path;
                if (result.Summary.TryGetValue("trajectory.trajectory_3d.png", out path))
                {
                    PointImageViewModel image = PointImageViewModel.FromFile(path, "三维目标轨迹");
                    if (image != null) view.PointImages.Add(image);
                }
                if (result.Summary.TryGetValue("trajectory.trajectory_range.png", out path))
                {
                    PointImageViewModel image = PointImageViewModel.FromFile(path, "目标到探测器距离曲线");
                    if (image != null) view.PointImages.Add(image);
                }
            }
            if (view.DataSets.Count > 0) view.SelectedDataSet = view.DataSets[0];
            if (view.PointImages.Count > 0) view.SelectedPointImage = view.PointImages[0];
            view.EmptyMessage = view.DataSets.Count == 0
                ? (result.AvailabilityMessage ?? "结果目录中没有可显示的数据表。")
                : String.Empty;
            view.TemperatureEmptyMessage = view.HasTemperatureChart ? String.Empty : "当前结果不包含可显示的温度曲线。";
            view.ImageEmptyMessage = view.HasPointImages ? String.Empty : view.IsTrajectoryResult ? "轨迹后处理未生成可显示的轨迹图。" : "当前结果格式不支持红外图像重建；接口已保留。";
            return view;
        }

        private static bool IsTrajectoryTitle(string title)
        { return !String.IsNullOrWhiteSpace(title) && title.IndexOf("轨迹", StringComparison.Ordinal) >= 0; }

        private static bool IsSimilarityOrSceneTitle(string title)
        {
            return !String.IsNullOrWhiteSpace(title)
                && (title.IndexOf("相似度评估", StringComparison.Ordinal) >= 0
                    || title.IndexOf("红外场景构建", StringComparison.Ordinal) >= 0);
        }

        public static ResultBrowserViewModel ReadFailed(RunRecord record, string moduleTitle, Exception error)
        {
            ResultBrowserViewModel view = FromRecord(record, moduleTitle);
            view.RunStatus = "结果读取失败";
            view.AvailabilityMessage = "后端运行记录已保留，但结果无法显示。";
            view.EmptyMessage = "请检查结果文件和警告信息。";
            view.TemperatureEmptyMessage = view.EmptyMessage;
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
            Add(view.Summary, "任务目录", record.TaskDirectory);
            Add(view.Summary, "结果目录", record.ResultDirectory ?? record.RunDirectory);
            if (record.StartedAt != default(DateTimeOffset)) Add(view.Summary, "开始时间", record.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            if (record.EndedAt != default(DateTimeOffset)) Add(view.Summary, "结束时间", record.EndedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            if (record.ExitCode.HasValue) Add(view.Summary, "退出码", record.ExitCode.Value.ToString(CultureInfo.InvariantCulture));
            if (record.ForwardExitCode.HasValue) Add(view.Summary, "01 入口退出码", record.ForwardExitCode.Value.ToString(CultureInfo.InvariantCulture));
            if (record.FeatureExitCode.HasValue) Add(view.Summary, "03 特征提取退出码", record.FeatureExitCode.Value.ToString(CultureInfo.InvariantCulture));
            Add(view.Summary, "03 特征运行编号", record.FeatureRunId);
            Add(view.Summary, "03 特征结果目录", record.FeatureResultDirectory);
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

        private static bool AddForwardFeatureDataSets(ResultBrowserViewModel view, RunResult result)
        {
            ResultTable timeSeries = result.Similarities.SelectMany(x => x.Tables)
                .FirstOrDefault(x => String.Equals(Path.GetFileName(x.SourcePath), "feature_timeseries.csv", StringComparison.OrdinalIgnoreCase));
            ResultTable periodic = result.Similarities.SelectMany(x => x.Tables)
                .FirstOrDefault(x => String.Equals(Path.GetFileName(x.SourcePath), "periodic_features.csv", StringComparison.OrdinalIgnoreCase));

            string[] requiredTimeSeriesColumns =
            {
                "time_s", "total_gray", "total_radiant_intensity_W_sr",
                "total_radiant_intensity_rate_W_sr_s", "temperature_K", "temperature_rate_K_s"
            };
            if (timeSeries == null || periodic == null
                || requiredTimeSeriesColumns.Any(key => !timeSeries.Columns.Any(column => column.Key == key)))
                return false;

            AddProjectedDataSet(view, timeSeries, "总灰度", "time_s", "total_gray", "gray_valid", "gray_invalid_reason");
            AddProjectedDataSet(view, timeSeries, "辐射强度", "time_s", "total_radiant_intensity_W_sr");
            AddProjectedDataSet(view, timeSeries, "辐射强度变化率", "time_s", "total_radiant_intensity_rate_W_sr_s");
            AddProjectedDataSet(view, timeSeries, "温度", "time_s", "temperature_object_id", "temperature_K");
            AddProjectedDataSet(view, timeSeries, "温度变化率", "time_s", "temperature_object_id", "temperature_rate_K_s");
            AddProjectedDataSet(view, periodic, "周期调制特征", periodic.Columns.Select(x => x.Key).ToArray());
            return true;
        }

        private static void AddProjectedDataSet(ResultBrowserViewModel view, ResultTable source, string name, params string[] columnKeys)
        {
            var projection = new ResultTable { Name = name, SourcePath = source.SourcePath };
            foreach (string key in columnKeys)
            {
                ResultColumn column = source.Columns.FirstOrDefault(x => x.Key == key);
                if (column != null)
                {
                    projection.Columns.Add(new ResultColumn
                    {
                        Key = column.Key,
                        DisplayName = FeatureColumnDisplayName(column.Key),
                        Unit = column.Unit
                    });
                }
            }
            foreach (ResultRow row in source.Rows) projection.Rows.Add(row);
            view.DataSets.Add(ToDataSet("响应特征", projection));
        }

        private static string FeatureColumnDisplayName(string key)
        {
            switch (key)
            {
                case "time_s": return "时间（s）";
                case "total_gray": return "总灰度";
                case "gray_valid": return "灰度有效标志";
                case "gray_invalid_reason": return "灰度无效原因";
                case "total_radiant_intensity_W_sr": return "总辐射强度（W·sr⁻¹）";
                case "total_radiant_intensity_rate_W_sr_s": return "总辐射强度变化率（W·sr⁻¹·s⁻¹）";
                case "temperature_object_id": return "温度目标编号";
                case "temperature_K": return "温度（K）";
                case "temperature_rate_K_s": return "温度变化率（K·s⁻¹）";
                case "signal": return "特征信号";
                case "periodic_valid": return "周期有效标志";
                case "invalid_reason": return "无效原因";
                case "sample_count": return "样本数";
                case "sample_interval_s": return "采样间隔（s）";
                case "nyquist_Hz": return "奈奎斯特频率（Hz）";
                case "dominant_frequency_Hz": return "主频（Hz）";
                case "period_s": return "周期（s）";
                case "modulation_amplitude": return "调制幅度";
                case "modulation_depth": return "调制度";
                case "spectral_concentration": return "频谱集中度";
                case "observed_cycles": return "观测周期数";
                default: return key;
            }
        }

        private static ResultDataSetViewModel ToDataSet(string artifactName, ResultTable source)
        {
            if (String.Equals(source.Name, "轨迹历史", StringComparison.Ordinal))
                return ToTrajectoryDataSet(artifactName, source);

            var table = new DataTable(source.Name ?? artifactName) { Locale = CultureInfo.InvariantCulture };
            IList<ResultColumn> visibleColumns = source.Columns;
            if (String.Equals(source.Name, "温度历史", StringComparison.Ordinal)
                || String.Equals(artifactName, "正向温度历史", StringComparison.Ordinal))
            {
                visibleColumns = source.Columns.Where(c => c.Key == "frame" || c.Key == "time_s" || c.Key == "T_1").ToList();
            }
            IEnumerable<ResultRow> visibleRows = source.Rows;
            if (String.Equals(source.Name, "轨迹历史", StringComparison.Ordinal)
                && source.Columns.Any(c => c.Key == "object_id"))
            {
                visibleRows = source.Rows.Where(row =>
                {
                    object value;
                    return row.Values.TryGetValue("object_id", out value)
                        && Convert.ToInt32(value, CultureInfo.InvariantCulture) == 1;
                });
            }
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ResultColumn column in visibleColumns)
            {
                string baseName = column.DisplayName ?? column.Key;
                // Keep generated DataGrid column names free of square brackets.
                // WPF treats brackets in an auto-generated binding path as an
                // indexer, so names such as "time_s [s]" render an empty cell.
                // Keep the raw key as the header; units are shown in the
                // surrounding result description and must not become part of
                // the WPF binding path.
                string name = baseName;
                for (int suffix = 2; !names.Add(name); suffix++) name = baseName + " (" + suffix.ToString(CultureInfo.InvariantCulture) + ")";
                // This table is a display projection for WPF DataGrid. Use a
                // concrete string column instead of object; boxed numeric
                // values in object columns can render as blank in the
                // auto-generated DataGrid columns.
                DataColumn dataColumn = table.Columns.Add(name, typeof(string));
                dataColumn.ExtendedProperties["ResultKey"] = column.Key;
            }
            foreach (ResultRow sourceRow in visibleRows)
            {
                DataRow row = table.NewRow();
                for (int index = 0; index < visibleColumns.Count; index++)
                {
                    object value;
                    if (!sourceRow.Values.TryGetValue(visibleColumns[index].Key, out value) || value == null)
                    {
                        row[index] = String.Empty;
                    }
                    else
                    {
                        // DataGrid/DataView can render boxed numeric values as blank when the
                        // generated DataColumn is typed as object. Store the display value as
                        // an invariant string; the original typed values remain in ResultTable
                        // for charts and image reconstruction.
                        row[index] = value is IFormattable
                            ? ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture)
                            : Convert.ToString(value, CultureInfo.InvariantCulture);
                    }
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

        private static ResultDataSetViewModel ToTrajectoryDataSet(string artifactName, ResultTable source)
        {
            string[] headers =
            {
                "时间（s）", "位置X（m）", "位置Y（m）", "位置Z（m）",
                "速度VX（m·s⁻¹）", "速度VY（m·s⁻¹）", "速度VZ（m·s⁻¹）",
                "偏航角（度）", "俯仰角（度）", "滚转角（度）",
                "偏航角速度（度·s⁻¹）", "俯仰角速度（度·s⁻¹）", "滚转角速度（度·s⁻¹）",
                "高度（m）", "全速度（m·s⁻¹）", "经度（度）", "纬度（度）"
            };
            var table = new DataTable(source.Name ?? artifactName) { Locale = CultureInfo.InvariantCulture };
            foreach (string header in headers) table.Columns.Add(header, typeof(string));

            IEnumerable<ResultRow> rows = source.Rows;
            if (source.Columns.Any(c => c.Key == "object_id"))
                rows = rows.Where(row => Number(row, "object_id") == 1.0);

            double previousTime = 0.0, previousYaw = 0.0, previousPitch = 0.0;
            bool hasPrevious = false;
            DataRow previousDataRow = null;
            foreach (ResultRow sourceRow in rows.OrderBy(row => Number(row, "time_s")))
            {
                double time = Number(sourceRow, "time_s");
                double x = Number(sourceRow, "x_m"), y = Number(sourceRow, "y_m"), z = Number(sourceRow, "z_m");
                double vx = Number(sourceRow, "vx_m_s"), vy = Number(sourceRow, "vy_m_s"), vz = Number(sourceRow, "vz_m_s");
                double horizontalSpeed = Math.Sqrt(vx * vx + vy * vy);
                double fullSpeed = Math.Sqrt(horizontalSpeed * horizontalSpeed + vz * vz);
                double yaw = RadiansToDegrees(Math.Atan2(vy, vx));
                double pitch = RadiansToDegrees(Math.Atan2(vz, horizontalSpeed));
                double radius = Math.Sqrt(x * x + y * y + z * z);
                double longitude = RadiansToDegrees(Math.Atan2(y, x));
                double latitude = radius > 0.0 ? RadiansToDegrees(Math.Asin(z / radius)) : 0.0;
                double altitude = radius - 6371008.8; // Mean Earth radius, metres.

                object yawRate = 0.0, pitchRate = 0.0, rollRate = 0.0;
                double elapsed = time - previousTime;
                if (hasPrevious && elapsed > 0.0)
                {
                    yawRate = NormalizeAngleDegrees(yaw - previousYaw) / elapsed;
                    pitchRate = (pitch - previousPitch) / elapsed;
                    // Give the first sample a forward-difference value; later
                    // samples retain the backward difference for their interval.
                    if (table.Rows.Count == 1 && previousDataRow != null)
                    {
                        previousDataRow[10] = FormatDisplayValue(yawRate);
                        previousDataRow[11] = FormatDisplayValue(pitchRate);
                        previousDataRow[12] = FormatDisplayValue(rollRate);
                    }
                }

                DataRow row = table.NewRow();
                object[] values =
                {
                    time, x, y, z, vx, vy, vz, yaw, pitch, 0.0,
                    yawRate, pitchRate, rollRate, altitude, fullSpeed, longitude, latitude
                };
                for (int index = 0; index < values.Length; index++)
                    row[index] = FormatDisplayValue(values[index]);
                table.Rows.Add(row);
                previousDataRow = row;

                previousTime = time;
                previousYaw = yaw;
                previousPitch = pitch;
                hasPrevious = true;
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

        private static double Number(ResultRow row, string key)
        {
            object value;
            return row.Values.TryGetValue(key, out value) && value != null
                ? Convert.ToDouble(value, CultureInfo.InvariantCulture)
                : 0.0;
        }

        private static string FormatDisplayValue(object value)
        {
            if (value == null) return String.Empty;
            var formattable = value as IFormattable;
            return formattable != null
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }

        private static double NormalizeAngleDegrees(double angle)
        {
            while (angle > 180.0) angle -= 360.0;
            while (angle <= -180.0) angle += 360.0;
            return angle;
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
