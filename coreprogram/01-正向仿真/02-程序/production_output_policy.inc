! Internal output policy: deliberately separate from clean business input and
! from the frozen numerical-reliability configuration.
! 声明逻辑开关，用于控制对应计算或输出路径。
logical, parameter :: WRITE_BUSINESS_CORE = .true.
logical, parameter :: WRITE_FRAME_SUMMARY = .true.
! 声明逻辑开关，用于控制对应计算或输出路径。
logical, parameter :: WRITE_DERIVED_OUTPUTS = .false.
logical, parameter :: WRITE_QA_DIAGNOSTICS = .false.
! 声明逻辑开关，用于控制对应计算或输出路径。
logical, parameter :: WRITE_DEBUG_OUTPUTS = .false.
