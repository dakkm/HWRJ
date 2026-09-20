using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using PreProcess.Wpf.Models.Results;
using PreProcess.Wpf.Services.Execution;

namespace PreProcess.Wpf.Services.Results
{
    public abstract class ResultReaderBase
    {
        // 保存该组件运行所需的配置或中间状态。
        private const int MaximumRows = 2000000;

        public abstract RunResult Read(RunResult result);

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public RunResult Read(RunRecord record, ResultModuleType? moduleOverride = null)
        {
            return Read(new ResultDirectoryManager().Create(record, moduleOverride));
        }

        protected static bool Prepare(RunResult result, params ResultModuleType[] supportedModules)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (result == null) throw new ArgumentNullException("result");
            if (!supportedModules.Contains(result.ModuleType))
                // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                throw new ArgumentException("结果读取器与模块类型不匹配。", "result");

            result.Summary.Clear();
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            result.Issues.Clear();
            result.Temperatures.Clear();
            result.Trajectories.Clear();
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            result.InfraredResponses.Clear();
            result.PointImages.Clear();
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            result.Similarities.Clear();
            result.Scenes.Clear();

            // 处理文件系统路径及数据，并在使用前确认目标有效。
            result.ResultExists = !String.IsNullOrWhiteSpace(result.OutputDirectory) && Directory.Exists(result.OutputDirectory);
            result.AvailabilityMessage = result.ResultExists
                ? "结果目录已定位。"
                // 检查输入及依赖是否满足要求，提前阻止无效操作。
                : String.IsNullOrWhiteSpace(result.OutputDirectory) ? "未提供结果目录。" : "结果目录不存在：" + result.OutputDirectory;
            if (!result.ResultExists) result.Issues.Add(result.AvailabilityMessage);
            // 返回当前步骤生成的结果，并结束本次调用。
            return result.ResultExists;
        }

