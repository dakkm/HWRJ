using System;
using System.Collections.ObjectModel;

namespace PreProcess.Wpf.Models
{
    // GUI aggregate, not a serialization schema. No backend mapping or file format.
    public sealed class TaskModel
    {
        public TaskSettings Settings { get; } = new TaskSettings();
        public TargetSettings Targets { get; } = new TargetSettings();
        public SceneMotionSettings Scene { get; } = new SceneMotionSettings();
        public EnvironmentObservationSettings Environment { get; } = new EnvironmentObservationSettings();
        public CalculationSettings Calculation { get; } = new CalculationSettings();
        private readonly ObservableCollection<TargetInstance> targets = new ObservableCollection<TargetInstance>();
        public ReadOnlyObservableCollection<TargetInstance> IndividualTargets { get; }

        public TaskModel()
        {
            IndividualTargets = new ReadOnlyObservableCollection<TargetInstance>(targets);
            SynchronizeTargets();
            Settings.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(TaskSettings.TargetCount)) SynchronizeTargets(); };
        }
        private void SynchronizeTargets()
        {
            while (targets.Count > Settings.TargetCount) targets.RemoveAt(targets.Count - 1);
            while (targets.Count < Settings.TargetCount)
                targets.Add(new TargetInstance(targets.Count + 1, Targets.Uniform));
        }
    }

    public sealed class GuiTaskMetadata : BindableModel
    {
        private string name = "未命名任务", description = "";
        public string Name { get => name; set => Set(ref name, value); }
        public string Description { get => description; set => Set(ref description, value); }
    }
    public sealed class TaskSettings : BindableModel
    {
        public GuiTaskMetadata Metadata { get; } = new GuiTaskMetadata();
        private double duration = 1000;
        private int targetCount = 16;
        public double Duration { get => duration; set { Positive(value); Set(ref duration, value); } }
        public int TargetCount
        {
            get => targetCount;
            set
            {
                // Editor memory guard, not a backend contract limit.
                if (value < 1 || value > 10000) throw new ArgumentException("编辑器支持 1～10000 个目标。");
                Set(ref targetCount, value);
            }
        }
    }
    public sealed class TargetSettings
    {
        public TargetPhysics Uniform { get; } = new TargetPhysics();
    }
    public sealed class TargetInstance : BindableModel
    {
        public int Id { get; }
        public string DisplayName => "目标 " + Id;
        private readonly TargetPhysics uniform;
        private TargetPhysics independent;
        private bool useOverride;
        public bool UseOverride
        {
            get => useOverride;
            set
            {
                if (useOverride == value) return;
                if (value && independent == null) independent = uniform.Copy();
                Set(ref useOverride, value);
                Notify(nameof(EffectivePhysics));
            }
        }
        public TargetPhysics EffectivePhysics => UseOverride ? independent : uniform;
        public TargetMotionSettings Motion { get; }
        public TargetInstance(int id, TargetPhysics uniform)
        {
            Id = id;
            this.uniform = uniform;
            Motion = new TargetMotionSettings(id == 1);
            Motion.Position.X = id - 1;
        }
    }
}
