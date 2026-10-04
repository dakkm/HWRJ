using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Input;
using Microsoft.Win32;

namespace PreProcess.Wpf.ViewModels
{
    public sealed class DisplayParameter : ObservableObject
    {
        public string Name { get; set; }
        private string value;
        public string Value { get { return value; } set { this.value = value; Notify(); } }
    }

    // This draft deliberately has no TaskModel, request mapper, or execution service.
    public sealed class CapabilityDraft
    {
        public string Format { get; set; }
        public string FeatureId { get; set; }
        public Dictionary<string, string> Parameters { get; set; }
    }

    public sealed class CapabilityPageViewModel : ObservableObject
    {
        public string FeatureId { get; private set; }
        public string Title { get; private set; }
        public string Scope { get; private set; }
        public string Inputs { get; private set; }
        public string Outputs { get; private set; }
        public bool HasActualPage { get; private set; }
        public string Mode { get { return HasActualPage ? "已有功能 · 查看能力范围" : "展示功能 · 不执行计算"; } }
        public string Notice { get { return HasActualPage
            ? "通过下方按钮进入已有功能。此页用于说明输入、输出与适用范围。"
            : "参数仅用于界面展示，可独立保存；不会提交后端，也不会生成计算结果。"; } }
        public ObservableCollection<DisplayParameter> Parameters { get; private set; }
        public bool HasParameters { get { return Parameters.Count > 0; } }
        public string Preview { get { return string.Join("\n", Parameters.Select(p => p.Name + "：" + p.Value)); } }
        private string status = "未保存";
        public string Status { get { return status; } private set { status = value; Notify(); } }
        public ICommand OpenActualCommand { get; private set; }
        public ICommand SaveDraftCommand { get; private set; }
        public ICommand OpenDraftCommand { get; private set; }

        public CapabilityPageViewModel(string id, string title, string scope, string inputs, string outputs,
            Action openActual, params string[] parameters)
        {
            FeatureId = id; Title = title; Scope = scope; Inputs = inputs; Outputs = outputs;
            HasActualPage = openActual != null;
            Parameters = new ObservableCollection<DisplayParameter>();
            for (int i = 0; i < parameters.Length; i += 2)
            {
                var p = new DisplayParameter { Name = parameters[i], Value = parameters[i + 1].Replace("（展示）", "").Replace("（展示，不自动启动）", "").Replace("（尚无计算数据）", "") };
                p.PropertyChanged += (s, e) => { Notify(nameof(Preview)); Status = "未保存"; };
                Parameters.Add(p);
            }
            OpenActualCommand = new RelayCommand(_ => { if (openActual != null) openActual(); });
            SaveDraftCommand = new RelayCommand(_ => SaveDraft());
            OpenDraftCommand = new RelayCommand(_ => OpenDraft());
        }

        public void SaveTo(string path)
        {
            var draft = new CapabilityDraft { Format = "display-only-v1", FeatureId = FeatureId,
                Parameters = Parameters.ToDictionary(p => p.Name, p => p.Value) };
            File.WriteAllText(path, new JavaScriptSerializer().Serialize(draft), new UTF8Encoding(false));
            Status = "已保存";
        }

        public void LoadFrom(string path)
        {
            var draft = new JavaScriptSerializer().Deserialize<CapabilityDraft>(File.ReadAllText(path, Encoding.UTF8));
            if (draft == null || draft.Format != "display-only-v1" || draft.FeatureId != FeatureId ||
                draft.Parameters == null || draft.Parameters.Count != Parameters.Count ||
                Parameters.Any(p => !draft.Parameters.ContainsKey(p.Name) || draft.Parameters[p.Name] == null))
                throw new InvalidDataException("配置文件不匹配或内容不完整。");
            foreach (var p in Parameters) p.Value = draft.Parameters[p.Name];
            Status = "已加载";
        }

        private void SaveDraft()
        {
            var dialog = new SaveFileDialog { Filter = "配置文件 (*.display.json)|*.display.json", DefaultExt = ".display.json", FileName = FeatureId + ".display.json" };
            if (dialog.ShowDialog() != true) return;
            try { SaveTo(dialog.FileName); } catch (Exception ex) { Status = "保存失败：" + ex.Message; }
        }
        private void OpenDraft()
        {
            var dialog = new OpenFileDialog { Filter = "配置文件 (*.display.json)|*.display.json", CheckFileExists = true };
            if (dialog.ShowDialog() != true) return;
            try { LoadFrom(dialog.FileName); } catch (Exception ex) { Status = "打开失败：" + ex.Message; }
        }
    }
}
