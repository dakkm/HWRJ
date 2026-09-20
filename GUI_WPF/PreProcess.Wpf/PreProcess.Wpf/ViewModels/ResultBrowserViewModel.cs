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
    // 定义 ResultSummaryItemViewModel 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class ResultSummaryItemViewModel
    {
        public string Name { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string Value { get; set; }
    }

    public sealed class ResultDataSetViewModel
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string Name { get; set; }
        public string ArtifactName { get; set; }
        public string SourcePath { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public int RowCount { get; set; }
        public DataView Rows { get; set; }
        // 将外部数据转换为目标类型，并保持约定的表示格式。
        public string DisplayName { get { return Name + "（" + RowCount.ToString(CultureInfo.InvariantCulture) + " 行）"; } }
    }

    public sealed class ResultBrowserStore
    {
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private readonly IDictionary<string, ResultBrowserViewModel> items = new Dictionary<string, ResultBrowserViewModel>();

        public void Remember(string moduleKey, ResultBrowserViewModel view)
        {
            if (String.IsNullOrWhiteSpace(moduleKey)) throw new ArgumentException("模块键不能为空。", "moduleKey");
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (view == null) throw new ArgumentNullException("view");
            items[moduleKey] = view;
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public ResultBrowserViewModel Select(string moduleKey, string moduleTitle)
        {
            ResultBrowserViewModel view;
            // 返回当前步骤生成的结果，并结束本次调用。
            return items.TryGetValue(moduleKey, out view) ? view : ResultBrowserViewModel.Empty(moduleTitle);
        }
    }

    public sealed class ResultBrowserViewModel : ObservableObject
    {
        private ResultDataSetViewModel selectedDataSet;
        // 保存该组件运行所需的配置或中间状态。
        private PointImageViewModel selectedPointImage;

        private ResultBrowserViewModel()
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            Summary = new ObservableCollection<ResultSummaryItemViewModel>();
            Issues = new ObservableCollection<string>();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            DataSets = new ObservableCollection<ResultDataSetViewModel>();
            PointImages = new ObservableCollection<PointImageViewModel>();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            TemperatureChart = new ChartViewModel { XAxisTitle = "Time / s", YAxisTitle = "Temperature / K" };
        }

        public string ModuleTitle { get; private set; }
        public string RunStatus { get; private set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string RunId { get; private set; }
        public string OutputDirectory { get; private set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string AvailabilityMessage { get; private set; }
        public string EmptyMessage { get; private set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public bool IsLoading { get; private set; }
        public bool HasResult { get; private set; }
        public bool HasIssues { get { return Issues.Count > 0; } }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public bool HasSummary { get { return Summary.Count > 0; } }
        public bool HasDataSets { get { return DataSets.Count > 0; } }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public bool HasTemperatureChart { get { return TemperatureChart != null && TemperatureChart.HasData; } }
        public bool HasPointImages { get { return PointImages.Count > 0; } }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public bool IsTrajectoryResult { get; private set; }
        // Similarity and scene-build results are evaluated artifacts, not primary visual outputs.
        public bool HidePhysicalVisualizations { get { return IsSimilarityOrSceneTitle(ModuleTitle); } }
        public string ImageTabTitle { get { return IsTrajectoryResult ? "轨迹图" : "红外图像"; } }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public ObservableCollection<ResultSummaryItemViewModel> Summary { get; private set; }
        public ObservableCollection<string> Issues { get; private set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public ObservableCollection<ResultDataSetViewModel> DataSets { get; private set; }
        public ObservableCollection<PointImageViewModel> PointImages { get; private set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public ChartViewModel TemperatureChart { get; private set; }
        public string TemperatureEmptyMessage { get; private set; }
        public string ImageEmptyMessage { get; private set; }

        // 保存该组件运行所需的配置或中间状态。
        public ResultDataSetViewModel SelectedDataSet
        {
            get { return selectedDataSet; }
            // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
            set { if (ReferenceEquals(selectedDataSet, value)) return; selectedDataSet = value; Notify(); }
        }

        public PointImageViewModel SelectedPointImage
        {
            // 继续处理当前业务步骤，保持上下文状态一致。
            get { return selectedPointImage; }
            set { if (ReferenceEquals(selectedPointImage, value)) return; selectedPointImage = value; Notify(); }
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public static ResultBrowserViewModel Empty(string moduleTitle)
        {
            return new ResultBrowserViewModel
            {
                ModuleTitle = moduleTitle,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                IsTrajectoryResult = IsTrajectoryTitle(moduleTitle),
                RunStatus = "尚未运行",
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                AvailabilityMessage = "当前模块尚无结果。",
                EmptyMessage = "运行当前模块后，可在此查看结果概要和数据表。"
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                , TemperatureEmptyMessage = "暂无温度曲线数据。"
                , ImageEmptyMessage = IsTrajectoryTitle(moduleTitle) ? "暂无可显示的轨迹图。" : "暂无可重建的红外图像数据。"
            };
        }

        public static ResultBrowserViewModel Loading(string moduleTitle)
        {
            // 返回当前步骤生成的结果，并结束本次调用。
            return new ResultBrowserViewModel
            {
                ModuleTitle = moduleTitle,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                IsTrajectoryResult = IsTrajectoryTitle(moduleTitle),
                RunStatus = "运行中",
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                AvailabilityMessage = "后端正在运行。",
                EmptyMessage = "结果生成后将自动读取。",
                IsLoading = true
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                , TemperatureEmptyMessage = "结果生成后将自动显示温度曲线。"
                , ImageEmptyMessage = IsTrajectoryTitle(moduleTitle) ? "后处理完成后将自动显示轨迹图。" : "结果生成后将自动显示红外图像。"
            };
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public static ResultBrowserViewModel FromRecord(RunRecord record, string moduleTitle)
        {
            if (record == null) throw new ArgumentNullException("record");
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var view = new ResultBrowserViewModel
            {
                ModuleTitle = moduleTitle,
                IsTrajectoryResult = IsTrajectoryTitle(moduleTitle),
                // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
                RunStatus = TranslateState(record.State.ToString()),
                RunId = record.RunId,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                OutputDirectory = record.ResultDirectory ?? record.RunDirectory,
                AvailabilityMessage = record.Message,
                // 将外部数据转换为目标类型，并保持约定的表示格式。
                EmptyMessage = record.State.ToString() == "Completed"
                    ? "本次运行未产生可显示的数据。"
                    // 继续处理当前业务步骤，保持上下文状态一致。
                    : "本次运行未完成，没有可显示的结果数据。",
                TemperatureEmptyMessage = "本次运行没有可显示的温度曲线。",
                ImageEmptyMessage = IsTrajectoryTitle(moduleTitle) ? "本次运行没有可显示的轨迹图。" : "本次运行没有可显示的红外图像。"
            };
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddRecordSummary(view, record);
            if (!String.IsNullOrWhiteSpace(record.Diagnostic)) view.Issues.Add(record.Diagnostic);
            // 返回当前步骤生成的结果，并结束本次调用。
            return view;
        }

        public static ResultBrowserViewModel FromResult(RunResult result, string moduleTitle, string completionMessage = null)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (result == null) throw new ArgumentNullException("result");
            var view = new ResultBrowserViewModel
            {
                ModuleTitle = moduleTitle,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                IsTrajectoryResult = result.ModuleType == ResultModuleType.Trajectory,
                RunStatus = TranslateState(result.RunState),
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                RunId = result.RunId,
                OutputDirectory = result.OutputDirectory,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                AvailabilityMessage = completionMessage ?? result.AvailabilityMessage,
                HasResult = result.ResultExists
            };

            Add(view.Summary, "模块", result.ModuleCode);
            // 将当前结果加入集合，供后续汇总或界面展示。
            Add(view.Summary, "run_id", result.RunId);
            Add(view.Summary, "运行状态", view.RunStatus);
            // 将当前结果加入集合，供后续汇总或界面展示。
            Add(view.Summary, "输入请求", result.InputRequestPath);
            Add(view.Summary, "输出目录", result.OutputDirectory);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (result.StartedAt.HasValue) Add(view.Summary, "开始时间", result.StartedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            if (result.EndedAt.HasValue) Add(view.Summary, "结束时间", result.EndedAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            foreach (KeyValuePair<string, string> item in result.Summary) Add(view.Summary, item.Key, item.Value);
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (string issue in result.Issues) if (!String.IsNullOrWhiteSpace(issue)) view.Issues.Add(issue);

            // A completed forward run also contains the feature-extraction CSVs.
            // On that page the data selector is a feature selector, so expose the
            // six business features instead of the unrelated physical source tables.
            bool addedForwardFeatures = result.ModuleType == ResultModuleType.Forward
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                && AddForwardFeatureDataSets(view, result);
            if (!addedForwardFeatures)
            {
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (result.ModuleType == ResultModuleType.Scene)
                {
                    // SceneResultReader puts the concise candidate-similarity
                    // table first. Load scene artifacts before their shared
                    // temperature table so it remains the default selection.
                    AddArtifacts(view, result.Scenes);
                    AddArtifacts(view, result.Temperatures);
                }
                // 当前置条件不成立时执行备用路径，保持处理结果完整。
                else if (result.ModuleType == ResultModuleType.Similarity)
                {
                    // Keep the concise percentage overview as the default table,
                    // matching the scene-build result presentation.
                    AddArtifacts(view, result.Similarities);
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    AddArtifacts(view, result.Temperatures);
                }
                else
                {
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    AddArtifacts(view, result.Temperatures);
                    AddArtifacts(view, result.Trajectories);
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    AddArtifacts(view, result.InfraredResponses);
                    AddArtifacts(view, result.Similarities);
                    AddArtifacts(view, result.Scenes);
                }
            }
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            view.TemperatureChart = ChartViewModel.From(result.Temperatures);
            foreach (PointImageResult image in result.PointImages)
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                PointImageViewModel converted = PointImageViewModel.From(image);
                if (converted != null) view.PointImages.Add(converted);
            }
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (view.IsTrajectoryResult)
            {
                string path;
                if (result.Summary.TryGetValue("trajectory.trajectory_3d.png", out path))
                {
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    PointImageViewModel image = PointImageViewModel.FromFile(path, "三维目标轨迹");
                    if (image != null) view.PointImages.Add(image);
                }
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (result.Summary.TryGetValue("trajectory.trajectory_range.png", out path))
                {
                    PointImageViewModel image = PointImageViewModel.FromFile(path, "目标到探测器距离曲线");
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (image != null) view.PointImages.Add(image);
                }
            }
            if (view.DataSets.Count > 0) view.SelectedDataSet = view.DataSets[0];
            if (view.PointImages.Count > 0) view.SelectedPointImage = view.PointImages[0];
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            view.EmptyMessage = view.DataSets.Count == 0
                ? (result.AvailabilityMessage ?? "结果目录中没有可显示的数据表。")
                // 继续处理当前业务步骤，保持上下文状态一致。
                : String.Empty;
            view.TemperatureEmptyMessage = view.HasTemperatureChart ? String.Empty : "当前结果不包含可显示的温度曲线。";
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            view.ImageEmptyMessage = view.HasPointImages ? String.Empty : view.IsTrajectoryResult ? "轨迹后处理未生成可显示的轨迹图。" : "当前结果不包含可显示的红外图像数据。";
            return view;
        }

        private static bool IsTrajectoryTitle(string title)
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        { return !String.IsNullOrWhiteSpace(title) && title.IndexOf("轨迹", StringComparison.Ordinal) >= 0; }

        private static bool IsSimilarityOrSceneTitle(string title)
        {
            // 返回当前步骤生成的结果，并结束本次调用。
            return !String.IsNullOrWhiteSpace(title)
                && (title.IndexOf("相似度评估", StringComparison.Ordinal) >= 0
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    || title.IndexOf("红外场景构建", StringComparison.Ordinal) >= 0);
        }

        public static ResultBrowserViewModel ReadFailed(RunRecord record, string moduleTitle, Exception error)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            ResultBrowserViewModel view = FromRecord(record, moduleTitle);
            view.RunStatus = "结果读取失败";
            view.AvailabilityMessage = "后端运行记录已保留，但结果无法显示。";
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            view.EmptyMessage = "请检查结果文件和警告信息。";
            view.TemperatureEmptyMessage = view.EmptyMessage;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            view.ImageEmptyMessage = view.EmptyMessage;
            view.Issues.Add(error == null ? "结果读取失败。" : error.Message);
            // 返回当前步骤生成的结果，并结束本次调用。
            return view;
        }

        private static void AddRecordSummary(ResultBrowserViewModel view, RunRecord record)
        {
            Add(view.Summary, "模块", record.Module);
            // 将当前结果加入集合，供后续汇总或界面展示。
            Add(view.Summary, "run_id", record.RunId);
            Add(view.Summary, "运行状态", view.RunStatus);
            // 将当前结果加入集合，供后续汇总或界面展示。
            Add(view.Summary, "输入请求", record.RequestPath);
            Add(view.Summary, "任务目录", record.TaskDirectory);
            // 将当前结果加入集合，供后续汇总或界面展示。
            Add(view.Summary, "结果目录", record.ResultDirectory ?? record.RunDirectory);
            if (record.StartedAt != default(DateTimeOffset)) Add(view.Summary, "开始时间", record.StartedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            if (record.EndedAt != default(DateTimeOffset)) Add(view.Summary, "结束时间", record.EndedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (record.ExitCode.HasValue) Add(view.Summary, "退出码", record.ExitCode.Value.ToString(CultureInfo.InvariantCulture));
            if (record.ForwardExitCode.HasValue) Add(view.Summary, "正向计算退出码", record.ForwardExitCode.Value.ToString(CultureInfo.InvariantCulture));
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (record.FeatureExitCode.HasValue) Add(view.Summary, "特征提取退出码", record.FeatureExitCode.Value.ToString(CultureInfo.InvariantCulture));
            Add(view.Summary, "特征提取运行编号", record.FeatureRunId);
            // 将当前结果加入集合，供后续汇总或界面展示。
            Add(view.Summary, "特征提取结果目录", record.FeatureResultDirectory);
            Add(view.Summary, "后端状态", record.BackendStatus);
        }

        private static void AddArtifacts<T>(ResultBrowserViewModel view, IEnumerable<T> artifacts) where T : ResultArtifact
        {
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (T artifact in artifacts)
            {
                foreach (KeyValuePair<string, string> item in artifact.Summary)
                    // 将当前结果加入集合，供后续汇总或界面展示。
                    Add(view.Summary, artifact.Name + "." + item.Key, item.Value);
                foreach (ResultTable table in artifact.Tables)
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (!view.DataSets.Any(x => String.Equals(x.SourcePath, NormalizeDisplayPath(table.SourcePath), StringComparison.OrdinalIgnoreCase) && x.Name == (table.Name ?? artifact.Name)))
                        view.DataSets.Add(ToDataSet(artifact.Name, table));
            }
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static bool AddForwardFeatureDataSets(ResultBrowserViewModel view, RunResult result)
        {
            ResultTable timeSeries = result.Similarities.SelectMany(x => x.Tables)
                .FirstOrDefault(x => String.Equals(Path.GetFileName(x.SourcePath), "feature_timeseries.csv", StringComparison.OrdinalIgnoreCase));
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            ResultTable periodic = result.Similarities.SelectMany(x => x.Tables)
                .FirstOrDefault(x => String.Equals(Path.GetFileName(x.SourcePath), "periodic_features.csv", StringComparison.OrdinalIgnoreCase));

            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string[] requiredTimeSeriesColumns =
            {
                "time_s", "total_gray", "total_radiant_intensity_W_sr",
                // 继续处理当前业务步骤，保持上下文状态一致。
                "total_radiant_intensity_rate_W_sr_s", "temperature_K", "temperature_rate_K_s"
            };
            if (timeSeries == null || periodic == null
                || requiredTimeSeriesColumns.Any(key => !timeSeries.Columns.Any(column => column.Key == key)))
                // 返回当前步骤生成的结果，并结束本次调用。
                return false;

            AddProjectedDataSet(view, timeSeries, "总灰度", "time_s", "total_gray", "gray_valid", "gray_invalid_reason");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddProjectedDataSet(view, timeSeries, "辐射强度", "time_s", "total_radiant_intensity_W_sr");
            AddProjectedDataSet(view, timeSeries, "辐射强度变化率", "time_s", "total_radiant_intensity_rate_W_sr_s");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            AddProjectedDataSet(view, timeSeries, "温度", "time_s", "temperature_object_id", "temperature_K");
            AddProjectedDataSet(view, timeSeries, "温度变化率", "time_s", "temperature_object_id", "temperature_rate_K_s");
            AddProjectedDataSet(view, periodic, "周期调制特征", periodic.Columns.Select(x => x.Key).ToArray());
            // 返回当前步骤生成的结果，并结束本次调用。
            return true;
        }

        private static void AddProjectedDataSet(ResultBrowserViewModel view, ResultTable source, string name, params string[] columnKeys)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var projection = new ResultTable { Name = name, SourcePath = source.SourcePath };
            foreach (string key in columnKeys)
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                ResultColumn column = source.Columns.FirstOrDefault(x => x.Key == key);
                if (column != null)
                {
                    projection.Columns.Add(new ResultColumn
                    {
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        Key = column.Key,
                        DisplayName = FeatureColumnDisplayName(column.Key),
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        Unit = column.Unit
                    });
                }
            }
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (ResultRow row in source.Rows) projection.Rows.Add(row);
            view.DataSets.Add(ToDataSet("响应特征", projection));
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static string FeatureColumnDisplayName(string key)
        {
            switch (key)
            {
                case "time_s": return "时间（s）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "total_gray": return "总灰度";
                case "gray_valid": return "灰度有效标志";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "gray_invalid_reason": return "灰度无效原因";
                case "total_radiant_intensity_W_sr": return "总辐射强度（W·sr⁻¹）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "total_radiant_intensity_rate_W_sr_s": return "总辐射强度变化率（W·sr⁻¹·s⁻¹）";
                case "temperature_object_id": return "温度目标编号";
                case "temperature_K": return "温度（K）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "temperature_rate_K_s": return "温度变化率（K·s⁻¹）";
                case "signal": return "特征信号";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "periodic_valid": return "周期有效标志";
                case "invalid_reason": return "无效原因";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "sample_count": return "样本数";
                case "sample_interval_s": return "采样间隔（s）";
                case "nyquist_Hz": return "奈奎斯特频率（Hz）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "dominant_frequency_Hz": return "主频（Hz）";
                case "period_s": return "周期（s）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "modulation_amplitude": return "调制幅度";
                case "modulation_depth": return "调制度";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "spectral_concentration": return "频谱集中度";
                case "observed_cycles": return "观测周期数";
                default: return LocalizeColumnKey(key);
            }
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static string ColumnDisplayName(ResultColumn column)
        {
            if (!String.IsNullOrWhiteSpace(column.DisplayName) && column.DisplayName.Any(ch => ch >= '\u4e00' && ch <= '\u9fff'))
                // 返回当前步骤生成的结果，并结束本次调用。
                return column.DisplayName;
            return LocalizeColumnKey(column.Key);
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static string LocalizeColumnKey(string key)
        {
            if (String.IsNullOrWhiteSpace(key)) return "数据项";
            if (key.StartsWith("T_", StringComparison.Ordinal))
                // 返回当前步骤生成的结果，并结束本次调用。
                return "目标" + key.Substring(2) + "温度（K）";
            switch (key)
            {
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "frame": return "帧序号";
                case "frame_id": return "帧编号";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "time_s": return "时间（s）";
                case "case_id": return "算例编号";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "object_id": return "目标编号";
                case "target_id": return "目标编号";
                case "source_case_id": return "源算例编号";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "sphere_id": return "球体编号";
                case "active_flag": return "有效标志";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "released_flag": return "释放标志";
                case "sphere_released_flag": return "球体释放标志";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "motion_stage": return "运动阶段";
                case "release_time_s": return "释放时间（s）";
                case "x_m": return "位置X（m）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "y_m": return "位置Y（m）";
                case "z_m": return "位置Z（m）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "vx_m_s": return "速度VX（m·s⁻¹）";
                case "vy_m_s": return "速度VY（m·s⁻¹）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "vz_m_s": return "速度VZ（m·s⁻¹）";
                case "speed_m_s": return "速度（m·s⁻¹）";
                case "range_to_detector_m": return "目标到探测器距离（m）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "radiation_power_W": return "辐射功率（W）";
                case "radiant_intensity_W_sr": return "辐射强度（W·sr⁻¹）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "detector_received_power_W": return "探测器接收功率（W）";
                case "detector_irradiance_W_m2": return "探测器辐照度（W·m⁻²）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "screen_x_m": return "屏幕X坐标（m）";
                case "screen_y_m": return "屏幕Y坐标（m）";
                case "in_screen_flag": return "屏幕内标志";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "sample_count": return "样本数";
                case "trajectory_start_time_s": return "轨迹开始时间（s）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "trajectory_end_time_s": return "轨迹结束时间（s）";
                case "trajectory_duration_s": return "轨迹持续时间（s）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "path_length_m": return "轨迹长度（m）";
                case "net_displacement_m": return "净位移（m）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "average_path_speed_m_s": return "平均速度（m·s⁻¹）";
                case "min_speed_m_s": return "最小速度（m·s⁻¹）";
                case "max_speed_m_s": return "最大速度（m·s⁻¹）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "min_range_to_detector_m": return "到探测器最小距离（m）";
                case "max_range_to_detector_m": return "到探测器最大距离（m）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "start_x_m": return "起点X（m）";
                case "start_y_m": return "起点Y（m）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "start_z_m": return "起点Z（m）";
                case "end_x_m": return "终点X（m）";
                case "end_y_m": return "终点Y（m）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "end_z_m": return "终点Z（m）";
                case "feature_name": return "特征名称";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "category": return "类别";
                case "raw_difference": return "原始差值";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "raw_unit": return "原始单位";
                case "similarity_percent": return "相似度（%）";
                case "valid_flag": return "有效标志";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "invalid_reason": return "无效原因";
                case "valid_sample_count": return "有效样本数";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "active_object_count": return "有效目标数";
                case "on_screen_object_count": return "屏幕内目标数";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "total_radiation_power_W": return "总辐射功率（W）";
                case "total_radiant_intensity_W_sr": return "总辐射强度（W·sr⁻¹）";
                case "peak_object_radiant_intensity_W_sr": return "目标峰值辐射强度（W·sr⁻¹）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "total_received_power_W": return "总接收功率（W）";
                case "peak_pixel_power_W": return "峰值像元功率（W）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "peak_pixel_irradiance_W_m2": return "峰值像元辐照度（W·m⁻²）";
                case "centroid_x_m": return "质心X（m）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "centroid_y_m": return "质心Y（m）";
                case "peak_x_m": return "峰值X（m）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "peak_y_m": return "峰值Y（m）";
                case "peak_object_id": return "峰值目标编号";
                case "nonzero_pixel_count": return "非零像元数";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "total_gray": return "总灰度";
                case "peak_gray": return "峰值灰度";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "gray_valid": return "灰度有效标志";
                case "gray_invalid_reason": return "灰度无效原因";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "total_radiant_intensity_rate_W_sr_s": return "总辐射强度变化率（W·sr⁻¹·s⁻¹）";
                case "total_received_power_rate_W_s": return "总接收功率变化率（W·s⁻¹）";
                case "peak_pixel_irradiance_rate_W_m2_s": return "峰值辐照度变化率（W·m⁻²·s⁻¹）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "total_gray_rate_per_s": return "总灰度变化率（s⁻¹）";
                case "temperature_object_id": return "温度目标编号";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "temperature_K": return "温度（K）";
                case "temperature_rate_K_s": return "温度变化率（K·s⁻¹）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "radiant_intensity_rate_W_sr_s": return "辐射强度变化率（W·sr⁻¹·s⁻¹）";
                case "radiation_power_rate_W_s": return "辐射功率变化率（W·s⁻¹）";
                case "detector_received_power_rate_W_s": return "接收功率变化率（W·s⁻¹）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "detector_irradiance_rate_W_m2_s": return "探测器辐照度变化率（W·m⁻²·s⁻¹）";
                case "signal": return "特征信号";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "periodic_valid": return "周期有效标志";
                case "sample_interval_s": return "采样间隔（s）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "nyquist_Hz": return "奈奎斯特频率（Hz）";
                case "dominant_frequency_Hz": return "主频（Hz）";
                case "period_s": return "周期（s）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "modulation_amplitude": return "调制幅度";
                case "modulation_depth": return "调制度";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "spectral_concentration": return "频谱集中度";
                case "observed_cycles": return "观测周期数";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "solution_index": return "解序号";
                case "sequence_index": return "候选序号";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "candidate_id": return "候选";
                case "q_int": return "内部热源（W）";
                case "emissivity_ir": return "红外发射率";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "absorptivity_solar": return "太阳吸收率";
                case "required_temperature_similarity_percent": return "要求温度相似度（%）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "target_temperature_range_K": return "目标温度范围（K）";
                case "temperature_margin_K": return "温度容差（K）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "target_temperature_K": return "目标温度（K）";
                case "m2_temperature_K": return "代理预测温度（K）";
                case "forward_temperature_K": return "正向复核温度（K）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "proxy_rule_source": return "代理筛选规则来源";
                case "proxy_pass": return "代理筛选通过";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "proxy_failed_conditions": return "代理筛选未通过条件";
                case "proxy_rmse_K": return "代理RMSE（K）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "proxy_mae_K": return "代理MAE（K）";
                case "proxy_max_abs_error_K": return "代理最大绝对误差（K）";
                case "proxy_final_abs_error_K": return "代理末值绝对误差（K）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "proxy_plateau_abs_error_K": return "代理平台段绝对误差（K）";
                case "proxy_early_abs_error_K": return "代理初期绝对误差（K）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "proxy_temperature_similarity_percent": return "代理预测相似度（%）";
                case "proxy_temperature_limiting_feature": return "代理相似度限制特征";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "proxy_temperature_trend_similarity_percent": return "代理温度趋势相似度（%）";
                case "forward_attempted": return "已执行正向复核";
                case "forward_output_valid": return "正向输出有效";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "forward_pass": return "正向复核通过";
                case "forward_failed_conditions": return "正向复核未通过条件";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "forward_return_code": return "正向复核退出码";
                case "forward_elapsed_seconds": return "单次复核耗时（s）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "accepted_count_after_candidate": return "累计有效候选数";
                case "decision": return "判定结果";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "forward_rmse_K": return "正向RMSE（K）";
                case "forward_mae_K": return "正向MAE（K）";
                case "forward_max_abs_error_K": return "正向最大绝对误差（K）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "forward_final_abs_error_K": return "正向末值绝对误差（K）";
                case "forward_plateau_abs_error_K": return "正向平台段绝对误差（K）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "forward_early_abs_error_K": return "正向初期绝对误差（K）";
                case "forward_temperature_similarity_percent": return "正向复核相似度（%）";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "forward_temperature_limiting_feature": return "正向相似度限制特征";
                case "forward_temperature_trend_similarity_percent": return "正向温度趋势相似度（%）";
                case "forward_level_margin_pass": return "正向温度容差通过";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "forward_run_dir": return "正向复核运行目录";
                default: return "数据项";
            }
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static ResultDataSetViewModel ToDataSet(string artifactName, ResultTable source)
        {
            if (String.Equals(source.Name, "轨迹历史", StringComparison.Ordinal))
                // 返回当前步骤生成的结果，并结束本次调用。
                return ToTrajectoryDataSet(artifactName, source);

            var table = new DataTable(source.Name ?? artifactName) { Locale = CultureInfo.InvariantCulture };
            IList<ResultColumn> visibleColumns = source.Columns;
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (String.Equals(source.Name, "温度历史", StringComparison.Ordinal)
                || String.Equals(artifactName, "正向温度历史", StringComparison.Ordinal))
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                visibleColumns = source.Columns.Where(c => c.Key == "frame" || c.Key == "time_s" || c.Key == "T_1").ToList();
            }
            IEnumerable<ResultRow> visibleRows = source.Rows;
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (String.Equals(source.Name, "轨迹历史", StringComparison.Ordinal)
                && source.Columns.Any(c => c.Key == "object_id"))
            {
                visibleRows = source.Rows.Where(row =>
                {
                    // 继续处理当前业务步骤，保持上下文状态一致。
                    object value;
                    return row.Values.TryGetValue("object_id", out value)
                        // 将外部数据转换为目标类型，并保持约定的表示格式。
                        && Convert.ToInt32(value, CultureInfo.InvariantCulture) == 1;
                });
            }
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ResultColumn column in visibleColumns)
            {
                string baseName = ColumnDisplayName(column);
                // Keep generated DataGrid column names free of square brackets.
                // WPF treats brackets in an auto-generated binding path as an
                // indexer, so names such as "time_s [s]" render an empty cell.
                // Keep the raw key as the header; units are shown in the
                // surrounding result description and must not become part of
                // the WPF binding path.
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                string name = baseName;
                for (int suffix = 2; !names.Add(name); suffix++) name = baseName + " (" + suffix.ToString(CultureInfo.InvariantCulture) + ")";
                // This table is a display projection for WPF DataGrid. Use a
                // concrete string column instead of object; boxed numeric
                // values in object columns can render as blank in the
                // auto-generated DataGrid columns.
                // 将当前结果加入集合，供后续汇总或界面展示。
                DataColumn dataColumn = table.Columns.Add(name, typeof(string));
                dataColumn.ExtendedProperties["ResultKey"] = column.Key;
            }
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (ResultRow sourceRow in visibleRows)
            {
                DataRow row = table.NewRow();
                // 遍历当前数据集合，逐项完成必要的转换或状态更新。
                for (int index = 0; index < visibleColumns.Count; index++)
                {
                    object value;
                    if (!sourceRow.Values.TryGetValue(visibleColumns[index].Key, out value) || value == null)
                    {
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        row[index] = String.Empty;
                    }
                    else
                    {
                        // DataGrid/DataView can render boxed numeric values as blank when the
                        // generated DataColumn is typed as object. Store the display value as
                        // an invariant string; the original typed values remain in ResultTable
                        // for charts and image reconstruction.
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        row[index] = value is IFormattable
                            ? ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture)
                            // 将外部数据转换为目标类型，并保持约定的表示格式。
                            : Convert.ToString(value, CultureInfo.InvariantCulture);
                    }
                }
                table.Rows.Add(row);
            }
            return new ResultDataSetViewModel
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Name = source.Name ?? artifactName,
                ArtifactName = artifactName,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                SourcePath = NormalizeDisplayPath(source.SourcePath),
                RowCount = table.Rows.Count,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Rows = table.DefaultView
            };
        }

        private static ResultDataSetViewModel ToTrajectoryDataSet(string artifactName, ResultTable source)
        {
            string[] headers =
            {
                // 继续处理当前业务步骤，保持上下文状态一致。
                "时间（s）", "位置X（m）", "位置Y（m）", "位置Z（m）",
                "速度VX（m·s⁻¹）", "速度VY（m·s⁻¹）", "速度VZ（m·s⁻¹）",
                // 继续处理当前业务步骤，保持上下文状态一致。
                "偏航角（度）", "俯仰角（度）", "滚转角（度）",
                "偏航角速度（度·s⁻¹）", "俯仰角速度（度·s⁻¹）", "滚转角速度（度·s⁻¹）",
                // 继续处理当前业务步骤，保持上下文状态一致。
                "高度（m）", "全速度（m·s⁻¹）", "经度（度）", "纬度（度）"
            };
            var table = new DataTable(source.Name ?? artifactName) { Locale = CultureInfo.InvariantCulture };
            foreach (string header in headers) table.Columns.Add(header, typeof(string));

            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            IEnumerable<ResultRow> rows = source.Rows;
            if (source.Columns.Any(c => c.Key == "object_id"))
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                rows = rows.Where(row => Number(row, "object_id") == 1.0);

            double previousTime = 0.0, previousYaw = 0.0, previousPitch = 0.0;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            bool hasPrevious = false;
            DataRow previousDataRow = null;
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (ResultRow sourceRow in rows.OrderBy(row => Number(row, "time_s")))
            {
                double time = Number(sourceRow, "time_s");
                double x = Number(sourceRow, "x_m"), y = Number(sourceRow, "y_m"), z = Number(sourceRow, "z_m");
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                double vx = Number(sourceRow, "vx_m_s"), vy = Number(sourceRow, "vy_m_s"), vz = Number(sourceRow, "vz_m_s");
                double horizontalSpeed = Math.Sqrt(vx * vx + vy * vy);
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                double fullSpeed = Math.Sqrt(horizontalSpeed * horizontalSpeed + vz * vz);
                double yaw = RadiansToDegrees(Math.Atan2(vy, vx));
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                double pitch = RadiansToDegrees(Math.Atan2(vz, horizontalSpeed));
                double radius = Math.Sqrt(x * x + y * y + z * z);
                double longitude = RadiansToDegrees(Math.Atan2(y, x));
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                double latitude = radius > 0.0 ? RadiansToDegrees(Math.Asin(z / radius)) : 0.0;
                double altitude = radius - 6371008.8; // Mean Earth radius, metres.

                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                object yawRate = 0.0, pitchRate = 0.0, rollRate = 0.0;
                double elapsed = time - previousTime;
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (hasPrevious && elapsed > 0.0)
                {
                    yawRate = NormalizeAngleDegrees(yaw - previousYaw) / elapsed;
                    pitchRate = (pitch - previousPitch) / elapsed;
                    // Give the first sample a forward-difference value; later
                    // samples retain the backward difference for their interval.
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (table.Rows.Count == 1 && previousDataRow != null)
                    {
                        previousDataRow[10] = FormatDisplayValue(yawRate);
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        previousDataRow[11] = FormatDisplayValue(pitchRate);
                        previousDataRow[12] = FormatDisplayValue(rollRate);
                    }
                }

                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                DataRow row = table.NewRow();
                object[] values =
                {
                    time, x, y, z, vx, vy, vz, yaw, pitch, 0.0,
                    // 继续处理当前业务步骤，保持上下文状态一致。
                    yawRate, pitchRate, rollRate, altitude, fullSpeed, longitude, latitude
                };
                for (int index = 0; index < values.Length; index++)
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    row[index] = FormatDisplayValue(values[index]);
                table.Rows.Add(row);
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                previousDataRow = row;

                previousTime = time;
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                previousYaw = yaw;
                previousPitch = pitch;
                hasPrevious = true;
            }

            // 返回当前步骤生成的结果，并结束本次调用。
            return new ResultDataSetViewModel
            {
                Name = source.Name ?? artifactName,
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                ArtifactName = artifactName,
                SourcePath = NormalizeDisplayPath(source.SourcePath),
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                RowCount = table.Rows.Count,
                Rows = table.DefaultView
            };
        }

        private static double Number(ResultRow row, string key)
        {
            // 继续处理当前业务步骤，保持上下文状态一致。
            object value;
            return row.Values.TryGetValue(key, out value) && value != null
                // 将外部数据转换为目标类型，并保持约定的表示格式。
                ? Convert.ToDouble(value, CultureInfo.InvariantCulture)
                : 0.0;
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static string FormatDisplayValue(object value)
        {
            if (value == null) return String.Empty;
            var formattable = value as IFormattable;
            // 返回当前步骤生成的结果，并结束本次调用。
            return formattable != null
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                // 将外部数据转换为目标类型，并保持约定的表示格式。
                : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static double RadiansToDegrees(double radians)
        {
            // 返回当前步骤生成的结果，并结束本次调用。
            return radians * 180.0 / Math.PI;
        }

        private static double NormalizeAngleDegrees(double angle)
        {
            while (angle > 180.0) angle -= 360.0;
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            while (angle <= -180.0) angle += 360.0;
            return angle;
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static string NormalizeDisplayPath(string path)
        {
            return String.IsNullOrWhiteSpace(path) ? "未记录源文件" : Path.GetFullPath(path);
        }

        // 将当前结果加入集合，供后续汇总或界面展示。
        private static void Add(ICollection<ResultSummaryItemViewModel> target, string name, string value)
        {
            if (!String.IsNullOrWhiteSpace(value)) target.Add(new ResultSummaryItemViewModel { Name = name, Value = value });
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static string TranslateState(string state)
        {
            switch (state ?? String.Empty)
            {
                case "Preparing": return "准备中";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "Running": return "运行中";
                case "Stopping": return "停止中";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "Completed": return "已完成";
                case "Failed": return "失败";
                // 根据当前枚举值选择对应处理路径，保证各类状态得到明确响应。
                case "Cancelled": return "已停止";
                default: return String.IsNullOrWhiteSpace(state) ? "未知" : state;
            }
        }
    }
}
