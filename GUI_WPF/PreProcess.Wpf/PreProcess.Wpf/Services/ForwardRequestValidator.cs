using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace PreProcess.Wpf.Services
{
    // This is the backend's custom contract descriptor, not a standard JSON Schema dialect.
    internal static class ForwardRequestValidator
    {
        // 检查输入及依赖是否满足要求，提前阻止无效操作。
        internal static void Validate(string json)
        {
            var serializer = RequestGenerator.Serializer();
            Dictionary<string, object> schema;
            using (var stream = typeof(RequestGenerator).Assembly.GetManifestResourceStream("PreProcess.Wpf.forward_request_schema.json"))
            using (var reader = new StreamReader(stream)) schema = Object(serializer.DeserializeObject(reader.ReadToEnd()), "schema");
            // 在持久化格式与内存对象之间转换，供后续流程继续使用。
            var request = Object(serializer.DeserializeObject(json), "请求");
            var fields = Object(schema["fields"], "schema.fields");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Keys(request, fields.Keys, "顶层");
            foreach (string section in new[] { "CASE", "ENVIRONMENT", "GROUP_STATE", "OBSERVATION" })
            {
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                var value = Object(request[section], section);
                var definitions = Object(fields[section], section);
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                Keys(value, definitions.Keys, section);
                foreach (var field in definitions)
                {
                    string path = section + "." + field.Key;
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    string type = (string)Object(field.Value, path)["type"];
                    var v = value[field.Key];
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (type == "boolean") Boolean(v, path);
                    else if (type == "string") Token(v, path);
                    // 当前置条件不成立时执行备用路径，保持处理结果完整。
                    else if (type == "vector3")
                    {
                        var vector = v as IList;
                        Require(vector != null && vector.Count == 3, path + " 必须为三分量数组。");
                        // 遍历当前数据集合，逐项完成必要的转换或状态更新。
                        foreach (var component in vector) Number(component, path);
                    }
                    else if (type == "integer") Integer(v, path);
                    // 当前置条件不成立时执行备用路径，保持处理结果完整。
                    else Number(v, path);
                }
            }
            var task = Object(request["CASE"], "CASE");
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Require(Number(task["TOTAL_TIME"], "TOTAL_TIME") > 0, "TOTAL_TIME 必须大于0。");
            double n = Integer(task["NUM_SPHERES"], "NUM_SPHERES");
            Require(n >= 1, "NUM_SPHERES 必须为正整数。");
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            foreach (string section in new[] { "TARGET_PHYSICS", "TARGET_SCENE" })
            {
                var rows = request[section] as IList;
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Require(rows != null && rows.Count == n, section + " 行数必须等于 NUM_SPHERES。");
                var definition = Object(fields[section], section);
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                var names = ((IList)definition["row_fields"]).Cast<string>().ToArray();
                var ids = new HashSet<double>();
                foreach (var item in rows)
                {
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    var row = Object(item, section);
                    Keys(row, names, section);
                    // 更新当前流程使用的数据，为下一处理步骤做好准备。
                    double id = Integer(row["id"], section + ".id");
                    // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
                    Require(id >= 1 && id <= n && ids.Add(id), section + " 的 ID 必须唯一且覆盖 1..NUM_SPHERES。");
                    // 遍历当前数据集合，逐项完成必要的转换或状态更新。
                    foreach (string name in names.Skip(1))
                    {
                        if (name == "active") Boolean(row[name], section + ".active");
                        else Number(row[name], section + "." + name);
                    }
                    // 校验当前条件，仅在满足业务约束时进入该处理分支。
                    if (section == "TARGET_PHYSICS")
                    {
                        Require(Number(row["r"], "r") > 0.005, "目标半径 r 必须大于0.005 m。");
                        // 遍历当前数据集合，逐项完成必要的转换或状态更新。
                        foreach (string name in new[] { "rho", "cp", "t_init" }) Require(Number(row[name], name) > 0, name + " 必须大于0。");
                    }
                    else
                    {
                        // 更新当前流程使用的数据，为下一处理步骤做好准备。
                        double release = Number(row["release_time"], "release_time");
                        Require(release >= 0, "release_time 必须非负。");
                        // 校验当前条件，仅在满足业务约束时进入该处理分支。
                        if (id == 1) Require(Math.Abs(release) <= 1e-12 && (bool)row["active"], "1号目标必须启用且释放时间为0。");
                    }
                }
            }
        }
        private static Dictionary<string, object> Object(object value, string path)
        {
            var result = value as Dictionary<string, object>;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            Require(result != null, path + " 必须为对象，不能缺失。");
            return result;
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void Keys(Dictionary<string, object> value, IEnumerable<string> names, string path)
        {
            string missing = String.Join(",", names.Except(value.Keys));
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            string extra = String.Join(",", value.Keys.Except(names));
            Require(missing.Length == 0 && extra.Length == 0, path + " 字段不符合合同；缺少=" + missing + "；未知=" + extra);
        }
        private static double Number(object value, string path)
        {
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Require(value is int || value is long || value is double || value is decimal || value is float, path + " 必须为数值。");
            double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            // 调用对应组件完成当前步骤，并保留产生的处理结果。
            Require(!Double.IsNaN(number) && !Double.IsInfinity(number), path + " 必须为有限数值。");
            return number;
        }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static double Integer(object value, string path)
        {
            double number = Number(value, path);
            Require(number == Math.Truncate(number), path + " 必须为整数。");
            // 返回当前步骤生成的结果，并结束本次调用。
            return number;
        }
        private static void Boolean(object value, string path) { Require(value is bool, path + " 必须为 JSON true/false。"); }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        private static void Token(object value, string path)
        {
            string text = value as string;
            // 检查输入及依赖是否满足要求，提前阻止无效操作。
            Require(!String.IsNullOrWhiteSpace(text) && text.IndexOfAny(new[] { ' ', '\t', '#', '=', '[', ']' }) < 0,
                path + " 必须为不含空格或输入格式保留字符的非空字符串。");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new ArgumentException(message); }
    }
}