        protected static ResultTable ReadCsv(RunResult result, string relativePath, string tableName,
            // 继续处理当前业务步骤，保持上下文状态一致。
            IEnumerable<string> requiredColumns, IEnumerable<string> numericColumns, bool required)
        {
            string path = Path.Combine(result.OutputDirectory, relativePath);
            if (!File.Exists(path))
            {
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (required) AddIssue(result, "缺少结果文件：" + path);
                return null;
            }

            // 进入可能失败的操作区间，并由后续异常分支统一处理故障。
            try
            {
                using (Stream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (Stream decoded = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                    ? (Stream)new GZipStream(stream, CompressionMode.Decompress, false) : stream)
                using (var reader = new StreamReader(decoded, new UTF8Encoding(false, true), true))
                {
                    // 将外部数据转换为目标类型，并保持约定的表示格式。
                    List<List<string>> records = ParseCsv(reader, path);
                    if (records.Count == 0) throw new ResultReadException(path, "文件为空。");
                    List<string> headers = records[0].Select((x, i) => (i == 0 ? x.TrimStart('\uFEFF') : x).Trim()).ToList();
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (headers.Any(String.IsNullOrWhiteSpace)) throw new ResultReadException(path, "表头包含空列名。");
                    if (headers.Distinct(StringComparer.Ordinal).Count() != headers.Count) throw new ResultReadException(path, "表头包含重复列名。");

                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    var headerSet = new HashSet<string>(headers, StringComparer.Ordinal);
                    string[] missing = (requiredColumns ?? Enumerable.Empty<string>()).Where(x => !headerSet.Contains(x)).ToArray();
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (missing.Length > 0) throw new ResultReadException(path, "缺少列：" + String.Join(", ", missing));

                    var numericSet = new HashSet<string>(numericColumns ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
                    // 处理文件系统路径及数据，并在使用前确认目标有效。
                    var table = new ResultTable { Name = tableName, SourcePath = Path.GetFullPath(path) };
                    foreach (string header in headers)
                        table.Columns.Add(new ResultColumn { Key = header, DisplayName = header, Unit = InferUnit(header) });

                    // 遍历当前数据集合，逐项完成必要的转换或状态更新。
                    for (int rowIndex = 1; rowIndex < records.Count; rowIndex++)
                    {
                        List<string> record = records[rowIndex];
                        // 校验当前条件，仅在满足业务约束时进入该处理分支。
                        if (record.Count == 1 && String.IsNullOrWhiteSpace(record[0])) continue;
                        if (record.Count != headers.Count)
                            // 发现无效输入或运行状态后立即终止，防止错误继续传播。
                            throw new ResultReadException(path, "第 " + (rowIndex + 1).ToString(CultureInfo.InvariantCulture) + " 行列数为 " + record.Count.ToString(CultureInfo.InvariantCulture) + "，应为 " + headers.Count.ToString(CultureInfo.InvariantCulture) + "。");
                        var row = new ResultRow();
                        for (int columnIndex = 0; columnIndex < headers.Count; columnIndex++)
                        {
                            // 更新当前流程使用的数据，为下一处理步骤做好准备。
                            string value = record[columnIndex].Trim();
                            object converted = ConvertCell(value, path, rowIndex + 1, headers[columnIndex], numericSet.Contains(headers[columnIndex]));
                            // 更新当前流程使用的数据，为下一处理步骤做好准备。
                            row.Values[headers[columnIndex]] = converted;
                        }
                        table.Rows.Add(row);
                        // 校验当前条件，仅在满足业务约束时进入该处理分支。
                        if (table.Rows.Count > MaximumRows) throw new ResultReadException(path, "数据行数超过 GUI 读取上限。");
                    }
                    return table;
                }
            }
            catch (ResultReadException) { throw; }
            // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
            catch (Exception ex) { throw new ResultReadException(path, ex.Message, ex); }
        }

        protected static IDictionary<string, object> ReadJson(RunResult result, string relativePath, bool required)
        {
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string path = Path.Combine(result.OutputDirectory, relativePath);
            if (!File.Exists(path))
            {
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (required) AddIssue(result, "缺少结果文件：" + path);
                return null;
            }
            try
            {
                // 继续处理当前业务步骤，保持上下文状态一致。
                string text;
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true)) text = reader.ReadToEnd();
                var value = new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }.DeserializeObject(text) as IDictionary<string, object>;
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (value == null) throw new ResultReadException(path, "JSON 根节点必须是对象。");
                return value;
            }
            // 捕获本步骤产生的异常，将故障转换为可追踪的运行状态。
            catch (ResultReadException) { throw; }
            catch (Exception ex) { throw new ResultReadException(path, ex.Message, ex); }
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        protected static void AddJsonSummary(IDictionary<string, string> destination, IDictionary<string, object> source, string prefix)
        {
            if (source == null) return;
            foreach (var item in source) AddJsonValue(destination, prefix + item.Key, item.Value);
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        protected static string OptionalString(IDictionary<string, object> source, string key)
        {
            if (source == null || !source.ContainsKey(key) || source[key] == null) return null;
            // 返回当前步骤生成的结果，并结束本次调用。
            return Convert.ToString(source[key], CultureInfo.InvariantCulture);
        }

        protected static void ValidateIdentity(IDictionary<string, object> json, RunResult result, string expectedModule, string relativePath)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (json == null) return;
            string module = OptionalString(json, "module");
            string runId = OptionalString(json, "run_id");
            // 处理文件系统路径及数据，并在使用前确认目标有效。
            string path = Path.Combine(result.OutputDirectory, relativePath);
            if (module != null && module != expectedModule) throw new ResultReadException(path, "module 应为 " + expectedModule + "，实际为 " + module + "。");
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (runId != null && !String.Equals(runId, result.RunId, StringComparison.Ordinal))
                throw new ResultReadException(path, "run_id 与当前运行记录不一致。");
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        protected static void AddIssue(RunResult result, string issue)
        {
            if (!result.Issues.Contains(issue)) result.Issues.Add(issue);
        }

        private static List<List<string>> ParseCsv(TextReader reader, string path)
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var records = new List<List<string>>();
            var row = new List<string>();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            var field = new StringBuilder();
            bool quoted = false;
            // 继续处理当前业务步骤，保持上下文状态一致。
            int value;
            while ((value = reader.Read()) >= 0)
            {
                char ch = (char)value;
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (quoted)
                {
                    if (ch == '"')
                    {
                        // 校验当前条件，仅在满足业务约束时进入该处理分支。
                        if (reader.Peek() == '"') { reader.Read(); field.Append('"'); }
                        else quoted = false;
                    }
                    // 当前置条件不成立时执行备用路径，保持处理结果完整。
                    else field.Append(ch);
                    continue;
                }
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (ch == '"')
                {
                    if (field.Length != 0) throw new ResultReadException(path, "引号出现在未加引号字段中。");
                    quoted = true;
                }
                // 当前置条件不成立时执行备用路径，保持处理结果完整。
                else if (ch == ',') { row.Add(field.ToString()); field.Clear(); }
                else if (ch == '\r' || ch == '\n')
                {
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (ch == '\r' && reader.Peek() == '\n') reader.Read();
                    row.Add(field.ToString()); field.Clear(); records.Add(row); row = new List<string>();
                }
                // 当前置条件不成立时执行备用路径，保持处理结果完整。
                else field.Append(ch);
            }
            if (quoted) throw new ResultReadException(path, "文件结束时引号未闭合。");
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); records.Add(row); }
            // 返回当前步骤生成的结果，并结束本次调用。
            return records;
        }

        private static object ConvertCell(string text, string path, int row, string column, bool mustBeNumeric)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (text.Length == 0)
            {
                if (mustBeNumeric) throw new ResultReadException(path, "第 " + row.ToString(CultureInfo.InvariantCulture) + " 行 " + column + " 不能为空。");
                // 返回当前步骤生成的结果，并结束本次调用。
                return null;
            }
            long integer;
            if (Int64.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer)) return integer;
            // 继续处理当前业务步骤，保持上下文状态一致。
            double number;
            if (Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (Double.IsNaN(number) || Double.IsInfinity(number)) throw new ResultReadException(path, "第 " + row.ToString(CultureInfo.InvariantCulture) + " 行 " + column + " 不是有限数值。");
                return number;
            }
            // 继续处理当前业务步骤，保持上下文状态一致。
            bool boolean;
            if (Boolean.TryParse(text, out boolean)) return boolean;
            if (mustBeNumeric) throw new ResultReadException(path, "第 " + row.ToString(CultureInfo.InvariantCulture) + " 行 " + column + " 不是数值。");
            // 返回当前步骤生成的结果，并结束本次调用。
            return text;
        }

        private static string InferUnit(string key)
        {
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (key == "time_s" || key.EndsWith("_time_s", StringComparison.Ordinal) || key.EndsWith("_seconds", StringComparison.Ordinal)) return "s";
            if (key.StartsWith("T_", StringComparison.Ordinal) || key.EndsWith("_K", StringComparison.Ordinal)) return "K";
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (key.EndsWith("_W_m2", StringComparison.Ordinal)) return "W/m²";
            if (key.EndsWith("_W_sr", StringComparison.Ordinal)) return "W/sr";
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (key.EndsWith("_W", StringComparison.Ordinal)) return "W";
            if (key.EndsWith("_m_s", StringComparison.Ordinal)) return "m/s";
            if (key.EndsWith("_m", StringComparison.Ordinal)) return "m";
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (key.EndsWith("_percent", StringComparison.Ordinal)) return "%";
            return null;
        }

        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void AddJsonValue(IDictionary<string, string> destination, string key, object value)
        {
            var objectValue = value as IDictionary<string, object>;
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (objectValue != null)
            {
                foreach (var child in objectValue) AddJsonValue(destination, key + "." + child.Key, child.Value);
                return;
            }
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (value == null) destination[key] = String.Empty;
            else if (value is bool) destination[key] = ((bool)value) ? "true" : "false";
            // 当前置条件不成立时执行备用路径，保持处理结果完整。
            else if (value is string) destination[key] = (string)value;
            else if (value is IFormattable) destination[key] = ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture);
            // 当前置条件不成立时执行备用路径，保持处理结果完整。
            else destination[key] = new JavaScriptSerializer().Serialize(value);
        }
    }
}
