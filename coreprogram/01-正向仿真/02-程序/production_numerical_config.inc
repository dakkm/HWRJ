! Generated from ../04_internal_numerical_config.json.
! This protected production configuration is not part of the case schema.
real(8), parameter :: CFG_TIME_STEP = 1.0d0
real(8), parameter :: CFG_ORBIT_INTEGRATOR_DT = 1.0d0
integer(8), parameter :: CFG_RAYS_PER_SPHERE = 200000_8
integer(8), parameter :: CFG_RAYS_SOLAR = 200000_8
integer, parameter :: CFG_MAX_BOUNCES = 10
real(8), parameter :: CFG_MC_MIX_BETA = 0.1d0
integer, parameter :: CFG_RANDOM_SEED = 537
integer, parameter :: CFG_OUTPUT_FRAME_INTERVAL = 10
integer, parameter :: CFG_GRID_NX = 256
integer, parameter :: CFG_GRID_NY = 256
integer, parameter :: CFG_SPOT_RADIUS_CELLS = 0
character(len=6), parameter :: CFG_OUTPUT_DIR = 'output'
logical, parameter :: CFG_SAVE_POINT_HISTORY = .true.
logical, parameter :: CFG_SAVE_FULL_SPOT_IMAGE = .false.
logical, parameter :: CFG_SAVE_QA_FULL_IMAGE = .true.
integer, parameter :: CFG_QA_IMAGE_FRAME_STRIDE = 100
integer, parameter :: CFG_QA_IMAGE_MAX_FRAMES = 5
real(8), parameter :: CFG_EARTH_MU = 3.986004418d14
character(len=6), parameter :: CFG_COMPANION_TYPE = 'SPHERE'
character(len=4), parameter :: CFG_ATTITUDE_MOTION_TYPE = 'NONE'
