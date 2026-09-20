using System.Collections.Generic;

namespace PreProcess.Wpf.Models.Results
{
    // Format-neutral containers. P5.2 readers will populate these from confirmed backend contracts.
    public sealed class ResultColumn
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string Unit { get; set; }
    }

    // 定义 ResultRow 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class ResultRow
    // 说明当前文件承载的类型职责，便于维护者快速定位功能边界。
    {
        public ResultRow()
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            Values = new Dictionary<string, object>();
        }

        public IDictionary<string, object> Values { get; private set; }
    }

    // 定义 ResultTable 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class ResultTable
    {
        public ResultTable()
        {
            Columns = new List<ResultColumn>();
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            Rows = new List<ResultRow>();
        }

        public string Name { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string SourcePath { get; set; }
        public IList<ResultColumn> Columns { get; private set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public IList<ResultRow> Rows { get; private set; }
    }

    public abstract class ResultArtifact
    {
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        protected ResultArtifact()
        // 本文件只处理所属模块的数据或界面逻辑，不在此处引入额外副作用。
        {
            Summary = new Dictionary<string, string>();
            Tables = new List<ResultTable>();
        }

        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string Name { get; set; }
        public string SourcePath { get; set; }
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public IDictionary<string, string> Summary { get; private set; }
        public IList<ResultTable> Tables { get; private set; }
    }
}
