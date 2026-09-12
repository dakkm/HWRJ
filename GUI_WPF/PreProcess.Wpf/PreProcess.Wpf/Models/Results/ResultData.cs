using System.Collections.Generic;

namespace PreProcess.Wpf.Models.Results
{
    // Format-neutral containers. P5.2 readers will populate these from confirmed backend contracts.
    public sealed class ResultColumn
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string Unit { get; set; }
    }

    public sealed class ResultRow
    {
        public ResultRow()
        {
            Values = new Dictionary<string, object>();
        }

        public IDictionary<string, object> Values { get; private set; }
    }

    public sealed class ResultTable
    {
        public ResultTable()
        {
            Columns = new List<ResultColumn>();
            Rows = new List<ResultRow>();
        }

        public string Name { get; set; }
        public string SourcePath { get; set; }
        public IList<ResultColumn> Columns { get; private set; }
        public IList<ResultRow> Rows { get; private set; }
    }

    public abstract class ResultArtifact
    {
        protected ResultArtifact()
        {
            Summary = new Dictionary<string, string>();
            Tables = new List<ResultTable>();
        }

        public string Name { get; set; }
        public string SourcePath { get; set; }
        public IDictionary<string, string> Summary { get; private set; }
        public IList<ResultTable> Tables { get; private set; }
    }
}
