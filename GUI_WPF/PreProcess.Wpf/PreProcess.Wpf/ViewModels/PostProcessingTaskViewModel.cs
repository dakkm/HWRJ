using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Input;
using System.Web.Script.Serialization;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Execution;
using PreProcess.Wpf.Services.Results;

namespace PreProcess.Wpf.ViewModels
{
    /// <summary>
    /// 后处理任务的只读显示模型。它只组织已有运行记录，不负责启动任何模块。
    /// </summary>
    public sealed class PostProcessingTaskViewModel : ObservableObject
    {
        private readonly PostProcessingResultIndex index = new PostProcessingResultIndex();
        private readonly ResultDirectoryLoader loader = new ResultDirectoryLoader();
        private PostProcessingResultIndexItem selectedResult;
        private ResultBrowserViewModel resultBrowser;

        public PostProcessingTaskViewModel()
        {
            Results = new ObservableCollection<PostProcessingResultIndexItem>();
            ResultBrowser = ResultBrowserViewModel.Empty("后处理结果");
            OpenSelectedResultCommand = new RelayCommand(_ => OpenSelectedResult());
        }

        public ObservableCollection<PostProcessingResultIndexItem> Results { get; private set; }

        public ResultBrowserViewModel ResultBrowser
        {
            get { return resultBrowser; }
            private set { resultBrowser = value; Notify(); }
        }

        public ICommand OpenSelectedResultCommand { get; private set; }

        public PostProcessingResultIndexItem SelectedResult
        {
            get { return selectedResult; }
            set
            {
                if (ReferenceEquals(selectedResult, value)) return;
                selectedResult = value;
                Notify();
                Notify("CanOpenSelectedResult");
            }
        }

        public bool HasAvailableResults
        {
            get
            {
                foreach (PostProcessingResultIndexItem item in Results)
                    if (item.Availability == PostProcessingResultAvailability.Available) return true;
                return false;
            }
        }

        public bool CanOpenSelectedResult
        {
            get { return SelectedResult != null && SelectedResult.Availability == PostProcessingResultAvailability.Available; }
        }

        public int ForwardCount { get { return Count(ResultModuleType.Forward); } }
        public int PredictionCount { get { return Count(ResultModuleType.Prediction); } }
        public int TrajectoryCount { get { return Count(ResultModuleType.Trajectory); } }
        public int SimilarityCount { get { return Count(ResultModuleType.Similarity); } }
        public int SceneCount { get { return Count(ResultModuleType.Scene); } }

        private int Count(ResultModuleType module)
        {
            int count = 0;
            foreach (PostProcessingResultIndexItem item in Results)
                if (item.ModuleType == module && item.Availability == PostProcessingResultAvailability.Available) count++;
            return count;
        }

        public void Refresh(IEnumerable<RunRecord> records)
        {
            Results.Clear();
            IList<PostProcessingResultIndexItem> items = index.Build(records);
            foreach (PostProcessingResultIndexItem item in items) Results.Add(item);
            SelectedResult = null;
            Notify("HasAvailableResults");
            Notify("CanOpenSelectedResult");
            Notify("ForwardCount");
            Notify("PredictionCount");
            Notify("TrajectoryCount");
            Notify("SimilarityCount");
            Notify("SceneCount");
        }

        public void RefreshFromTaskDirectory(string taskDirectory, IEnumerable<RunRecord> additionalRecords)
        {
            var records = new List<RunRecord>();
            if (!String.IsNullOrWhiteSpace(taskDirectory) && Directory.Exists(taskDirectory))
            {
                var serializer = new JavaScriptSerializer();
                foreach (string path in Directory.GetFiles(taskDirectory, "run-location.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        RunRecord record = serializer.Deserialize<RunRecord>(File.ReadAllText(path, Encoding.UTF8));
                        if (record != null) records.Add(record);
                    }
                    catch (Exception) { }
                }
            }
            if (additionalRecords != null) records.AddRange(additionalRecords);
            Refresh(records);
        }

        /// <summary>
        /// 将选中的结果交给现有结果读取器；调用方可继续复用 ResultBrowserViewModel。
        /// </summary>
        public RunResult OpenSelectedResult()
        {
            if (!CanOpenSelectedResult) throw new InvalidOperationException("当前没有可打开的后处理结果。");
            RunResult result = loader.Load(SelectedResult.ResultDirectory);
            ResultBrowser = ResultBrowserViewModel.FromResult(result, SelectedResult.DisplayTitle, "后处理结果已加载。");
            return result;
        }
    }
}
