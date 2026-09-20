using System;
using System.Collections.ObjectModel;

namespace PreProcess.Wpf.Models
{
    // GUI aggregate, not a serialization schema. No backend mapping or file format.
    public sealed class TaskModel
    {
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public TaskSettings Settings { get; } = new TaskSettings();
        public TargetSettings Targets { get; } = new TargetSettings();
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public SceneMotionSettings Scene { get; } = new SceneMotionSettings();
        public EnvironmentObservationSettings Environment { get; } = new EnvironmentObservationSettings();
        private readonly ObservableCollection<TargetInstance> targets = new ObservableCollection<TargetInstance>();
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public ReadOnlyObservableCollection<TargetInstance> IndividualTargets { get; }

        public TaskModel()
        {
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            IndividualTargets = new ReadOnlyObservableCollection<TargetInstance>(targets);
            SynchronizeTargets();
            // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
            Settings.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(TaskSettings.TargetCount)) SynchronizeTargets(); };
        }
        private void SynchronizeTargets()
        {
            while (targets.Count > Settings.TargetCount) targets.RemoveAt(targets.Count - 1);
            // 遍历当前数据集合，逐项完成必要的转换或状态更新。
            while (targets.Count < Settings.TargetCount)
                targets.Add(new TargetInstance(targets.Count + 1, Targets.Uniform));
        }
    }

    // 定义 GuiTaskMetadata 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class GuiTaskMetadata : BindableModel
    {
        private string name = "未命名任务", description = "";
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public string Name { get => name; set => Set(ref name, value); }
        public string Description { get => description; set => Set(ref description, value); }
    }
    // 定义 TaskSettings 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class TaskSettings : BindableModel
    {
        public GuiTaskMetadata Metadata { get; } = new GuiTaskMetadata();
        private double duration = 1000;
        // 保存该组件运行所需的配置或中间状态。
        private int targetCount = 16;
        private double similarityIndex = 90;
        // Percentage points, not a normalized 0..1 value. GUI design default only.
        // 保存该组件运行所需的配置或中间状态。
        public double SimilarityIndex
        {
            get => similarityIndex;
            // 继续处理当前业务步骤，保持上下文状态一致。
            set
            {
                Finite(value);
                if (value < 50 || value > 100) throw new ArgumentException("相似指标必须在 50%～100% 之间。");
                // 调用对应组件完成当前步骤，并保留产生的处理结果。
                Set(ref similarityIndex, value);
            }
        }
        public double Duration { get => duration; set { Positive(value); Set(ref duration, value); } }
        // 保存该组件运行所需的配置或中间状态。
        public int TargetCount
        {
            get => targetCount;
            // 继续处理当前业务步骤，保持上下文状态一致。
            set
            {
                // Editor memory guard, not a backend contract limit.
                if (value < 1 || value > 10000) throw new ArgumentException("编辑器支持 1～10000 个目标。");
                Set(ref targetCount, value);
            }
        }
    }
    // 定义 TargetSettings 类型，集中封装与该领域对象相关的状态和行为。
    public sealed class TargetSettings
    {
        public TargetPhysics Uniform { get; } = new TargetPhysics();
        // 保存该组件运行所需的配置或中间状态。
        public TargetGeometrySettings Geometry => Uniform.Geometry;
    }
    public sealed class TargetInstance : BindableModel
    {
        // 保存该组件运行所需的配置或中间状态。
        private static readonly double[,] DefaultVelocities =
        {
            { 0.0, 0.0, 0.0 },
            { 4.9956754, 0.0, 0.20791169 },
            // 继续处理当前业务步骤，保持上下文状态一致。
            { 4.5360553, 2.0195819, 0.58778525 },
            { 3.2950861, 3.6595639, 0.8660254 },
            // 继续处理当前业务步骤，保持上下文状态一致。
            { 1.5142125, 4.6602668, 0.9945219 },
            { -0.51310053, 4.8818254, 0.95105652 },
            // 继续处理当前业务步骤，保持上下文状态一致。
            { -2.4722326, 4.2820324, 0.74314483 },
            { -4.0316788, 2.9291861, 0.40673664 },
            // 继续处理当前业务步骤，保持上下文状态一致。
            { -4.890738, 1.0395585, 5.6655389E-16 },
            { -4.8745292, -1.0361132, -0.40673664 },
            { -4.0001563, -2.9062837, -0.74314483 },
            // 继续处理当前业务步骤，保持上下文状态一致。
            { -2.4543579, -4.2510726, -0.95105652 },
            { -0.51219935, -4.8732513, -0.9945219 },
            // 继续处理当前业务步骤，保持上下文状态一致。
            { 1.5217322, -4.6834102, -0.8660254 },
            { 3.3224547, -3.6899598, -0.58778525 },
            // 继续处理当前业务步骤，保持上下文状态一致。
            { 4.5637766, -2.0319242, -0.20791169 }
        };
        private static readonly double[] DefaultReleaseTimes =
            { 0.0, 20.0, 25.0, 30.0, 35.0, 40.0, 45.0, 50.0, 55.0, 60.0, 65.0, 70.0, 75.0, 80.0, 85.0, 90.0 };
        // 通过属性封装状态访问，并在变更时执行必要的同步操作。
        public int Id { get; }
        public string DisplayName => "目标 " + Id;
        // 保存该组件运行所需的配置或中间状态。
        private readonly TargetPhysics uniform;
        private TargetPhysics independent;
        // 保存该组件运行所需的配置或中间状态。
        private bool useOverride;
        public bool UseOverride
        {
            get => useOverride;
            // 继续处理当前业务步骤，保持上下文状态一致。
            set
            {
                if (useOverride == value) return;
                // 校验当前条件，仅在满足业务约束时进入该处理分支。
                if (value && independent == null) independent = uniform.Copy();
                Set(ref useOverride, value);
                // 通知界面绑定层刷新相关状态，确保显示内容与模型一致。
                Notify(nameof(EffectivePhysics));
            }
        }
        public TargetPhysics EffectivePhysics => UseOverride ? independent : uniform;
        public TargetMotionSettings Motion { get; }
        // 执行该成员负责的业务步骤，并向调用方提供一致的处理结果。
        public TargetInstance(int id, TargetPhysics uniform)
        {
            Id = id;
            // 更新当前流程使用的数据，为下一处理步骤做好准备。
            this.uniform = uniform;
            Motion = new TargetMotionSettings(id == 1);
            // 校验当前条件，仅在满足业务约束时进入该处理分支。
            if (id >= 1 && id <= DefaultReleaseTimes.Length)
            {
                Motion.Velocity.X = DefaultVelocities[id - 1, 0];
                // 更新当前流程使用的数据，为下一处理步骤做好准备。
                Motion.Velocity.Y = DefaultVelocities[id - 1, 1];
                Motion.Velocity.Z = DefaultVelocities[id - 1, 2];
                Motion.ReleaseTime = DefaultReleaseTimes[id - 1];
            }
            // 当前置条件不成立时执行备用路径，保持处理结果完整。
            else Motion.Position.X = id - 1;
        }
    }
}
