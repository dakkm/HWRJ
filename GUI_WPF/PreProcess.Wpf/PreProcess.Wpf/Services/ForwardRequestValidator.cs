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
        internal static void Validate(string json)
        {
            var serializer = RequestGenerator.Serializer();
            Dictionary<string, object> schema;
            using (var stream = typeof(RequestGenerator).Assembly.GetManifestResourceStream("PreProcess.Wpf.forward_request_schema.json"))
            using (var reader = new StreamReader(stream)) schema = Object(serializer.DeserializeObject(reader.ReadToEnd()), "schema");
            var request = Object(serializer.DeserializeObject(json), "请求");
            var fields = Object(schema["fields"], "schema.fields");
            Keys(request, fields.Keys, "顶层");
            foreach (string section in new[] { "CASE", "ENVIRONMENT", "GROUP_STATE", "OBSERVATION" })
            {
                var value = Object(request[section], section);
                var definitions = Object(fields[section], section);
                Keys(value, definitions.Keys, section);
                foreach (var field in definitions)
                {
                    string path = section + "." + field.Key;
                    string type = (string)Object(field.Value, path)["type"];
                    var v = value[field.Key];
                    if (type == "boolean") Boolean(v, path);
                    else if (type == "string") Token(v, path);
                    else if (type == "vector3")
                    {
                        var vector = v as IList;
                        Require(vector != null && vector.Count == 3, path + " 必须为三分量数组。");
                        foreach (var component in vector) Number(component, path);
                    }
                    else if (type == "integer") Integer(v, path);
                    else Number(v, path);
                }
            }
            var task = Object(request["CASE"], "CASE");
            Require(Number(task["TOTAL_TIME"], "TOTAL_TIME") > 0, "TOTAL_TIME 必须大于0。");
            double n = Integer(task["NUM_SPHERES"], "NUM_SPHERES");
            Require(n >= 1, "NUM_SPHERES 必须为正整数。");
            foreach (string section in new[] { "TARGET_PHYSICS", "TARGET_SCENE" })
            {
                var rows = request[section] as IList;
                Require(rows != null && rows.Count == n, section + " 行数必须等于 NUM_SPHERES。");
                var definition = Object(fields[section], section);
                var names = ((IList)definition["row_fields"]).Cast<string>().ToArray();
                var ids = new HashSet<double>();
                foreach (var item in rows)
                {
                    var row = Object(item, section);
                    Keys(row, names, section);
                    double id = Integer(row["id"], section + ".id");
                    Require(id >= 1 && id <= n && ids.Add(id), section + " 的 ID 必须唯一且覆盖 1..NUM_SPHERES。");
                    foreach (string name in names.Skip(1))
                    {
                        if (name == "active") Boolean(row[name], section + ".active");
                        else Number(row[name], section + "." + name);
                    }
                    if (section == "TARGET_PHYSICS")
                    {
                        Require(Number(row["r"], "r") > 0.005, "目标半径 r 必须大于0.005 m。");
                        foreach (string name in new[] { "rho", "cp", "t_init" }) Require(Number(row[name], name) > 0, name + " 必须大于0。");
                    }
                    else
                    {
                        double release = Number(row["release_time"], "release_time");
                        Require(release >= 0, "release_time 必须非负。");
                        if (id == 1) Require(Math.Abs(release) <= 1e-12 && (bool)row["active"], "1号目标必须启用且释放时间为0。");
                    }
                }
            }
        }
        private static Dictionary<string, object> Object(object value, string path)
        {
            var result = value as Dictionary<string, object>;
            Require(result != null, path + " 必须为对象，不能缺失。");
            return result;
        }
        private static void Keys(Dictionary<string, object> value, IEnumerable<string> names, string path)
        {
            string missing = String.Join(",", names.Except(value.Keys));
            string extra = String.Join(",", value.Keys.Except(names));
            Require(missing.Length == 0 && extra.Length == 0, path + " 字段不符合合同；缺少=" + missing + "；未知=" + extra);
        }
        private static double Number(object value, string path)
        {
            Require(value is int || value is long || value is double || value is decimal || value is float, path + " 必须为数值。");
            double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            Require(!Double.IsNaN(number) && !Double.IsInfinity(number), path + " 必须为有限数值。");
            return number;
        }
        private static double Integer(object value, string path)
        {
            double number = Number(value, path);
            Require(number == Math.Truncate(number), path + " 必须为整数。");
            return number;
        }
        private static void Boolean(object value, string path) { Require(value is bool, path + " 必须为 JSON true/false。"); }
        private static void Token(object value, string path)
        {
            string text = value as string;
            Require(!String.IsNullOrWhiteSpace(text) && text.IndexOfAny(new[] { ' ', '\t', '#', '=', '[', ']' }) < 0,
                path + " 必须为不含空格或输入格式保留字符的非空字符串。");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new ArgumentException(message); }
    }
}
