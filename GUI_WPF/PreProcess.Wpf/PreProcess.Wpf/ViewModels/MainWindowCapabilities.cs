using System;

namespace PreProcess.Wpf.ViewModels
{
    public sealed partial class MainWindowViewModel
    {
        private readonly System.Collections.Generic.Dictionary<NavigationItemViewModel, NavigationItemViewModel> capabilityTargets
            = new System.Collections.Generic.Dictionary<NavigationItemViewModel, NavigationItemViewModel>();
        private void AddCapabilityPages()
        {
            Capability(0, "planning", "规划任务总览", "配置目标、编组、分离运动与环境观测。任务树编号是产品分组，实际计算由正向、智能预测、相似度和场景构建四个入口完成。", "目标参数、场景、环境", "统一任务文件", Navigation[0].Children[0]);
            Capability(0, "targets", "目标对象与编组", "已有球体目标参数和统一/逐目标设置。任意几何结构的通用求解不属于当前后端能力。", "几何、密度、热容、内热源、初温", "目标任务参数", Navigation[0].Children[1]);
            Capability(1, "model", "模型导入", "支持模型文件检查、球壳四面体预览网格及独立网格包。预览网格不参与后端计算；后端采用球体参数模型。", "模型文件或球壳参数", "文件检查和网格预览", Navigation[1].Children[0]);
            Capability(1, "material", "光热物性", "密度、比热容、吸收率、发射率沿用实际任务字段。逐四面体热导率与材料分区在下方专用展示页配置。", "目标物性", "已有物性任务字段", Navigation[1].Children[0]);
            Capability(1, "tetra-material", "单元材料与相邻关系（展示）", "未发现通用四面体网格导入求解、单元相邻关系及单元导热模型的后端接口。此处展示材料分区设计。", "四面体网格、材料分区", "仅显示材料分配草稿", null,
                "单元分区", "区域 A", "密度 (kg/m³)", "2700", "比热容 (J/kg·K)", "900", "热导率 (W/m·K)", "205", "邻接关系", "共享三角面的单元相邻");
            Capability(1, "thermal-boundary", "热学边界条件", "后端已有目标内热源和初始温度。逐表面固定温度、电加热热流密度及控温逻辑仅作展示。", "表面分区、边界类型", "独立展示草稿", null,
                "表面分区", "外表面", "边界类型", "固定温度", "边界温度 (K)", "300", "电加热热流密度 (W/m²)", "0", "控温范围 (K)", "295～305");
            Capability(1, "external-boundary", "外热流边界条件", "已有太阳通量、太阳方向与环境温度输入。地球红外、反照和阴影模型请查看 04 展示节点。", "太阳与环境参数", "任务环境字段", Navigation[0].Children[3]);
            Capability(1, "infrared-parameters", "红外辐射与探测参数", "已有目标红外物性、孔径、观测姿态和探测平面参数。独立光谱窗口配置请查看 06 展示节点。", "目标红外物性、环境与观测", "红外响应计算输入", Navigation[0].Children[3]);
            Capability(2, "time", "求解时间与步长", "仿真时长来自任务。页面中的时间步长与输出间隔仅保存为前端配置；实际后端数值配置独立管理。", "仿真时长、展示控制参数", "任务时长 / 独立控制配置", Navigation[2].Children[0]);
            Capability(2, "precision", "空间离散与精度", "当前后端使用内部光线数和图像网格配置，不接收此处的四面体离散与精度设置。", "网格精度、局部加密要求", "仅显示离散方案", null,
                "离散方式", "四面体", "目标单元尺寸 (m)", "0.01", "局部加密区域", "边界附近", "精度等级", "标准");
            Capability(2, "convergence", "收敛与停止条件", "现有求解控制页可以保存前端收敛与停止条件；这些参数不控制当前后端。", "阈值、迭代次数、超时", "独立控制配置", Navigation[2].Children[0]);
            Capability(2, "solver", "求解器选择", "当前实际运行使用已有正向求解内核；通用稳态/瞬态场求解器的切换仅作展示。", "求解模式", "求解方案草稿", null,
                "求解模式", "瞬态", "温度求解器", "通用温度场求解（展示）", "空间外热流求解器", "太阳 + 地球红外 + 反照（展示）");
            Capability(2, "resources", "运行资源与过程控制", "运行、停止与日志使用实际执行页。多任务队列和 CPU/GPU 资源调度未发现后端支持，以下仅为资源规划。", "资源需求", "资源规划草稿", null,
                "CPU 核数", "4", "计算设备", "CPU", "任务优先级", "普通", "排队策略", "顺序执行（展示）");
            Capability(3, "flux-input", "外热流输入检查", "正向入口校验完整任务参数。太阳通量、方向和几何参与现有计算。", "任务与环境", "输入校验或诊断", Navigation[5].Children[0]);
            Capability(3, "flux-compute", "空间外热流计算", "已有太阳光线追踪、球体之间的遮挡与反射。与运动、温度和红外计算在同一正向执行链中完成，不作为独立求解器启动。", "球体几何、位置、太阳输入", "正向运行记录；无独立外热流场导出接口", Navigation[3].Children[0]);
            Capability(3, "earth", "地球阴影与辐射（展示）", "已有二体轨道运动不能等同于地球阴影判断。未发现地球阴影、地球红外热流和地球反照热流的正式实现。", "轨道、地球与太阳几何", "仅显示地球环境方案", null,
                "阴影模型", "圆柱阴影（展示）", "地球红外通量 (W/m²)", "237", "地球反照率", "0.3", "阴影区处理", "关闭直射太阳项（展示）");
            Capability(3, "flux-results", "外热流结果检查", "实际页面提供正向运行上下文，尚无可独立浏览的外热流场。不会用温度结果冒充外热流结果。", "正向运行记录", "状态与输出位置", Navigation[3].Children[0]);
            Capability(4, "temperature-initial", "温度场初始条件", "当前求解采用逐球体初始温度和内热源，不是逐四面体初温场。", "目标初温与内热源", "任务物性字段", Navigation[0].Children[1]);
            Capability(4, "temperature-compute", "温度场计算", "已有球体集总热容瞬态温度计算，包含太阳吸收、内热源、环境辐射和目标之间的辐射换热。", "正向任务", "逐目标温度历史", Navigation[4].Children[0]);
            Capability(4, "temperature-field", "通用稳态与瞬态温度场（展示）", "未发现基于四面体的空间导热场求解或通用稳态温度场入口。此页仅展示求解方案，不生成温度云图或数值结果。", "单元材料、热边界、网格", "温度场方案草稿", null,
                "求解类型", "稳态", "初始温度 (K)", "300", "热源分区", "区域 A", "展示结果类型", "单元温度场（尚无计算数据）");
            Capability(4, "temperature-results", "温度场结果检查", "从实际正向结果读取逐目标温度历史。球壳着色仅为逐目标温度的几何展示，不代表单元场求解。", "温度历史文件", "温度曲线与表格", Navigation[4].Children[0]);
            Capability(5, "radiation", "红外辐射计算", "已有红外辐射换热、目标辐射响应及辐射强度输出。未发现可配置的中波/长波独立光谱积分接口。", "温度、红外物性、几何", "红外响应历史", Navigation[5].Children[0]);
            Capability(5, "detector", "探测器响应计算", "已有孔径观测响应和探测平面点图像；与正向计算统一执行。", "目标辐射与观测参数", "响应、点图像及相关结果", Navigation[5].Children[0]);
            Capability(5, "spectral", "中波与长波窗口（展示）", "用于展示规划中的红外波段设置。当前后端没有接收波段边界或大气透过率的独立接口。", "波段范围、透过率", "波段方案草稿", null,
                "中波窗口 (μm)", "3～5", "长波窗口 (μm)", "8～14", "大气透过率", "1.0", "输出形式", "分波段辐射强度（展示）");
            Capability(5, "trajectory", "轨迹与姿态后处理", "读取实际正向轨迹，生成轨迹指标及图像，不重新求解正向过程。", "正向轨迹历史", "轨迹图与统计", Navigation[9].Children[1]);
            Capability(5, "scene-results", "红外场景结果", "汇总当前正向运行的温度、红外响应和轨迹结果。", "实际正向输出", "结果摘要", Navigation[5].Children[1]);
            Capability(6, "domain", "模型适用域检查", "完整任务输入由预测入口进行适用域检查。域外警告不等于已验证可泛化；结果应在模型适用范围内解释。", "完整任务与冻结模型", "适用域诊断", Navigation[6].Children[0]);
            Capability(6, "surrogate", "温度代理预测", "当前可执行路径使用温度代理模型预测目标温度，初始值被设为 300 K；不提供任意网格的温度场预测。", "模型特征与时间", "温度预测曲线", Navigation[6].Children[0]);
            Capability(6, "point-image", "红外点图像预测", "已有神经网络点图像预测及模型适用域约束。", "模型特征与固定场景模板", "点图像预测", Navigation[6].Children[0]);
            Capability(6, "comparison", "AI 结果与正向结果对比", "可将正向与预测输出作为参考/候选目录进入相似度评估。", "两组实际结果目录", "相似度与差异指标", Navigation[7].Children[0]);
            Capability(7, "reference", "参考结果选择", "通过相似度执行页选择实际参考结果。", "结果目录", "参考数据", Navigation[7].Children[0]);
            Capability(7, "candidate", "候选结果选择", "通过相似度执行页选择实际候选结果。", "结果目录", "候选数据", Navigation[7].Children[0]);
            Capability(7, "metrics", "评价指标与阈值", "当前评估使用既有指标配置；此处展示自定义指标权重方案，不修改实际评估配置。场景构建的实际验收阈值来自目标参数。", "指标与权重", "评价方案草稿", null,
                "温度指标", "RMSE、MAE、最大偏差", "辐射指标", "辐射强度差异", "温度权重", "0.5", "辐射权重", "0.5", "验收阈值 (%)", "90");
            Capability(7, "similarity-results", "相似度结果", "实际评估结果从运行输出读取，不以展示阈值重写验收结论。", "实际评估输出", "相似度结果", Navigation[7].Children[0]);
            Capability(8, "search", "候选参数搜索", "已有温度相似度逆向搜索。目标参数池、代理初筛、正向复核与相似度验收属于同一组合流程，不是四个独立求解器。", "任务、温度相似度要求、候选数量", "候选与最终验收记录", Navigation[8].Children[0]);
            Capability(8, "prescreen", "AI 初筛", "场景构建内部使用温度代理对候选参数进行初筛。", "候选参数池", "初筛候选", Navigation[8].Children[0]);
            Capability(8, "recheck", "正向复核", "场景构建内部调用实际正向求解，复核候选温度曲线。", "初筛候选", "正向复核结果", Navigation[8].Children[0]);
            Capability(8, "acceptance", "相似度验收", "正式逆搜索接口目前仅支持温度相似度要求，不应解释为任意红外场景多指标优化。", "正向复核温度与阈值", "温度相似度验收结论", Navigation[8].Children[0]);
            Capability(8, "build-results", "场景构建结果", "读取场景构建输出中的候选与验收信息。", "实际构建输出", "候选场景及统计", Navigation[8].Children[0]);
            Capability(10, "queue", "运行队列与进度（展示）", "当前软件可执行单个任务并显示真实日志与进度；多任务队列调度仅作展示。", "计划任务列表", "排队方案草稿", null,
                "任务 1", "正向仿真", "任务 2", "智能预测", "任务 3", "相似度评估", "执行策略", "逐项执行（展示，不自动启动）");
        }

        private void Capability(int groupIndex, string id, string title, string scope, string inputs, string outputs,
            NavigationItemViewModel actual, params string[] parameters)
        {
            title = title.Replace("（展示）", "");
            var node = new NavigationItemViewModel(title, scope, "");
            Navigation[groupIndex].Children.Add(node);
            Action open = actual == null ? (Action)null : () => SelectedPage = actual;
            pages.Add(node, new CapabilityPageViewModel(id, title, scope, inputs, outputs, open, parameters));
            if (actual != null) capabilityTargets.Add(node, actual);
        }
    }
}
