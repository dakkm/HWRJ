using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace PreProcess.Wpf.Services.Process
{
    public static class GuiProgressParser
    {
        public static bool TryParse(string line, out ProcessProgressEvent progress)
        {
            // 记录本阶段的状态信息，供界面反馈和问题诊断使用。
            progress = null;
            if (line == null || !line.StartsWith("GUI_PROGRESS ", StringComparison.Ordinal)) return false;
            // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
            try
            {
                // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
                var fields = new JavaScriptSerializer().DeserializeObject(line.Substring(13)) as Dictionary<string, object>;
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (fields == null || String.IsNullOrEmpty(Text(fields, "state"))) return false;
                double timestamp;
                object raw;
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                double? time = fields.TryGetValue("timestamp", out raw) && raw != null && !(raw is bool) &&
                    Double.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out timestamp) &&
                    // 调用对应组件完成当前步骤，并保留产生的处理结果。
                    !Double.IsNaN(timestamp) && !Double.IsInfinity(timestamp) ? (double?)timestamp : null;
                progress = new ProcessProgressEvent { Module = Text(fields, "module"), State = Text(fields, "state"),
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    RunId = Text(fields, "run_id"), RunDirectory = Text(fields, "run_dir"), Timestamp = time, Fields = fields };
                return true;
            }
            catch (ArgumentException) { return false; }
            // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
            catch (InvalidOperationException) { return false; }
        }
        private static string Text(Dictionary<string, object> fields, string key)
        // 调用对应组件完成当前步骤，并保留产生的处理结果。
        { object value; return fields.TryGetValue(key, out value) ? value as string : null; }
    }
}
