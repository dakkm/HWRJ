! Internal output policy: deliberately separate from clean business input and
! from the frozen numerical-reliability configuration.
logical, parameter :: WRITE_BUSINESS_CORE = .true.
logical, parameter :: WRITE_FRAME_SUMMARY = .true.
logical, parameter :: WRITE_DERIVED_OUTPUTS = .false.
logical, parameter :: WRITE_QA_DIAGNOSTICS = .false.
logical, parameter :: WRITE_DEBUG_OUTPUTS = .false.
