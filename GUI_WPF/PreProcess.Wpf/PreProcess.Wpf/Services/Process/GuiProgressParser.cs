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
            progress = null;
            if (line == null || !line.StartsWith("GUI_PROGRESS ", StringComparison.Ordinal)) return false;
            try
            {
                var fields = new JavaScriptSerializer().DeserializeObject(line.Substring(13)) as Dictionary<string, object>;
                if (fields == null || String.IsNullOrEmpty(Text(fields, "state"))) return false;
                double timestamp;
                object raw;
                double? time = fields.TryGetValue("timestamp", out raw) && raw != null && !(raw is bool) &&
                    Double.TryParse(Convert.ToString(raw, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out timestamp) &&
                    !Double.IsNaN(timestamp) && !Double.IsInfinity(timestamp) ? (double?)timestamp : null;
                progress = new ProcessProgressEvent { Module = Text(fields, "module"), State = Text(fields, "state"),
                    RunId = Text(fields, "run_id"), RunDirectory = Text(fields, "run_dir"), Timestamp = time, Fields = fields };
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }
        private static string Text(Dictionary<string, object> fields, string key)
        { object value; return fields.TryGetValue(key, out value) ? value as string : null; }
    }
}
