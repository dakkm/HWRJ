

program sphere_ir_radiation_production
    ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
    use, intrinsic :: ieee_arithmetic, only: ieee_is_finite
    implicit none

    !--------------------------------------------------------------------------
    ! Physical Constants
    !--------------------------------------------------------------------------
    real(8), parameter :: PI = 3.14159265358979323846d0
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), parameter :: STEFAN_BOLTZMANN = 5.670374419d-8
    real(8), parameter :: EPS = 1.0d-10
    real(8), parameter :: DEG_TO_RAD = PI / 180.0d0
    ! Frozen target material contract: every input radius is the outer radius
    ! of a shell with this fixed wall thickness.  This is intentionally not a
    ! runtime input or mode selector.
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), parameter :: TARGET_SHELL_THICKNESS = 0.005d0
    include 'production_numerical_config.inc'
    include 'production_output_policy.inc'

    !--------------------------------------------------------------------------
    ! Mixed importance sampling parameter (runtime, from input file)
    ! mc_mix_beta: fraction of full-sphere sampling for initial directions
    !--------------------------------------------------------------------------
     real(8) :: mc_mix_beta

    !--------------------------------------------------------------------------
    ! Verification / reproducibility controls
    !--------------------------------------------------------------------------
    ! 声明计数器、索引或离散控制参数。
    integer :: random_seed_user
    integer :: random_seed_used

    !--------------------------------------------------------------------------
    ! Input/Output classification controls required by interface document
    !--------------------------------------------------------------------------
    character(len=32) :: companion_type_default
    ! 声明文本字段，用于保存路径、状态或接口数据。
    character(len=32) :: attitude_motion_type
    real(8) :: micro_motion_params(3)
    character(len=32) :: similarity_level
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: reference_temperature
    real(8) :: reference_intensity
    character(len=256) :: output_dir

    !--------------------------------------------------------------------------
    ! Simulation Control (from input file)
    !--------------------------------------------------------------------------
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: time_step
    real(8) :: total_time
    integer :: motion_frame_count
    ! 声明计数器、索引或离散控制参数。
    integer :: current_frame_index
    real(8) :: current_motion_time
    real(8) :: earth_mu
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: orbit_integrator_dt
    integer, parameter :: MEMBER_STAGE_INACTIVE = 0
    integer, parameter :: MEMBER_STAGE_FORMATION = 1
    integer, parameter :: MEMBER_STAGE_RELEASED = 2
    ! Rule-B thermal coupling uses sphere 1 as the carrier/reference object.
    ! Only this model-level invariant remains in the solver; all other release
    ! schedule, count, geometry, velocity, acceleration, and active choices are
    ! scenario parameters supplied by the input file.
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), parameter :: PRIMARY_RELEASE_TOL = 1.0d-9
    ! Constant-cp internal-energy datum used by the open active-domain ledger.
    ! Only energy differences and release-carried energy relative to this common
    ! datum are interpreted; 0 K is explicit and independent of similarity inputs.
    real(8), parameter :: INTERNAL_ENERGY_REFERENCE_K = 0.0d0

    !--------------------------------------------------------------------------
    ! Monte Carlo Parameters (from input file)
    !--------------------------------------------------------------------------
    integer(8) :: rays_per_sphere
    ! 声明计数器、索引或离散控制参数。
    integer(8) :: rays_solar                    ! 太阳光线数量
    integer :: max_bounces
    integer(8), parameter :: REPORT_INTERVAL = 1000000_8

    !--------------------------------------------------------------------------
    ! Environment (from input file)
    !--------------------------------------------------------------------------
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: solar_flux
    real(8) :: solar_direction(3)
    real(8) :: environment_temp

    !--------------------------------------------------------------------------
    ! Group / Carrier Motion Parameters
    !--------------------------------------------------------------------------
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: group_center(3)
    real(8) :: group_center_initial(3)
    real(8) :: group_velocity(3)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: group_velocity_initial(3)
    real(8) :: group_acceleration(3)
    real(8) :: group_acceleration_input(3)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: group_normal(3)
    real(8) :: group_normal_initial(3)
    real(8) :: group_up_hint(3)
    real(8) :: group_up_initial(3)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: group_u(3)
    real(8) :: group_v(3)
    real(8) :: group_angular_velocity(3)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: group_angular_acceleration(3)

    !--------------------------------------------------------------------------
    ! Aperture & Image Plane Parameters
    !--------------------------------------------------------------------------
    real(8) :: aperture_size
    real(8) :: aperture_center(3)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: aperture_normal(3)
    real(8) :: aperture_u(3)
    real(8) :: aperture_v(3)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: aperture_center_initial(3)
    real(8) :: aperture_velocity(3)
    real(8) :: aperture_velocity_initial(3)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: aperture_acceleration(3)
    real(8) :: aperture_acceleration_input(3)
    real(8) :: aperture_normal_initial(3)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: aperture_up_hint(3)
    real(8) :: aperture_up_initial(3)
    real(8) :: aperture_angular_velocity(3)
    real(8) :: aperture_angular_acceleration(3)
    ! 声明逻辑开关，用于控制对应计算或输出路径。
    logical :: aperture_track_target
    integer :: nx, ny
    real(8) :: dx, dy, cell_area

!    real(8) :: image_distance
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: image_center(3)
    real(8) :: detector_center(3)
    real(8) :: detector_normal(3)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: detector_u(3)
    real(8) :: detector_v(3)
    real(8) :: detector_center_initial(3)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: detector_velocity(3)
    real(8) :: detector_velocity_initial(3)
    real(8) :: detector_acceleration(3)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: detector_acceleration_input(3)
    real(8) :: detector_normal_initial(3)
    real(8) :: detector_up_hint(3)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: detector_up_initial(3)
    real(8) :: detector_angular_velocity(3)
    real(8) :: detector_angular_acceleration(3)
    logical :: detector_track_target
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: detector_offset_local(3)
    real(8) :: target_detector_relative_position(3)
    real(8) :: target_detector_relative_velocity(3)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: target_detector_range
    real(8) :: target_detector_los_azimuth
    real(8) :: target_detector_los_elevation
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: target_detector_los_rate(3)
    real(8) :: target_detector_los_rate_mag
    integer :: output_frame_interval
    ! 声明逻辑开关，用于控制对应计算或输出路径。
    logical :: save_point_history
    logical :: save_full_spot_image
    logical :: save_qa_full_image
    ! 声明计数器、索引或离散控制参数。
    integer :: qa_image_frame_stride
    integer :: qa_image_max_frames
    integer :: qa_image_written_count

    !--------------------------------------------------------------------------
    ! Sphere Data
    !--------------------------------------------------------------------------
    ! 声明计数器、索引或离散控制参数。
    integer :: num_spheres
    real(8), allocatable :: sphere_centers(:,:)      ! (3, num_spheres)
    real(8), allocatable :: sphere_centers_initial(:,:)   ! relative offset to group center at t = 0
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: sphere_velocity(:,:)          ! relative separation velocity after release
    real(8), allocatable :: sphere_acceleration(:,:)      ! relative separation acceleration after release
    real(8), allocatable :: sphere_world_velocity(:,:)
    real(8), allocatable :: sphere_release_time(:)
    ! 声明逻辑开关，用于控制对应计算或输出路径。
    logical, allocatable :: sphere_is_active(:)
    character(len=32), allocatable :: companion_type(:)
    character(len=32), allocatable :: sphere_attitude_motion_type(:)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: sphere_micro_motion(:,:)
    integer, allocatable :: sphere_motion_stage(:)
    real(8), allocatable :: sphere_orientation(:,:)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: sphere_angular_velocity(:,:)
    real(8), allocatable :: sphere_angular_acceleration(:,:)
    real(8), allocatable :: sphere_radius(:)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: sphere_density(:)
    real(8), allocatable :: sphere_specific_heat(:)
    real(8), allocatable :: sphere_ir_emissivity(:)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: sphere_solar_absorptivity(:)
    real(8), allocatable :: sphere_ir_reflectivity(:)
    real(8), allocatable :: sphere_solar_reflectivity(:)  ! 太阳反射率
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: sphere_internal_heat(:)
    real(8), allocatable :: sphere_initial_temp(:)

    ! Derived sphere properties
    real(8), allocatable :: sphere_area(:)
    real(8), allocatable :: sphere_volume(:)       ! shell material volume
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: sphere_mass(:)
    real(8), allocatable :: sphere_thermal_capacity(:)
    real(8), allocatable :: sphere_distances(:)

    ! Sphere thermal state
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: sphere_temperature(:)
    real(8), allocatable :: sphere_temperature_old(:)
    real(8), allocatable :: sphere_power(:)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: sphere_solar_heat(:)
    real(8), allocatable :: solar_heat_direct(:)         ! 直接吸收的太阳热
    real(8), allocatable :: solar_heat_indirect(:)       ! 反射后吸收的太阳热
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: sphere_radiation_to_space(:)
    real(8), allocatable :: sphere_net_exchange(:)
    real(8), allocatable :: sphere_radiation_from_environment(:)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: sphere_radiation_to_aperture(:)
    real(8), allocatable :: sphere_heat_exchange(:,:)          ! i->j power
    real(8), allocatable :: sphere_view_factor(:,:)            ! MC transfer τ_i->j
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: sphere_view_factor_to_space(:)     ! τ_i->space

    !--------------------------------------------------------------------------
    ! Target-directed solar Monte Carlo ledger
    !--------------------------------------------------------------------------
    real(8) :: solar_initial_power
    real(8) :: solar_escape_power
    real(8) :: solar_truncation_residual
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: solar_closure_error
    integer(8) :: solar_valid_first_hits
    integer(8) :: solar_empty_rays
    ! 声明计数器、索引或离散控制参数。
    integer(8) :: solar_reflection_events
    integer(8) :: solar_paths_with_reflection
    integer(8) :: solar_paths_with_two_plus_reflections
    ! 声明计数器、索引或离散控制参数。
    integer(8) :: solar_paths_returned_to_first
    integer(8) :: solar_reflected_escape_rays
    integer :: solar_max_reflections_observed

    !--------------------------------------------------------------------------
    ! Grid Data (IMAGE PLANE = pupil plane)
    !--------------------------------------------------------------------------
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: node_u(:), node_v(:)
    real(8), allocatable :: cell_u(:), cell_v(:)
    real(8), allocatable :: cell_global_x(:,:), cell_global_y(:,:), cell_global_z(:,:)

    !--------------------------------------------------------------------------
    ! Energy Distribution on image plane
    !--------------------------------------------------------------------------
    ! Dimensionless MC aperture transfer factors are kept separate from the
    ! power-valued image-plane fields.  This prevents repeated multiplication
    ! by sphere power when the same geometry is reused by a later thermal slice.
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: grid_direct_factor(:,:,:)
    real(8), allocatable :: grid_indirect_factor(:,:,:)
    real(8), allocatable :: grid_direct(:,:,:)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: grid_direct_total(:,:)
    real(8), allocatable :: grid_indirect(:,:,:)
    real(8), allocatable :: grid_indirect_total(:,:)
    real(8), allocatable :: grid_total(:,:)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: grid_irradiance(:,:)

    !--------------------------------------------------------------------------
    ! Spot-image (post-processing) parameters
    !--------------------------------------------------------------------------
    real(8) :: spot_plane_size
    real(8) :: spot_focal_length
    ! 声明计数器、索引或离散控制参数。
    integer :: spot_radius_cells

    real(8), allocatable :: spot_total(:,:)
    real(8), allocatable :: spot_irradiance(:,:)

    !--------------------------------------------------------------------------
    ! Statistics
    !--------------------------------------------------------------------------
    ! 声明计数器、索引或离散控制参数。
    integer(8), allocatable :: rays_emitted(:)
    integer(8), allocatable :: rays_direct_hit(:)
    integer(8), allocatable :: rays_indirect_hit(:)

    ! 声明计数器、索引或离散控制参数。
    integer(8), allocatable :: rays_escaped_source(:)
    integer(8), allocatable :: rays_terminated_on_sphere(:)
    integer(8), allocatable :: rays_self_absorbed(:)

    ! 声明计数器、索引或离散控制参数。
    integer(8) :: rays_escaped
    integer(8) :: rays_hit_pupil
    integer(8) :: rays_on_sensor

    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: power_direct_factor(:)
    real(8), allocatable :: power_indirect_factor(:)
    real(8), allocatable :: power_direct(:)
    real(8), allocatable :: power_indirect(:)
    
    !--------------------------------------------------------------------------
    ! Weighted estimators for beta-unbiasedness verification
    !--------------------------------------------------------------------------
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: wt_emitted(:)
    real(8), allocatable :: wt_pupil_cross(:)
    real(8), allocatable :: wt_pupil_grid(:)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: wt_pupil_direct(:)
    real(8), allocatable :: wt_pupil_indirect(:)
    real(8), allocatable :: wt_sphere_term(:)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: wt_self_absorb(:)
    real(8), allocatable :: wt_space_escape(:)    

    !--------------------------------------------------------------------------
    ! MC transfer factor accumulators (before normalization)
    !--------------------------------------------------------------------------
    real(8), allocatable :: vf_energy_s2s(:,:)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: vf_energy_s2space(:)

    !--------------------------------------------------------------------------
    ! Importance-sampling cones (per sphere)
    !--------------------------------------------------------------------------
    real(8), allocatable :: cone_axis(:,:)
    real(8), allocatable :: cone_cos_theta_max(:)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8), allocatable :: cone_prob(:)

    !--------------------------------------------------------------------------
    ! Timing and Misc
    !--------------------------------------------------------------------------
    real(8) :: start_time, end_time, temp_solve_time
    real(8) :: last_frame_compute_time, cumulative_compute_time
    ! 声明文本字段，用于保存路径、状态或接口数据。
    character(len=20) :: date_str, time_str
    character(len=256) :: sphere_file
    character(len=256) :: input_file
    character(len=512) :: executable_path
    ! 声明文本字段，用于保存路径、状态或接口数据。
    character(len=512) :: executable_dir
    character(len=512) :: input_file_dir
    character(len=512) :: resolved_output_dir
    ! 声明计数器、索引或离散控制参数。
    integer :: ierr
    logical :: temperature_history_initialized
    character(len=40) :: solver_status
    ! 声明文本字段，用于保存路径、状态或接口数据。
    character(len=256) :: solver_message
    logical :: result_valid
    real(8) :: cumulative_energy_residual
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: cumulative_release_carried_energy
    real(8) :: cumulative_system_energy_residual
    real(8) :: slice_solar_input_energy
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: slice_internal_input_energy
    real(8) :: slice_environment_input_energy
    real(8) :: slice_space_loss_energy
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: slice_aperture_loss_energy
    real(8) :: slice_sphere_exchange_energy
    integer(8) :: two_body_propagator_call_count
    integer(8) :: orbital_step_count
    ! 声明计数器、索引或离散控制参数。
    integer(8) :: transient_update_count
    integer(8) :: thermal_step_count
    !--------------------------------------------------------------------------
    ! Program Start
    !--------------------------------------------------------------------------
    call cpu_time(start_time)
    ! 调用子过程完成当前数值计算或状态更新。
    call get_datetime(date_str, time_str)
    last_frame_compute_time = 0.0d0
    cumulative_compute_time = 0.0d0
    ! 维护输入输出路径及文件数据，确保结果写入约定位置。
    resolved_output_dir = ''
    solver_status = 'INTERNAL_ERROR'
    solver_message = 'production initialization incomplete'
    ! 更新仿真变量或中间量，供下一数值步骤继续计算。
    result_valid = .false.

    call get_command_argument(0, executable_path)
    executable_dir = directory_name(executable_path)
    ! 调用子过程完成当前数值计算或状态更新。
    call get_command_argument(1, input_file)
    call resolve_input_file_path(input_file)

    write(*,*)
    ! 按照接口约定读写数据文件，并维护文件单元状态。
    write(*,'(A)') ' ============================================================'
    write(*,'(A)') ' clean_v2 PRODUCTION: Two-Body + Transient + Radiation'
    write(*,'(A)') ' With target-directed solar Monte Carlo'



    write(*,'(A)') ' ============================================================'
    ! 按照接口约定读写数据文件，并维护文件单元状态。
    write(*,*)
    write(*,'(A,A)') ' Reading configuration from: ', trim(input_file)

    input_file_dir = directory_name(input_file)
    ! 调用子过程完成当前数值计算或状态更新。
    call read_clean_input_file(input_file, ierr)
    if (ierr /= 0) call production_fail(solver_status, solver_message, 2)
    call validate_solar_contract(ierr)
    ! 检查数值状态和业务条件，仅在满足约束时进入分支。
    if (ierr /= 0) call production_fail('INVALID_INPUT', 'solar input contract failed', 2)
    call resolve_output_directory()
    call ensure_output_directory()
    ! 按照接口约定读写数据文件，并维护文件单元状态。
    write(*,'(A,A)') ' Output directory resolved to: ', trim(resolved_output_dir)
    write(*,'(A)') ' Solar path: TARGETED_MONTE_CARLO (solar_flux=0 disables tracing)'

    call init_random_seed(random_seed_user, random_seed_used)
    ! 按照接口约定读写数据文件，并维护文件单元状态。
    write(*,'(A,I12)') ' Random seed used: ', random_seed_used

    ! Single-file input mode: sphere/object records are read from the same input file.
    call read_clean_sphere_tables(input_file, ierr)
    if (ierr /= 0) call production_fail(solver_status, solver_message, 2)
    ! 调用子过程完成当前数值计算或状态更新。
    call validate_ir_property_contract(ierr)
    if (ierr /= 0) call production_fail('INVALID_INPUT', 'IR property contract failed', 2)
    call validate_production_state(ierr)
    if (ierr /= 0) call production_fail(solver_status, solver_message, 2)

    ! 调用子过程完成当前数值计算或状态更新。
    call allocate_arrays()
    call initialize_sphere_properties()
    call initialize_motion_state()
    ! 调用子过程完成当前数值计算或状态更新。
    call reset_history_files()

    call run_motion_simulation()
    call write_solver_status_file('SUCCESS', .true., 'production run completed')
    ! 调用子过程完成当前数值计算或状态更新。
    call deallocate_arrays()

contains

!=========================== Utility routines =================================
    subroutine init_random_seed(seed_user, seed_used)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in)  :: seed_user
        integer, intent(out) :: seed_used

        ! 声明计数器、索引或离散控制参数。
        integer :: n, clock, i
        integer :: base_seed
        integer, allocatable :: seed(:)

        ! 调用子过程完成当前数值计算或状态更新。
        call random_seed(size=n)
        allocate(seed(n))

        if (seed_user > 0) then
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            base_seed = seed_user
        else
            call system_clock(count=clock)
            if (clock <= 0) clock = 13579
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            base_seed = clock
        end if

        seed_used = base_seed

        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do i = 1, n
            seed(i) = modulo(base_seed + 37*(i-1), 2147483646)
            if (seed(i) <= 0) seed(i) = i
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do

        call random_seed(put=seed)
        deallocate(seed)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine init_random_seed

    subroutine get_datetime(d_str, t_str)
        character(len=*), intent(out) :: d_str, t_str
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=8) :: d_val
        character(len=10) :: t_val
        call date_and_time(date=d_val, time=t_val)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(d_str,'(A4,A1,A2,A1,A2)') d_val(1:4),'-',d_val(5:6),'-',d_val(7:8)
        write(t_str,'(A2,A1,A2,A1,A2)') t_val(1:2),':',t_val(3:4),':',t_val(5:6)
    end subroutine

    function output_path(filename) result(fullpath)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        character(len=*), intent(in) :: filename
        character(len=512) :: fullpath
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=512) :: dir_trimmed

        dir_trimmed = adjustl(trim(resolved_output_dir))
        if (len_trim(dir_trimmed) <= 0 .or. trim(dir_trimmed) == '.') then
            ! 维护输入输出路径及文件数据，确保结果写入约定位置。
            fullpath = trim(filename)
        else if (dir_trimmed(len_trim(dir_trimmed):len_trim(dir_trimmed)) == '/') then
            fullpath = trim(dir_trimmed) // trim(filename)
        ! 当前条件不成立时执行替代计算路径。
        else
            fullpath = trim(dir_trimmed) // '/' // trim(filename)
        end if
    ! 结束当前计算单元，使过程边界保持清晰。
    end function output_path

    subroutine resolve_input_file_path(path_text)
        implicit none
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=*), intent(inout) :: path_text
        character(len=512) :: candidate_path

        if (len_trim(path_text) <= 0) then
            call production_fail('INVALID_INPUT', &
                'explicit production input file argument is required; default input fallback is disabled', 2)
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if

        if (is_absolute_path(path_text)) then
            path_text = normalize_path(path_text)
        ! 当前条件不成立时执行替代计算路径。
        else
            candidate_path = normalize_path(path_text)
            if (file_exists(candidate_path)) then
                ! 维护输入输出路径及文件数据，确保结果写入约定位置。
                path_text = candidate_path
            else if (len_trim(executable_dir) > 0 .and. trim(executable_dir) /= '.') then
                candidate_path = join_paths(executable_dir, path_text)
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if (file_exists(candidate_path)) then
                    path_text = candidate_path
                else
                    ! 维护输入输出路径及文件数据，确保结果写入约定位置。
                    path_text = normalize_path(path_text)
                end if
            else
                ! 维护输入输出路径及文件数据，确保结果写入约定位置。
                path_text = normalize_path(path_text)
            end if
        end if

        if (.not. file_exists(path_text)) then
            ! 调用子过程完成当前数值计算或状态更新。
            call production_fail('INVALID_INPUT', 'explicit input file not found: '//trim(path_text), 2)
        end if
    end subroutine resolve_input_file_path

    ! 定义 resolve_output_directory 计算单元，封装该步骤的数据处理规则。
    subroutine resolve_output_directory()
        implicit none

        if (len_trim(output_dir) <= 0 .or. trim(output_dir) == '.') then
            ! 维护输入输出路径及文件数据，确保结果写入约定位置。
            resolved_output_dir = '.'
        else if (is_absolute_path(output_dir)) then
            resolved_output_dir = normalize_path(output_dir)
        ! 当前条件不成立时执行替代计算路径。
        else if (len_trim(input_file_dir) > 0 .and. trim(input_file_dir) /= '.') then
            resolved_output_dir = join_paths(input_file_dir, output_dir)
        else if (len_trim(executable_dir) > 0 .and. trim(executable_dir) /= '.') then
            ! 维护输入输出路径及文件数据，确保结果写入约定位置。
            resolved_output_dir = join_paths(executable_dir, output_dir)
        else
            resolved_output_dir = normalize_path(output_dir)
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
    end subroutine resolve_output_directory

    subroutine ensure_output_directory()
        implicit none
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=512) :: command_line
        character(len=512) :: dir_trimmed
        character(len=64) :: os_name
        ! 声明计数器、索引或离散控制参数。
        integer :: os_len, os_status
        integer :: cmdstat, exitstat
        character(len=512) :: cmdmsg

        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        dir_trimmed = adjustl(trim(resolved_output_dir))
        if (len_trim(dir_trimmed) <= 0 .or. trim(dir_trimmed) == '.') return

        call get_environment_variable('OS', os_name, length=os_len, status=os_status)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (os_status == 0 .and. os_len > 0 .and. index(os_name(1:os_len), 'Windows') > 0) then
            command_line = 'cmd /c if not exist "' // trim(dir_trimmed) // '" mkdir "' // trim(dir_trimmed) // '"'
        else
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            command_line = 'mkdir -p "' // trim(dir_trimmed) // '"'
        end if

        cmdmsg = ''
        ! 调用子过程完成当前数值计算或状态更新。
        call execute_command_line(trim(command_line), wait=.true., exitstat=exitstat, cmdstat=cmdstat, cmdmsg=cmdmsg)
        if (cmdstat /= 0 .or. exitstat /= 0) then
            write(*,'(A)') ' ERROR: Failed to create output directory.'
            write(*,'(A,A)') '   Requested directory: ', trim(dir_trimmed)
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(*,'(A,A)') '   Command: ', trim(command_line)
            if (len_trim(cmdmsg) > 0) write(*,'(A,A)') '   Message: ', trim(cmdmsg)
            call production_fail('OUTPUT_WRITE_FAILURE','cannot create output directory',5)
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
    end subroutine ensure_output_directory

    logical function file_exists(path_text)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        character(len=*), intent(in) :: path_text

        inquire(file=trim(path_text), exist=file_exists)
    ! 结束当前计算单元，使过程边界保持清晰。
    end function file_exists

    logical function is_absolute_path(path_text)
        implicit none
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=*), intent(in) :: path_text
        character(len=512) :: trimmed_path
        integer :: n

        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        trimmed_path = adjustl(trim(path_text))
        n = len_trim(trimmed_path)
        is_absolute_path = .false.
        if (n <= 0) return

        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (n >= 2) then
            if (trimmed_path(2:2) == ':') then
                is_absolute_path = .true.
                ! 满足当前控制条件后结束或跳过本次处理。
                return
            end if
        end if
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (trimmed_path(1:1) == '/' .or. trimmed_path(1:1) == '\') then
            is_absolute_path = .true.
        end if
    ! 结束当前计算单元，使过程边界保持清晰。
    end function is_absolute_path

    function normalize_path(path_text) result(path_out)
        implicit none
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=*), intent(in) :: path_text
        character(len=512) :: path_out
        integer :: i, n

        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        path_out = adjustl(trim(path_text))
        n = len_trim(path_out)
        do i = 1, n
            if (path_out(i:i) == '\') path_out(i:i) = '/'
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do
    end function normalize_path

    function join_paths(base_path, child_path) result(path_out)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        character(len=*), intent(in) :: base_path, child_path
        character(len=512) :: path_out
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=512) :: base_norm, child_norm
        integer :: base_len

        base_norm = normalize_path(base_path)
        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        child_norm = normalize_path(child_path)
        base_len = len_trim(base_norm)

        if (base_len <= 0 .or. trim(base_norm) == '.') then
            ! 维护输入输出路径及文件数据，确保结果写入约定位置。
            path_out = trim(child_norm)
        else if (base_norm(base_len:base_len) == '/') then
            path_out = trim(base_norm) // trim(child_norm)
        ! 当前条件不成立时执行替代计算路径。
        else
            path_out = trim(base_norm) // '/' // trim(child_norm)
        end if
    end function join_paths

    ! 定义 directory_name 计算单元，封装该步骤的数据处理规则。
    function directory_name(path_text) result(dir_out)
        implicit none
        character(len=*), intent(in) :: path_text
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=512) :: dir_out
        character(len=512) :: path_norm
        integer :: i, last_sep

        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        path_norm = normalize_path(path_text)
        last_sep = 0
        do i = len_trim(path_norm), 1, -1
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (path_norm(i:i) == '/') then
                last_sep = i
                exit
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
        end do

        if (last_sep > 0) then
            ! 维护输入输出路径及文件数据，确保结果写入约定位置。
            dir_out = path_norm(1:last_sep-1)
            if (len_trim(dir_out) <= 0) dir_out = '.'
        else
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            dir_out = '.'
        end if
    end function directory_name

    function base_name(path_text) result(name_out)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        character(len=*), intent(in) :: path_text
        character(len=256) :: name_out
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=512) :: path_norm
        integer :: i, last_sep, n

        path_norm = normalize_path(path_text)
        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        n = len_trim(path_norm)
        do while (n > 1 .and. path_norm(n:n) == '/')
            n = n - 1
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do
        last_sep = 0
        do i = n, 1, -1
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (path_norm(i:i) == '/') then
                last_sep = i
                exit
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
        end do
        if (last_sep > 0) then
            name_out = path_norm(last_sep+1:n)
        ! 当前条件不成立时执行替代计算路径。
        else
            name_out = path_norm(1:n)
        end if
    ! 结束当前计算单元，使过程边界保持清晰。
    end function base_name

    subroutine parse_strict_logical_token(token, logical_value, parse_ok)
        implicit none
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=*), intent(in) :: token
        logical, intent(out) :: logical_value, parse_ok
        character(len=32) :: normalized

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        normalized = trim(to_uppercase(adjustl(token)))
        logical_value = .false.
        parse_ok = .true.

        ! 根据离散状态选择对应算法分支。
        select case (trim(normalized))
        case ('T', 'TRUE', '.TRUE.', 'Y', 'YES', '1')
            logical_value = .true.
        ! 根据离散状态选择对应算法分支。
        case ('F', 'FALSE', '.FALSE.', 'N', 'NO', '0')
            logical_value = .false.
        case default
            parse_ok = .false.
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end select
    end subroutine parse_strict_logical_token

    integer function count_data_tokens(text_value)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        character(len=*), intent(in) :: text_value
        integer :: char_idx
        ! 声明逻辑开关，用于控制对应计算或输出路径。
        logical :: in_token, is_delimiter

        count_data_tokens = 0
        in_token = .false.
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do char_idx = 1, len_trim(text_value)
            is_delimiter = text_value(char_idx:char_idx) == ' ' .or. &
                           text_value(char_idx:char_idx) == achar(9) .or. &
                           text_value(char_idx:char_idx) == ','
            if (is_delimiter) then
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                in_token = .false.
            else if (.not. in_token) then
                count_data_tokens = count_data_tokens + 1
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                in_token = .true.
            end if
        end do
    end function count_data_tokens

    ! 定义 to_uppercase 计算单元，封装该步骤的数据处理规则。
    function to_uppercase(text) result(text_upper)
        implicit none
        character(len=*), intent(in) :: text
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=len(text)) :: text_upper
        integer :: i, code_value

        text_upper = text
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do i = 1, len(text)
            code_value = iachar(text(i:i))
            if (code_value >= iachar('a') .and. code_value <= iachar('z')) then
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                text_upper(i:i) = achar(code_value - 32)
            end if
        end do
    ! 结束当前计算单元，使过程边界保持清晰。
    end function to_uppercase

    integer function count_members_in_stage(stage_value)
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: stage_value

        if (.not. allocated(sphere_motion_stage)) then
            count_members_in_stage = 0
        else
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            count_members_in_stage = count(sphere_motion_stage == stage_value)
        end if
    end function count_members_in_stage

    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) function next_release_time_after(time_value)
        implicit none
        real(8), intent(in) :: time_value
        ! 声明计数器、索引或离散控制参数。
        integer :: idx

        next_release_time_after = huge(1.0d0)
        if (.not. allocated(sphere_release_time)) return
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. allocated(sphere_is_active)) return

        do idx = 1, num_spheres
            if (.not. is_input_enabled(idx)) cycle
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (sphere_release_time(idx) <= time_value + EPS) cycle
            if (sphere_release_time(idx) > total_time + EPS) cycle
            next_release_time_after = min(next_release_time_after, sphere_release_time(idx))
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do
    end function next_release_time_after

    real(8) function next_event_aligned_time(time_value)
        implicit none
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(in) :: time_value
        real(8) :: nominal_end, release_event_time

        nominal_end = min(time_value + time_step, total_time)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        release_event_time = next_release_time_after(time_value)
        next_event_aligned_time = min(nominal_end, release_event_time)

        if (next_event_aligned_time <= time_value + EPS .and. time_value < total_time - EPS) then
            ! 调用子过程完成当前数值计算或状态更新。
            call production_fail('INTERNAL_ERROR', 'event scheduler failed to advance time', 3)
        end if
    end function next_event_aligned_time

    ! 声明计数器、索引或离散控制参数。
    integer function count_event_aligned_steps()
        implicit none
        integer :: guard_count, guard_limit
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: time_now, time_next

        count_event_aligned_steps = 0
        guard_count = 0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        guard_limit = max(100, ceiling(total_time / max(time_step, EPS)) + num_spheres + 10)
        time_now = 0.0d0

        do while (time_now < total_time - EPS)
            time_next = next_event_aligned_time(time_now)
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            count_event_aligned_steps = count_event_aligned_steps + 1
            guard_count = guard_count + 1
            if (guard_count > guard_limit) then
                ! 调用子过程完成当前数值计算或状态更新。
                call production_fail('INTERNAL_ERROR', 'event scheduler exceeded guard limit', 3)
            end if
            time_now = time_next
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do
    end function count_event_aligned_steps

    subroutine reset_history_files()
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer :: uf

        temperature_history_initialized = .false.

        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (WRITE_DERIVED_OUTPUTS) then
            call delete_output_pattern('frame_*_geometry.dat')
            call delete_output_pattern('frame_*_sphere_temperature.dat')
            ! 调用子过程完成当前数值计算或状态更新。
            call delete_output_pattern('frame_*_spot_image.dat')
            call delete_output_pattern('spot_point_history.csv')
        end if

        uf = 41
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (WRITE_DERIVED_OUTPUTS) then
            open(unit=uf, file=output_path('geometry_history.dat'), status='replace')
            close(uf)
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            open(unit=uf, file=output_path('screen_history.dat'), status='replace')
            close(uf)
        end if

        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (WRITE_FRAME_SUMMARY) then
            open(unit=uf, file=output_path('frame_summary.csv'), status='replace')
            close(uf)
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if

        open(unit=uf, file=output_path('temperature_history.csv'), status='replace')
        close(uf)

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=output_path('infrared_response_history.csv'), status='replace')
        close(uf)

        ! Formal per-object motion output.  This is a business output rather than
        ! a derived/QA file, so it is always reset and written independently of
        ! WRITE_DERIVED_OUTPUTS.
        open(unit=uf, file=output_path('trajectory_history.csv'), status='replace')
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)

        qa_image_written_count = 0
    end subroutine reset_history_files

    subroutine delete_output_pattern(pattern)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        character(len=*), intent(in) :: pattern
        character(len=512) :: command_line
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=512) :: target_pattern
        character(len=512) :: target_pattern_windows
        character(len=512) :: dir_trimmed
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=64) :: os_name
        integer :: os_len, os_status, i
        integer :: cmdstat, exitstat
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=512) :: cmdmsg

        dir_trimmed = adjustl(trim(resolved_output_dir))
        if (len_trim(dir_trimmed) <= 0 .or. trim(dir_trimmed) == '.') return

        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        target_pattern = join_paths(dir_trimmed, pattern)

        call get_environment_variable('OS', os_name, length=os_len, status=os_status)
        if (os_status == 0 .and. os_len > 0 .and. index(os_name(1:os_len), 'Windows') > 0) then
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            target_pattern_windows = trim(target_pattern)
            do i = 1, len_trim(target_pattern_windows)
                if (target_pattern_windows(i:i) == '/') target_pattern_windows(i:i) = '\'
            ! 结束本轮迭代范围，继续处理汇总后的计算结果。
            end do
            command_line = 'cmd /c if exist "' // trim(target_pattern_windows) // '" del /q "' // &
                trim(target_pattern_windows) // '" >nul 2>nul'
        else
            command_line = 'rm -f "' // trim(target_pattern) // '"'
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if

        cmdmsg = ''
        call execute_command_line(trim(command_line), wait=.true., exitstat=exitstat, cmdstat=cmdstat, cmdmsg=cmdmsg)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (cmdstat /= 0 .or. exitstat /= 0) then
            write(*,'(A,A)') ' WARNING: Failed to clear old frame files for pattern: ', trim(pattern)
            if (len_trim(cmdmsg) > 0) write(*,'(A,A)') '   Message: ', trim(cmdmsg)
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
    end subroutine delete_output_pattern

    subroutine initialize_motion_state()
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        real(8) :: detector_offset_world(3)
        call normalize_vector(group_normal)
        ! 调用子过程完成当前数值计算或状态更新。
        call normalize_vector(aperture_normal)
        call normalize_vector(solar_direction)
        call normalize_vector(detector_normal)
        ! 调用子过程完成当前数值计算或状态更新。
        call setup_aperture_coordinate_system()
        call setup_detector_coordinate_system()
        detector_offset_world = detector_center - aperture_center
        detector_offset_local(1) = sum(detector_offset_world * detector_u)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        detector_offset_local(2) = sum(detector_offset_world * detector_v)
        detector_offset_local(3) = sum(detector_offset_world * detector_normal)
        group_center_initial = group_center
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        group_velocity_initial = group_velocity
        group_normal_initial = group_normal
        group_up_initial = group_up_hint
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        aperture_center_initial = aperture_center
        aperture_velocity_initial = aperture_velocity
        aperture_normal_initial = aperture_normal
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        aperture_up_initial = aperture_up_hint
        detector_center_initial = detector_center
        detector_velocity_initial = detector_velocity
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        detector_normal_initial = detector_normal
        detector_up_initial = detector_up_hint
        current_frame_index = 0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        current_motion_time = 0.0d0
        motion_frame_count = count_event_aligned_steps()
        cumulative_energy_residual = 0.0d0
        cumulative_release_carried_energy = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        cumulative_system_energy_residual = 0.0d0
        slice_solar_input_energy = 0.0d0
        slice_internal_input_energy = 0.0d0
        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        slice_environment_input_energy = 0.0d0
        slice_space_loss_energy = 0.0d0
        slice_aperture_loss_energy = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        slice_sphere_exchange_energy = 0.0d0
        two_body_propagator_call_count = 0_8
        orbital_step_count = 0_8
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        transient_update_count = 0_8
        thermal_step_count = 0_8
    end subroutine initialize_motion_state
! 定义 cross_product 计算单元，封装该步骤的数据处理规则。
subroutine cross_product(a, b, c)
        implicit none
        real(8), intent(in) :: a(3), b(3)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(out) :: c(3)

        c(1) = a(2)*b(3) - a(3)*b(2)
        c(2) = a(3)*b(1) - a(1)*b(3)
        c(3) = a(1)*b(2) - a(2)*b(1)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine cross_product

    real(8) function wrap_angle_radians(angle_value)
        implicit none
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(in) :: angle_value

        wrap_angle_radians = modulo(angle_value, 2.0d0 * PI)
        if (wrap_angle_radians < 0.0d0) wrap_angle_radians = wrap_angle_radians + 2.0d0 * PI
    ! 结束当前计算单元，使过程边界保持清晰。
    end function wrap_angle_radians
    subroutine compute_two_body_acceleration(position_vec, mu_value, acceleration_vec)
        implicit none
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(in) :: position_vec(3), mu_value
        real(8), intent(out) :: acceleration_vec(3)
        real(8) :: radius_norm

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        radius_norm = sqrt(sum(position_vec**2))
        if (radius_norm <= EPS) then
            acceleration_vec = 0.0d0
        ! 当前条件不成立时执行替代计算路径。
        else
            acceleration_vec = -mu_value * position_vec / (radius_norm**3)
        end if
    end subroutine compute_two_body_acceleration

    ! 定义 compute_two_body_rhs 计算单元，封装该步骤的数据处理规则。
    subroutine compute_two_body_rhs(state_vec, mu_value, rhs_vec)
        implicit none
        real(8), intent(in) :: state_vec(6), mu_value
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(out) :: rhs_vec(6)

        rhs_vec(1:3) = state_vec(4:6)
        call compute_two_body_acceleration(state_vec(1:3), mu_value, rhs_vec(4:6))
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine compute_two_body_rhs

    subroutine rk4_two_body_step(state_in, dt_step, mu_value, state_out)
        implicit none
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(in) :: state_in(6), dt_step, mu_value
        real(8), intent(out) :: state_out(6)
        real(8) :: k1(6), k2(6), k3(6), k4(6), state_work(6)

        ! 调用子过程完成当前数值计算或状态更新。
        call compute_two_body_rhs(state_in, mu_value, k1)

        state_work = state_in + 0.5d0 * dt_step * k1
        call compute_two_body_rhs(state_work, mu_value, k2)

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        state_work = state_in + 0.5d0 * dt_step * k2
        call compute_two_body_rhs(state_work, mu_value, k3)

        state_work = state_in + dt_step * k3
        call compute_two_body_rhs(state_work, mu_value, k4)

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        state_out = state_in + (dt_step / 6.0d0) * (k1 + 2.0d0*k2 + 2.0d0*k3 + k4)
    end subroutine rk4_two_body_step

    subroutine propagate_two_body_state(position_initial, velocity_initial, time_value, &
                                        integrator_dt_value, mu_value, position_out, &
                                        velocity_out, acceleration_out)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        real(8), intent(in) :: position_initial(3), velocity_initial(3)
        real(8), intent(in) :: time_value, integrator_dt_value, mu_value
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(out) :: position_out(3), velocity_out(3), acceleration_out(3)
        real(8) :: state_now(6), state_next(6), dt_step, time_remaining

        state_now(1:3) = position_initial
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        state_now(4:6) = velocity_initial

        time_remaining = time_value
        do while (time_remaining > EPS)
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            dt_step = min(integrator_dt_value, time_remaining)
            call rk4_two_body_step(state_now, dt_step, mu_value, state_next)
            state_now = state_next
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            time_remaining = time_remaining - dt_step
        end do

        position_out = state_now(1:3)
        velocity_out = state_now(4:6)
        ! 调用子过程完成当前数值计算或状态更新。
        call compute_two_body_acceleration(position_out, mu_value, acceleration_out)
    end subroutine propagate_two_body_state

    subroutine rotate_vector_with_motion(v0, omega0, alpha0, time_value, v_rot)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        real(8), intent(in) :: v0(3), omega0(3), alpha0(3), time_value
        real(8), intent(out) :: v_rot(3)

        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: theta_vec(3), axis(3), cross_term(3)
        real(8) :: theta, ctheta, stheta

        theta_vec = omega0 * time_value + 0.5d0 * alpha0 * time_value * time_value
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        theta = sqrt(sum(theta_vec**2))

        if (theta <= EPS) then
            v_rot = v0
            ! 满足当前控制条件后结束或跳过本次处理。
            return
        end if

        axis = theta_vec / theta
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        ctheta = cos(theta)
        stheta = sin(theta)
        call cross_product(axis, v0, cross_term)

        v_rot = v0 * ctheta + cross_term * stheta + axis * sum(axis * v0) * (1.0d0 - ctheta)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine rotate_vector_with_motion

    subroutine evaluate_member_stage(member_idx, time_value, stage_out, release_elapsed)
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: member_idx
        real(8), intent(in) :: time_value
        integer, intent(out) :: stage_out
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(out) :: release_elapsed

        release_elapsed = 0.0d0
        if (.not. is_input_enabled(member_idx)) then
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            stage_out = MEMBER_STAGE_INACTIVE
        else if (time_value + EPS < sphere_release_time(member_idx)) then
            stage_out = MEMBER_STAGE_FORMATION
        ! 当前条件不成立时执行替代计算路径。
        else
            stage_out = MEMBER_STAGE_RELEASED
            release_elapsed = max(0.0d0, time_value - sphere_release_time(member_idx))
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
    end subroutine evaluate_member_stage
    subroutine update_member_motion_state(member_idx, time_value)
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: member_idx
        real(8), intent(in) :: time_value

        integer :: member_stage
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: release_elapsed
        real(8) :: base_local(3), separation_local(3), separation_velocity_local(3)
        real(8) :: total_local(3), total_world(3), separation_velocity_world(3)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: group_velocity_now(3), group_omega_now(3), omega_cross(3)

        call evaluate_member_stage(member_idx, time_value, member_stage, release_elapsed)
        sphere_motion_stage(member_idx) = member_stage

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        base_local = sphere_centers_initial(:,member_idx)
        separation_local = 0.0d0
        separation_velocity_local = 0.0d0

        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (member_stage == MEMBER_STAGE_RELEASED) then
            separation_local = sphere_velocity(:,member_idx) * release_elapsed + &
                               0.5d0 * sphere_acceleration(:,member_idx) * release_elapsed * release_elapsed
            separation_velocity_local = sphere_velocity(:,member_idx) + &
                                        sphere_acceleration(:,member_idx) * release_elapsed
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if

        total_local = base_local + separation_local
        call rotate_vector_with_motion(total_local, group_angular_velocity, &
                                       group_angular_acceleration, time_value, total_world)
        sphere_centers(:,member_idx) = group_center + total_world

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        group_velocity_now = group_velocity
        group_omega_now = group_angular_velocity + group_angular_acceleration * time_value
        call cross_product(group_omega_now, total_world, omega_cross)
        ! 调用子过程完成当前数值计算或状态更新。
        call rotate_vector_with_motion(separation_velocity_local, group_angular_velocity, &
                                       group_angular_acceleration, time_value, separation_velocity_world)

        sphere_world_velocity(:,member_idx) = group_velocity_now + omega_cross + separation_velocity_world
    end subroutine update_member_motion_state

    ! 定义 update_motion_state 计算单元，封装该步骤的数据处理规则。
    subroutine update_motion_state(time_value)
        implicit none
        real(8), intent(in) :: time_value
        ! 声明计数器、索引或离散控制参数。
        integer :: i
        real(8) :: dt_advance
        real(8) :: group_r_start(3), group_v_start(3)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: aperture_r_start(3), aperture_v_start(3)
        real(8) :: detector_r_start(3), detector_v_start(3)

        dt_advance = time_value - current_motion_time
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (dt_advance < -EPS) call production_fail('INTERNAL_ERROR','motion time moved backward',3)
        group_r_start = group_center; group_v_start = group_velocity
        aperture_r_start = aperture_center; aperture_v_start = aperture_velocity
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        detector_r_start = detector_center; detector_v_start = detector_velocity

        if (dt_advance > EPS) then
            call propagate_two_body_state(group_r_start, group_v_start, dt_advance, orbit_integrator_dt, &
                                          earth_mu, group_center, group_velocity, group_acceleration)
            call propagate_two_body_state(aperture_r_start, aperture_v_start, dt_advance, orbit_integrator_dt, &
                                          earth_mu, aperture_center, aperture_velocity, aperture_acceleration)
            ! 调用子过程完成当前数值计算或状态更新。
            call propagate_two_body_state(detector_r_start, detector_v_start, dt_advance, orbit_integrator_dt, &
                                          earth_mu, detector_center, detector_velocity, detector_acceleration)
            two_body_propagator_call_count = two_body_propagator_call_count + 3_8
            orbital_step_count = orbital_step_count + 3_8
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (any(.not.ieee_is_finite(group_center)) .or. any(.not.ieee_is_finite(group_velocity)) .or. &
                any(.not.ieee_is_finite(aperture_center)) .or. any(.not.ieee_is_finite(aperture_velocity)) .or. &
                any(.not.ieee_is_finite(detector_center)) .or. any(.not.ieee_is_finite(detector_velocity)) .or. &
                maxval(abs(group_center))>1.0d100 .or. maxval(abs(group_velocity))>1.0d100 .or. &
                maxval(abs(aperture_center))>1.0d100 .or. maxval(abs(aperture_velocity))>1.0d100 .or. &
                maxval(abs(detector_center))>1.0d100 .or. maxval(abs(detector_velocity))>1.0d100) then
                call production_fail('ORBITAL_PROPAGATION_FAILURE','nonfinite two-body propagated state',6)
            end if
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if

        call rotate_vector_with_motion(group_normal_initial, group_angular_velocity, &
                                       group_angular_acceleration, time_value, group_normal)
        call rotate_vector_with_motion(group_up_initial, group_angular_velocity, &
                                       group_angular_acceleration, time_value, group_up_hint)
        ! 调用子过程完成当前数值计算或状态更新。
        call normalize_vector(group_normal)
        call setup_group_coordinate_system()
        do i = 1, num_spheres
            ! 调用子过程完成当前数值计算或状态更新。
            call update_member_motion_state(i, time_value)
        end do
        call rotate_vector_with_motion(aperture_normal_initial, aperture_angular_velocity, &
                                       aperture_angular_acceleration, time_value, aperture_normal)
        ! 调用子过程完成当前数值计算或状态更新。
        call rotate_vector_with_motion(aperture_up_initial, aperture_angular_velocity, &
                                       aperture_angular_acceleration, time_value, aperture_up_hint)
        if (aperture_track_target) call point_axis_to_target(aperture_center, group_center, aperture_up_hint, aperture_normal)
        call normalize_vector(aperture_normal)
        call rotate_vector_with_motion(detector_normal_initial, detector_angular_velocity, &
                                       detector_angular_acceleration, time_value, detector_normal)
        ! 调用子过程完成当前数值计算或状态更新。
        call rotate_vector_with_motion(detector_up_initial, detector_angular_velocity, &
                                       detector_angular_acceleration, time_value, detector_up_hint)
        call normalize_vector(detector_normal)
        call setup_aperture_coordinate_system()
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (detector_track_target) then
            call point_axis_to_target(aperture_center, group_center, detector_up_hint, detector_normal)
            call normalize_vector(detector_normal)
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
        call setup_detector_coordinate_system()
        if (detector_track_target) then
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            detector_center = aperture_center + detector_offset_local(1) * detector_u + &
                              detector_offset_local(2) * detector_v + detector_offset_local(3) * detector_normal
            detector_velocity = aperture_velocity
            detector_acceleration = aperture_acceleration
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
        call update_relative_geometry()
        call append_orbital_state_row(current_frame_index, current_motion_time, time_value, &
             group_r_start, group_center, group_v_start, group_velocity, merge(3_8,0_8,dt_advance>EPS))
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        current_motion_time = time_value
    end subroutine update_motion_state
subroutine update_relative_geometry()
        implicit none
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: cross_term(3), rel_u, rel_v, rel_n, rel_transverse

        target_detector_relative_position = group_center - detector_center
        target_detector_relative_velocity = group_velocity - detector_velocity
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        target_detector_range = sqrt(sum(target_detector_relative_position**2))

        if (target_detector_range <= EPS) then
            target_detector_los_azimuth = 0.0d0
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            target_detector_los_elevation = 0.0d0
            target_detector_los_rate = 0.0d0
            target_detector_los_rate_mag = 0.0d0
            ! 满足当前控制条件后结束或跳过本次处理。
            return
        end if

        rel_u = sum(target_detector_relative_position * detector_u)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        rel_v = sum(target_detector_relative_position * detector_v)
        rel_n = sum(target_detector_relative_position * detector_normal)
        rel_transverse = sqrt(rel_u*rel_u + rel_v*rel_v)

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        target_detector_los_azimuth = atan2(rel_v, rel_u)
        target_detector_los_elevation = atan2(rel_n, max(rel_transverse, EPS))

        call cross_product(target_detector_relative_position, target_detector_relative_velocity, cross_term)
        target_detector_los_rate = cross_term / (target_detector_range * target_detector_range)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        target_detector_los_rate_mag = sqrt(sum(target_detector_los_rate**2))
    end subroutine update_relative_geometry

    subroutine compute_object_detector_geometry(object_idx, range_value, los_azimuth, &
                                                los_elevation, los_rate_magnitude)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in) :: object_idx
        real(8), intent(out) :: range_value, los_azimuth, los_elevation, los_rate_magnitude
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: rel_position(3), rel_velocity(3), cross_term(3)
        real(8) :: rel_u, rel_v, rel_n, rel_transverse

        range_value = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        los_azimuth = 0.0d0
        los_elevation = 0.0d0
        los_rate_magnitude = 0.0d0
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. is_valid_sphere_index(object_idx)) return

        rel_position = sphere_centers(:,object_idx) - detector_center
        rel_velocity = sphere_world_velocity(:,object_idx) - detector_velocity
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        range_value = sqrt(sum(rel_position**2))
        if (range_value <= EPS) return

        rel_u = sum(rel_position * detector_u)
        rel_v = sum(rel_position * detector_v)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        rel_n = sum(rel_position * detector_normal)
        rel_transverse = sqrt(rel_u*rel_u + rel_v*rel_v)
        los_azimuth = atan2(rel_v, rel_u)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        los_elevation = atan2(rel_n, max(rel_transverse, EPS))

        call cross_product(rel_position, rel_velocity, cross_term)
        los_rate_magnitude = sqrt(sum(cross_term**2)) / (range_value * range_value)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine compute_object_detector_geometry

    real(8) function object_internal_energy(object_idx, temperature_value)
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: object_idx
        real(8), intent(in) :: temperature_value

        object_internal_energy = 0.0d0
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. is_valid_sphere_index(object_idx)) return
        object_internal_energy = sphere_thermal_capacity(object_idx) * &
            (temperature_value - INTERNAL_ENERGY_REFERENCE_K)
    end function object_internal_energy

    ! 声明计数器、索引或离散控制参数。
    integer function active_object_count()
        implicit none
        integer :: idx

        active_object_count = 0
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do idx = 1, num_spheres
            if (participates_in_thermal(idx)) active_object_count = active_object_count + 1
        end do
    ! 结束当前计算单元，使过程边界保持清晰。
    end function active_object_count

    real(8) function active_mass_total()
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer :: idx

        active_mass_total = 0.0d0
        do idx = 1, num_spheres
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (participates_in_thermal(idx)) active_mass_total = active_mass_total + sphere_mass(idx)
        end do
    end function active_mass_total

    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) function active_internal_energy_total()
        implicit none
        integer :: idx

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        active_internal_energy_total = 0.0d0
        do idx = 1, num_spheres
            if (.not. participates_in_thermal(idx)) cycle
            active_internal_energy_total = active_internal_energy_total + &
                object_internal_energy(idx, sphere_temperature(idx))
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do
    end function active_internal_energy_total

    subroutine prepare_frame_geometry_and_mc()
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none

        call normalize_vector(aperture_normal)
        call normalize_vector(solar_direction)
        ! 调用子过程完成当前数值计算或状态更新。
        call normalize_vector(detector_normal)
        call setup_aperture_coordinate_system()
        call setup_detector_coordinate_system()
        ! 调用子过程完成当前数值计算或状态更新。
        call setup_grid_coordinates()
        call calculate_sphere_distances()
        call setup_aperture_cones()
        ! 调用子过程完成当前数值计算或状态更新。
        call update_relative_geometry()

        call initialize_mc_arrays()
        call run_monte_carlo_simulation()

        ! 调用子过程完成当前数值计算或状态更新。
        call calculate_solar_heat_dispatch()
    end subroutine prepare_frame_geometry_and_mc

    subroutine run_motion_simulation()
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer :: frame_idx, newly_released_count
        integer :: active_count_start, active_count_end
        integer, allocatable :: previous_stage(:)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: frame_time, slice_dt, previous_time
        real(8) :: frame_cpu_start, frame_cpu_end
        real(8) :: primary_temperature_before_event
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: active_mass_start, active_mass_end
        real(8) :: energy_start, energy_before_release, energy_end
        real(8) :: release_added_mass, release_carried_energy

        ! 按当前问题规模分配或释放数组存储空间。
        allocate(previous_stage(num_spheres))
        sphere_temperature = sphere_initial_temp
        previous_time = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        current_frame_index = 0
        current_motion_time = 0.0d0

        call initialize_temperature_history_file()
        ! 调用子过程完成当前数值计算或状态更新。
        call initialize_energy_ledger_file()
        call initialize_system_energy_ledger_file()
        call initialize_orbital_state_file()
        call initialize_release_event_file()

        ! At t=0, sphere 1 establishes the initial active-domain baseline.
        ! Any additional member released at t=0 inherits sphere 1 temperature
        ! and is recorded as release-carried mass and internal energy.
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do frame_idx = 1, num_spheres
            if (is_input_enabled(frame_idx)) then
                previous_stage(frame_idx) = MEMBER_STAGE_FORMATION
            ! 当前条件不成立时执行替代计算路径。
            else
                previous_stage(frame_idx) = MEMBER_STAGE_INACTIVE
            end if
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do
        primary_temperature_before_event = sphere_temperature(1)
        call update_motion_state(0.0d0)
        ! 调用子过程完成当前数值计算或状态更新。
        call apply_release_temperature_inheritance(previous_stage, 0.0d0, 0.0d0, &
                                                   primary_temperature_before_event, newly_released_count, &
                                                   release_added_mass, release_carried_energy)
        call append_initial_system_state_row(release_added_mass, release_carried_energy)
        call append_temperature_history_row(0, 0.0d0)
        ! 调用子过程完成当前数值计算或状态更新。
        call print_header()
        call prepare_frame_geometry_and_mc()
        call calculate_heat_transfer()
        ! 调用子过程完成当前数值计算或状态更新。
        call update_sphere_power()
        call calculate_total_energy()
        call build_spot_image()
        ! 调用子过程完成当前数值计算或状态更新。
        call cpu_time(frame_cpu_start)
        call cpu_time(frame_cpu_end)
        last_frame_compute_time = frame_cpu_end - frame_cpu_start
        call write_motion_frame_outputs(0, 0.0d0, .true.)

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        frame_idx = 0
        do while (previous_time < total_time - EPS)
            frame_time = next_event_aligned_time(previous_time)
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            slice_dt = frame_time - previous_time
            frame_idx = frame_idx + 1
            current_frame_index = frame_idx
            ! 调用子过程完成当前数值计算或状态更新。
            call cpu_time(frame_cpu_start)

            active_count_start = active_object_count()
            active_mass_start = active_mass_total()
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            energy_start = active_internal_energy_total()

            ! Advance temperature over [previous_time, frame_time] using the
            ! geometry, MC loads, and participation state at previous_time.
            call solve_temperature_transient_slice(slice_dt, previous_time)
            energy_before_release = active_internal_energy_total()

            ! Only after the preceding interval is complete may the release
            ! event at frame_time alter stage, geometry, and participation.
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            previous_stage = sphere_motion_stage
            primary_temperature_before_event = sphere_temperature(1)
            call update_motion_state(frame_time)
            ! 调用子过程完成当前数值计算或状态更新。
            call apply_release_temperature_inheritance(previous_stage, previous_time, frame_time, &
                                                       primary_temperature_before_event, newly_released_count, &
                                                       release_added_mass, release_carried_energy)

            active_count_end = active_object_count()
            active_mass_end = active_mass_total()
            energy_end = active_internal_energy_total()
            ! 调用子过程完成当前数值计算或状态更新。
            call append_system_energy_ledger_row(previous_time, frame_time, active_count_start, &
                active_count_end, active_mass_start, active_mass_end, energy_start, &
                energy_before_release, energy_end, release_added_mass, release_carried_energy)

            ! Rebuild endpoint geometry and loads for the next interval and for
            ! outputs at the event-aligned node.
            call prepare_frame_geometry_and_mc()
            call calculate_heat_transfer()
            ! 调用子过程完成当前数值计算或状态更新。
            call update_sphere_power()
            call calculate_total_energy()
            call build_spot_image()
            ! 调用子过程完成当前数值计算或状态更新。
            call append_temperature_history_row(frame_idx, frame_time)

            call cpu_time(frame_cpu_end)
            last_frame_compute_time = frame_cpu_end - frame_cpu_start
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            cumulative_compute_time = frame_cpu_end - start_time
            call write_motion_frame_outputs(frame_idx, frame_time, .true.)
            previous_time = frame_time
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do

        if (frame_idx /= motion_frame_count) then
            call production_fail('INTERNAL_ERROR', 'event-aligned frame count mismatch', 3)
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if

        deallocate(previous_stage)
        call cpu_time(end_time)
        cumulative_compute_time = end_time - start_time
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine run_motion_simulation
logical function should_write_output_frame(frame_idx)
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: frame_idx

        should_write_output_frame = .false.
        if (frame_idx == 0) then
            ! 维护输入输出路径及文件数据，确保结果写入约定位置。
            should_write_output_frame = .true.
        else if (frame_idx == motion_frame_count) then
            should_write_output_frame = .true.
        ! 当前条件不成立时执行替代计算路径。
        else if (output_frame_interval <= 1) then
            should_write_output_frame = .true.
        else if (mod(frame_idx, output_frame_interval) == 0) then
            ! 维护输入输出路径及文件数据，确保结果写入约定位置。
            should_write_output_frame = .true.
        end if
    end function should_write_output_frame

!==================== Reading input & spheres ==================================

    ! 定义 read_clean_input_file 计算单元，封装该步骤的数据处理规则。
    subroutine read_clean_input_file(filename, ierr)
        implicit none
        character(len=*), intent(in) :: filename
        integer, intent(out) :: ierr
        ! 声明计数器、索引或离散控制参数。
        integer :: uf, ios, eq_pos, idx
        character(len=1024) :: line
        character(len=256) :: key, value_str
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=32) :: section
        real(8) :: v1, v2, v3
        logical :: seen(31)

        ! 检查并记录计算状态，使异常能够被上层流程识别。
        ierr = 0
        solver_status = 'INVALID_INPUT'
        solver_message = 'clean input validation failed'
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        seen = .false.
        section = ''

        ! One protected numerical/reliability owner. Case input cannot override it.
        time_step = CFG_TIME_STEP
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        earth_mu = CFG_EARTH_MU
        orbit_integrator_dt = CFG_ORBIT_INTEGRATOR_DT
        rays_per_sphere = CFG_RAYS_PER_SPHERE
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        rays_solar = CFG_RAYS_SOLAR
        max_bounces = CFG_MAX_BOUNCES
        mc_mix_beta = CFG_MC_MIX_BETA
        random_seed_user = CFG_RANDOM_SEED
        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        output_frame_interval = CFG_OUTPUT_FRAME_INTERVAL
        nx = CFG_GRID_NX
        ny = CFG_GRID_NY
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        spot_radius_cells = CFG_SPOT_RADIUS_CELLS
        output_dir = CFG_OUTPUT_DIR
        save_point_history = CFG_SAVE_POINT_HISTORY
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        save_full_spot_image = CFG_SAVE_FULL_SPOT_IMAGE
        save_qa_full_image = CFG_SAVE_QA_FULL_IMAGE
        qa_image_frame_stride = CFG_QA_IMAGE_FRAME_STRIDE
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        qa_image_max_frames = CFG_QA_IMAGE_MAX_FRAMES
        qa_image_written_count = 0

        ! Fixed solver and removed-downstream contracts are deliberately not case fields.
        companion_type_default = CFG_COMPANION_TYPE
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        attitude_motion_type = CFG_ATTITUDE_MOTION_TYPE
        micro_motion_params = 0.0d0
        similarity_level = 'DATASET_BASIC'
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        reference_temperature = -1.0d0
        reference_intensity = -1.0d0
        group_acceleration = 0.0d0
        aperture_acceleration = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        detector_acceleration = 0.0d0
        sphere_file = trim(filename)
        temperature_history_initialized = .false.
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        motion_frame_count = 0
        current_frame_index = 0
        current_motion_time = 0.0d0

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(newunit=uf, file=filename, status='old', action='read', iostat=ios)
        if (ios /= 0) then
            solver_message = 'cannot open clean input file: '//trim(filename); ierr = 1; return
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
        do
            read(uf, '(A)', iostat=ios) line
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (ios /= 0) exit
            line = adjustl(trim(line))
            if (len_trim(line) == 0 .or. line(1:1) == '#') cycle
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (line(1:1) == '[') then
                if (line(len_trim(line):len_trim(line)) /= ']') then
                    solver_message='malformed clean section header'; ierr=1; exit
                end if
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                section = trim(to_uppercase(line(2:len_trim(line)-1)))
                if (section /= 'CASE' .and. section /= 'ENVIRONMENT' .and. section /= 'GROUP_STATE' .and. &
                    section /= 'OBSERVATION' .and. section /= 'TARGET_PHYSICS' .and. section /= 'TARGET_SCENE') then
                    solver_message='unknown clean section: '//trim(section); ierr=1; exit
                ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
                end if
                cycle
            end if
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            eq_pos = index(line, '=')
            if (eq_pos == 0) then
                if (section == 'TARGET_PHYSICS' .or. section == 'TARGET_SCENE') cycle
                ! 检查并记录计算状态，使异常能够被上层流程识别。
                solver_message='data row outside target table'; ierr=1; exit
            end if
            key = trim(to_uppercase(adjustl(line(1:eq_pos-1))))
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            value_str = adjustl(line(eq_pos+1:))
            idx = index(value_str, '#')
            if (idx > 0) value_str = value_str(1:idx-1)
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            value_str = trim(value_str)

            select case(trim(key))
            case('TOTAL_TIME')
                call require_clean_field(section,'CASE',seen(1),key,ierr); if(ierr/=0) exit
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str,*,iostat=ios) total_time
            case('NUM_SPHERES')
                call require_clean_field(section,'CASE',seen(2),key,ierr); if(ierr/=0) exit
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str,*,iostat=ios) num_spheres
            case('SOLAR_FLUX')
                call require_clean_field(section,'ENVIRONMENT',seen(3),key,ierr); if(ierr/=0) exit
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str,*,iostat=ios) solar_flux
            case('SOLAR_DIRECTION')
                call require_clean_field(section,'ENVIRONMENT',seen(4),key,ierr); if(ierr/=0) exit
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str,*,iostat=ios) v1,v2,v3; solar_direction=(/v1,v2,v3/)
            case('ENVIRONMENT_TEMP')
                call require_clean_field(section,'ENVIRONMENT',seen(5),key,ierr); if(ierr/=0) exit
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str,*,iostat=ios) environment_temp
            case('GROUP_CENTER')
                call require_clean_field(section,'GROUP_STATE',seen(6),key,ierr); if(ierr/=0) exit
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str,*,iostat=ios) v1,v2,v3; group_center=(/v1,v2,v3/)
            case('GROUP_NORMAL')
                call require_clean_field(section,'GROUP_STATE',seen(7),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) v1,v2,v3; group_normal=(/v1,v2,v3/)
            ! 根据离散状态选择对应算法分支。
            case('GROUP_UP')
                call require_clean_field(section,'GROUP_STATE',seen(8),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) v1,v2,v3; group_up_hint=(/v1,v2,v3/)
            ! 根据离散状态选择对应算法分支。
            case('GROUP_VELOCITY')
                call require_clean_field(section,'GROUP_STATE',seen(9),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) v1,v2,v3; group_velocity=(/v1,v2,v3/)
            ! 根据离散状态选择对应算法分支。
            case('GROUP_ANGULAR_VELOCITY')
                call require_clean_field(section,'GROUP_STATE',seen(10),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) v1,v2,v3; group_angular_velocity=(/v1,v2,v3/)
            ! 根据离散状态选择对应算法分支。
            case('GROUP_ANGULAR_ACCELERATION')
                call require_clean_field(section,'GROUP_STATE',seen(11),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) v1,v2,v3; group_angular_acceleration=(/v1,v2,v3/)
            ! 根据离散状态选择对应算法分支。
            case('COMPANION_TYPE')
                call require_clean_field(section,'GROUP_STATE',seen(28),key,ierr); if(ierr/=0) exit
                companion_type_default=trim(to_uppercase(value_str)); ios=0
            ! 根据离散状态选择对应算法分支。
            case('ATTITUDE_MOTION_TYPE')
                call require_clean_field(section,'GROUP_STATE',seen(29),key,ierr); if(ierr/=0) exit
                attitude_motion_type=trim(to_uppercase(value_str)); ios=0
            ! 根据离散状态选择对应算法分支。
            case('MICRO_MOTION_PARAMS')
                call require_clean_field(section,'GROUP_STATE',seen(30),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) v1,v2,v3; micro_motion_params=(/v1,v2,v3/)
            case('SIMILARITY_LEVEL')
                ! 调用子过程完成当前数值计算或状态更新。
                call require_clean_field(section,'GROUP_STATE',seen(31),key,ierr); if(ierr/=0) exit
                similarity_level=trim(to_uppercase(value_str)); ios=0
            case('APERTURE_SIZE')
                ! 调用子过程完成当前数值计算或状态更新。
                call require_clean_field(section,'OBSERVATION',seen(12),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) aperture_size
            case('APERTURE_CENTER')
                ! 调用子过程完成当前数值计算或状态更新。
                call require_clean_field(section,'OBSERVATION',seen(13),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) v1,v2,v3; aperture_center=(/v1,v2,v3/)
            case('APERTURE_NORMAL')
                ! 调用子过程完成当前数值计算或状态更新。
                call require_clean_field(section,'OBSERVATION',seen(14),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) v1,v2,v3; aperture_normal=(/v1,v2,v3/)
            case('APERTURE_UP')
                ! 调用子过程完成当前数值计算或状态更新。
                call require_clean_field(section,'OBSERVATION',seen(15),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) v1,v2,v3; aperture_up_hint=(/v1,v2,v3/)
            case('APERTURE_VELOCITY')
                ! 调用子过程完成当前数值计算或状态更新。
                call require_clean_field(section,'OBSERVATION',seen(16),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) v1,v2,v3; aperture_velocity=(/v1,v2,v3/)
            case('APERTURE_ANGULAR_VELOCITY')
                call require_clean_field(section,'OBSERVATION',seen(17),key,ierr); if(ierr/=0) exit
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str,*,iostat=ios) v1,v2,v3; aperture_angular_velocity=(/v1,v2,v3/)
            case('APERTURE_ANGULAR_ACCELERATION')
                call require_clean_field(section,'OBSERVATION',seen(18),key,ierr); if(ierr/=0) exit
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str,*,iostat=ios) v1,v2,v3; aperture_angular_acceleration=(/v1,v2,v3/)
            case('APERTURE_TRACK_TARGET')
                call require_clean_field(section,'OBSERVATION',seen(19),key,ierr); if(ierr/=0) exit
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str,*,iostat=ios) aperture_track_target
            case('DETECTOR_NORMAL')
                call require_clean_field(section,'OBSERVATION',seen(20),key,ierr); if(ierr/=0) exit
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str,*,iostat=ios) v1,v2,v3; detector_normal=(/v1,v2,v3/)
            case('DETECTOR_UP')
                call require_clean_field(section,'OBSERVATION',seen(21),key,ierr); if(ierr/=0) exit
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str,*,iostat=ios) v1,v2,v3; detector_up_hint=(/v1,v2,v3/)
            case('DETECTOR_VELOCITY')
                call require_clean_field(section,'OBSERVATION',seen(22),key,ierr); if(ierr/=0) exit
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str,*,iostat=ios) v1,v2,v3; detector_velocity=(/v1,v2,v3/)
            case('DETECTOR_ANGULAR_VELOCITY')
                call require_clean_field(section,'OBSERVATION',seen(23),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) v1,v2,v3; detector_angular_velocity=(/v1,v2,v3/)
            ! 根据离散状态选择对应算法分支。
            case('DETECTOR_ANGULAR_ACCELERATION')
                call require_clean_field(section,'OBSERVATION',seen(24),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) v1,v2,v3; detector_angular_acceleration=(/v1,v2,v3/)
            ! 根据离散状态选择对应算法分支。
            case('DETECTOR_TRACK_TARGET')
                call require_clean_field(section,'OBSERVATION',seen(25),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) detector_track_target
            ! 根据离散状态选择对应算法分支。
            case('SPOT_PLANE_SIZE')
                call require_clean_field(section,'OBSERVATION',seen(26),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) spot_plane_size
            ! 根据离散状态选择对应算法分支。
            case('SPOT_FOCAL_LENGTH')
                call require_clean_field(section,'OBSERVATION',seen(27),key,ierr); if(ierr/=0) exit
                read(value_str,*,iostat=ios) spot_focal_length
            ! 根据离散状态选择对应算法分支。
            case('TIME_STEP','ORBIT_INTEGRATOR_DT','RAYS_PER_SPHERE','RAYS_SOLAR','MAX_BOUNCES', &
                 'MC_MIX_BETA','RANDOM_SEED','OUTPUT_FRAME_INTERVAL','OUTPUT_DIR','GRID_NX','GRID_NY', &
                 'SPOT_RADIUS_CELLS','SAVE_POINT_HISTORY','SAVE_FULL_SPOT_IMAGE','SAVE_QA_FULL_IMAGE', &
                 'QA_IMAGE_FRAME_STRIDE','QA_IMAGE_MAX_FRAMES')
                solver_status='FORBIDDEN_INTERNAL_FIELD'
                solver_message=trim(key)//' belongs to protected internal numerical config; use migration script for old inputs'
                ! 检查并记录计算状态，使异常能够被上层流程识别。
                ierr=1; exit
            case('REFERENCE_TEMPERATURE','REFERENCE_INTENSITY')
                solver_status='FORBIDDEN_DOWNSTREAM_FIELD'
                solver_message=trim(key)//' belongs to downstream evaluation and is not a clean solver field'
                ! 检查并记录计算状态，使异常能够被上层流程识别。
                ierr=1; exit
            case('EARTH_MU','COMPANION_TYPE_DEFAULT')
                solver_status='FORBIDDEN_FIXED_CONTRACT_FIELD'
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                solver_message=trim(key)//' is a fixed solver contract and cannot be overridden'
                ierr=1; exit
            case default
                ! 检查并记录计算状态，使异常能够被上层流程识别。
                solver_message='unknown clean business field: '//trim(key); ierr=1; exit
            end select
            if (ios /= 0) then
                ! 检查并记录计算状态，使异常能够被上层流程识别。
                solver_message='malformed required clean field: '//trim(key); ierr=1; exit
            end if
        end do
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)
        if (ierr /= 0) return
        if (any(.not. seen)) then
            ! 检查并记录计算状态，使异常能够被上层流程识别。
            solver_message='clean input is missing one or more required scalar fields'; ierr=1; return
        end if
        if (num_spheres < 1 .or. .not.ieee_is_finite(total_time) .or. total_time <= 0.0d0) then
            solver_message='NUM_SPHERES and TOTAL_TIME must be positive'; ierr=1; return
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
        detector_center = aperture_center + spot_focal_length * detector_normal
        dx=aperture_size/dble(nx); dy=aperture_size/dble(ny); cell_area=dx*dy
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A)') ' Clean production configuration loaded; protected numerical config applied.'
    end subroutine read_clean_input_file

    subroutine require_clean_field(actual_section, expected_section, was_seen, key, ierr)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        character(len=*), intent(in) :: actual_section, expected_section, key
        logical, intent(inout) :: was_seen
        ! 声明计数器、索引或离散控制参数。
        integer, intent(out) :: ierr
        ierr=0
        if (trim(actual_section) /= trim(expected_section)) then
            ! 检查并记录计算状态，使异常能够被上层流程识别。
            solver_message=trim(key)//' appears in wrong section; expected ['//trim(expected_section)//']'; ierr=1
        else if (was_seen) then
            solver_message='duplicate required clean field: '//trim(key); ierr=1
        ! 当前条件不成立时执行替代计算路径。
        else
            was_seen=.true.
        end if
    end subroutine require_clean_field

    ! 定义 read_clean_sphere_tables 计算单元，封装该步骤的数据处理规则。
    subroutine read_clean_sphere_tables(filename, ierr)
        implicit none
        character(len=*), intent(in) :: filename
        ! 声明计数器、索引或离散控制参数。
        integer, intent(out) :: ierr
        integer :: uf, ios, id, line_number, comment_pos, token_count
        integer :: physics_count, scene_count
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=1024) :: line, data_line
        character(len=32) :: section, active_token
        real(8) :: x,y,z,r,rho,cp_,eps_ir,alpha_s,rho_ir,q_int,t_init
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: vx,vy,vz,ax,ay,az,release_time_value
        logical :: active_value, active_parse_ok
        logical, allocatable :: physics_seen(:), scene_seen(:)

        ! 检查并记录计算状态，使异常能够被上层流程识别。
        ierr=0; solver_status='INVALID_INPUT'; solver_message='clean target table validation failed'
        allocate(physics_seen(num_spheres),scene_seen(num_spheres)); physics_seen=.false.; scene_seen=.false.
        allocate(sphere_centers(3,num_spheres),sphere_centers_initial(3,num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(sphere_velocity(3,num_spheres),sphere_acceleration(3,num_spheres),sphere_world_velocity(3,num_spheres))
        allocate(sphere_release_time(num_spheres),sphere_is_active(num_spheres),companion_type(num_spheres))
        allocate(sphere_attitude_motion_type(num_spheres),sphere_micro_motion(3,num_spheres),sphere_motion_stage(num_spheres))
        allocate(sphere_orientation(3,num_spheres),sphere_angular_velocity(3,num_spheres),sphere_angular_acceleration(3,num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(sphere_radius(num_spheres),sphere_density(num_spheres),sphere_specific_heat(num_spheres))
        allocate(sphere_ir_emissivity(num_spheres),sphere_solar_absorptivity(num_spheres),sphere_ir_reflectivity(num_spheres))
        allocate(sphere_internal_heat(num_spheres),sphere_initial_temp(num_spheres))
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        sphere_centers=0d0; sphere_velocity=0d0; sphere_acceleration=0d0; sphere_world_velocity=0d0
        sphere_release_time=0d0; sphere_is_active=.false.; companion_type=companion_type_default
        sphere_attitude_motion_type=attitude_motion_type; sphere_micro_motion=0d0; sphere_motion_stage=MEMBER_STAGE_INACTIVE
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        sphere_orientation=0d0; sphere_angular_velocity=0d0; sphere_angular_acceleration=0d0
        sphere_radius=0d0; sphere_density=0d0; sphere_specific_heat=0d0; sphere_ir_emissivity=0d0
        sphere_solar_absorptivity=0d0; sphere_ir_reflectivity=0d0; sphere_internal_heat=0d0; sphere_initial_temp=0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        section=''; physics_count=0; scene_count=0; line_number=0
        open(newunit=uf,file=filename,status='old',action='read',iostat=ios)
        if(ios/=0) then; solver_message='cannot reopen clean input'; ierr=1; return; end if
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do
            read(uf,'(A)',iostat=ios) line
            if(ios/=0) exit
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            line_number=line_number+1
            comment_pos=index(line,'#'); if(comment_pos>0) line=line(1:comment_pos-1)
            data_line=adjustl(trim(line)); if(len_trim(data_line)==0) cycle
            if(data_line(1:1)=='[') then
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                section=trim(to_uppercase(data_line(2:len_trim(data_line)-1))); cycle
            end if
            if(index(data_line,'=')>0) cycle
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if(section=='TARGET_PHYSICS') then
                token_count=count_data_tokens(data_line)
                if(token_count/=9) then; solver_message='TARGET_PHYSICS row must contain exactly 9 columns'; ierr=1; exit; end if
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(data_line,*,iostat=ios) id,r,rho,cp_,eps_ir,alpha_s,rho_ir,q_int,t_init
                if(ios/=0) then; solver_message='TARGET_PHYSICS row parse failure'; ierr=1; exit; end if
                if(id<1.or.id>num_spheres) then; solver_message='TARGET_PHYSICS ID out of range'; ierr=1; exit; end if
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if(physics_seen(id)) then; solver_message='duplicate TARGET_PHYSICS ID'; ierr=1; exit; end if
                if(any(.not.ieee_is_finite((/r,rho,cp_,eps_ir,alpha_s,rho_ir,q_int,t_init/)))) then
                    solver_message='nonfinite TARGET_PHYSICS value'; ierr=1; exit
                ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
                end if
                if(r<=TARGET_SHELL_THICKNESS) then; solver_message='radius must be greater than fixed 0.005 m shell thickness'; ierr=1; exit; end if
                if(rho<=0d0.or.cp_<=0d0.or.t_init<=0d0) then; solver_message='rho, cp, and t_init must be positive'; ierr=1; exit; end if
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                sphere_radius(id)=r; sphere_density(id)=rho; sphere_specific_heat(id)=cp_
                sphere_ir_emissivity(id)=eps_ir; sphere_solar_absorptivity(id)=alpha_s
                sphere_ir_reflectivity(id)=rho_ir; sphere_internal_heat(id)=q_int; sphere_initial_temp(id)=t_init
                physics_seen(id)=.true.; physics_count=physics_count+1
            ! 当前条件不成立时执行替代计算路径。
            else if(section=='TARGET_SCENE') then
                token_count=count_data_tokens(data_line)
                if(token_count/=12) then; solver_message='TARGET_SCENE row must contain exactly 12 columns'; ierr=1; exit; end if
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(data_line,*,iostat=ios) id,x,y,z,vx,vy,vz,ax,ay,az,release_time_value,active_token
                if(ios/=0) then; solver_message='TARGET_SCENE row parse failure'; ierr=1; exit; end if
                if(id<1.or.id>num_spheres) then; solver_message='TARGET_SCENE ID out of range'; ierr=1; exit; end if
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if(scene_seen(id)) then; solver_message='duplicate TARGET_SCENE ID'; ierr=1; exit; end if
                call parse_strict_logical_token(active_token,active_value,active_parse_ok)
                if(.not.active_parse_ok) then; solver_message='TARGET_SCENE active must be explicit T/F'; ierr=1; exit; end if
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if(any(.not.ieee_is_finite((/x,y,z,vx,vy,vz,ax,ay,az,release_time_value/)))) then
                    solver_message='nonfinite TARGET_SCENE value'; ierr=1; exit
                end if
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if(release_time_value<0d0) then; solver_message='release_time must be nonnegative'; ierr=1; exit; end if
                sphere_centers(:,id)=group_center+(/x,y,z/); sphere_velocity(:,id)=(/vx,vy,vz/)
                sphere_acceleration(:,id)=(/ax,ay,az/); sphere_release_time(id)=release_time_value
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                sphere_is_active(id)=active_value; scene_seen(id)=.true.; scene_count=scene_count+1
            end if
        end do
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)
        if(ierr/=0) return
        if(physics_count/=num_spheres.or.any(.not.physics_seen)) then
            solver_message='TARGET_PHYSICS IDs must uniquely cover 1..NUM_SPHERES'; ierr=1; return
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
        if(scene_count/=num_spheres.or.any(.not.scene_seen)) then
            solver_message='TARGET_SCENE IDs must uniquely cover 1..NUM_SPHERES and match TARGET_PHYSICS'; ierr=1; return
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
        sphere_centers_initial=sphere_centers-spread(group_center,2,num_spheres)
        if(.not.sphere_is_active(1)) then; solver_message='sphere 1 must be active for Rule B'; ierr=1; return; end if
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if(abs(sphere_release_time(1))>PRIMARY_RELEASE_TOL) then; solver_message='sphere 1 release_time must be 0 for Rule B'; ierr=1; return; end if
        deallocate(physics_seen,scene_seen)
        write(*,'(A)') ' Clean target tables: PASS (complete paired IDs and explicit per-target values)'
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine read_clean_sphere_tables

    subroutine read_input_file(filename, ierr)
        character(len=*), intent(in) :: filename
        ! 声明计数器、索引或离散控制参数。
        integer, intent(out) :: ierr

        integer :: uf, ios
        character(len=256) :: line, key, value_str
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: v1, v2, v3
        integer :: eq_pos
        logical :: detector_center_given, detector_normal_given, detector_up_given

        ierr = 0
        ! 检查并记录计算状态，使异常能够被上层流程识别。
        solver_status='INVALID_INPUT'
        solver_message='input validation failed'
        uf = 10

        ! 默认值
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        time_step = 0.5d0
        total_time = 5000.0d0
        motion_frame_count = 0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        current_frame_index = 0
        current_motion_time = 0.0d0
        earth_mu = 3.986004418d14
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        orbit_integrator_dt = 0.0d0
        temperature_history_initialized = .false.

        rays_per_sphere = 500000_8
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        rays_solar = 1000000_8
        max_bounces = 10

        mc_mix_beta = 0.1d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        random_seed_user = -1

        companion_type_default = 'SPHERE'
        attitude_motion_type = 'NONE'
        micro_motion_params = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        similarity_level = 'UNSPECIFIED'
        reference_temperature = -1.0d0
        reference_intensity = -1.0d0
        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        output_dir = 'output'

        solar_flux = 1361.0d0
        solar_direction = (/0.0d0, 0.0d0, -1.0d0/)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        environment_temp = 3.0d0

        group_center = 0.0d0
        group_velocity = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        group_acceleration = 0.0d0
        group_normal = (/0.0d0, 0.0d0, 1.0d0/)
        group_up_hint = (/0.0d0, 1.0d0, 0.0d0/)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        group_angular_velocity = 0.0d0
        group_angular_acceleration = 0.0d0

        aperture_size = 1.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        aperture_center = (/0.0d0, 0.0d0, 0.0d0/)
        aperture_normal = (/0.0d0, 0.0d0, 1.0d0/)
        aperture_up_hint = (/0.0d0, 1.0d0, 0.0d0/)
        aperture_velocity = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        aperture_acceleration = 0.0d0
        aperture_angular_velocity = 0.0d0
        aperture_angular_acceleration = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        aperture_track_target = .false.
        nx = 10
        ny = 10

        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        sphere_file = trim(filename)  ! kept for backward-compatible summary only
!        image_distance = 0.1d0
        spot_plane_size = aperture_size
        spot_focal_length = 5d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        spot_radius_cells = 0
        output_frame_interval = 1
        save_point_history = .true.
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        save_full_spot_image = .false.
        save_qa_full_image = .true.
        qa_image_frame_stride = 100
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        qa_image_max_frames = 5
        qa_image_written_count = 0

        detector_center = 0.0d0
        detector_normal = (/0.0d0, 0.0d0, 1.0d0/)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        detector_up_hint = (/0.0d0, 1.0d0, 0.0d0/)
        detector_velocity = 0.0d0
        detector_acceleration = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        detector_angular_velocity = 0.0d0
        detector_angular_acceleration = 0.0d0
        detector_track_target = .false.
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        detector_center_given = .false.
        detector_normal_given = .false.
        detector_up_given = .false.

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=filename, status='old', iostat=ios)
        if (ios /= 0) then
            write(*,'(A,A)') ' ERROR: Cannot open file: ', trim(filename)
            ! 检查并记录计算状态，使异常能够被上层流程识别。
            ierr = 1
            return
        end if

        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do
            read(uf, '(A)', iostat=ios) line
            if (ios /= 0) exit

            line = adjustl(line)
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (len_trim(line) == 0) cycle
            if (line(1:1) == '#') cycle

            eq_pos = index(line, '=')
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (eq_pos == 0) cycle

            key = adjustl(line(1:eq_pos-1))
            value_str = adjustl(line(eq_pos+1:))

            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            eq_pos = index(value_str, '#')
            if (eq_pos > 0) value_str = value_str(1:eq_pos-1)
            value_str = adjustl(trim(value_str))

            ! 根据离散状态选择对应算法分支。
            select case (trim(key))
            case ('SOLVE_MODE','THERMAL_MODE','MAX_ITERATIONS','STEADY_TOLERANCE','RELAXATION_FACTOR')
                solver_status='UNSUPPORTED_LEGACY_FIELD'; solver_message='deprecated thermal field: '//trim(key); ierr=1



            ! 根据离散状态选择对应算法分支。
            case ('TIME_STEP')
                read(value_str, *) time_step

            case ('TOTAL_TIME')
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str, *) total_time
            case ('MOTION_DT','MOTION_TOTAL_TIME','MOTION_ENABLED','MOTION_MODEL','LINEAR_MOTION','STATIC')
                solver_status='UNSUPPORTED_LEGACY_FIELD'; solver_message='deprecated motion field: '//trim(key); ierr=1



            case ('EARTH_MU')
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str, *) earth_mu

            case ('ORBIT_INTEGRATOR_DT')
                read(value_str, *) orbit_integrator_dt
            ! 根据离散状态选择对应算法分支。
            case ('ORBIT_INPUT_MODE','GROUP_ORBIT_INPUT_MODE','GROUP_ORBITAL_ELEMENTS','GROUP_KEPLERIAN_ELEMENTS', &
                  'GROUP_CLASSICAL_ORBITAL_ELEMENTS','GROUP_ORBIT_ELEMENTS','GROUP_ORBIT_ELEMENTS_MEAN', &
                  'GROUP_ORBITAL_ELEMENTS_MEAN','GROUP_KEPLERIAN_ELEMENTS_MEAN','GROUP_ORBIT_ANOMALY_TYPE', &
                  'APERTURE_ORBITAL_ELEMENTS','APERTURE_KEPLERIAN_ELEMENTS','APERTURE_CLASSICAL_ORBITAL_ELEMENTS', &
                  'APERTURE_ORBITAL_ELEMENTS_MEAN','APERTURE_KEPLERIAN_ELEMENTS_MEAN','APERTURE_ORBIT_ANOMALY_TYPE', &
                  'DETECTOR_ORBITAL_ELEMENTS','DETECTOR_KEPLERIAN_ELEMENTS','DETECTOR_CLASSICAL_ORBITAL_ELEMENTS', &
                  'DETECTOR_ORBITAL_ELEMENTS_MEAN','DETECTOR_KEPLERIAN_ELEMENTS_MEAN','DETECTOR_ORBIT_ANOMALY_TYPE')
                solver_status='UNSUPPORTED_LEGACY_FIELD'; solver_message='deprecated orbit selector: '//trim(key); ierr=1



            case ('RAYS_PER_SPHERE')
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str, *) rays_per_sphere

            case ('RAYS_SOLAR')
                read(value_str, *) rays_solar

            ! 根据离散状态选择对应算法分支。
            case ('MAX_BOUNCES')
                read(value_str, *) max_bounces

            case ('MC_MIX_BETA')
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str, *) mc_mix_beta

            case ('VERIFY_ONLY')
                solver_status='UNSUPPORTED_LEGACY_FIELD'; solver_message='deprecated test bypass: VERIFY_ONLY'; ierr=1

            ! 根据离散状态选择对应算法分支。
            case ('RANDOM_SEED')
                read(value_str, *) random_seed_user

            case ('COMPANION_TYPE', 'COMPANION_TYPE_DEFAULT', 'OBJECT_TYPE', 'OBJECT_TYPE_DEFAULT')
                companion_type_default = trim(to_uppercase(value_str))

            ! 根据离散状态选择对应算法分支。
            case ('ATTITUDE_MOTION_TYPE', 'ATTITUDE_MODEL')
                attitude_motion_type = trim(to_uppercase(value_str))

            case ('MICRO_MOTION_PARAMS', 'MICRO_MOTION')
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str, *) v1, v2, v3
                micro_motion_params = (/v1, v2, v3/)

            case ('SIMILARITY_LEVEL')
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                similarity_level = trim(to_uppercase(value_str))

            case ('REFERENCE_TEMPERATURE')
                read(value_str, *) reference_temperature

            ! 根据离散状态选择对应算法分支。
            case ('REFERENCE_INTENSITY')
                read(value_str, *) reference_intensity

            case ('OUTPUT_DIR')
                ! 维护输入输出路径及文件数据，确保结果写入约定位置。
                output_dir = trim(value_str)

            case ('SOLAR_FLUX')
                read(value_str, *) solar_flux

            ! 根据离散状态选择对应算法分支。
            case ('SOLAR_DIRECTION')
                read(value_str, *) v1, v2, v3
                solar_direction = (/v1, v2, v3/)

            case ('SOLAR_MODEL')
                ! 检查并记录计算状态，使异常能够被上层流程识别。
                solver_status='UNSUPPORTED_LEGACY_FIELD'; solver_message='deprecated solar selector: SOLAR_MODEL'; ierr=1

            case ('ENVIRONMENT_TEMP')
                read(value_str, *) environment_temp

            ! 根据离散状态选择对应算法分支。
            case ('GROUP_CENTER')
                read(value_str, *) v1, v2, v3
                group_center = (/v1, v2, v3/)

            ! 根据离散状态选择对应算法分支。
            case ('GROUP_NORMAL')
                read(value_str, *) v1, v2, v3
                group_normal = (/v1, v2, v3/)

            ! 根据离散状态选择对应算法分支。
            case ('GROUP_UP')
                read(value_str, *) v1, v2, v3
                group_up_hint = (/v1, v2, v3/)

            ! 根据离散状态选择对应算法分支。
            case ('GROUP_VELOCITY')
                read(value_str, *) v1, v2, v3
                group_velocity = (/v1, v2, v3/)
            ! 根据离散状态选择对应算法分支。
            case ('GROUP_ACCELERATION')
                solver_status='UNSUPPORTED_LEGACY_FIELD'; solver_message='deprecated linear acceleration field: GROUP_ACCELERATION'; ierr=1

            case ('GROUP_ANGULAR_VELOCITY')
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str, *) v1, v2, v3
                group_angular_velocity = (/v1, v2, v3/)

            case ('GROUP_ANGULAR_ACCELERATION')
                read(value_str, *) v1, v2, v3
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                group_angular_acceleration = (/v1, v2, v3/)

            case ('APERTURE_SIZE')
                read(value_str, *) aperture_size

            ! 根据离散状态选择对应算法分支。
            case ('APERTURE_CENTER')
                read(value_str, *) v1, v2, v3
                aperture_center = (/v1, v2, v3/)

            ! 根据离散状态选择对应算法分支。
            case ('APERTURE_NORMAL')
                read(value_str, *) v1, v2, v3
                aperture_normal = (/v1, v2, v3/)

            ! 根据离散状态选择对应算法分支。
            case ('APERTURE_UP')
                read(value_str, *) v1, v2, v3
                aperture_up_hint = (/v1, v2, v3/)

            ! 根据离散状态选择对应算法分支。
            case ('APERTURE_VELOCITY')
                read(value_str, *) v1, v2, v3
                aperture_velocity = (/v1, v2, v3/)
            ! 根据离散状态选择对应算法分支。
            case ('APERTURE_ACCELERATION')
                solver_status='UNSUPPORTED_LEGACY_FIELD'; solver_message='deprecated linear acceleration field: APERTURE_ACCELERATION'; ierr=1

            case ('APERTURE_ANGULAR_VELOCITY')
                read(value_str, *) v1, v2, v3
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                aperture_angular_velocity = (/v1, v2, v3/)

            case ('APERTURE_ANGULAR_ACCELERATION')
                read(value_str, *) v1, v2, v3
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                aperture_angular_acceleration = (/v1, v2, v3/)

            case ('APERTURE_TRACK_TARGET')
                read(value_str, *) aperture_track_target

            ! 根据离散状态选择对应算法分支。
            case ('DETECTOR_CENTER')
                read(value_str, *) v1, v2, v3
                detector_center = (/v1, v2, v3/)
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                detector_center_given = .true.

            case ('DETECTOR_NORMAL')
                read(value_str, *) v1, v2, v3
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                detector_normal = (/v1, v2, v3/)
                detector_normal_given = .true.

            case ('DETECTOR_UP')
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str, *) v1, v2, v3
                detector_up_hint = (/v1, v2, v3/)
                detector_up_given = .true.

            case ('DETECTOR_VELOCITY')
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str, *) v1, v2, v3
                detector_velocity = (/v1, v2, v3/)
            case ('DETECTOR_ACCELERATION')
                ! 检查并记录计算状态，使异常能够被上层流程识别。
                solver_status='UNSUPPORTED_LEGACY_FIELD'; solver_message='deprecated linear acceleration field: DETECTOR_ACCELERATION'; ierr=1

            case ('DETECTOR_ANGULAR_VELOCITY')
                read(value_str, *) v1, v2, v3
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                detector_angular_velocity = (/v1, v2, v3/)

            case ('DETECTOR_ANGULAR_ACCELERATION')
                read(value_str, *) v1, v2, v3
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                detector_angular_acceleration = (/v1, v2, v3/)

            case ('DETECTOR_TRACK_TARGET')
                read(value_str, *) detector_track_target

            ! 根据离散状态选择对应算法分支。
            case ('TRACK_TARGET_GROUP')
                read(value_str, *) detector_track_target
                aperture_track_target = detector_track_target

            ! 根据离散状态选择对应算法分支。
            case ('GRID_NX')
                read(value_str, *) nx

            case ('GRID_NY')
                read(value_str, *) ny

            ! 根据离散状态选择对应算法分支。
            case ('SPHERE_FILE')
                solver_status='INVALID_INPUT'; solver_message='SPHERE_FILE indirection is disabled; use the explicit single production input file'; ierr=1

!            case ('IMAGE_DISTANCE')
!                read(value_str, *) image_distance

            case ('SPOT_PLANE_SIZE')
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str, *) spot_plane_size

            case ('SPOT_FOCAL_LENGTH')
                read(value_str, *) spot_focal_length

            ! 根据离散状态选择对应算法分支。
            case ('SPOT_RADIUS_CELLS')
                read(value_str, *) spot_radius_cells

            case ('OUTPUT_FRAME_INTERVAL')
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str, *) output_frame_interval

            case ('SAVE_POINT_HISTORY')
                read(value_str, *) save_point_history

            ! 根据离散状态选择对应算法分支。
            case ('SAVE_FULL_SPOT_IMAGE')
                read(value_str, *) save_full_spot_image

            case ('SAVE_QA_FULL_IMAGE')
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str, *) save_qa_full_image

            case ('QA_IMAGE_FRAME_STRIDE')
                read(value_str, *) qa_image_frame_stride

            case ('QA_IMAGE_MAX_FRAMES')
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(value_str, *) qa_image_max_frames
            case ('NUM_SPHERES')
                continue
            ! 根据离散状态选择对应算法分支。
            case default
                solver_status='INVALID_INPUT'; solver_message='unknown production field: '//trim(key); ierr=1
            end select
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do

        close(uf)

        if (output_frame_interval < 1) output_frame_interval = 1
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (qa_image_frame_stride < 1) qa_image_frame_stride = 1
        if (qa_image_max_frames < 0) qa_image_max_frames = 0

        if (.not. detector_normal_given) detector_normal = aperture_normal
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. detector_up_given) detector_up_hint = aperture_up_hint
        if (.not. detector_center_given) detector_center = aperture_center + spot_focal_length * detector_normal

        if (.not. ieee_is_finite(time_step) .or. time_step <= 0.0d0) then
            ! 检查并记录计算状态，使异常能够被上层流程识别。
            solver_status='INVALID_TIME_STEP'; solver_message='TIME_STEP must be finite and positive'; ierr=1
        else if (.not. ieee_is_finite(total_time) .or. total_time <= 0.0d0) then
            solver_status='INVALID_INPUT'; solver_message='TOTAL_TIME must be finite and positive'; ierr=1
        else if (.not. ieee_is_finite(earth_mu) .or. earth_mu <= 0.0d0) then
            ! 检查并记录计算状态，使异常能够被上层流程识别。
            solver_status='INVALID_ORBITAL_INPUT'; solver_message='EARTH_MU must be finite and positive'; ierr=1
        else if (any(.not. ieee_is_finite(group_center)) .or. any(.not. ieee_is_finite(group_velocity)) .or. &
                 any(.not. ieee_is_finite(aperture_center)) .or. any(.not. ieee_is_finite(aperture_velocity)) .or. &
                 any(.not. ieee_is_finite(detector_center)) .or. any(.not. ieee_is_finite(detector_velocity))) then
            solver_status='INVALID_ORBITAL_INPUT'; solver_message='orbital Cartesian state must be finite'; ierr=1
        ! 当前条件不成立时执行替代计算路径。
        else if (sqrt(sum(group_center**2)) <= EPS .or. sqrt(sum(aperture_center**2)) <= EPS .or. &
                 sqrt(sum(detector_center**2)) <= EPS) then
            solver_status='INVALID_ORBITAL_INPUT'; solver_message='orbital position radius must be positive'; ierr=1
        else if (rays_per_sphere <= 0_8 .or. rays_solar <= 0_8 .or. max_bounces < 1) then
            ! 检查并记录计算状态，使异常能够被上层流程识别。
            solver_status='INVALID_INPUT'; solver_message='ray counts and MAX_BOUNCES must be positive'; ierr=1
        end if
        if (orbit_integrator_dt <= EPS) orbit_integrator_dt=time_step
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. ieee_is_finite(orbit_integrator_dt) .or. orbit_integrator_dt <= 0.0d0) then
            solver_status='INVALID_ORBITAL_INPUT'; solver_message='ORBIT_INTEGRATOR_DT must be finite and positive'; ierr=1
        end if
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (mc_mix_beta < 0.0d0 .or. mc_mix_beta > 1.0d0) then
            solver_status='INVALID_INPUT'; solver_message='MC_MIX_BETA must be within [0,1]'; ierr=1
        end if
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        dx=aperture_size/dble(nx); dy=aperture_size/dble(ny); cell_area=dx*dy
        write(*,'(A)') ' Production configuration loaded.'
        write(*,'(A,I12)') ' RANDOM_SEED input: ',random_seed_user
end subroutine read_input_file

    ! 定义 read_sphere_file 计算单元，封装该步骤的数据处理规则。
    subroutine read_sphere_file(filename, ierr)
        implicit none
        character(len=*), intent(in) :: filename
        ! 声明计数器、索引或离散控制参数。
        integer, intent(out) :: ierr

        integer :: uf, ios, id, line_number, comment_pos, eq_pos
        integer :: declared_count, record_count, token_count
        ! 声明计数器、索引或离散控制参数。
        integer :: declared_num_spheres
        character(len=1024) :: line, data_line
        character(len=256) :: key, value_str
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: x, y, z, r, rho, cp_, eps_ir, alpha_s, rho_ir, q_int, t_init
        real(8) :: vx, vy, vz, ax, ay, az, release_time_value
        real(8) :: numeric_values(18)
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=32) :: active_token
        logical :: active_value, active_parse_ok
        logical, allocatable :: id_seen(:)

        ! 检查并记录计算状态，使异常能够被上层流程识别。
        ierr = 0
        solver_status = 'INVALID_INPUT'
        solver_message = 'layered solver input contract failed'
        uf = 11

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=filename, status='old', action='read', iostat=ios)
        if (ios /= 0) then
            solver_message = 'cannot open explicit production input file: '//trim(filename)
            ! 检查并记录计算状态，使异常能够被上层流程识别。
            ierr = 1
            return
        end if

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        declared_count = 0
        declared_num_spheres = -1
        line_number = 0
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do
            read(uf, '(A)', iostat=ios) line
            if (ios /= 0) exit
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            line_number = line_number + 1
            comment_pos = index(line, '#')
            if (comment_pos > 0) line = line(1:comment_pos-1)
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            line = adjustl(trim(line))
            if (len_trim(line) == 0) cycle
            eq_pos = index(line, '=')
            if (eq_pos <= 0) cycle
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            key = adjustl(trim(line(1:eq_pos-1)))
            value_str = adjustl(trim(line(eq_pos+1:)))
            if (trim(key) /= 'NUM_SPHERES') cycle
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            declared_count = declared_count + 1
            read(value_str, *, iostat=ios) declared_num_spheres
            if (ios /= 0) then
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                write(solver_message,'(A,I0)') 'NUM_SPHERES is malformed at line ', line_number
                ierr = 1
                close(uf)
                ! 满足当前控制条件后结束或跳过本次处理。
                return
            end if
        end do

        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (declared_count /= 1) then
            write(solver_message,'(A,I0)') 'NUM_SPHERES must appear exactly once; occurrences=', declared_count
            ierr = 1
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            close(uf)
            return
        end if
        if (declared_num_spheres < 1) then
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(solver_message,'(A,I0)') 'NUM_SPHERES must be a positive integer; received ', declared_num_spheres
            ierr = 1
            close(uf)
            ! 满足当前控制条件后结束或跳过本次处理。
            return
        end if

        num_spheres = declared_num_spheres
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,I0)') ' Scenario target count: ', num_spheres

        allocate(sphere_centers(3, num_spheres))
        allocate(sphere_centers_initial(3, num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(sphere_velocity(3, num_spheres))
        allocate(sphere_acceleration(3, num_spheres))
        allocate(sphere_world_velocity(3, num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(sphere_release_time(num_spheres))
        allocate(sphere_is_active(num_spheres))
        allocate(companion_type(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(sphere_attitude_motion_type(num_spheres))
        allocate(sphere_micro_motion(3, num_spheres))
        allocate(sphere_motion_stage(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(sphere_orientation(3, num_spheres))
        allocate(sphere_angular_velocity(3, num_spheres))
        allocate(sphere_angular_acceleration(3, num_spheres))
        allocate(sphere_radius(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(sphere_density(num_spheres))
        allocate(sphere_specific_heat(num_spheres))
        allocate(sphere_ir_emissivity(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(sphere_solar_absorptivity(num_spheres))
        allocate(sphere_ir_reflectivity(num_spheres))
        allocate(sphere_internal_heat(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(sphere_initial_temp(num_spheres))
        allocate(id_seen(num_spheres))

        sphere_centers = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        sphere_centers_initial = 0.0d0
        sphere_velocity = 0.0d0
        sphere_acceleration = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        sphere_world_velocity = 0.0d0
        sphere_release_time = 0.0d0
        sphere_is_active = .false.
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        companion_type = companion_type_default
        sphere_attitude_motion_type = attitude_motion_type
        sphere_micro_motion = spread(micro_motion_params, 2, num_spheres)
        sphere_motion_stage = MEMBER_STAGE_INACTIVE
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        sphere_orientation = 0.0d0
        sphere_angular_velocity = 0.0d0
        sphere_angular_acceleration = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        sphere_radius = 0.0d0
        sphere_density = 0.0d0
        sphere_specific_heat = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        sphere_ir_emissivity = 0.0d0
        sphere_solar_absorptivity = 0.0d0
        sphere_ir_reflectivity = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        sphere_internal_heat = 0.0d0
        sphere_initial_temp = 0.0d0
        id_seen = .false.

        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        rewind(uf)
        record_count = 0
        line_number = 0
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do
            read(uf, '(A)', iostat=ios) line
            if (ios /= 0) exit
            line_number = line_number + 1
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            comment_pos = index(line, '#')
            if (comment_pos > 0) line = line(1:comment_pos-1)
            data_line = adjustl(trim(line))
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (len_trim(data_line) == 0) cycle
            if (index(data_line, '=') > 0) cycle

            token_count = count_data_tokens(data_line)
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (token_count /= 20) then
                write(solver_message,'(A,I0,A,I0,A)') 'sphere record at line ', line_number, &
                    ' must contain exactly 20 columns; received ', token_count, &
                    ' (ID x y z r rho cp eps_ir alpha_s rho_ir q_int T vx vy vz ax ay az release_time active)'
                ierr = 1
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                close(uf)
                return
            end if

            ! 按照接口约定读写数据文件，并维护文件单元状态。
            read(data_line, *, iostat=ios) id, x, y, z, r, rho, cp_, eps_ir, alpha_s, rho_ir, &
                q_int, t_init, vx, vy, vz, ax, ay, az, release_time_value, active_token
            if (ios /= 0) then
                write(solver_message,'(A,I0)') 'sphere record parse failure at line ', line_number
                ! 检查并记录计算状态，使异常能够被上层流程识别。
                ierr = 1
                close(uf)
                return
            end if

            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (id < 1 .or. id > num_spheres) then
                write(solver_message,'(A,I0,A,I0)') 'sphere ID out of range at line ', line_number, ': ', id
                ierr = 1
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                close(uf)
                return
            end if
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (id_seen(id)) then
                write(solver_message,'(A,I0,A,I0)') 'duplicate sphere ID at line ', line_number, ': ', id
                ierr = 1
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                close(uf)
                return
            end if

            ! 调用子过程完成当前数值计算或状态更新。
            call parse_strict_logical_token(active_token, active_value, active_parse_ok)
            if (.not. active_parse_ok) then
                write(solver_message,'(A,I0,A,A)') 'invalid explicit active token at line ', line_number, ': ', trim(active_token)
                ! 检查并记录计算状态，使异常能够被上层流程识别。
                ierr = 1
                close(uf)
                return
            end if

            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            numeric_values = (/x, y, z, r, rho, cp_, eps_ir, alpha_s, rho_ir, q_int, t_init, &
                               vx, vy, vz, ax, ay, az, release_time_value/)
            if (any(.not. ieee_is_finite(numeric_values))) then
                write(solver_message,'(A,I0,A,I0)') 'nonfinite sphere value at line ', line_number, ', ID=', id
                ! 检查并记录计算状态，使异常能够被上层流程识别。
                ierr = 1
                close(uf)
                return
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
            if (r <= 0.0d0 .or. rho <= 0.0d0 .or. cp_ <= 0.0d0 .or. t_init <= 0.0d0) then
                write(solver_message,'(A,I0)') 'radius, density, specific heat, and initial temperature must be positive for ID=', id
                ! 检查并记录计算状态，使异常能够被上层流程识别。
                ierr = 1
                close(uf)
                return
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
            if (release_time_value < 0.0d0) then
                write(solver_message,'(A,I0,A,ES14.6)') 'release_time must be nonnegative for ID=', id, ': ', release_time_value
                ! 检查并记录计算状态，使异常能够被上层流程识别。
                ierr = 1
                close(uf)
                return
            end if

            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            sphere_centers(:,id) = group_center + (/x, y, z/)
            sphere_radius(id) = r
            sphere_density(id) = rho
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            sphere_specific_heat(id) = cp_
            sphere_ir_emissivity(id) = eps_ir
            sphere_solar_absorptivity(id) = alpha_s
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            sphere_ir_reflectivity(id) = rho_ir
            sphere_internal_heat(id) = q_int
            sphere_initial_temp(id) = t_init
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            sphere_velocity(:,id) = (/vx, vy, vz/)
            sphere_acceleration(:,id) = (/ax, ay, az/)
            sphere_release_time(id) = release_time_value
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            sphere_is_active(id) = active_value
            companion_type(id) = companion_type_default
            sphere_attitude_motion_type(id) = attitude_motion_type
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            sphere_micro_motion(:,id) = micro_motion_params

            id_seen(id) = .true.
            record_count = record_count + 1
        end do
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)

        if (record_count /= num_spheres .or. any(.not. id_seen)) then
            write(solver_message,'(A,I0,A,I0)') 'exactly ', num_spheres, &
                ' unique sphere records are required; received ', record_count
            ! 检查并记录计算状态，使异常能够被上层流程识别。
            ierr = 1
            return
        end if

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        sphere_centers_initial = sphere_centers - spread(group_center, 2, num_spheres)

        ! Model-level invariant retained by rule B: sphere 1 is the carrier/
        ! reference thermal state. The remaining target count, active flags,
        ! release times, positions, velocities, and accelerations are scenario
        ! parameters and are intentionally not hard-coded here.
        if (.not. sphere_is_active(1)) then
            solver_message = 'sphere 1 must be active because rule B uses it as the release-temperature source'
            ! 检查并记录计算状态，使异常能够被上层流程识别。
            ierr = 1
            return
        end if
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (abs(sphere_release_time(1)) > PRIMARY_RELEASE_TOL) then
            write(solver_message,'(A,ES14.6)') &
                'sphere 1 release_time must be 0 for the rule-B carrier/reference state; received ', &
                sphere_release_time(1)
            ierr = 1
            ! 满足当前控制条件后结束或跳过本次处理。
            return
        end if

        deallocate(id_seen)
        write(*,'(A)') ' Layered solver input contract: PASS'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,I0,A)') '   Structure: ', num_spheres, ' complete 20-column records; IDs unique and cover 1..NUM_SPHERES'
        write(*,'(A)') '   Physical validity: finite values; positive r/rho/cp/T; nonnegative release_time; explicit active'
        write(*,'(A)') '   Scenario-free inputs: target count, active flags (except sphere 1), release schedule, offsets, v, a, and properties'
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine read_sphere_file

!============================ Allocation ======================================
!数组分配
    subroutine allocate_arrays()
        allocate(sphere_area(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(sphere_volume(num_spheres))
        allocate(sphere_mass(num_spheres))
        allocate(sphere_thermal_capacity(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(sphere_distances(num_spheres))
        allocate(sphere_solar_reflectivity(num_spheres))

        allocate(sphere_temperature(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(sphere_temperature_old(num_spheres))
        allocate(sphere_power(num_spheres))
        allocate(sphere_solar_heat(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(solar_heat_direct(num_spheres))
        allocate(solar_heat_indirect(num_spheres))
        allocate(sphere_radiation_to_space(num_spheres))
        allocate(sphere_net_exchange(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(sphere_radiation_from_environment(num_spheres))
        allocate(sphere_radiation_to_aperture(num_spheres))
        allocate(sphere_heat_exchange(num_spheres, num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(sphere_view_factor(num_spheres, num_spheres))
        allocate(sphere_view_factor_to_space(num_spheres))

        allocate(node_u(nx+1))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(node_v(ny+1))
        allocate(cell_u(nx))
        allocate(cell_v(ny))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(cell_global_x(nx, ny))
        allocate(cell_global_y(nx, ny))
        allocate(cell_global_z(nx, ny))

        ! 按当前问题规模分配或释放数组存储空间。
        allocate(grid_direct_factor(nx, ny, num_spheres))
        allocate(grid_indirect_factor(nx, ny, num_spheres))
        allocate(grid_direct(nx, ny, num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(grid_direct_total(nx, ny))
        allocate(grid_indirect(nx, ny, num_spheres))
        allocate(grid_indirect_total(nx, ny))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(grid_total(nx, ny))
        allocate(grid_irradiance(nx, ny))

        allocate(rays_emitted(num_spheres))
        allocate(rays_direct_hit(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(rays_indirect_hit(num_spheres))
        allocate(rays_escaped_source(num_spheres))
        allocate(rays_terminated_on_sphere(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(rays_self_absorbed(num_spheres))
        
        allocate(wt_emitted(num_spheres))
        allocate(wt_pupil_cross(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(wt_pupil_grid(num_spheres))
        allocate(wt_pupil_direct(num_spheres))
        allocate(wt_pupil_indirect(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(wt_sphere_term(num_spheres))
        allocate(wt_self_absorb(num_spheres))
        allocate(wt_space_escape(num_spheres)) 

        ! 按当前问题规模分配或释放数组存储空间。
        allocate(power_direct_factor(num_spheres))
        allocate(power_indirect_factor(num_spheres))
        allocate(power_direct(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(power_indirect(num_spheres))

        allocate(cone_axis(3, num_spheres))
        allocate(cone_cos_theta_max(num_spheres))
        allocate(cone_prob(num_spheres))

        ! 按当前问题规模分配或释放数组存储空间。
        allocate(spot_total(nx,ny))
        allocate(spot_irradiance(nx,ny))

        allocate(vf_energy_s2s(num_spheres,num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(vf_energy_s2space(num_spheres))
    end subroutine

!数组释放
    subroutine deallocate_arrays()
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(sphere_centers)) deallocate(sphere_centers)
        if (allocated(sphere_centers_initial)) deallocate(sphere_centers_initial)
        if (allocated(sphere_velocity)) deallocate(sphere_velocity)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(sphere_acceleration)) deallocate(sphere_acceleration)
        if (allocated(sphere_world_velocity)) deallocate(sphere_world_velocity)
        if (allocated(sphere_release_time)) deallocate(sphere_release_time)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(sphere_is_active)) deallocate(sphere_is_active)
        if (allocated(companion_type)) deallocate(companion_type)
        if (allocated(sphere_attitude_motion_type)) deallocate(sphere_attitude_motion_type)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(sphere_micro_motion)) deallocate(sphere_micro_motion)
        if (allocated(sphere_motion_stage)) deallocate(sphere_motion_stage)
        if (allocated(sphere_orientation)) deallocate(sphere_orientation)
        if (allocated(sphere_angular_velocity)) deallocate(sphere_angular_velocity)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(sphere_angular_acceleration)) deallocate(sphere_angular_acceleration)
        if (allocated(sphere_radius)) deallocate(sphere_radius)
        if (allocated(sphere_density)) deallocate(sphere_density)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(sphere_specific_heat)) deallocate(sphere_specific_heat)
        if (allocated(sphere_ir_emissivity)) deallocate(sphere_ir_emissivity)
        if (allocated(sphere_solar_absorptivity)) deallocate(sphere_solar_absorptivity)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(sphere_ir_reflectivity)) deallocate(sphere_ir_reflectivity)
        if (allocated(sphere_solar_reflectivity)) deallocate(sphere_solar_reflectivity)
        if (allocated(sphere_internal_heat)) deallocate(sphere_internal_heat)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(sphere_initial_temp)) deallocate(sphere_initial_temp)
        if (allocated(sphere_area)) deallocate(sphere_area)
        if (allocated(sphere_volume)) deallocate(sphere_volume)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(sphere_mass)) deallocate(sphere_mass)
        if (allocated(sphere_thermal_capacity)) deallocate(sphere_thermal_capacity)
        if (allocated(sphere_distances)) deallocate(sphere_distances)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(sphere_temperature)) deallocate(sphere_temperature)
        if (allocated(sphere_temperature_old)) deallocate(sphere_temperature_old)
        if (allocated(sphere_power)) deallocate(sphere_power)
        if (allocated(sphere_solar_heat)) deallocate(sphere_solar_heat)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(solar_heat_direct)) deallocate(solar_heat_direct)
        if (allocated(solar_heat_indirect)) deallocate(solar_heat_indirect)
        if (allocated(sphere_radiation_to_space)) deallocate(sphere_radiation_to_space)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(sphere_net_exchange)) deallocate(sphere_net_exchange)
        if (allocated(sphere_radiation_from_environment)) deallocate(sphere_radiation_from_environment)
        if (allocated(sphere_radiation_to_aperture)) deallocate(sphere_radiation_to_aperture)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(sphere_heat_exchange)) deallocate(sphere_heat_exchange)
        if (allocated(sphere_view_factor)) deallocate(sphere_view_factor)
        if (allocated(sphere_view_factor_to_space)) deallocate(sphere_view_factor_to_space)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(node_u)) deallocate(node_u)
        if (allocated(node_v)) deallocate(node_v)
        if (allocated(cell_u)) deallocate(cell_u)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(cell_v)) deallocate(cell_v)
        if (allocated(cell_global_x)) deallocate(cell_global_x)
        if (allocated(cell_global_y)) deallocate(cell_global_y)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(cell_global_z)) deallocate(cell_global_z)
        if (allocated(grid_direct_factor)) deallocate(grid_direct_factor)
        if (allocated(grid_indirect_factor)) deallocate(grid_indirect_factor)
        if (allocated(grid_direct)) deallocate(grid_direct)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(grid_direct_total)) deallocate(grid_direct_total)
        if (allocated(grid_indirect)) deallocate(grid_indirect)
        if (allocated(grid_indirect_total)) deallocate(grid_indirect_total)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(grid_total)) deallocate(grid_total)
        if (allocated(grid_irradiance)) deallocate(grid_irradiance)
        if (allocated(rays_emitted)) deallocate(rays_emitted)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(rays_direct_hit)) deallocate(rays_direct_hit)
        if (allocated(rays_indirect_hit)) deallocate(rays_indirect_hit)
        if (allocated(rays_escaped_source)) deallocate(rays_escaped_source)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(rays_terminated_on_sphere)) deallocate(rays_terminated_on_sphere)
        if (allocated(rays_self_absorbed)) deallocate(rays_self_absorbed)

        if (allocated(wt_emitted)) deallocate(wt_emitted)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(wt_pupil_cross)) deallocate(wt_pupil_cross)
        if (allocated(wt_pupil_grid)) deallocate(wt_pupil_grid)
        if (allocated(wt_pupil_direct)) deallocate(wt_pupil_direct)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(wt_pupil_indirect)) deallocate(wt_pupil_indirect)
        if (allocated(wt_sphere_term)) deallocate(wt_sphere_term)
        if (allocated(wt_self_absorb)) deallocate(wt_self_absorb)
        if (allocated(wt_space_escape)) deallocate(wt_space_escape)    
        
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(power_direct_factor)) deallocate(power_direct_factor)
        if (allocated(power_indirect_factor)) deallocate(power_indirect_factor)
        if (allocated(power_direct)) deallocate(power_direct)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(power_indirect)) deallocate(power_indirect)
        if (allocated(cone_axis)) deallocate(cone_axis)
        if (allocated(cone_cos_theta_max)) deallocate(cone_cos_theta_max)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(cone_prob)) deallocate(cone_prob)
        if (allocated(spot_total)) deallocate(spot_total)
        if (allocated(spot_irradiance)) deallocate(spot_irradiance)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (allocated(vf_energy_s2s)) deallocate(vf_energy_s2s)
        if (allocated(vf_energy_s2space)) deallocate(vf_energy_s2space)
    end subroutine

!======================= Geometry / Cones / Grid ==============================

    ! 定义 initialize_sphere_properties 计算单元，封装该步骤的数据处理规则。
    subroutine initialize_sphere_properties()
        integer :: i
        real(8) :: sphere_inner_radius
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do i = 1, num_spheres
            sphere_area(i) = 4.0d0 * PI * sphere_radius(i)**2
            sphere_inner_radius = sphere_radius(i) - TARGET_SHELL_THICKNESS
            sphere_volume(i) = (4.0d0/3.0d0) * PI * &
                (sphere_radius(i)**3 - sphere_inner_radius**3)
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            sphere_mass(i) = sphere_density(i) * sphere_volume(i)
            sphere_thermal_capacity(i) = sphere_mass(i) * sphere_specific_heat(i)
            ! 计算太阳反射率
            sphere_solar_reflectivity(i) = 1.0d0 - sphere_solar_absorptivity(i)
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do
    end subroutine

    subroutine validate_ir_property_contract(validation_ierr)
        ! 声明计数器、索引或离散控制参数。
        integer, intent(out) :: validation_ierr
        integer :: i
        real(8), parameter :: PROPERTY_TOL = 1.0d-8

        ! 检查并记录计算状态，使异常能够被上层流程识别。
        validation_ierr = 0
        do i = 1, num_spheres
            if (sphere_ir_emissivity(i) < 0.0d0 .or. sphere_ir_emissivity(i) > 1.0d0) then
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                write(*,'(A,I0,A,ES14.6)') ' ERROR IR_PROPERTY_CONTRACT sphere ', i, &
                    ': epsilon_ir out of [0,1]: ', sphere_ir_emissivity(i)
                validation_ierr = 1
            end if
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (sphere_ir_reflectivity(i) < 0.0d0 .or. sphere_ir_reflectivity(i) > 1.0d0) then
                write(*,'(A,I0,A,ES14.6)') ' ERROR IR_PROPERTY_CONTRACT sphere ', i, &
                    ': rho_ir out of [0,1]: ', sphere_ir_reflectivity(i)
                validation_ierr = 1
            end if
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (abs(sphere_ir_emissivity(i) + sphere_ir_reflectivity(i) - 1.0d0) > PROPERTY_TOL) then
                write(*,'(A,I0,A,2(ES14.6,1X))') ' ERROR IR_PROPERTY_CONTRACT sphere ', i, &
                    ': opaque gray body requires epsilon_ir + rho_ir = 1; values=', &
                    sphere_ir_emissivity(i), sphere_ir_reflectivity(i)
                validation_ierr = 1
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
            if (sphere_solar_absorptivity(i) < 0.0d0 .or. sphere_solar_absorptivity(i) > 1.0d0) then
                write(*,'(A,I0,A,ES14.6)') ' ERROR IR_PROPERTY_CONTRACT sphere ', i, &
                    ': alpha_solar out of [0,1]: ', sphere_solar_absorptivity(i)
                ! 检查并记录计算状态，使异常能够被上层流程识别。
                validation_ierr = 1
            end if
        end do
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine validate_ir_property_contract

    subroutine normalize_vector(v)
        real(8), intent(inout) :: v(3)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: mag
        mag = sqrt(sum(v**2))
        if (mag > EPS) v = v / mag
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine

    subroutine build_local_coordinate_system(normal_vec, up_hint_vec, u_vec, v_vec)
        real(8), intent(in) :: normal_vec(3), up_hint_vec(3)
        real(8), intent(out) :: u_vec(3), v_vec(3)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: temp(3)

        temp = up_hint_vec - sum(up_hint_vec * normal_vec) * normal_vec
        if (sqrt(sum(temp**2)) <= EPS) then
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (abs(normal_vec(1)) < 0.9d0) then
                temp = (/1.0d0, 0.0d0, 0.0d0/)
            else
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                temp = (/0.0d0, 1.0d0, 0.0d0/)
            end if
        end if

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        u_vec = temp - sum(temp * normal_vec) * normal_vec
        call normalize_vector(u_vec)
        call cross_product(normal_vec, u_vec, v_vec)
        ! 调用子过程完成当前数值计算或状态更新。
        call normalize_vector(v_vec)
    end subroutine build_local_coordinate_system

    subroutine point_axis_to_target(center, target, up_hint_vec, normal_vec)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(in) :: center(3), target(3)
        real(8), intent(inout) :: up_hint_vec(3), normal_vec(3)
        real(8) :: los_vec(3)

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        los_vec = target - center
        if (sqrt(sum(los_vec**2)) <= EPS) return

        normal_vec = los_vec
        call normalize_vector(normal_vec)

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        up_hint_vec = up_hint_vec - sum(up_hint_vec * normal_vec) * normal_vec
        if (sqrt(sum(up_hint_vec**2)) <= EPS) then
            if (abs(normal_vec(1)) < 0.9d0) then
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                up_hint_vec = (/1.0d0, 0.0d0, 0.0d0/)
            else
                up_hint_vec = (/0.0d0, 1.0d0, 0.0d0/)
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
            up_hint_vec = up_hint_vec - sum(up_hint_vec * normal_vec) * normal_vec
        end if
        ! 调用子过程完成当前数值计算或状态更新。
        call normalize_vector(up_hint_vec)
    end subroutine point_axis_to_target

    subroutine setup_group_coordinate_system()
        ! 调用子过程完成当前数值计算或状态更新。
        call build_local_coordinate_system(group_normal, group_up_hint, group_u, group_v)
    end subroutine setup_group_coordinate_system

    subroutine setup_aperture_coordinate_system()
        ! 调用子过程完成当前数值计算或状态更新。
        call build_local_coordinate_system(aperture_normal, aperture_up_hint, aperture_u, aperture_v)
    end subroutine

    subroutine setup_detector_coordinate_system()
        call build_local_coordinate_system(detector_normal, detector_up_hint, detector_u, detector_v)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine setup_detector_coordinate_system

    subroutine setup_grid_coordinates()
        integer :: ix, iy
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: u_loc, v_loc

        image_center = aperture_center

        do ix = 1, nx+1
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            node_u(ix) = -aperture_size/2.0d0 + (ix-1)*dx
        end do
        do iy = 1, ny+1
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            node_v(iy) = -aperture_size/2.0d0 + (iy-1)*dy
        end do
        do ix = 1, nx
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            cell_u(ix) = 0.5d0*(node_u(ix)+node_u(ix+1))
        end do
        do iy = 1, ny
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            cell_v(iy) = 0.5d0*(node_v(iy)+node_v(iy+1))
        end do

        do iy = 1, ny
            do ix = 1, nx
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                u_loc = cell_u(ix); v_loc = cell_v(iy)
                cell_global_x(ix,iy) = aperture_center(1) + u_loc*aperture_u(1) + v_loc*aperture_v(1)
                cell_global_y(ix,iy) = aperture_center(2) + u_loc*aperture_u(2) + v_loc*aperture_v(2)
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                cell_global_z(ix,iy) = aperture_center(3) + u_loc*aperture_u(3) + v_loc*aperture_v(3)
            end do
        end do
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine setup_grid_coordinates

    subroutine calculate_sphere_distances()
        integer :: i
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: d(3)
        do i = 1, num_spheres
            d = sphere_centers(:,i) - aperture_center
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            sphere_distances(i) = sqrt(sum(d**2))
        end do
    end subroutine

    ! 定义 setup_aperture_cones 计算单元，封装该步骤的数据处理规则。
    subroutine setup_aperture_cones()
        integer :: s, ic
        integer :: uf_dbg
        real(8) :: d(3), R, axis(3)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: corner(3,4), vc(3)
        real(8) :: cosang, cos_theta_max, omega_cap, dotna, theta_max, delta_theta

        corner(:,1) = aperture_center + 0.5d0*aperture_size*( aperture_u + aperture_v )
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        corner(:,2) = aperture_center + 0.5d0*aperture_size*( aperture_u - aperture_v )
        corner(:,3) = aperture_center + 0.5d0*aperture_size*( -aperture_u + aperture_v )
        corner(:,4) = aperture_center + 0.5d0*aperture_size*( -aperture_u - aperture_v )

        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do s = 1, num_spheres
            if (.not. participates_in_ir_source(s)) then
                cone_axis(:,s) = aperture_normal
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                cone_cos_theta_max(s) = 1.0d0
                cone_prob(s) = 0.0d0
                cycle
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if

            d = aperture_center - sphere_centers(:,s)
            R = sqrt(sum(d**2))
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (R <= EPS) then
                cone_axis(:,s)        = aperture_normal
                cone_cos_theta_max(s) = 1.0d0
                cone_prob(s)          = 0.0d0
                ! 满足当前控制条件后结束或跳过本次处理。
                cycle
            end if

            axis = d / R
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            cone_axis(:,s) = axis

            dotna = sum(axis * aperture_normal)
            if (dotna >= 0.0d0) then
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                cone_cos_theta_max(s) = 1.0d0
                cone_prob(s)          = 0.0d0
                cycle
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if

            cos_theta_max = 1.0d0
            do ic = 1,4
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                vc = corner(:,ic) - sphere_centers(:,s)
                vc = vc / sqrt(sum(vc**2))
                cosang = sum(axis * vc)
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if (cosang < cos_theta_max) cos_theta_max = cosang
            end do

            ! Expand the cap slightly because rays are emitted from the sphere surface,
            ! not the center. Without this padding, rare valid aperture hits can fall
            ! just outside the cap and receive an oversized isotropic weight.
            theta_max = acos(max(-1.0d0, min(1.0d0, cos_theta_max)))
            delta_theta = asin(min(1.0d0, sphere_radius(s) / R))
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            theta_max = min(PI, theta_max + delta_theta)
            cos_theta_max = cos(theta_max)

            cos_theta_max = max(-1.0d0, min(1.0d0, cos_theta_max))
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            omega_cap = 2.0d0 * PI * (1.0d0 - cos_theta_max)
            cone_cos_theta_max(s) = cos_theta_max
            cone_prob(s)          = omega_cap / (4.0d0 * PI)
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do

        uf_dbg = 99
        if (WRITE_DEBUG_OUTPUTS) then
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            open(unit=uf_dbg, file=output_path('mc_debug.log'), status='replace')
            write(uf_dbg,'(A)') '=== setup_aperture_cones ==='
            write(uf_dbg,'(A)') 's    P_cap (Omega/4pi)      cos_theta_max'
            ! 进入迭代计算，逐项更新仿真状态或累计量。
            do s = 1, num_spheres
                write(uf_dbg,'(I3,2(1X,ES14.6))') s, cone_prob(s), cone_cos_theta_max(s)
            end do
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf_dbg,*)
            close(uf_dbg)
        end if
    end subroutine setup_aperture_cones

!===================== Solar Monte Carlo ======================================

    ! 定义 validate_solar_contract 计算单元，封装该步骤的数据处理规则。
    subroutine validate_solar_contract(validation_ierr)
        integer, intent(out) :: validation_ierr
        real(8) :: direction_norm

        ! 检查并记录计算状态，使异常能够被上层流程识别。
        validation_ierr = 0
        if (solar_flux < 0.0d0) then
            write(*,'(A,ES14.6)') ' ERROR SOLAR_INPUT_CONTRACT: negative SOLAR_FLUX: ', solar_flux
            ! 检查并记录计算状态，使异常能够被上层流程识别。
            validation_ierr = 1
        end if
        if (solar_flux > 0.0d0 .and. rays_solar <= 0_8) then
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(*,'(A,I0)') ' ERROR SOLAR_INPUT_CONTRACT: RAYS_SOLAR must be positive: ', rays_solar
            validation_ierr = 1
        end if
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        direction_norm = sqrt(sum(solar_direction**2))
        if (solar_flux > 0.0d0 .and. direction_norm <= EPS) then
            write(*,'(A)') ' ERROR SOLAR_INPUT_CONTRACT: SOLAR_DIRECTION has zero norm'
            ! 检查并记录计算状态，使异常能够被上层流程识别。
            validation_ierr = 1
        end if
        if (validation_ierr == 0 .and. direction_norm > EPS) solar_direction = solar_direction / direction_norm
    end subroutine validate_solar_contract

    ! 定义 calculate_solar_heat_dispatch 计算单元，封装该步骤的数据处理规则。
    subroutine calculate_solar_heat_dispatch()
        integer :: uf
        integer(8), allocatable :: zero_hits(:)

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        sphere_solar_heat = 0.0d0
        solar_heat_direct = 0.0d0
        solar_heat_indirect = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        solar_initial_power = 0.0d0
        solar_escape_power = 0.0d0
        solar_truncation_residual = 0.0d0
        ! 检查并记录计算状态，使异常能够被上层流程识别。
        solar_closure_error = 0.0d0
        solar_valid_first_hits = 0_8
        solar_empty_rays = 0_8
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        solar_reflection_events = 0_8
        solar_paths_with_reflection = 0_8
        solar_paths_with_two_plus_reflections = 0_8
        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        solar_paths_returned_to_first = 0_8
        solar_reflected_escape_rays = 0_8
        solar_max_reflections_observed = 0

        if (solar_flux > 0.0d0) then
            ! 调用子过程完成当前数值计算或状态更新。
            call calculate_solar_heat_mc()
        else
            allocate(zero_hits(num_spheres))
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            zero_hits = 0_8
            call write_solar_statistics(zero_hits, zero_hits, 0.0d0)
            deallocate(zero_hits)
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
        uf = 46
        if (WRITE_QA_DIAGNOSTICS) then
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            open(unit=uf, file=output_path('solar_model_contract.txt'), status='replace')
        else
            open(unit=uf, status='scratch')
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
        write(uf,'(A)') 'production_path=TARGETED_MONTE_CARLO'
        write(uf,'(A,L1)') 'solar_tracing_enabled=', solar_flux > 0.0d0
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)') 'total_absorbed_power_W=', sum(sphere_solar_heat)
        write(uf,'(A,ES24.16)') 'closure_error=', solar_closure_error
        close(uf)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine calculate_solar_heat_dispatch

    subroutine calculate_solar_heat_mc()
        implicit none
        integer(8) :: i_ray
        ! 声明计数器、索引或离散控制参数。
        integer :: i, j, selected, multiplicity, hit_sphere, last_hit_sphere, first_hit_sphere
        integer :: active_count, reflection_count
        real(8) :: ray_origin(3), ray_dir(3), new_dir(3)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: hit_point(3), hit_normal(3), t_hit
        real(8) :: e1(3), e2(3), temp(3), reference_center(3), plane_point(3)
        real(8) :: u1, u2, selector, radial, theta, px, py, dx2, dy2
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: total_disk_area, upstream_coordinate, safety_padding, max_radius
        real(8) :: ray_energy, initial_ray_energy, absorbed_power, reflected_power
        real(8) :: closure_scale, tolerance_radius
        ! 声明逻辑开关，用于控制对应计算或输出路径。
        logical :: hit_any, returned_to_first

        integer(8), allocatable :: solar_hits_direct(:), solar_hits_indirect(:)
        integer, allocatable :: active_ids(:)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), allocatable :: disk_x(:), disk_y(:), disk_radius(:), disk_area(:), cumulative_area(:)
        real(8), parameter :: SOLAR_DISK_TOL = 1.0d-10
        real(8), parameter :: SOLAR_MIN_WEIGHT_FRACTION = 1.0d-12

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,*)
        write(*,'(A)') ' ============================================================'
        write(*,'(A)') '   Solar Monte Carlo (with shadowing & multiple reflections)'
        write(*,'(A)') ' ============================================================'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,*)
        write(*,'(A,I12)')     '   Rays:        ', rays_solar
        write(*,'(A,I6)')      '   Max bounces: ', max_bounces
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,F10.1,A)') '   Solar flux:  ', solar_flux, ' W/m^2'
        write(*,*)

        allocate(solar_hits_direct(num_spheres))
        ! 按当前问题规模分配或释放数组存储空间。
        allocate(solar_hits_indirect(num_spheres))
        allocate(active_ids(num_spheres), disk_x(num_spheres), disk_y(num_spheres))
        allocate(disk_radius(num_spheres), disk_area(num_spheres), cumulative_area(num_spheres))
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        solar_hits_direct = 0_8
        solar_hits_indirect = 0_8

        active_count = 0
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do i = 1, num_spheres
            if (.not. participates_in_solar(i)) cycle
            if (sphere_radius(i) <= 0.0d0) cycle
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            active_count = active_count + 1
            active_ids(active_count) = i
        end do
        if (active_count == 0) then
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(*,'(A)') '   No positive-radius active sphere; solar power is zero.'
            call write_solar_statistics(solar_hits_direct, solar_hits_indirect, 0.0d0)
            deallocate(solar_hits_direct, solar_hits_indirect, active_ids, disk_x, disk_y)
            ! 按当前问题规模分配或释放数组存储空间。
            deallocate(disk_radius, disk_area, cumulative_area)
            return
        end if

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        reference_center = sphere_centers(:,active_ids(1))
        if (abs(solar_direction(1)) <= abs(solar_direction(2)) .and. &
            abs(solar_direction(1)) <= abs(solar_direction(3))) then
            temp = (/1.0d0, 0.0d0, 0.0d0/)
        ! 当前条件不成立时执行替代计算路径。
        else if (abs(solar_direction(2)) <= abs(solar_direction(3))) then
            temp = (/0.0d0, 1.0d0, 0.0d0/)
        else
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            temp = (/0.0d0, 0.0d0, 1.0d0/)
        end if
        e1 = temp - sum(temp * solar_direction) * solar_direction
        ! 调用子过程完成当前数值计算或状态更新。
        call normalize_vector(e1)
        call cross_product(solar_direction, e1, e2)
        call normalize_vector(e2)

        total_disk_area = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        max_radius = 0.0d0
        upstream_coordinate = huge(1.0d0)
        do i = 1, active_count
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            j = active_ids(i)
            disk_x(i) = sum((sphere_centers(:,j) - reference_center) * e1)
            disk_y(i) = sum((sphere_centers(:,j) - reference_center) * e2)
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            disk_radius(i) = sphere_radius(j)
            disk_area(i) = PI * disk_radius(i)**2
            total_disk_area = total_disk_area + disk_area(i)
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            cumulative_area(i) = total_disk_area
            max_radius = max(max_radius, disk_radius(i))
            upstream_coordinate = min(upstream_coordinate, sum(sphere_centers(:,j)*solar_direction)-disk_radius(i))
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do
        safety_padding = max(1.0d-9, 1.0d-6 * max_radius)
        upstream_coordinate = upstream_coordinate - safety_padding

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,I0)') '   Active projected disks: ', active_count
        write(*,'(A,ES12.4,A)') '   Sum projected disk area: ', total_disk_area, ' m^2'
        write(*,'(A,ES12.4,A)') '   Nominal ray weight: ', solar_flux*total_disk_area/dble(rays_solar), ' W'
        write(*,*)

        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do i_ray = 1_8, rays_solar

            if (mod(i_ray, REPORT_INTERVAL) == 0_8) then
                write(*,'(A,F6.1,A)') '    Solar MC Progress: ', &
                    100.0d0 * dble(i_ray) / dble(rays_solar), '%'
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if

            call random_number(selector)
            selector = selector * total_disk_area
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            selected = active_count
            do i = 1, active_count
                if (selector < cumulative_area(i)) then
                    ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                    selected = i
                    exit
                end if
            ! 结束本轮迭代范围，继续处理汇总后的计算结果。
            end do
            call random_number(u1)
            call random_number(u2)
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            radial = disk_radius(selected) * sqrt(u1)
            theta = 2.0d0 * PI * u2
            px = disk_x(selected) + radial*cos(theta)
            py = disk_y(selected) + radial*sin(theta)

            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            multiplicity = 0
            do i = 1, active_count
                dx2 = px - disk_x(i)
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                dy2 = py - disk_y(i)
                tolerance_radius = disk_radius(i) + SOLAR_DISK_TOL
                if (dx2*dx2 + dy2*dy2 <= tolerance_radius*tolerance_radius) multiplicity = multiplicity + 1
            ! 结束本轮迭代范围，继续处理汇总后的计算结果。
            end do
            if (multiplicity < 1) then
                write(*,'(A,I0)') ' ERROR SOLAR_SAMPLING_MULTIPLICITY_ZERO at ray ', i_ray
                ! 检查并记录计算状态，使异常能够被上层流程识别。
                error stop 1
            end if

            initial_ray_energy = solar_flux * total_disk_area / &
                                 (dble(rays_solar) * dble(multiplicity))
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            solar_initial_power = solar_initial_power + initial_ray_energy
            ray_energy = initial_ray_energy
            plane_point = reference_center + px*e1 + py*e2
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            ray_origin = plane_point + (upstream_coordinate-sum(plane_point*solar_direction))*solar_direction
            ray_dir = solar_direction
            last_hit_sphere = -1
            first_hit_sphere = -1
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            reflection_count = 0
            returned_to_first = .false.

            do
                ! 调用子过程完成当前数值计算或状态更新。
                call find_nearest_sphere_hit_solar(ray_origin, ray_dir, last_hit_sphere, &
                                                   hit_sphere, hit_any, t_hit, hit_point, hit_normal)

                if (.not. hit_any) then
                    solar_escape_power = solar_escape_power + ray_energy
                    ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                    if (reflection_count == 0) solar_empty_rays = solar_empty_rays + 1_8
                    if (reflection_count > 0) solar_reflected_escape_rays = solar_reflected_escape_rays + 1_8
                    exit
                ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
                end if

                if (reflection_count == 0) then
                    first_hit_sphere = hit_sphere
                    ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                    solar_valid_first_hits = solar_valid_first_hits + 1_8
                    solar_hits_direct(hit_sphere) = solar_hits_direct(hit_sphere) + 1_8
                else
                    ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                    solar_hits_indirect(hit_sphere) = solar_hits_indirect(hit_sphere) + 1_8
                    if (hit_sphere == first_hit_sphere .and. .not. returned_to_first) then
                        solar_paths_returned_to_first = solar_paths_returned_to_first + 1_8
                        returned_to_first = .true.
                    ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
                    end if
                end if

                absorbed_power = sphere_solar_absorptivity(hit_sphere) * ray_energy
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                reflected_power = sphere_solar_reflectivity(hit_sphere) * ray_energy
                if (reflection_count == 0) then
                    solar_heat_direct(hit_sphere) = solar_heat_direct(hit_sphere) + absorbed_power
                ! 当前条件不成立时执行替代计算路径。
                else
                    solar_heat_indirect(hit_sphere) = solar_heat_indirect(hit_sphere) + absorbed_power
                end if
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                sphere_solar_heat(hit_sphere) = sphere_solar_heat(hit_sphere) + absorbed_power
                ray_energy = reflected_power

                if (ray_energy <= max(tiny(1.0d0), initial_ray_energy*SOLAR_MIN_WEIGHT_FRACTION)) then
                    ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                    solar_truncation_residual = solar_truncation_residual + ray_energy
                    exit
                end if
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if (reflection_count >= max_bounces) then
                    solar_truncation_residual = solar_truncation_residual + ray_energy
                    exit
                end if

                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                reflection_count = reflection_count + 1
                solar_reflection_events = solar_reflection_events + 1_8
                if (reflection_count == 1) solar_paths_with_reflection = solar_paths_with_reflection + 1_8
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if (reflection_count == 2) solar_paths_with_two_plus_reflections = &
                    solar_paths_with_two_plus_reflections + 1_8
                solar_max_reflections_observed = max(solar_max_reflections_observed, reflection_count)
                call generate_cosine_direction(new_dir, hit_normal)
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                ray_origin = hit_point + EPS * hit_normal
                ray_dir = new_dir
                last_hit_sphere = hit_sphere
            ! 结束本轮迭代范围，继续处理汇总后的计算结果。
            end do
        end do

        closure_scale = max(solar_initial_power, 1.0d-30)
        ! 检查并记录计算状态，使异常能够被上层流程识别。
        solar_closure_error = abs(solar_initial_power - sum(solar_heat_direct) - &
                                  sum(solar_heat_indirect) - solar_escape_power - &
                                  solar_truncation_residual) / closure_scale

        write(*,*)
        write(*,'(A)') ' --- Solar MC Statistics ---'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,I12)')    '   Total rays:      ', rays_solar
        write(*,'(A,I12)')    '   Valid first hits:', solar_valid_first_hits
        write(*,'(A,I12)')    '   Empty rays:      ', solar_empty_rays
        write(*,'(A,ES14.6)') '   Initial power:   ', solar_initial_power
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,ES14.6)') '   Escape power:    ', solar_escape_power
        write(*,'(A,ES14.6)') '   Trunc residual:  ', solar_truncation_residual
        write(*,'(A,ES14.6)') '   Closure error:   ', solar_closure_error
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,*)

        write(*,'(A)') '   Per-sphere solar heat:'
        write(*,'(A)') '   ID   DirHits  IndHits    Direct[W]    Indirect[W]      Total[W]'
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do hit_sphere = 1, min(num_spheres, 20)
            write(*,'(I5,2I9,3ES14.4)') hit_sphere, &
                solar_hits_direct(hit_sphere), solar_hits_indirect(hit_sphere), &
                solar_heat_direct(hit_sphere), solar_heat_indirect(hit_sphere), &
                sphere_solar_heat(hit_sphere)
        end do
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (num_spheres > 20) write(*,'(A)') '   ... (truncated)'

        write(*,*)
        write(*,'(A,ES14.4,A)') '   Total solar absorbed: ', sum(sphere_solar_heat), ' W'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,ES14.4,A)') '   Direct component:     ', sum(solar_heat_direct), ' W'
        write(*,'(A,ES14.4,A)') '   Indirect component:   ', sum(solar_heat_indirect), ' W'
        write(*,*)

        ! 调用子过程完成当前数值计算或状态更新。
        call write_solar_statistics(solar_hits_direct, solar_hits_indirect, total_disk_area)

        deallocate(solar_hits_direct, solar_hits_indirect, active_ids, disk_x, disk_y)
        deallocate(disk_radius, disk_area, cumulative_area)

    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine calculate_solar_heat_mc

    subroutine find_nearest_sphere_hit_solar(origin, direction, exclude, &
                                              hit_idx, hit_any, t_min, h_pt, h_nrm)
        implicit none
        real(8), intent(in) :: origin(3), direction(3)
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: exclude
        integer, intent(out) :: hit_idx
        logical, intent(out) :: hit_any
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(out) :: t_min, h_pt(3), h_nrm(3)

        real(8) :: L(3), t_ca, d2, t_hc, t_cur, nm, r
        integer :: idx

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        hit_any = .false.
        hit_idx = -1
        t_min = huge(1.0d0)

        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do idx = 1, num_spheres
            if (idx == exclude) cycle
            if (.not. participates_in_occlusion(idx)) cycle

            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            r = sphere_radius(idx)
            L = sphere_centers(:,idx) - origin
            t_ca = sum(L * direction)

            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            d2 = sum(L**2) - t_ca**2
            if (d2 > r**2) cycle

            t_hc = sqrt(r**2 - d2)

            ! 取较小的正根
            t_cur = t_ca - t_hc
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (t_cur < EPS) then
                t_cur = t_ca + t_hc
            end if

            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (t_cur > EPS .and. t_cur < t_min) then
                t_min = t_cur
                hit_idx = idx
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                hit_any = .true.
            end if
        end do

        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (hit_any) then
            h_pt = origin + t_min * direction
            h_nrm = h_pt - sphere_centers(:,hit_idx)
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            nm = sqrt(sum(h_nrm**2))
            if (nm > EPS) h_nrm = h_nrm / nm
        end if

    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine find_nearest_sphere_hit_solar

    subroutine write_solar_statistics(hits_direct, hits_indirect, total_disk_area)
        implicit none
        integer(8), intent(in) :: hits_direct(:), hits_indirect(:)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(in) :: total_disk_area
        integer :: uf, i

        if (.not. WRITE_QA_DIAGNOSTICS) return
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        uf = 40
        open(unit=uf, file=output_path('solar_statistics.txt'), status='replace')

        write(uf,'(A)') '===================================================='
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A)') ' Target-directed Solar Monte Carlo Statistics'
        write(uf,'(A)') '===================================================='
        write(uf,*)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)')     'Number of spheres:     ', num_spheres
        write(uf,'(A,I0)')     'Total solar rays:      ', rays_solar
        write(uf,'(A,I0)')     'Max bounces:           ', max_bounces
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)')     'Random seed:           ', random_seed_used
        write(uf,'(A,F10.1,A)') 'Solar flux:           ', solar_flux, ' W/m^2'
        write(uf,'(A,3F10.5)') 'Solar direction:       ', solar_direction
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES14.6,A)') 'Sum disk area:        ', total_disk_area, ' m^2'
        write(uf,*)
        write(uf,'(A,I0)')     'Valid first hits:      ', solar_valid_first_hits
        write(uf,'(A,I0)')     'Empty rays:             ', solar_empty_rays
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES18.10)') 'Initial power W:       ', solar_initial_power
        write(uf,'(A,ES18.10)') 'Direct absorbed W:     ', sum(solar_heat_direct)
        write(uf,'(A,ES18.10)') 'Reflected absorbed W:  ', sum(solar_heat_indirect)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES18.10)') 'Escape power W:        ', solar_escape_power
        write(uf,'(A,ES18.10)') 'Truncation residual W: ', solar_truncation_residual
        write(uf,'(A,ES18.10)') 'Closure error:         ', solar_closure_error
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,F12.6)')  'Mean reflections:      ', &
            dble(solar_reflection_events)/max(1.0d0,dble(rays_solar))
        write(uf,'(A,I0)')     'Max reflections:       ', solar_max_reflections_observed
        write(uf,'(A,I0)')     'Paths with reflection: ', solar_paths_with_reflection
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)')     'Paths with 2+ refl:     ', solar_paths_with_two_plus_reflections
        write(uf,'(A,I0)')     'Returned to first:      ', solar_paths_returned_to_first
        write(uf,'(A,I0)')     'Reflected escape rays:  ', solar_reflected_escape_rays
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,*)
        write(uf,'(A,ES14.6,A)') 'Total solar absorbed:  ', sum(sphere_solar_heat), ' W'
        write(uf,'(A,ES14.6,A)') 'Direct component:      ', sum(solar_heat_direct), ' W'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES14.6,A)') 'Indirect component:    ', sum(solar_heat_indirect), ' W'
        write(uf,*)

        write(uf,'(A)') 'Per-sphere details:'
        write(uf,'(A)') 'ID      Radius    Absorpt.   DirHits   IndHits      Direct[W]     Indirect[W]        Total[W]'
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do i = 1, num_spheres
            if (.not. participates_in_solar(i)) cycle
            if (sphere_radius(i) <= 0.0d0) cycle
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(I4,2F10.4,2I10,3ES15.6)') i, sphere_radius(i), &
                sphere_solar_absorptivity(i), hits_direct(i), hits_indirect(i), &
                solar_heat_direct(i), solar_heat_indirect(i), sphere_solar_heat(i)
        end do

        close(uf)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine write_solar_statistics

!===================== Heat transfer calculation ==============================

    subroutine calculate_heat_transfer()
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer :: i_s, j_s
        real(8) :: P_emit(num_spheres)
        real(8) :: Pin, Pspace, Pto_others

        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do i_s = 1, num_spheres
            if (.not. participates_in_ir_source(i_s)) then
                P_emit(i_s) = 0.0d0
                ! 满足当前控制条件后结束或跳过本次处理。
                cycle
            end if
            P_emit(i_s) = sphere_ir_emissivity(i_s) * STEFAN_BOLTZMANN * &
                          sphere_area(i_s) * sphere_temperature(i_s)**4
        end do

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        sphere_radiation_to_space = 0.0d0
        sphere_net_exchange       = 0.0d0
        sphere_radiation_from_environment = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        sphere_radiation_to_aperture = 0.0d0
        sphere_heat_exchange      = 0.0d0

        do i_s = 1, num_spheres
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (.not. participates_in_thermal(i_s)) cycle
            Pspace = sphere_view_factor_to_space(i_s) * P_emit(i_s)
            sphere_radiation_to_space(i_s) = Pspace
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            sphere_radiation_from_environment(i_s) = sphere_view_factor_to_space(i_s) * &
                sphere_ir_emissivity(i_s) * STEFAN_BOLTZMANN * sphere_area(i_s) * environment_temp**4
            if (allocated(grid_direct_factor)) then
                sphere_radiation_to_aperture(i_s) = &
                    (sum(grid_direct_factor(:,:,i_s)) + sum(grid_indirect_factor(:,:,i_s))) * P_emit(i_s)
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if

            do j_s = 1, num_spheres
                if (.not. participates_in_ir_target(j_s)) cycle
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                sphere_heat_exchange(i_s,j_s) = sphere_view_factor(i_s,j_s) * P_emit(i_s)
            end do

            Pto_others = sum(sphere_heat_exchange(i_s,:))

            Pin = 0.0d0
            ! 进入迭代计算，逐项更新仿真状态或累计量。
            do j_s = 1, num_spheres
                if (.not. participates_in_ir_source(j_s)) cycle
                Pin = Pin + sphere_view_factor(j_s,i_s) * P_emit(j_s)
            ! 结束本轮迭代范围，继续处理汇总后的计算结果。
            end do

            sphere_net_exchange(i_s) = Pto_others - Pin
        end do
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine calculate_heat_transfer


    subroutine write_solver_status_file(status_value, valid_value, message_value)
        character(len=*), intent(in) :: status_value, message_value
        ! 声明逻辑开关，用于控制对应计算或输出路径。
        logical, intent(in) :: valid_value
        integer :: uf, ios
        uf=96
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf,file=output_path('solver_status.txt'),status='replace',iostat=ios)
        if (ios==0) then
            write(uf,'(A,A)') 'solver_status=',trim(status_value)
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,L1)') 'result_valid=',valid_value
            write(uf,'(A,A)') 'message=',trim(message_value)
            close(uf)
        end if
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine write_solver_status_file

    subroutine production_fail(status_value,message_value,code_value)
        character(len=*), intent(in) :: status_value,message_value
        ! 声明计数器、索引或离散控制参数。
        integer,intent(in) :: code_value
        solver_status=status_value; solver_message=message_value; result_valid=.false.
        write(*,'(A,A)') 'SOLVER_STATUS=',trim(status_value)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A)') 'RESULT_VALID=F'
        write(*,'(A,A)') 'MESSAGE=',trim(message_value)
        if (len_trim(resolved_output_dir)>0) call write_solver_status_file(status_value,.false.,message_value)
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        stop code_value
    end subroutine production_fail

    subroutine validate_production_state(validation_ierr)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(out) :: validation_ierr
        integer :: i

        ! 检查并记录计算状态，使异常能够被上层流程识别。
        validation_ierr = 0
        if (num_spheres < 1) then
            solver_status='INVALID_INPUT'; solver_message='NUM_SPHERES must be positive'; validation_ierr=1; return
        end if
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (count(sphere_is_active) == 0) then
            solver_status='RAY_TRACING_FAILURE'; solver_message='no active sphere available for ray tracing'; validation_ierr=1; return
        end if
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. sphere_is_active(1)) then
            solver_status='INVALID_INPUT'; solver_message='sphere 1 must be active for rule-B temperature inheritance'; validation_ierr=1; return
        end if
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (abs(sphere_release_time(1)) > PRIMARY_RELEASE_TOL) then
            solver_status='INVALID_INPUT'; solver_message='sphere 1 release_time must be 0 for rule-B temperature inheritance'; validation_ierr=1; return
        end if

        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do i = 1, num_spheres
            if (.not. ieee_is_finite(sphere_initial_temp(i))) then
                solver_status='TEMPERATURE_NAN_OR_INF'; solver_message='initial temperature is nonfinite'; validation_ierr=1; return
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
            if (sphere_initial_temp(i) <= 0.0d0) then
                solver_status='TRANSIENT_NONPHYSICAL_STATE'; solver_message='initial temperature must be positive'; validation_ierr=1; return
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
            if (.not. ieee_is_finite(sphere_radius(i)) .or. &
                sphere_radius(i) <= TARGET_SHELL_THICKNESS) then
                solver_status='INVALID_INPUT'; solver_message='outer radius must exceed TARGET_SHELL_THICKNESS'; validation_ierr=1; return
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
            if (.not. ieee_is_finite(sphere_radius(i)) .or. sphere_radius(i) <= 0.0d0 .or. &
                .not. ieee_is_finite(sphere_density(i)) .or. sphere_density(i) <= 0.0d0 .or. &
                .not. ieee_is_finite(sphere_specific_heat(i)) .or. sphere_specific_heat(i) <= 0.0d0 .or. &
                any(.not. ieee_is_finite(sphere_centers_initial(:,i))) .or. &
                any(.not. ieee_is_finite(sphere_velocity(:,i))) .or. &
                any(.not. ieee_is_finite(sphere_acceleration(:,i))) .or. &
                .not. ieee_is_finite(sphere_release_time(i)) .or. sphere_release_time(i) < 0.0d0 .or. &
                .not. ieee_is_finite(sphere_internal_heat(i))) then
                solver_status='INVALID_INPUT'; solver_message='nonfinite or nonphysical sphere property or motion value'; validation_ierr=1; return
            end if
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do
    end subroutine validate_production_state

    subroutine initialize_energy_ledger_file()
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer :: uf

        if (.not. WRITE_QA_DIAGNOSTICS) return
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        uf = 94
        open(unit=uf, file=output_path('energy_ledger.csv'), status='replace')
        write(uf,'(A)') 'frame,thermal_step,sphere_id,time_start,time_end,T_old,T_new,delta_T,' // &
            'internal_energy_change_J,net_power_W,time_step_s,step_energy_residual_J,' // &
            'cumulative_energy_residual_J'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)
    end subroutine initialize_energy_ledger_file

    subroutine append_energy_ledger_row(step_index,time_a,time_b,idx,temp_a,temp_b,Qnet,dt_local)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in) :: step_index, idx
        real(8), intent(in) :: time_a, time_b, temp_a, temp_b, Qnet, dt_local
        ! 声明计数器、索引或离散控制参数。
        integer :: uf
        real(8) :: dE, residual

        dE = sphere_thermal_capacity(idx) * (temp_b-temp_a)
        residual = dE-Qnet*dt_local
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        cumulative_energy_residual = cumulative_energy_residual + residual
        if (.not. WRITE_QA_DIAGNOSTICS) return
        uf = 94
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf,file=output_path('energy_ledger.csv'),status='unknown',position='append')
        write(uf,'(I0,A,I0,A,I0,A,10(ES24.16,A),ES24.16)') current_frame_index,',',step_index,',',idx,',',time_a,',',time_b,',', &
          temp_a,',',temp_b,',',temp_b-temp_a,',',dE,',',Qnet,',',dt_local,',',residual,',',cumulative_energy_residual
        close(uf)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine append_energy_ledger_row

    subroutine initialize_system_energy_ledger_file()
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer :: uf

        if (.not. WRITE_QA_DIAGNOSTICS) return
        uf = 91
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=output_path('system_energy_ledger.csv'), status='replace')
        write(uf,'(A)') 'record_type,frame,time_start,time_end,dt_s,active_count_start,active_count_end,' // &
            'initial_domain_baseline_mass_kg,initial_domain_baseline_internal_energy_J,' // &
            'active_mass_start_kg,active_mass_end_kg,active_internal_energy_start_J,' // &
            'active_internal_energy_before_release_J,active_internal_energy_end_J,' // &
            'delta_active_internal_energy_J,solar_input_J,internal_heat_input_J,' // &
            'environment_input_J,space_loss_J,aperture_loss_J,' // &
            'sphere_exchange_pair_residual_J,release_added_mass_kg,' // &
            'release_carried_internal_energy_J,cumulative_release_carried_energy_J,' // &
            'system_energy_residual_J,cumulative_system_energy_residual_J,' // &
            'internal_energy_reference_K'
        close(uf)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine initialize_system_energy_ledger_file

    subroutine append_initial_system_state_row(initial_release_added_mass, initial_release_energy)
        implicit none
        real(8), intent(in) :: initial_release_added_mass, initial_release_energy
        ! 声明计数器、索引或离散控制参数。
        integer :: uf
        real(8) :: active_mass_now, active_energy_now

        active_mass_now = active_mass_total()
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        active_energy_now = active_internal_energy_total()
        if (.not. WRITE_QA_DIAGNOSTICS) return
        uf = 91
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=output_path('system_energy_ledger.csv'), status='unknown', position='append')
        write(uf,'(A)',advance='no') 'INITIAL_STATE'
        write(uf,'(A,I0)',advance='no') ',',0
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',0.0d0
        write(uf,'(A,ES24.16)',advance='no') ',',0.0d0
        write(uf,'(A,ES24.16)',advance='no') ',',0.0d0
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)',advance='no') ',',0
        write(uf,'(A,I0)',advance='no') ',',active_object_count()
        write(uf,'(A,ES24.16)',advance='no') ',',sphere_mass(1)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',object_internal_energy(1, sphere_temperature(1))
        write(uf,'(A,ES24.16)',advance='no') ',',0.0d0
        write(uf,'(A,ES24.16)',advance='no') ',',active_mass_now
        write(uf,'(A,ES24.16)',advance='no') ',',0.0d0
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',object_internal_energy(1, sphere_temperature(1))
        write(uf,'(A,ES24.16)',advance='no') ',',active_energy_now
        write(uf,'(A,ES24.16)',advance='no') ',',active_energy_now
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',0.0d0
        write(uf,'(A,ES24.16)',advance='no') ',',0.0d0
        write(uf,'(A,ES24.16)',advance='no') ',',0.0d0
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',0.0d0
        write(uf,'(A,ES24.16)',advance='no') ',',0.0d0
        write(uf,'(A,ES24.16)',advance='no') ',',0.0d0
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',initial_release_added_mass
        write(uf,'(A,ES24.16)',advance='no') ',',initial_release_energy
        write(uf,'(A,ES24.16)',advance='no') ',',cumulative_release_carried_energy
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',0.0d0
        write(uf,'(A,ES24.16)',advance='no') ',',cumulative_system_energy_residual
        write(uf,'(A,ES24.16)') ',',INTERNAL_ENERGY_REFERENCE_K
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)
    end subroutine append_initial_system_state_row

    subroutine append_system_energy_ledger_row(time_start, time_end, active_count_start, active_count_end, &
                                               active_mass_start, active_mass_end, energy_start, &
                                               energy_before_release, energy_end, release_added_mass, &
                                               release_carried_energy)
        implicit none
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(in) :: time_start, time_end
        integer, intent(in) :: active_count_start, active_count_end
        real(8), intent(in) :: active_mass_start, active_mass_end
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(in) :: energy_start, energy_before_release, energy_end
        real(8), intent(in) :: release_added_mass, release_carried_energy
        integer :: uf
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: delta_energy, expected_energy, residual

        delta_energy = energy_end - energy_start
        expected_energy = slice_solar_input_energy + slice_internal_input_energy + &
            slice_environment_input_energy - slice_space_loss_energy - &
            slice_aperture_loss_energy + slice_sphere_exchange_energy + &
            release_carried_energy
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        residual = delta_energy - expected_energy
        cumulative_system_energy_residual = cumulative_system_energy_residual + residual

        if (.not. WRITE_QA_DIAGNOSTICS) return
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        uf = 91
        open(unit=uf, file=output_path('system_energy_ledger.csv'), status='unknown', position='append')
        write(uf,'(A)',advance='no') 'INTERVAL'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)',advance='no') ',',current_frame_index
        write(uf,'(A,ES24.16)',advance='no') ',',time_start
        write(uf,'(A,ES24.16)',advance='no') ',',time_end
        write(uf,'(A,ES24.16)',advance='no') ',',time_end-time_start
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)',advance='no') ',',active_count_start
        write(uf,'(A,I0)',advance='no') ',',active_count_end
        write(uf,'(A,ES24.16)',advance='no') ',',0.0d0
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',0.0d0
        write(uf,'(A,ES24.16)',advance='no') ',',active_mass_start
        write(uf,'(A,ES24.16)',advance='no') ',',active_mass_end
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',energy_start
        write(uf,'(A,ES24.16)',advance='no') ',',energy_before_release
        write(uf,'(A,ES24.16)',advance='no') ',',energy_end
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',delta_energy
        write(uf,'(A,ES24.16)',advance='no') ',',slice_solar_input_energy
        write(uf,'(A,ES24.16)',advance='no') ',',slice_internal_input_energy
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',slice_environment_input_energy
        write(uf,'(A,ES24.16)',advance='no') ',',slice_space_loss_energy
        write(uf,'(A,ES24.16)',advance='no') ',',slice_aperture_loss_energy
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',slice_sphere_exchange_energy
        write(uf,'(A,ES24.16)',advance='no') ',',release_added_mass
        write(uf,'(A,ES24.16)',advance='no') ',',release_carried_energy
        write(uf,'(A,ES24.16)',advance='no') ',',cumulative_release_carried_energy
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',residual
        write(uf,'(A,ES24.16)',advance='no') ',',cumulative_system_energy_residual
        write(uf,'(A,ES24.16)') ',',INTERNAL_ENERGY_REFERENCE_K
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)
    end subroutine append_system_energy_ledger_row

    subroutine initialize_orbital_state_file()
        ! 声明计数器、索引或离散控制参数。
        integer::uf
        if (.not. WRITE_QA_DIAGNOSTICS) return
        uf=93; open(unit=uf,file=output_path('orbital_state_history.csv'),status='replace')
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A)') 'frame,time_start,time_end,rx_start,ry_start,rz_start,rx_end,ry_end,rz_end,vx_start,vy_start,vz_start,vx_end,vy_end,vz_end,two_body_propagator_call_count,orbital_step_count'
        close(uf)
    end subroutine initialize_orbital_state_file

    ! 定义 append_orbital_state_row 计算单元，封装该步骤的数据处理规则。
    subroutine append_orbital_state_row(frame,t0,t1,r0,r1,v0,v1,calls_this_step)
        integer,intent(in)::frame
        integer(8),intent(in)::calls_this_step
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8),intent(in)::t0,t1,r0(3),r1(3),v0(3),v1(3)
        integer::uf
        if (.not. WRITE_QA_DIAGNOSTICS) return
        uf=93; open(unit=uf,file=output_path('orbital_state_history.csv'),status='unknown',position='append')
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(I0,A,14(ES24.16,A),I0,A,I0)') frame,',',t0,',',t1,',',r0(1),',',r0(2),',',r0(3),',', &
          r1(1),',',r1(2),',',r1(3),',',v0(1),',',v0(2),',',v0(3),',',v1(1),',',v1(2),',',v1(3),',', &
          calls_this_step,',',calls_this_step
        close(uf)
    end subroutine append_orbital_state_row

    ! 定义 initialize_release_event_file 计算单元，封装该步骤的数据处理规则。
    subroutine initialize_release_event_file()
        implicit none
        integer :: uf

        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. WRITE_QA_DIAGNOSTICS) return
        uf = 92
        open(unit=uf, file=output_path('release_event_history.csv'), status='replace')
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A)') 'frame,time_start,time_end,event_time,sphere_id,old_stage,new_stage,' // &
            'input_active,initial_domain_baseline,primary_temperature_before_K,' // &
            'member_temperature_before_K,release_temperature_K,released_mass_kg,' // &
            'release_added_mass_kg,release_carried_internal_energy_J,' // &
            'cumulative_release_carried_energy_J,active_mass_after_event_kg,' // &
            'active_internal_energy_after_event_J'
        close(uf)
    end subroutine initialize_release_event_file

    ! 定义 append_release_event_row 计算单元，封装该步骤的数据处理规则。
    subroutine append_release_event_row(time_start, time_end, event_time, sphere_idx, old_stage, new_stage, &
                                        primary_temperature_before, member_temperature_before, &
                                        member_temperature_after, initial_domain_baseline, release_added_mass, &
                                        release_carried_energy)
        implicit none
        real(8), intent(in) :: time_start, time_end, event_time
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: sphere_idx, old_stage, new_stage
        real(8), intent(in) :: primary_temperature_before, member_temperature_before, member_temperature_after
        logical, intent(in) :: initial_domain_baseline
        real(8), intent(in) :: release_added_mass, release_carried_energy
        ! 声明计数器、索引或离散控制参数。
        integer :: uf

        if (.not. WRITE_QA_DIAGNOSTICS) return
        uf = 92
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=output_path('release_event_history.csv'), status='unknown', position='append')
        write(uf,'(I0)',advance='no') current_frame_index
        write(uf,'(A,ES24.16)',advance='no') ',',time_start
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',time_end
        write(uf,'(A,ES24.16)',advance='no') ',',event_time
        write(uf,'(A,I0)',advance='no') ',',sphere_idx
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)',advance='no') ',',old_stage
        write(uf,'(A,I0)',advance='no') ',',new_stage
        write(uf,'(A,I0)',advance='no') ',',merge(1,0,is_input_enabled(sphere_idx))
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)',advance='no') ',',merge(1,0,initial_domain_baseline)
        write(uf,'(A,ES24.16)',advance='no') ',',primary_temperature_before
        write(uf,'(A,ES24.16)',advance='no') ',',member_temperature_before
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',member_temperature_after
        write(uf,'(A,ES24.16)',advance='no') ',',sphere_mass(sphere_idx)
        write(uf,'(A,ES24.16)',advance='no') ',',release_added_mass
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES24.16)',advance='no') ',',release_carried_energy
        write(uf,'(A,ES24.16)',advance='no') ',',cumulative_release_carried_energy
        write(uf,'(A,ES24.16)',advance='no') ',',active_mass_total()
        write(uf,'(A,ES24.16)') ',',active_internal_energy_total()
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)
    end subroutine append_release_event_row

    subroutine apply_release_temperature_inheritance(previous_stage, time_start, event_time, &
                                                     primary_temperature_before, newly_released_count, &
                                                     release_added_mass, release_carried_energy)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in) :: previous_stage(:)
        real(8), intent(in) :: time_start, event_time, primary_temperature_before
        ! 声明计数器、索引或离散控制参数。
        integer, intent(out) :: newly_released_count
        real(8), intent(out) :: release_added_mass, release_carried_energy
        integer :: idx
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: member_temperature_before(num_spheres)
        real(8) :: event_mass, event_energy
        logical :: initial_domain_baseline

        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (size(previous_stage) /= num_spheres) then
            call production_fail('INTERNAL_ERROR', 'release stage snapshot size mismatch', 3)
        end if
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. ieee_is_finite(primary_temperature_before) .or. primary_temperature_before <= 0.0d0) then
            call production_fail('TRANSIENT_NONPHYSICAL_STATE', &
                                 'primary temperature invalid at release event', 4)
        end if

        newly_released_count = 0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        release_added_mass = 0.0d0
        release_carried_energy = 0.0d0
        member_temperature_before = sphere_temperature

        ! First pass: initialize every newly released target before any event row
        ! is written. This keeps simultaneous-release diagnostics on one common,
        ! fully initialized endpoint state.
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do idx = 1, num_spheres
            if (previous_stage(idx) == MEMBER_STAGE_RELEASED) cycle
            if (sphere_motion_stage(idx) /= MEMBER_STAGE_RELEASED) cycle

            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (idx /= 1 .and. sphere_motion_stage(1) /= MEMBER_STAGE_RELEASED) then
                call production_fail('INVALID_INPUT', &
                    'a secondary target cannot release before sphere 1 under rule-B temperature inheritance', 2)
            end if

            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (idx /= 1) sphere_temperature(idx) = primary_temperature_before
            newly_released_count = newly_released_count + 1

            initial_domain_baseline = idx == 1 .and. abs(event_time) <= EPS
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (.not. initial_domain_baseline) then
                release_added_mass = release_added_mass + sphere_mass(idx)
                release_carried_energy = release_carried_energy + &
                    object_internal_energy(idx, sphere_temperature(idx))
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
        end do

        ! Second pass: update the cumulative open-domain influx and write event
        ! evidence after all simultaneous targets have valid inherited states.
        do idx = 1, num_spheres
            if (previous_stage(idx) == MEMBER_STAGE_RELEASED) cycle
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (sphere_motion_stage(idx) /= MEMBER_STAGE_RELEASED) cycle

            initial_domain_baseline = idx == 1 .and. abs(event_time) <= EPS
            if (initial_domain_baseline) then
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                event_mass = 0.0d0
                event_energy = 0.0d0
            else
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                event_mass = sphere_mass(idx)
                event_energy = object_internal_energy(idx, sphere_temperature(idx))
                cumulative_release_carried_energy = cumulative_release_carried_energy + event_energy
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if

            call append_release_event_row(time_start, event_time, event_time, idx, previous_stage(idx), &
                                          sphere_motion_stage(idx), primary_temperature_before, &
                                          member_temperature_before(idx), sphere_temperature(idx), &
                                          initial_domain_baseline, event_mass, event_energy)
        end do
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine apply_release_temperature_inheritance

!========================== Temperature solvers ===============================

    subroutine initialize_temperature_history_file()
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer :: uf, idx

        uf = 39
        open(unit=uf, file=output_path('temperature_history.csv'), status='replace')
        write(uf,'(A)', advance='no') 'frame,time_s'
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do idx = 1, num_spheres
            write(uf,'(A)', advance='no') ','
            write(uf,'(A,I0)', advance='no') 'T_', idx
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do
        write(uf,*)
        close(uf)

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        temperature_history_initialized = .true.
    end subroutine initialize_temperature_history_file

    subroutine append_temperature_history_row(frame_idx, time_value)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in) :: frame_idx
        real(8), intent(in) :: time_value
        ! 声明计数器、索引或离散控制参数。
        integer :: uf, idx


        if (.not. temperature_history_initialized) call initialize_temperature_history_file()

        uf = 39
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=output_path('temperature_history.csv'), status='unknown', position='append')
        write(uf,'(I0,A,ES24.16)', advance='no') frame_idx, ',', time_value
        do idx = 1, num_spheres
            write(uf,'(A)', advance='no') ','
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(ES24.16)', advance='no') sphere_temperature(idx)
        end do
        write(uf,*)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)
    end subroutine append_temperature_history_row

    subroutine solve_temperature_transient_slice(slice_duration, time_start)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        real(8), intent(in) :: slice_duration, time_start

        integer :: idx, n_substeps, step
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: time_current, dt_local, remainder, Q_net, dT_dt
        real(8) :: q_solar, q_internal, q_environment, q_space, q_aperture, q_exchange
        real(8) :: T_old(num_spheres)

        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        slice_solar_input_energy = 0.0d0
        slice_internal_input_energy = 0.0d0
        slice_environment_input_energy = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        slice_space_loss_energy = 0.0d0
        slice_aperture_loss_energy = 0.0d0
        slice_sphere_exchange_energy = 0.0d0

        if (slice_duration <= EPS) then
            ! 调用子过程完成当前数值计算或状态更新。
            call calculate_heat_transfer()
            return
        end if

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        n_substeps = 0
        if (time_step > EPS) n_substeps = int(slice_duration / time_step)
        remainder = slice_duration - dble(n_substeps) * time_step
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        time_current = time_start

        do step = 1, n_substeps
            dt_local = time_step
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            time_current = time_current + dt_local
            T_old = sphere_temperature

            call calculate_heat_transfer()

            ! 进入迭代计算，逐项更新仿真状态或累计量。
            do idx = 1, num_spheres
                if (.not. participates_in_thermal(idx)) cycle
                q_solar = sphere_solar_heat(idx)
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                q_internal = sphere_internal_heat(idx)
                q_environment = sphere_radiation_from_environment(idx)
                q_space = sphere_radiation_to_space(idx)
                q_aperture = sphere_radiation_to_aperture(idx)
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                q_exchange = -sphere_net_exchange(idx)
                Q_net = q_solar + q_internal + q_environment - q_space - q_aperture + q_exchange

                slice_solar_input_energy = slice_solar_input_energy + q_solar * dt_local
                ! 维护输入输出路径及文件数据，确保结果写入约定位置。
                slice_internal_input_energy = slice_internal_input_energy + q_internal * dt_local
                slice_environment_input_energy = slice_environment_input_energy + q_environment * dt_local
                slice_space_loss_energy = slice_space_loss_energy + q_space * dt_local
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                slice_aperture_loss_energy = slice_aperture_loss_energy + q_aperture * dt_local
                slice_sphere_exchange_energy = slice_sphere_exchange_energy + q_exchange * dt_local

                dT_dt = Q_net / sphere_thermal_capacity(idx)
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                sphere_temperature(idx) = T_old(idx) + dT_dt * dt_local
                if (.not.ieee_is_finite(sphere_temperature(idx))) call production_fail('TEMPERATURE_NAN_OR_INF','nonfinite transient temperature',4)
                if (sphere_temperature(idx)<=0.0d0) call production_fail('TRANSIENT_NONPHYSICAL_STATE','temperature must remain positive',4)
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                transient_update_count=transient_update_count+1_8
                thermal_step_count=thermal_step_count+1_8
                call append_energy_ledger_row(step,time_current-dt_local,time_current,idx,T_old(idx),sphere_temperature(idx),Q_net,dt_local)
            ! 结束本轮迭代范围，继续处理汇总后的计算结果。
            end do
        end do

        if (remainder > EPS) then
            dt_local = remainder
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            time_current = time_current + dt_local
            T_old = sphere_temperature

            call calculate_heat_transfer()

            ! 进入迭代计算，逐项更新仿真状态或累计量。
            do idx = 1, num_spheres
                if (.not. participates_in_thermal(idx)) cycle
                q_solar = sphere_solar_heat(idx)
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                q_internal = sphere_internal_heat(idx)
                q_environment = sphere_radiation_from_environment(idx)
                q_space = sphere_radiation_to_space(idx)
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                q_aperture = sphere_radiation_to_aperture(idx)
                q_exchange = -sphere_net_exchange(idx)
                Q_net = q_solar + q_internal + q_environment - q_space - q_aperture + q_exchange

                ! 维护输入输出路径及文件数据，确保结果写入约定位置。
                slice_solar_input_energy = slice_solar_input_energy + q_solar * dt_local
                slice_internal_input_energy = slice_internal_input_energy + q_internal * dt_local
                slice_environment_input_energy = slice_environment_input_energy + q_environment * dt_local
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                slice_space_loss_energy = slice_space_loss_energy + q_space * dt_local
                slice_aperture_loss_energy = slice_aperture_loss_energy + q_aperture * dt_local
                slice_sphere_exchange_energy = slice_sphere_exchange_energy + q_exchange * dt_local

                dT_dt = Q_net / sphere_thermal_capacity(idx)
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                sphere_temperature(idx) = T_old(idx) + dT_dt * dt_local
                if (.not.ieee_is_finite(sphere_temperature(idx))) call production_fail('TEMPERATURE_NAN_OR_INF','nonfinite transient temperature',4)
                if (sphere_temperature(idx)<=0.0d0) call production_fail('TRANSIENT_NONPHYSICAL_STATE','temperature must remain positive',4)
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                transient_update_count=transient_update_count+1_8
                thermal_step_count=thermal_step_count+1_8
                call append_energy_ledger_row(step,time_current-dt_local,time_current,idx,T_old(idx),sphere_temperature(idx),Q_net,dt_local)
            ! 结束本轮迭代范围，继续处理汇总后的计算结果。
            end do
        end if

        call calculate_heat_transfer()
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine solve_temperature_transient_slice

    subroutine advance_temperature_slice(slice_duration, time_start)
        implicit none
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(in) :: slice_duration, time_start
        call solve_temperature_transient_slice(slice_duration, time_start)
    end subroutine advance_temperature_slice
! 定义 update_sphere_power 计算单元，封装该步骤的数据处理规则。
subroutine update_sphere_power()
        integer :: i
        do i = 1, num_spheres
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (participates_in_ir_source(i)) then
                sphere_power(i) = sphere_ir_emissivity(i) * STEFAN_BOLTZMANN * &
                                  sphere_area(i) * sphere_temperature(i)**4
            else
                sphere_power(i) = 0.0d0
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
        end do
    end subroutine update_sphere_power

!====================== MC initialization & sampling ==========================
!初始化统计量
    ! 定义 initialize_mc_arrays 计算单元，封装该步骤的数据处理规则。
    subroutine initialize_mc_arrays()
        grid_direct_factor  = 0.0d0
        grid_indirect_factor= 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        grid_direct         = 0.0d0
        grid_direct_total   = 0.0d0
        grid_indirect       = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        grid_indirect_total = 0.0d0
        grid_total          = 0.0d0
        grid_irradiance     = 0.0d0

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        vf_energy_s2s = 0.0d0
        vf_energy_s2space = 0.0d0

        rays_emitted = 0_8
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        rays_direct_hit = 0_8
        rays_indirect_hit = 0_8
        rays_escaped_source = 0_8
        rays_terminated_on_sphere = 0_8
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        rays_self_absorbed = 0_8

        rays_escaped = 0_8
        rays_hit_pupil = 0_8
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        rays_on_sensor = 0_8

        power_direct_factor = 0.0d0
        power_indirect_factor = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        power_direct = 0.0d0
        power_indirect = 0.0d0
        
        wt_emitted = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        wt_pupil_cross = 0.0d0
        wt_pupil_grid = 0.0d0
        wt_pupil_direct = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        wt_pupil_indirect = 0.0d0
        wt_sphere_term = 0.0d0
        wt_self_absorb = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        wt_space_escape = 0.0d0
        
        spot_total = 0.0d0
        spot_irradiance = 0.0d0
    end subroutine initialize_mc_arrays

    ! 定义 generate_isotropic_direction 计算单元，封装该步骤的数据处理规则。
    subroutine generate_isotropic_direction(dir_out)
        real(8), intent(out) :: dir_out(3)
        real(8) :: u1, u2, cos_t, sin_t, phi
        ! 调用子过程完成当前数值计算或状态更新。
        call random_number(u1)
        call random_number(u2)
        cos_t = 2.0d0*u1 - 1.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        sin_t = sqrt(max(0.0d0, 1.0d0 - cos_t*cos_t))
        phi   = 2.0d0 * PI * u2
        dir_out(1) = sin_t*cos(phi)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        dir_out(2) = sin_t*sin(phi)
        dir_out(3) = cos_t
    end subroutine

    ! 定义 generate_direction_in_cone 计算单元，封装该步骤的数据处理规则。
    subroutine generate_direction_in_cone(dir_out, axis, cos_theta_max)
        real(8), intent(out) :: dir_out(3)
        real(8), intent(in)  :: axis(3), cos_theta_max
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: w(3), u(3), v(3), r1, r2, phi, cos_t, sin_t, nm

        w = axis
        call normalize_vector(w)
        if (abs(w(1)) > 0.9d0) then
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            u = (/0.0d0,1.0d0,0.0d0/)
        else
            u = (/1.0d0,0.0d0,0.0d0/)
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
        u = u - sum(u*w)*w
        nm = sqrt(sum(u**2)); if (nm > EPS) u = u/nm
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        v(1) = w(2)*u(3) - w(3)*u(2)
        v(2) = w(3)*u(1) - w(1)*u(3)
        v(3) = w(1)*u(2) - w(2)*u(1)

        ! 调用子过程完成当前数值计算或状态更新。
        call random_number(r1); call random_number(r2)
        phi   = 2.0d0*PI*r1
        cos_t = 1.0d0 - (1.0d0 - cos_theta_max)*r2
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        cos_t = max(-1.0d0, min(1.0d0, cos_t))
        sin_t = sqrt(max(0.0d0, 1.0d0 - cos_t*cos_t))

        dir_out = sin_t*cos(phi)*u + sin_t*sin(phi)*v + cos_t*w
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine

    subroutine generate_cosine_direction(dir_out, nrm_in)
        real(8), intent(out) :: dir_out(3)
        real(8), intent(in)  :: nrm_in(3)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: u(3), v(3), w(3), r1, r2, phi, ct, st, ld(3), nm
        w = nrm_in
        if (abs(w(1)) > 0.9d0) then
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            u = (/0.0d0,1.0d0,0.0d0/)
        else
            u = (/1.0d0,0.0d0,0.0d0/)
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
        u = u - sum(u*w)*w
        nm = sqrt(sum(u**2)); if (nm > EPS) u = u/nm
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        v(1) = w(2)*u(3) - w(3)*u(2)
        v(2) = w(3)*u(1) - w(1)*u(3)
        v(3) = w(1)*u(2) - w(2)*u(1)
        ! 调用子过程完成当前数值计算或状态更新。
        call random_number(r1); call random_number(r2)
        phi = 2.0d0*PI*r1
        ct  = sqrt(r2)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        st  = sqrt(1.0d0-r2)
        ld  = (/st*cos(phi), st*sin(phi), ct/)
        dir_out = ld(1)*u + ld(2)*v + ld(3)*w
    end subroutine

    ! 定义 random_point_on_sphere 计算单元，封装该步骤的数据处理规则。
    subroutine random_point_on_sphere(center, radius, pt, nrm)
        real(8), intent(in)  :: center(3), radius
        real(8), intent(out) :: pt(3), nrm(3)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: u, v, theta, phi, st
        call random_number(u); call random_number(v)
        theta = acos(1.0d0 - 2.0d0*u)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        phi = 2.0d0*PI*v
        st = sin(theta)
        nrm = (/st*cos(phi), st*sin(phi), cos(theta)/)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        pt  = center + radius*nrm
    end subroutine

!====================== Ray / Geometry helpers ================================

    subroutine check_aperture_hit(origin, direction, hit, t_hit, pos, u_loc, v_loc)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(in)  :: origin(3), direction(3)
        logical, intent(out) :: hit
        real(8), intent(out) :: t_hit
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(out) :: pos(3), u_loc, v_loc
        real(8) :: denom, t, d(3)
        hit = .false.; t_hit = huge(1.0d0)
        pos = 0.0d0; u_loc = 0.0d0; v_loc = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        denom = sum(direction*aperture_normal)
        if (abs(denom) < EPS) return
        d = aperture_center - origin
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        t = sum(d*aperture_normal)/denom
        if (t < EPS) return
        pos = origin + t*direction
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        d = pos - aperture_center
        u_loc = sum(d*aperture_u)
        v_loc = sum(d*aperture_v)
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (abs(u_loc) <= aperture_size/2.0d0 .and. &
            abs(v_loc) <= aperture_size/2.0d0) then
            hit = .true.
            t_hit = t
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
    end subroutine

    subroutine find_nearest_transport_event(origin, direction, exclude, event_kind, &
                                            hit_idx, t_event, event_pos, event_normal, &
                                            u_loc, v_loc)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(in) :: origin(3), direction(3)
        integer, intent(in) :: exclude
        integer, intent(out) :: event_kind, hit_idx
        real(8), intent(out) :: t_event, event_pos(3), event_normal(3), u_loc, v_loc
        ! 声明逻辑开关，用于控制对应计算或输出路径。
        logical :: hit_aperture, hit_sphere
        real(8) :: t_aperture, t_sphere, aperture_pos(3), sphere_pos(3), sphere_normal(3)

        call check_aperture_hit(origin, direction, hit_aperture, t_aperture, &
                                aperture_pos, u_loc, v_loc)
        ! 调用子过程完成当前数值计算或状态更新。
        call find_nearest_sphere_hit(origin, direction, exclude, hit_idx, hit_sphere, &
                                     t_sphere, sphere_pos, sphere_normal)

        event_kind = 0
        t_event = huge(1.0d0)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        event_pos = 0.0d0
        event_normal = 0.0d0
        if (hit_sphere .and. (.not. hit_aperture .or. t_sphere < t_aperture)) then
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            event_kind = 1
            t_event = t_sphere
            event_pos = sphere_pos
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            event_normal = sphere_normal
        else if (hit_aperture) then
            event_kind = 2
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            hit_idx = -1
            t_event = t_aperture
            event_pos = aperture_pos
        else
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            hit_idx = -1
        end if
    end subroutine find_nearest_transport_event
    ! 定义 find_nearest_sphere_hit 计算单元，封装该步骤的数据处理规则。
    subroutine find_nearest_sphere_hit(origin, direction, exclude, &
                                       hit_idx, hit_any, t_min, h_pt, h_nrm)
        real(8), intent(in) :: origin(3), direction(3)
        integer, intent(in) :: exclude
        ! 声明计数器、索引或离散控制参数。
        integer, intent(out):: hit_idx
        logical, intent(out):: hit_any
        real(8), intent(out):: t_min, h_pt(3), h_nrm(3)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: L(3), t_ca, d2, t_hc, t_cur, nm, r
        integer :: idx

        hit_any = .false.; hit_idx = -1; t_min = huge(1.0d0)
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do idx = 1, num_spheres
            if (idx == exclude) cycle
            if (.not. participates_in_ir_target(idx)) cycle
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            r = sphere_radius(idx)
            L = sphere_centers(:,idx) - origin
            t_ca = sum(L*direction)
            if (t_ca < 0.0d0) cycle
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            d2 = sum(L**2) - t_ca**2
            if (d2 > r**2) cycle
            t_hc = sqrt(r**2 - d2)
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            t_cur = t_ca - t_hc
            if (t_cur > EPS .and. t_cur < t_min) then
                t_min = t_cur
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                hit_idx = idx
                hit_any = .true.
            end if
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do
        if (hit_any) then
            h_pt  = origin + t_min*direction
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            h_nrm = h_pt - sphere_centers(:,hit_idx)
            nm = sqrt(sum(h_nrm**2))
            h_nrm = h_nrm/nm
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
    end subroutine
    !--------------------------------------------------------------------------
    ! P2 layered participation contract
    ! input_enabled: record is enabled by input.
    ! released: enabled record has reached MEMBER_STAGE_RELEASED.
    ! FORMATION members do not participate independently in solar, IR,
    ! occlusion, thermal update, statistics, or imaging.
    !
    ! P3 extension: release-event time alignment and rule-B temperature
    ! inheritance T_i(t_r+) = T_1(t_r-) are implemented by the event scheduler
    ! and apply_release_temperature_inheritance().
    !--------------------------------------------------------------------------
    logical function is_valid_sphere_index(idx)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in) :: idx

        is_valid_sphere_index = .false.
        if (idx < 1 .or. idx > num_spheres) return
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        is_valid_sphere_index = .true.
    end function is_valid_sphere_index

    logical function is_input_enabled(idx)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in) :: idx

        is_input_enabled = .false.
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. is_valid_sphere_index(idx)) return
        if (.not. allocated(sphere_is_active)) return
        if (idx > size(sphere_is_active)) return
        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        is_input_enabled = sphere_is_active(idx)
    end function is_input_enabled

    logical function is_released(idx)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in) :: idx

        is_released = .false.
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. is_input_enabled(idx)) return
        if (.not. allocated(sphere_motion_stage)) return
        if (idx > size(sphere_motion_stage)) return
        is_released = sphere_motion_stage(idx) == MEMBER_STAGE_RELEASED
    ! 结束当前计算单元，使过程边界保持清晰。
    end function is_released

    logical function participates_in_solar(idx)
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: idx
        participates_in_solar = is_released(idx)
    end function participates_in_solar

    ! 声明逻辑开关，用于控制对应计算或输出路径。
    logical function participates_in_ir_source(idx)
        implicit none
        integer, intent(in) :: idx
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        participates_in_ir_source = is_released(idx)
    end function participates_in_ir_source

    logical function participates_in_ir_target(idx)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in) :: idx
        participates_in_ir_target = is_released(idx)
    ! 结束当前计算单元，使过程边界保持清晰。
    end function participates_in_ir_target

    logical function participates_in_occlusion(idx)
        implicit none
        integer, intent(in) :: idx
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        participates_in_occlusion = is_released(idx)
    end function participates_in_occlusion

    logical function participates_in_thermal(idx)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in) :: idx
        participates_in_thermal = is_released(idx)
    ! 结束当前计算单元，使过程边界保持清晰。
    end function participates_in_thermal

    logical function participates_in_imaging(idx)
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: idx
        participates_in_imaging = is_released(idx)
    end function participates_in_imaging

    ! 声明逻辑开关，用于控制对应计算或输出路径。
    logical function participates_in_statistics(idx)
        implicit none
        integer, intent(in) :: idx
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        participates_in_statistics = is_released(idx)
    end function participates_in_statistics

    integer function count_statistics_participants()
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer :: idx

        count_statistics_participants = 0
        do idx = 1, num_spheres
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (participates_in_statistics(idx)) then
                count_statistics_participants = count_statistics_participants + 1
            end if
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do
    end function count_statistics_participants

    subroutine get_participant_temperature_statistics(T_avg, T_min, T_max, participant_count)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        real(8), intent(out) :: T_avg, T_min, T_max
        integer, intent(out) :: participant_count
        ! 声明计数器、索引或离散控制参数。
        integer :: idx
        real(8) :: T_sum

        participant_count = 0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        T_sum = 0.0d0
        T_min = huge(1.0d0)
        T_max = -huge(1.0d0)

        do idx = 1, num_spheres
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (.not. participates_in_statistics(idx)) cycle
            participant_count = participant_count + 1
            T_sum = T_sum + sphere_temperature(idx)
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            T_min = min(T_min, sphere_temperature(idx))
            T_max = max(T_max, sphere_temperature(idx))
        end do

        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (participant_count > 0) then
            T_avg = T_sum / dble(participant_count)
        else
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            T_avg = 0.0d0
            T_min = 0.0d0
            T_max = 0.0d0
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
    end subroutine get_participant_temperature_statistics
    subroutine get_grid_index(u_loc, v_loc, ix_out, iy_out)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(in) :: u_loc, v_loc
        integer, intent(out):: ix_out, iy_out
        ix_out = int((u_loc + aperture_size/2.0d0)/dx) + 1
        iy_out = int((v_loc + aperture_size/2.0d0)/dy) + 1
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (ix_out < 1 .or. ix_out > nx) ix_out = 0
        if (iy_out < 1 .or. iy_out > ny) iy_out = 0
    end subroutine

    ! 定义 project_point_to_detector 计算单元，封装该步骤的数据处理规则。
    subroutine project_point_to_detector(point, valid_projection, hit_point, detector_u_loc, detector_v_loc)
        implicit none
        real(8), intent(in) :: point(3)
        ! 声明逻辑开关，用于控制对应计算或输出路径。
        logical, intent(out) :: valid_projection
        real(8), intent(out) :: hit_point(3), detector_u_loc, detector_v_loc
        real(8) :: ray_dir(3), denom, distance_to_plane, diff_vec(3), ray_norm

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        valid_projection = .false.
        hit_point = 0.0d0
        detector_u_loc = 0.0d0
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        detector_v_loc = 0.0d0

        ray_dir = point - aperture_center
        ray_norm = sqrt(sum(ray_dir**2))
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (ray_norm <= EPS) return
        ray_dir = ray_dir / ray_norm

        denom = sum(ray_dir * detector_normal)
        if (abs(denom) <= EPS) return

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        distance_to_plane = sum((detector_center - aperture_center) * detector_normal) / denom
        if (distance_to_plane <= EPS) return

        hit_point = aperture_center + distance_to_plane * ray_dir
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        diff_vec = hit_point - detector_center
        detector_u_loc = sum(diff_vec * detector_u)
        detector_v_loc = sum(diff_vec * detector_v)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        valid_projection = .true.
    end subroutine project_point_to_detector

!===================== Unified global MC: τ & ψ ===============================

    subroutine run_monte_carlo_simulation()
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer :: source_sphere, bounce, hit_sphere, event_kind, last_hit_sphere
        integer(8) :: i_ray
        ! 声明计数器、索引或离散控制参数。
        integer :: ix_hit, iy_hit, i
        real(8) :: ray_origin(3), ray_dir(3), new_dir(3)
        real(8) :: hit_point(3), hit_normal(3), aperture_pos(3)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: this_energy_per_ray, energy_this_ray, t_hit, rand_val
        real(8) :: local_u, local_v
        real(8) :: cos_to_axis, omega_cap, q_inside, weight_ray
        real(8) :: wt_closure_res
        ! 声明逻辑开关，用于控制对应计算或输出路径。
        logical :: hit_any, hit_aperture, is_direct, inside_cap

        write(*,*)
        write(*,'(A)') ' ============================================================'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A)') '   Global Monte Carlo for IR transfer factors (tau, psi)'
        write(*,'(A)') '   (with mixed importance sampling to pupil)'
        write(*,'(A)') ' ============================================================'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,*)

        do source_sphere = 1, num_spheres
            if (.not. participates_in_ir_source(source_sphere)) cycle
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(*,'(A,I3,A,I3)') &
                ' >> MC source sphere ', source_sphere,' /',num_spheres

            this_energy_per_ray = 1.0d0 / dble(rays_per_sphere)

            do i_ray = 1_8, rays_per_sphere
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if (mod(i_ray,REPORT_INTERVAL) == 0_8) then
                    write(*,'(A,F6.1,A)') '    Progress: ', &
                        100.0d0*dble(i_ray)/dble(rays_per_sphere),'%'
                end if

                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                rays_emitted(source_sphere) = rays_emitted(source_sphere) + 1_8

                call random_number(rand_val)
                if (cone_prob(source_sphere) > 0.0d0 .and. rand_val > mc_mix_beta) then
                    call generate_direction_in_cone(ray_dir, cone_axis(:,source_sphere), &
                                                    cone_cos_theta_max(source_sphere))
                ! 当前条件不成立时执行替代计算路径。
                else
                    call generate_isotropic_direction(ray_dir)
                end if

                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                ray_origin = sphere_centers(:,source_sphere) + &
                             (sphere_radius(source_sphere)+EPS)*ray_dir
                is_direct = .true.

                if (cone_prob(source_sphere) > 0.0d0) then
                    ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                    cos_to_axis = sum(ray_dir*cone_axis(:,source_sphere))
                    inside_cap  = (cos_to_axis >= cone_cos_theta_max(source_sphere))
                else
                    ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                    inside_cap = .false.
                end if

                if (cone_prob(source_sphere) > 0.0d0 .and. inside_cap) then
                    ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                    omega_cap = 4.0d0*PI*cone_prob(source_sphere)
                    q_inside = mc_mix_beta/(4.0d0*PI) + (1.0d0-mc_mix_beta)/omega_cap
                    weight_ray = (1.0d0/(4.0d0*PI)) / q_inside
                ! 当前条件不成立时执行替代计算路径。
                else
                    if (mc_mix_beta > 0.0d0) then
                        weight_ray = 1.0d0 / mc_mix_beta
                    ! 当前条件不成立时执行替代计算路径。
                    else
                        weight_ray = 1.0d0
                    end if
                end if

                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                energy_this_ray = this_energy_per_ray * weight_ray
                wt_emitted(source_sphere) = wt_emitted(source_sphere) + energy_this_ray
                last_hit_sphere = source_sphere

                ! 进入迭代计算，逐项更新仿真状态或累计量。
                do bounce = 0, max_bounces

                    call find_nearest_transport_event(ray_origin, ray_dir, last_hit_sphere, &
                                                      event_kind, hit_sphere, t_hit, &
                                                      hit_point, hit_normal, local_u, local_v)
                    hit_aperture = (event_kind == 2)
                    ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                    hit_any = (event_kind == 1)
                    aperture_pos = hit_point
                    if (hit_aperture) then
                        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                        rays_hit_pupil = rays_hit_pupil + 1_8
                        wt_pupil_cross(source_sphere) = wt_pupil_cross(source_sphere) + energy_this_ray

                        call get_grid_index(local_u, local_v, ix_hit, iy_hit)
                        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                        if (ix_hit > 0 .and. iy_hit > 0) then
                            rays_on_sensor = rays_on_sensor + 1_8
                            wt_pupil_grid(source_sphere) = wt_pupil_grid(source_sphere) + energy_this_ray

                            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                            if (is_direct) then
                                grid_direct(ix_hit,iy_hit,source_sphere) = &
                                    grid_direct(ix_hit,iy_hit,source_sphere) + energy_this_ray
                                rays_direct_hit(source_sphere) = rays_direct_hit(source_sphere) + 1_8
                                power_direct(source_sphere)   = power_direct(source_sphere) + energy_this_ray
                                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                                wt_pupil_direct(source_sphere) = wt_pupil_direct(source_sphere) + energy_this_ray
                            else
                                grid_indirect(ix_hit,iy_hit,source_sphere) = &
                                    grid_indirect(ix_hit,iy_hit,source_sphere) + energy_this_ray
                                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                                rays_indirect_hit(source_sphere) = rays_indirect_hit(source_sphere) + 1_8
                                power_indirect(source_sphere)   = power_indirect(source_sphere) + energy_this_ray
                                wt_pupil_indirect(source_sphere) = wt_pupil_indirect(source_sphere) + energy_this_ray
                            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
                            end if
                        end if
                        exit
                    ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
                    end if

                    if (.not. hit_any) then
                        vf_energy_s2space(source_sphere) = vf_energy_s2space(source_sphere) + energy_this_ray
                        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                        rays_escaped = rays_escaped + 1_8
                        rays_escaped_source(source_sphere) = rays_escaped_source(source_sphere) + 1_8
                        wt_space_escape(source_sphere) = wt_space_escape(source_sphere) + energy_this_ray
                        ! 满足当前控制条件后结束或跳过本次处理。
                        exit
                    end if

                    call random_number(rand_val)

                    if (rand_val > sphere_ir_reflectivity(hit_sphere)) then
                        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                        rays_terminated_on_sphere(source_sphere) = &
                            rays_terminated_on_sphere(source_sphere) + 1_8
                        wt_sphere_term(source_sphere) = wt_sphere_term(source_sphere) + energy_this_ray

                        if (hit_sphere == source_sphere) then
                            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                            rays_self_absorbed(source_sphere) = rays_self_absorbed(source_sphere) + 1_8
                            wt_self_absorb(source_sphere) = wt_self_absorb(source_sphere) + energy_this_ray
                        end if

                        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                        if (hit_sphere /= source_sphere) then
                            vf_energy_s2s(source_sphere, hit_sphere) = &
                                vf_energy_s2s(source_sphere, hit_sphere) + energy_this_ray
                        else
                            ! 保持你原有矩阵记账逻辑不变
                            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                            vf_energy_s2s(source_sphere,source_sphere) = &
                                vf_energy_s2s(source_sphere,source_sphere) + energy_this_ray
                        end if
                        exit
                    ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
                    end if

                    is_direct = .false.
                    call generate_cosine_direction(new_dir, hit_normal)
                    ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                    ray_origin = hit_point + EPS*hit_normal
                    ray_dir = new_dir
                    last_hit_sphere = hit_sphere

                    if (bounce == max_bounces) then
                        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                        vf_energy_s2space(source_sphere) = vf_energy_s2space(source_sphere) + energy_this_ray
                        rays_escaped = rays_escaped + 1_8
                        rays_escaped_source(source_sphere) = rays_escaped_source(source_sphere) + 1_8
                        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                        wt_space_escape(source_sphere) = wt_space_escape(source_sphere) + energy_this_ray
                        exit
                    end if
                ! 结束本轮迭代范围，继续处理汇总后的计算结果。
                end do
            end do
        end do

        ! 调用子过程完成当前数值计算或状态更新。
        call finalize_transfer_factors()

        write(*,'(A)') '--- Ray hit summary (global MC) ---'
        do i = 1, num_spheres
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(*,'(I3,2(A,I12))') i, '  direct=', rays_direct_hit(i), &
                                       '  indirect=', rays_indirect_hit(i)
        end do

        write(*,'(A,I12)') 'Total hits (pupil grid): ', sum(rays_direct_hit) + sum(rays_indirect_hit)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,I12)') 'Rays that crossed pupil: ', rays_hit_pupil
        write(*,'(A,I12)') 'Rays on pupil grid: ', rays_on_sensor
        write(*,'(A,I12)') 'Rays escaped to space: ', rays_escaped
        write(*,'(A,I12)') 'Rays terminated on sphere: ', sum(rays_terminated_on_sphere)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,I12)') 'Self-absorbed rays: ', sum(rays_self_absorbed)
        write(*,'(A,I12)') 'Count closure residual: ', &
            sum(rays_emitted) - ( &
            sum(rays_direct_hit) + sum(rays_indirect_hit) + &
            sum(rays_escaped_source) + sum(rays_terminated_on_sphere) )

        write(*,*)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,ES14.6)') 'Weighted emitted total        : ', sum(wt_emitted)
        write(*,'(A,ES14.6)') 'Weighted pupil cross total    : ', sum(wt_pupil_cross)
        write(*,'(A,ES14.6)') 'Weighted pupil direct total   : ', sum(wt_pupil_direct)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,ES14.6)') 'Weighted pupil indirect total : ', sum(wt_pupil_indirect)
        write(*,'(A,ES14.6)') 'Weighted sphere term total    : ', sum(wt_sphere_term)
        write(*,'(A,ES14.6)') 'Weighted space escape total   : ', sum(wt_space_escape)

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        wt_closure_res = sum(wt_emitted) - &
                         (sum(wt_pupil_cross) + sum(wt_sphere_term) + sum(wt_space_escape))
        write(*,'(A,ES14.6)') 'Weighted closure residual     : ', wt_closure_res
    end subroutine run_monte_carlo_simulation

    ! 定义 finalize_transfer_factors 计算单元，封装该步骤的数据处理规则。
    subroutine finalize_transfer_factors()
        implicit none
        integer :: s
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: total_s

        do s = 1, num_spheres
            if (.not. participates_in_ir_source(s)) then
                sphere_view_factor(s,:) = 0.0d0
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                sphere_view_factor_to_space(s) = 0.0d0
                grid_direct(:,:,s) = 0.0d0
                grid_indirect(:,:,s) = 0.0d0
                ! 满足当前控制条件后结束或跳过本次处理。
                cycle
            end if

            total_s = vf_energy_s2space(s) + &
                      sum(vf_energy_s2s(s,:)) + &
                      sum(grid_direct(:,:,s)) + &
                      sum(grid_indirect(:,:,s))

            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (total_s > 0.0d0) then
                sphere_view_factor(s,:)        = vf_energy_s2s(s,:)       / total_s
                sphere_view_factor_to_space(s) = vf_energy_s2space(s)     / total_s
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                grid_direct(:,:,s)   = grid_direct(:,:,s)   / total_s
                grid_indirect(:,:,s) = grid_indirect(:,:,s) / total_s
            else
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                sphere_view_factor(s,:)        = 0.0d0
                sphere_view_factor_to_space(s) = 1.0d0
                grid_direct(:,:,s)   = 0.0d0
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                grid_indirect(:,:,s) = 0.0d0
            end if
        end do

        ! Freeze the normalized dimensionless aperture transfer factors.
        ! The public grid_direct/grid_indirect arrays are power-valued outputs
        ! and are rebuilt from these factors in calculate_total_energy().
        grid_direct_factor = grid_direct
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        grid_indirect_factor = grid_indirect
        do s = 1, num_spheres
            power_direct_factor(s) = sum(grid_direct_factor(:,:,s))
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            power_indirect_factor(s) = sum(grid_indirect_factor(:,:,s))
        end do

        write(*,'(A,ES12.4)') '   Max sphere-sphere factor (MC): ', maxval(sphere_view_factor)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,ES12.4)') '   Min factor to space (MC):      ', minval(sphere_view_factor_to_space)
        write(*,*)
    end subroutine finalize_transfer_factors

!====================== Imaging: total energy & spots =========================

    ! 定义 calculate_total_energy 计算单元，封装该步骤的数据处理规则。
    subroutine calculate_total_energy()
        implicit none
        integer :: ix, iy, s
        ! 声明计数器、索引或离散控制参数。
        integer :: uf_dbg2

        do s = 1, num_spheres
            do iy = 1, ny
                ! 进入迭代计算，逐项更新仿真状态或累计量。
                do ix = 1, nx
                    grid_direct(ix,iy,s) = grid_direct_factor(ix,iy,s) * sphere_power(s)
                    grid_indirect(ix,iy,s) = grid_indirect_factor(ix,iy,s) * sphere_power(s)
                end do
            ! 结束本轮迭代范围，继续处理汇总后的计算结果。
            end do
            power_direct(s) = power_direct_factor(s) * sphere_power(s)
            power_indirect(s) = power_indirect_factor(s) * sphere_power(s)
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do

        do iy = 1, ny
            do ix = 1, nx
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                grid_direct_total(ix,iy)   = sum(grid_direct(ix,iy,:))
                grid_indirect_total(ix,iy) = sum(grid_indirect(ix,iy,:))
                grid_total(ix,iy)          = grid_direct_total(ix,iy) + grid_indirect_total(ix,iy)
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                grid_irradiance(ix,iy)     = grid_total(ix,iy) / cell_area
            end do
        end do

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        uf_dbg2 = 99
        if (WRITE_DEBUG_OUTPUTS) then
            open(unit=uf_dbg2, file=output_path('mc_debug.log'), status='unknown', position='append')
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf_dbg2,'(A)') '=== unified MC: Ray hit summary ==='
            write(uf_dbg2,'(A)') 's    direct_hits    indirect_hits'
            do s = 1, num_spheres
                write(uf_dbg2,'(I3,2(1X,I12))') s, rays_direct_hit(s), rays_indirect_hit(s)
            ! 结束本轮迭代范围，继续处理汇总后的计算结果。
            end do
            write(uf_dbg2,'(A,I12)') 'Total hits: ', sum(rays_direct_hit) + sum(rays_indirect_hit)
            write(uf_dbg2,*)
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            close(uf_dbg2)
        end if
    end subroutine calculate_total_energy

  ! 定义 build_spot_image 计算单元，封装该步骤的数据处理规则。
  subroutine build_spot_image()
    implicit none
    integer :: s, ix_c, iy_c, i_min, i_max, j_min, j_max
    ! 声明计数器、索引或离散控制参数。
    integer :: i, j
    integer :: uf_dbg
    real(8) :: hit_point(3)
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: Uc, Vc
    real(8) :: Es
    real(8) :: dxs, dys
    ! 声明双精度数值或物理量，为后续仿真计算保存状态。
    real(8) :: Ucell, Vcell
    real(8) :: rr2, R_phys2
    real(8) :: cell_count
    ! 声明逻辑开关，用于控制对应计算或输出路径。
    logical :: projection_ok

    dxs = spot_plane_size / dble(nx)
    dys = spot_plane_size / dble(ny)

    spot_total = 0.0d0

    ! 更新仿真变量或中间量，供下一数值步骤继续计算。
    uf_dbg = 98
    if (WRITE_DEBUG_OUTPUTS) then
        open(unit=uf_dbg, file=output_path('spot_debug.log'), status='replace')
    ! 当前条件不成立时执行替代计算路径。
    else
        open(unit=uf_dbg, status='scratch')
    end if

    ! 按照接口约定读写数据文件，并维护文件单元状态。
    write(uf_dbg,'(A)') '=== build_spot_image debug info ==='
    write(uf_dbg,'(A,I6)')     'num_spheres       = ', num_spheres
    write(uf_dbg,'(A,I6)')     'nx, ny            = ', nx
    ! 按照接口约定读写数据文件，并维护文件单元状态。
    write(uf_dbg,'(A,F10.4)')  'spot_plane_size   = ', spot_plane_size
    write(uf_dbg,'(A,F10.4)')  'spot_focal_length = ', spot_focal_length
    write(uf_dbg,'(A,I6)')     'spot_radius_cells = ', spot_radius_cells
    ! 按照接口约定读写数据文件，并维护文件单元状态。
    write(uf_dbg,'(A,ES14.6)') 'sum(grid_total)   = ', sum(grid_total)
    write(uf_dbg,'(A,3ES14.6)') 'aperture_center   = ', aperture_center
    write(uf_dbg,'(A,3ES14.6)') 'detector_center   = ', detector_center
    ! 按照接口约定读写数据文件，并维护文件单元状态。
    write(uf_dbg,*)

    do s = 1, num_spheres
        if (.not. participates_in_imaging(s)) cycle
        Es = sum(grid_direct(:,:,s)) + sum(grid_indirect(:,:,s))
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (Es <= 0.0d0) cycle

        call project_point_to_detector(sphere_centers(:,s), projection_ok, hit_point, Uc, Vc)
        if (.not. projection_ok) cycle

        ! 找到投影中心所在的网格
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        ix_c = int((Uc + spot_plane_size/2.0d0) / dxs) + 1
        iy_c = int((Vc + spot_plane_size/2.0d0) / dys) + 1

        if (ix_c < 1 .or. ix_c > nx .or. iy_c < 1 .or. iy_c > ny) cycle

        !==============================================================
        ! 关键修改：spot_radius_cells = 0 时，
        ! 能量全部集中在投影中心所在的单个网格
        !==============================================================
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (spot_radius_cells <= 0) then
            ! 点光斑模式：全部能量放入单个网格
            spot_total(ix_c, iy_c) = spot_total(ix_c, iy_c) + Es
        else
            ! 扩展光斑模式：能量均匀分布在圆形区域内
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            i_min = max(1,  ix_c - spot_radius_cells)
            i_max = min(nx, ix_c + spot_radius_cells)
            j_min = max(1,  iy_c - spot_radius_cells)
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            j_max = min(ny, iy_c + spot_radius_cells)

            R_phys2 = (spot_radius_cells * dxs)**2

            ! 第一遍：统计圆内网格数
            cell_count = 0.0d0
            ! 进入迭代计算，逐项更新仿真状态或累计量。
            do j = j_min, j_max
                Vcell = -spot_plane_size/2.0d0 + (dble(j) - 0.5d0) * dys
                do i = i_min, i_max
                    Ucell = -spot_plane_size/2.0d0 + (dble(i) - 0.5d0) * dxs
                    ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                    rr2 = (Ucell - Uc)**2 + (Vcell - Vc)**2
                    if (rr2 <= R_phys2) cell_count = cell_count + 1.0d0
                end do
            ! 结束本轮迭代范围，继续处理汇总后的计算结果。
            end do

            if (cell_count <= 0.0d0) then
                ! 安全回退：如果圆内没有网格中心，
                ! 退化到单网格模式
                spot_total(ix_c, iy_c) = spot_total(ix_c, iy_c) + Es
            ! 当前条件不成立时执行替代计算路径。
            else
                ! 第二遍：均匀分配能量
                do j = j_min, j_max
                    Vcell = -spot_plane_size/2.0d0 + (dble(j) - 0.5d0) * dys
                    ! 进入迭代计算，逐项更新仿真状态或累计量。
                    do i = i_min, i_max
                        Ucell = -spot_plane_size/2.0d0 + (dble(i) - 0.5d0) * dxs
                        rr2 = (Ucell - Uc)**2 + (Vcell - Vc)**2
                        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                        if (rr2 <= R_phys2) then
                            spot_total(i, j) = spot_total(i, j) + Es / cell_count
                        end if
                    ! 结束本轮迭代范围，继续处理汇总后的计算结果。
                    end do
                end do
            end if
        end if

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf_dbg,'(I3,1X,ES12.4,5(1X,F8.4),2(1X,I5))') &
            s, Es, hit_point(1), hit_point(2), hit_point(3), Uc, Vc, ix_c, iy_c

    end do

    spot_irradiance = spot_total / (dxs * dys)

    ! 按照接口约定读写数据文件，并维护文件单元状态。
    write(uf_dbg,*)
    write(uf_dbg,'(A,ES14.6)') 'sum(spot_total) = ', sum(spot_total)
    close(uf_dbg)

  ! 结束当前计算单元，使过程边界保持清晰。
  end subroutine build_spot_image

!===================== Printing & Output ======================================

    subroutine print_header()
        real(8) :: T_min, T_max
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,*)
        write(*,'(A)') ' ============================================================'
        write(*,'(A)') '   Configuration Summary'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A)') ' ============================================================'
        write(*,*)
        write(*,'(A)') ' [Aperture & Image Plane]'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,F10.3,A)')  '   Aperture size: ', aperture_size,' m'
        write(*,'(A,3F10.3)')   '   Aperture center: ', aperture_center
        write(*,'(A,3F10.5)')   '   Aperture normal: ', aperture_normal
        write(*,'(A,3F10.3)')   '   Detector center: ', detector_center
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,3F10.5)')   '   Detector normal: ', detector_normal
        write(*,'(A,L1)')       '   Aperture tracks target: ', aperture_track_target
        write(*,'(A,L1)')       '   Detector tracks target: ', detector_track_target
!        write(*,'(A,F10.3,A)')  '   Image distance:  ', image_distance,' m'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,3F10.3)')   '   Image center:    ', image_center
        write(*,'(A,I5,A,I5)')  '   Image grid:      ', nx,' x ',ny
        write(*,*)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A)') ' [Group Motion]'
        write(*,'(A,3F10.3)')   '   Group center:    ', group_center
        write(*,'(A,3F10.5)')   '   Group normal:    ', group_normal
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,3F10.3)')   '   Group velocity:  ', group_velocity
        write(*,'(A,3F10.3)')   '   Group accel:     ', group_acceleration
        write(*,'(A,ES14.6)')  '   Range to detector: ', target_detector_range
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,2ES14.6)') '   LOS az/el [rad]:   ', target_detector_los_azimuth, &
                                target_detector_los_elevation
        write(*,'(A,ES14.6)')  '   LOS rate [rad/s]:  ', target_detector_los_rate_mag
        write(*,'(A)') '   Member stages at current frame:'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,I6)')       '      Inactive: ', count_members_in_stage(MEMBER_STAGE_INACTIVE)
        write(*,'(A,I6)')       '      Formation: ', count_members_in_stage(MEMBER_STAGE_FORMATION)
        write(*,'(A,I6)')       '      Released: ', count_members_in_stage(MEMBER_STAGE_RELEASED)
        write(*,*)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A)') ' [Environment]'
        write(*,'(A,F10.1,A)')  '   Solar flux:    ', solar_flux,' W/m^2'
        write(*,'(A,3F8.3)')    '   Solar dir:     ', solar_direction
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,F10.1,A)')  '   Background T:  ', environment_temp,' K'
        write(*,*)
        write(*,'(A)') ' [Spheres]'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,I6)')       '   Count:    ', num_spheres
        T_min = minval(sphere_initial_temp); T_max = maxval(sphere_initial_temp)
        write(*,'(A,F8.2,A,F8.2,A)') '   Initial T range: ',T_min,' - ',T_max,' K'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,F8.3,A,F8.3,A)') '   Radius range: ',minval(sphere_radius),' - ', &
                                      maxval(sphere_radius),' m'
        write(*,*)
        write(*,'(A)') ' [Monte Carlo]'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,I15)') ' Rays/sphere (IR): ', rays_per_sphere
        write(*,'(A,I15)') ' Rays (Solar): ', rays_solar
        write(*,'(A,I10)') ' Max bounces: ', max_bounces
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,F6.3)') ' MC_MIX_BETA: ', mc_mix_beta
        write(*,'(A,I12)')  ' RANDOM_SEED_USED: ', random_seed_used
        write(*,*)
    end subroutine

    ! 定义 print_temperature_results 计算单元，封装该步骤的数据处理规则。
    subroutine print_temperature_results()
        integer :: idx, participant_count, printed_count
        real(8) :: T_min, T_max, T_avg

        ! 调用子过程完成当前数值计算或状态更新。
        call get_participant_temperature_statistics(T_avg, T_min, T_max, participant_count)
        write(*,*)
        write(*,'(A)') ' [Temperature Results: released thermal participants only]'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A)') ' ------------------------------------------------------------'
        write(*,'(A)') '   ID    T[K]     Q_sol      Q_int      Q_space    Q_exch'
        printed_count = 0
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do idx = 1, num_spheres
            if (.not. participates_in_statistics(idx)) cycle
            printed_count = printed_count + 1
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (printed_count > 20) exit
            write(*,'(I5,F9.2,4ES11.2)') idx, sphere_temperature(idx), &
                sphere_solar_heat(idx), sphere_internal_heat(idx), &
                sphere_radiation_to_space(idx), sphere_net_exchange(idx)
        end do
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (participant_count > 20) write(*,'(A)') '   ... (truncated)'
        write(*,'(A)') ' ------------------------------------------------------------'
        write(*,'(A,I0)') '   Participant count: ', participant_count
        write(*,'(A,F10.2,A)') '   Average: ',T_avg,' K'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,F10.2,A)') '   Min:     ',T_min,' K'
        write(*,'(A,F10.2,A)') '   Max:     ',T_max,' K'
        write(*,'(A,F10.2,A)') '   Range:   ',T_max-T_min,' K'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,*)
    end subroutine print_temperature_results

    subroutine print_radiation_results()
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: td, ti, tt
        td = sum(grid_direct_total)
        ti = sum(grid_indirect_total)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        tt = sum(grid_total)
        write(*,*)
        write(*,'(A)') ' [Radiation Results on Image Plane]'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A)') ' ------------------------------------------------------------'
        write(*,'(A,ES15.6,A)') '   Total emitted:   ', sum(sphere_power),' W'
        write(*,'(A,ES15.6,A)') '   Direct received: ', td,' W'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,ES15.6,A)') '   Indirect received:',ti,' W'
        write(*,'(A,ES15.6,A)') '   Total received:  ', tt,' W'
        write(*,'(A,F15.8,A)')  '   Efficiency:      ',100.0d0*tt/sum(sphere_power),' %'
        write(*,*)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,ES15.6,A)') '   Max irradiance:  ',maxval(grid_irradiance),' W/m^2'
        write(*,'(A,ES15.6,A)') '   Avg irradiance:  ',tt/(nx*ny*cell_area),' W/m^2'
        write(*,*)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(*,'(A,F12.2,A)')  '   Total time:      ',end_time-start_time,' s'
        write(*,*)
    end subroutine

    ! 定义 write_verification_report 计算单元，封装该步骤的数据处理规则。
    subroutine write_verification_report()
        implicit none
        integer :: uf
        ! 声明计数器、索引或离散控制参数。
        integer(8) :: total_accounted, total_unclosed

        uf = 98
        open(unit=uf, file=output_path('verification_report.txt'), status='replace')

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A)') '===================================================='
        write(uf,'(A)') 'Verification Report'
        write(uf,'(A)') '===================================================='
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,A)') 'Date: ', trim(date_str)
        write(uf,'(A,A)') 'Time: ', trim(time_str)
        write(uf,*)

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)') 'Random seed used: ', random_seed_used
        write(uf,'(A,F10.6)') 'MC_MIX_BETA: ', mc_mix_beta
        write(uf,'(A,I0)') 'Rays per sphere: ', rays_per_sphere
        write(uf,'(A,ES15.6)') 'Latest frame compute time [s]: ', last_frame_compute_time
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)') 'Cumulative compute time [s]: ', cumulative_compute_time
        write(uf,'(A,ES15.6)') 'Total compute time [s]: ', cumulative_compute_time
        write(uf,'(A,I0)') 'Active object count: ', active_object_count()
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)') 'Active mass [kg]: ', active_mass_total()
        write(uf,'(A,ES15.6)') 'Active internal energy [J]: ', active_internal_energy_total()
        write(uf,'(A,ES15.6)') 'Cumulative release-carried energy [J]: ', cumulative_release_carried_energy
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)') 'Cumulative system energy residual [J]: ', cumulative_system_energy_residual
        write(uf,'(A,I0)') 'Rays solar: ', rays_solar
        write(uf,'(A,I0)') 'Max bounces: ', max_bounces
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,*)

        write(uf,'(A,I20)') 'Total rays emitted: ', sum(rays_emitted)
        write(uf,'(A,I20)') 'Total rays hit pupil: ', rays_hit_pupil
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I20)') 'Total rays on sensor grid: ', rays_on_sensor
        write(uf,'(A,I20)') 'Total rays escaped: ', rays_escaped
        write(uf,'(A,I20)') 'Total direct hits: ', sum(rays_direct_hit)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I20)') 'Total indirect hits: ', sum(rays_indirect_hit)
        write(uf,'(A,I20)') 'Total terminated on sphere: ', sum(rays_terminated_on_sphere)
        write(uf,'(A,I20)') 'Total self-absorbed: ', sum(rays_self_absorbed)

        total_accounted = sum(rays_direct_hit) + sum(rays_indirect_hit) + &
                          sum(rays_escaped_source) + sum(rays_terminated_on_sphere)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        total_unclosed = sum(rays_emitted) - total_accounted

        write(uf,'(A,I20)') 'Total accounted rays: ', total_accounted
        write(uf,'(A,I20)') 'Count closure residual: ', total_unclosed
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES14.6)') 'Relative closure error: ', &
            dble(abs(total_unclosed)) / max(1.0d0, dble(sum(rays_emitted)))
        write(uf,*)

        write(uf,'(A,ES14.6)') 'IR direct energy accumulator: ', sum(power_direct)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES14.6)') 'IR indirect energy accumulator: ', sum(power_indirect)
        write(uf,'(A,ES14.6)') 'Total solar absorbed: ', sum(sphere_solar_heat)
        write(uf,*)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES14.6)') 'Total emitted power: ', sum(sphere_power)
        write(uf,'(A,ES14.6)') 'Image plane total received: ', sum(grid_total)
        write(uf,'(A,ES14.6)') 'Max irradiance: ', maxval(grid_irradiance)

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)
    end subroutine write_verification_report

    subroutine get_frame_filename(frame_idx, suffix, filename)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in) :: frame_idx
        character(len=*), intent(in) :: suffix
        character(len=*), intent(out) :: filename
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=256) :: base_filename

        write(base_filename,'("frame_",I6.6,"_",A)') frame_idx, trim(suffix)
        filename = output_path(trim(base_filename))
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine get_frame_filename

    subroutine write_frame_geometry_snapshot(frame_idx, time_value)
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: frame_idx
        real(8), intent(in) :: time_value
        integer :: uf, idx
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=256) :: filename
        real(8) :: velocity_now(3)

        call get_frame_filename(frame_idx, 'geometry.dat', filename)

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        uf = 43
        open(unit=uf, file=filename, status='replace')
        write(uf,'(A,I0)') 'frame=', frame_idx
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)') 'time_s=', time_value
        write(uf,'(A,3ES15.6)') 'group_center=', group_center
        write(uf,'(A,3ES15.6)') 'group_velocity=', group_velocity
        write(uf,'(A,3ES15.6)') 'group_acceleration=', group_acceleration
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,3ES15.6)') 'group_normal=', group_normal
        write(uf,'(A,3ES15.6)') 'aperture_center=', aperture_center
        write(uf,'(A,3ES15.6)') 'aperture_velocity=', aperture_velocity
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,3ES15.6)') 'aperture_acceleration=', aperture_acceleration
        write(uf,'(A,3ES15.6)') 'aperture_normal=', aperture_normal
        write(uf,'(A,3ES15.6)') 'detector_center=', detector_center
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,3ES15.6)') 'detector_velocity=', detector_velocity
        write(uf,'(A,3ES15.6)') 'detector_acceleration=', detector_acceleration
        write(uf,'(A,3ES15.6)') 'detector_normal=', detector_normal
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,3ES15.6)') 'target_detector_relative_position=', target_detector_relative_position
        write(uf,'(A,3ES15.6)') 'target_detector_relative_velocity=', target_detector_relative_velocity
        write(uf,'(A,ES15.6)') 'target_detector_range=', target_detector_range
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)') 'target_detector_los_azimuth_rad=', target_detector_los_azimuth
        write(uf,'(A,ES15.6)') 'target_detector_los_elevation_rad=', target_detector_los_elevation
        write(uf,'(A,3ES15.6)') 'target_detector_los_rate_vector=', target_detector_los_rate
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)') 'target_detector_los_rate_mag=', target_detector_los_rate_mag
        write(uf,'(A)') 'sphere_id,is_active,motion_stage,release_time_s,rel_x,rel_y,rel_z,x,y,z,vx,vy,vz'
        do idx = 1, num_spheres
            velocity_now = sphere_world_velocity(:,idx)
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(I0,A,L1,A,I0,10(A,ES15.6))') idx, ',', sphere_is_active(idx), ',', sphere_motion_stage(idx), &
                ',', sphere_release_time(idx), ',', sphere_centers_initial(1,idx), ',', sphere_centers_initial(2,idx), &
                ',', sphere_centers_initial(3,idx), ',', sphere_centers(1,idx), ',', sphere_centers(2,idx), &
                ',', sphere_centers(3,idx), ',', velocity_now(1), ',', velocity_now(2), ',', velocity_now(3)
        end do
        close(uf)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine write_frame_geometry_snapshot

    subroutine write_frame_temperature_snapshot(frame_idx)
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: frame_idx
        integer :: uf, idx, participant_count
        character(len=256) :: filename

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        participant_count = count_statistics_participants()
        call get_frame_filename(frame_idx, 'sphere_temperature.dat', filename)

        uf = 44
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=filename, status='replace')
        write(uf,'(A)') 'TITLE = "Sphere Temperature"'
        write(uf,'(A)') 'VARIABLES = "X", "Y", "Z", "T [K]", "Power [W]", "Q_solar", "Q_int", "Q_space", "Q_exch"'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0,A)') 'ZONE T="ReleasedSpheres", I=',participant_count,', F=POINT'
        do idx = 1, num_spheres
            if (.not. participates_in_thermal(idx)) cycle
            write(uf,'(9ES15.6)') sphere_centers(1,idx),sphere_centers(2,idx), &
                sphere_centers(3,idx),sphere_temperature(idx),sphere_power(idx), &
                sphere_solar_heat(idx),sphere_internal_heat(idx), &
                sphere_radiation_to_space(idx),sphere_net_exchange(idx)
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do
        close(uf)
    end subroutine write_frame_temperature_snapshot

    ! 定义 write_frame_spot_snapshot 计算单元，封装该步骤的数据处理规则。
    subroutine write_frame_spot_snapshot(frame_idx)
        implicit none
        integer, intent(in) :: frame_idx
        ! 声明计数器、索引或离散控制参数。
        integer :: uf, ix, iy
        real(8) :: dxs, dys, U, V
        character(len=256) :: filename

        ! 调用子过程完成当前数值计算或状态更新。
        call get_frame_filename(frame_idx, 'spot_image.dat', filename)

        dxs = spot_plane_size / dble(nx)
        dys = spot_plane_size / dble(ny)

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        uf = 45
        open(unit=uf, file=filename, status='replace')
        write(uf,'(A)') 'TITLE = "Detector Plane Spot Image (one spot per object)"'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A)') 'VARIABLES = "U [m]", "V [m]", "Total [W]", "Irradiance [W/m2]"'
        write(uf,'(A,I0,A,I0,A)') 'ZONE T="DetectorPlane", I=',nx,', J=',ny,', F=POINT'

        do iy = 1, ny
            V = -spot_plane_size/2.0d0 + (dble(iy)-0.5d0)*dys
            ! 进入迭代计算，逐项更新仿真状态或累计量。
            do ix = 1, nx
                U = -spot_plane_size/2.0d0 + (dble(ix)-0.5d0)*dxs
                write(uf,'(4ES15.6)') U, V, spot_total(ix,iy), spot_irradiance(ix,iy)
            ! 结束本轮迭代范围，继续处理汇总后的计算结果。
            end do
        end do

        close(uf)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine write_frame_spot_snapshot

    subroutine write_frame_summary_row(frame_idx, time_value)
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: frame_idx
        real(8), intent(in) :: time_value
        integer :: uf, ios, participant_count
        ! 声明逻辑开关，用于控制对应计算或输出路径。
        logical :: file_exists, write_header
        character(len=1) :: first_char
        real(8) :: participant_temp_avg, participant_temp_min, participant_temp_max

        ! 调用子过程完成当前数值计算或状态更新。
        call get_participant_temperature_statistics(participant_temp_avg, participant_temp_min, &
                                                     participant_temp_max, participant_count)

        uf = 46
        inquire(file=output_path('frame_summary.csv'), exist=file_exists)
        write_header = .false.
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. file_exists) then
            write_header = .true.
        else
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            open(unit=uf, file=output_path('frame_summary.csv'), status='old', iostat=ios)
            if (ios == 0) then
                read(uf, '(A1)', iostat=ios) first_char
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if (ios /= 0) write_header = .true.
                close(uf)
            else
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                write_header = .true.
            end if
        end if

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=output_path('frame_summary.csv'), status='unknown', position='append')
        if (write_header) then
            write(uf,'(A)') &
                'frame,time_s,group_x,group_y,group_z,group_vx,group_vy,group_vz,' // &
                'aperture_x,aperture_y,aperture_z,detector_x,detector_y,detector_z,' // &
                'rel_x,rel_y,rel_z,rel_vx,rel_vy,rel_vz,range_m,los_az_rad,' // &
                'los_el_rad,los_rate_rad_s,inactive_count,formation_count,' // &
                'released_count,active_count,active_mass_kg,active_internal_energy_J,' // &
                'temp_avg,temp_min,temp_max,total_received_W,max_irradiance_W_m2,' // &
                'spot_total_power_W,spot_peak_cell_power_W,spot_peak_irradiance_W_m2,' // &
                'total_solar_absorbed_W,cumulative_release_carried_energy_J,' // &
                'cumulative_system_energy_residual_J,frame_compute_time_s,cumulative_compute_time_s'
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if

        write(uf,'(I0)',advance='no') frame_idx
        write(uf,'(A,ES15.6)',advance='no') ',',time_value
        write(uf,'(A,ES15.6)',advance='no') ',',group_center(1)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)',advance='no') ',',group_center(2)
        write(uf,'(A,ES15.6)',advance='no') ',',group_center(3)
        write(uf,'(A,ES15.6)',advance='no') ',',group_velocity(1)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)',advance='no') ',',group_velocity(2)
        write(uf,'(A,ES15.6)',advance='no') ',',group_velocity(3)
        write(uf,'(A,ES15.6)',advance='no') ',',aperture_center(1)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)',advance='no') ',',aperture_center(2)
        write(uf,'(A,ES15.6)',advance='no') ',',aperture_center(3)
        write(uf,'(A,ES15.6)',advance='no') ',',detector_center(1)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)',advance='no') ',',detector_center(2)
        write(uf,'(A,ES15.6)',advance='no') ',',detector_center(3)
        write(uf,'(A,ES15.6)',advance='no') ',',target_detector_relative_position(1)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)',advance='no') ',',target_detector_relative_position(2)
        write(uf,'(A,ES15.6)',advance='no') ',',target_detector_relative_position(3)
        write(uf,'(A,ES15.6)',advance='no') ',',target_detector_relative_velocity(1)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)',advance='no') ',',target_detector_relative_velocity(2)
        write(uf,'(A,ES15.6)',advance='no') ',',target_detector_relative_velocity(3)
        write(uf,'(A,ES15.6)',advance='no') ',',target_detector_range
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)',advance='no') ',',target_detector_los_azimuth
        write(uf,'(A,ES15.6)',advance='no') ',',target_detector_los_elevation
        write(uf,'(A,ES15.6)',advance='no') ',',target_detector_los_rate_mag
        write(uf,'(A,I0)',advance='no') ',',count_members_in_stage(MEMBER_STAGE_INACTIVE)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)',advance='no') ',',count_members_in_stage(MEMBER_STAGE_FORMATION)
        write(uf,'(A,I0)',advance='no') ',',count_members_in_stage(MEMBER_STAGE_RELEASED)
        write(uf,'(A,I0)',advance='no') ',',active_object_count()
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)',advance='no') ',',active_mass_total()
        write(uf,'(A,ES15.6)',advance='no') ',',active_internal_energy_total()
        write(uf,'(A,ES15.6)',advance='no') ',',participant_temp_avg
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)',advance='no') ',',participant_temp_min
        write(uf,'(A,ES15.6)',advance='no') ',',participant_temp_max
        write(uf,'(A,ES15.6)',advance='no') ',',sum(grid_total)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)',advance='no') ',',maxval(grid_irradiance)
        write(uf,'(A,ES15.6)',advance='no') ',',sum(spot_total)
        write(uf,'(A,ES15.6)',advance='no') ',',maxval(spot_total)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)',advance='no') ',',maxval(spot_irradiance)
        write(uf,'(A,ES15.6)',advance='no') ',',sum(sphere_solar_heat)
        write(uf,'(A,ES15.6)',advance='no') ',',cumulative_release_carried_energy
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)',advance='no') ',',cumulative_system_energy_residual
        write(uf,'(A,ES15.6)',advance='no') ',',last_frame_compute_time
        write(uf,'(A,ES15.6)') ',',cumulative_compute_time
        close(uf)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine write_frame_summary_row

    logical function should_write_full_spot_frame(frame_idx)
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: frame_idx

        if (save_full_spot_image) then
            should_write_full_spot_frame = .true.
            ! 满足当前控制条件后结束或跳过本次处理。
            return
        end if

        should_write_full_spot_frame = .false.
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. save_qa_full_image) return
        if (qa_image_written_count >= qa_image_max_frames) return
        if (mod(frame_idx, qa_image_frame_stride) /= 0) return

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        qa_image_written_count = qa_image_written_count + 1
        should_write_full_spot_frame = .true.
    end function should_write_full_spot_frame

    ! 定义 append_spot_point_row 计算单元，封装该步骤的数据处理规则。
    subroutine append_spot_point_row(uf, case_id, frame_idx, time_value, sphere_idx, active_i, &
                                     in_screen_i, screen_x, screen_y, pixel_x, pixel_y, &
                                     spot_power_W, spot_cell_power_W, spot_irradiance_W_m2, &
                                     distance_to_detector, los_azimuth, los_elevation, los_rate_magnitude)
        implicit none
        integer, intent(in) :: uf, frame_idx, sphere_idx, active_i, in_screen_i, pixel_x, pixel_y
        character(len=*), intent(in) :: case_id
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(in) :: time_value, screen_x, screen_y
        real(8), intent(in) :: spot_power_W, spot_cell_power_W, spot_irradiance_W_m2
        real(8), intent(in) :: distance_to_detector, los_azimuth, los_elevation, los_rate_magnitude

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A)',advance='no') trim(case_id)
        write(uf,'(A,I0)',advance='no') ',',frame_idx
        write(uf,'(A,ES15.6)',advance='no') ',',time_value
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)',advance='no') ',',sphere_idx
        write(uf,'(A,I0)',advance='no') ',',sphere_idx
        write(uf,'(A,I0)',advance='no') ',',active_i
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)',advance='no') ',',in_screen_i
        write(uf,'(A,ES15.6)',advance='no') ',',screen_x
        write(uf,'(A,ES15.6)',advance='no') ',',screen_y
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)',advance='no') ',',pixel_x
        write(uf,'(A,I0)',advance='no') ',',pixel_y
        write(uf,'(A,ES15.6)',advance='no') ',',spot_power_W
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)',advance='no') ',',spot_cell_power_W
        write(uf,'(A,ES15.6)',advance='no') ',',spot_irradiance_W_m2
        write(uf,'(A,ES15.6)',advance='no') ',',sphere_temperature(sphere_idx)
        write(uf,'(A,ES15.6)',advance='no') ',',sphere_power(sphere_idx)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)',advance='no') ',',distance_to_detector
        write(uf,'(A,ES15.6)',advance='no') ',',los_azimuth
        write(uf,'(A,ES15.6)',advance='no') ',',los_elevation
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)') ',',los_rate_magnitude
    end subroutine append_spot_point_row

    subroutine write_spot_point_history(frame_idx, time_value)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in) :: frame_idx
        real(8), intent(in) :: time_value
        ! 声明计数器、索引或离散控制参数。
        integer :: uf, ios, s, i, j, ix_c, iy_c, i_min, i_max, j_min, j_max
        integer :: active_i, in_screen_i
        logical :: file_exists, write_header, projection_ok
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=1) :: first_char
        character(len=256) :: case_id
        real(8) :: hit_point(3), Uc, Vc, Es, dxs, dys, Ucell, Vcell
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: rr2, R_phys2, cell_count, distance_to_detector
        real(8) :: los_azimuth, los_elevation, los_rate_magnitude, spot_cell_area

        if (.not. save_point_history) return

        dxs = spot_plane_size / dble(nx)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        dys = spot_plane_size / dble(ny)
        spot_cell_area = dxs * dys
        case_id = base_name(directory_name(resolved_output_dir))
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (trim(case_id) == '.' .or. len_trim(case_id) == 0) case_id = base_name(resolved_output_dir)

        uf = 88
        inquire(file=output_path('spot_point_history.csv'), exist=file_exists)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        write_header = .false.
        if (.not. file_exists) then
            write_header = .true.
        ! 当前条件不成立时执行替代计算路径。
        else
            open(unit=uf, file=output_path('spot_point_history.csv'), status='old', iostat=ios)
            if (ios == 0) then
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(uf, '(A1)', iostat=ios) first_char
                if (ios /= 0) write_header = .true.
                close(uf)
            ! 当前条件不成立时执行替代计算路径。
            else
                write_header = .true.
            end if
        end if

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=output_path('spot_point_history.csv'), status='unknown', position='append')
        if (write_header) then
            write(uf,'(A)') 'case_id,frame_id,time_s,object_id,sphere_id,active_flag,in_screen_flag,' // &
                'screen_x,screen_y,pixel_x,pixel_y,spot_power_W,spot_cell_power_W,' // &
                'spot_irradiance_W_m2,temperature_K,radiation_power_W,' // &
                'range_to_detector_m,los_az_rad,los_el_rad,los_rate_rad_s'
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if

        do s = 1, num_spheres
            if (.not. participates_in_imaging(s)) cycle
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            Es = sum(grid_direct(:,:,s)) + sum(grid_indirect(:,:,s))
            active_i = merge(1, 0, is_input_enabled(s))
            call compute_object_detector_geometry(s, distance_to_detector, los_azimuth, &
                                                  los_elevation, los_rate_magnitude)
            ! 调用子过程完成当前数值计算或状态更新。
            call project_point_to_detector(sphere_centers(:,s), projection_ok, hit_point, Uc, Vc)

            ix_c = -1
            iy_c = -1
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            in_screen_i = 0
            if (projection_ok) then
                ix_c = int((Uc + spot_plane_size/2.0d0) / dxs) + 1
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                iy_c = int((Vc + spot_plane_size/2.0d0) / dys) + 1
                if (ix_c >= 1 .and. ix_c <= nx .and. iy_c >= 1 .and. iy_c <= ny) in_screen_i = 1
            else
                Uc = 0.0d0
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                Vc = 0.0d0
            end if

            if (in_screen_i == 0 .or. Es <= 0.0d0) then
                ! 调用子过程完成当前数值计算或状态更新。
                call append_spot_point_row(uf, case_id, frame_idx, time_value, s, active_i, &
                    in_screen_i, Uc, Vc, merge(ix_c, -1, in_screen_i == 1), &
                    merge(iy_c, -1, in_screen_i == 1), Es, 0.0d0, 0.0d0, &
                    distance_to_detector, los_azimuth, los_elevation, los_rate_magnitude)
            else if (spot_radius_cells <= 0) then
                call append_spot_point_row(uf, case_id, frame_idx, time_value, s, active_i, &
                    in_screen_i, Uc, Vc, ix_c, iy_c, Es, Es, Es / spot_cell_area, &
                    distance_to_detector, los_azimuth, los_elevation, los_rate_magnitude)
            ! 当前条件不成立时执行替代计算路径。
            else
                i_min = max(1,  ix_c - spot_radius_cells)
                i_max = min(nx, ix_c + spot_radius_cells)
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                j_min = max(1,  iy_c - spot_radius_cells)
                j_max = min(ny, iy_c + spot_radius_cells)
                R_phys2 = (spot_radius_cells * dxs)**2
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                cell_count = 0.0d0
                do j = j_min, j_max
                    Vcell = -spot_plane_size/2.0d0 + (dble(j) - 0.5d0) * dys
                    ! 进入迭代计算，逐项更新仿真状态或累计量。
                    do i = i_min, i_max
                        Ucell = -spot_plane_size/2.0d0 + (dble(i) - 0.5d0) * dxs
                        rr2 = (Ucell - Uc)**2 + (Vcell - Vc)**2
                        if (rr2 <= R_phys2) cell_count = cell_count + 1.0d0
                    ! 结束本轮迭代范围，继续处理汇总后的计算结果。
                    end do
                end do
                if (cell_count <= 0.0d0) then
                    ! 调用子过程完成当前数值计算或状态更新。
                    call append_spot_point_row(uf, case_id, frame_idx, time_value, s, active_i, &
                        in_screen_i, Uc, Vc, ix_c, iy_c, Es, Es, Es / spot_cell_area, &
                    distance_to_detector, los_azimuth, los_elevation, los_rate_magnitude)
                else
                    do j = j_min, j_max
                        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                        Vcell = -spot_plane_size/2.0d0 + (dble(j) - 0.5d0) * dys
                        do i = i_min, i_max
                            Ucell = -spot_plane_size/2.0d0 + (dble(i) - 0.5d0) * dxs
                            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                            rr2 = (Ucell - Uc)**2 + (Vcell - Vc)**2
                            if (rr2 <= R_phys2) then
                                call append_spot_point_row(uf, case_id, frame_idx, time_value, s, active_i, &
                                    in_screen_i, Uc, Vc, i, j, Es, Es / cell_count, &
                                    (Es / cell_count) / spot_cell_area, distance_to_detector, &
                                    los_azimuth, los_elevation, los_rate_magnitude)
                            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
                            end if
                        end do
                    end do
                ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
                end if
            end if
        end do
        close(uf)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine write_spot_point_history

    subroutine write_motion_frame_outputs(frame_idx, time_value, include_thermal_outputs)
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: frame_idx
        real(8), intent(in) :: time_value
        logical, intent(in) :: include_thermal_outputs

        ! The motion trajectory is a formal business output.  Keep its temporal
        ! resolution at every event-aligned solver node (normally 1 s, plus any
        ! release-event nodes), independent of the lower-rate thermal/IR output
        ! frame interval.
        ! 调用子过程完成当前数值计算或状态更新。
        call write_trajectory_history_row(frame_idx, time_value)

        if (.not. should_write_output_frame(frame_idx)) return

        if (WRITE_DERIVED_OUTPUTS) then
            ! 调用子过程完成当前数值计算或状态更新。
            call write_frame_geometry_snapshot(frame_idx, time_value)
            call write_geometry_line(frame_idx, time_value)
            call write_trajectory_output(frame_idx, time_value)
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
        if (WRITE_QA_DIAGNOSTICS) then
            call write_input_parameter_summary()
            ! 调用子过程完成当前数值计算或状态更新。
            call write_mc_statistics()
            call write_verification_report()
            call write_runtime_summary()
        end if

        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (include_thermal_outputs) then
            if (WRITE_BUSINESS_CORE) call write_infrared_response_history(frame_idx, time_value)
            if (WRITE_FRAME_SUMMARY) call write_frame_summary_row(frame_idx, time_value)
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (WRITE_DERIVED_OUTPUTS) then
                call write_tecplot_temperature()
                call write_spot_image()
                ! 调用子过程完成当前数值计算或状态更新。
                call write_frame_temperature_snapshot(frame_idx)
                call write_spot_point_history(frame_idx, time_value)
                if (should_write_full_spot_frame(frame_idx)) call write_frame_spot_snapshot(frame_idx)
                ! 调用子过程完成当前数值计算或状态更新。
                call write_screen_history(frame_idx, time_value)
                call write_infrared_characteristics(frame_idx, time_value)
            end if
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
    end subroutine write_motion_frame_outputs

    subroutine write_infrared_response_history(frame_idx, time_value)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in) :: frame_idx
        real(8), intent(in) :: time_value
        ! 声明计数器、索引或离散控制参数。
        integer :: uf, ios, s, active_i, released_i, in_screen_i, pixel_x, pixel_y
        logical :: file_exists, write_header, projection_ok
        character(len=1) :: first_char
        character(len=256) :: case_id
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: hit_point(3), screen_x, screen_y, detector_power, detector_irradiance
        real(8) :: distance_to_detector, los_azimuth, los_elevation, los_rate_magnitude
        real(8) :: dxs, dys, pixel_area, emitted_power, radiant_intensity

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        dxs = spot_plane_size / dble(nx)
        dys = spot_plane_size / dble(ny)
        pixel_area = dxs * dys
        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        case_id = base_name(directory_name(resolved_output_dir))
        if (trim(case_id) == '.' .or. len_trim(case_id) == 0) case_id = base_name(resolved_output_dir)

        uf = 87
        ! 维护输入输出路径及文件数据，确保结果写入约定位置。
        inquire(file=output_path('infrared_response_history.csv'), exist=file_exists)
        write_header = .false.
        if (.not. file_exists) then
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            write_header = .true.
        else
            open(unit=uf, file=output_path('infrared_response_history.csv'), status='old', iostat=ios)
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (ios == 0) then
                read(uf, '(A1)', iostat=ios) first_char
                if (ios /= 0) write_header = .true.
                close(uf)
            ! 当前条件不成立时执行替代计算路径。
            else
                write_header = .true.
            end if
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if

        open(unit=uf, file=output_path('infrared_response_history.csv'), status='unknown', position='append')
        if (write_header) then
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A)') 'case_id,frame_id,time_s,object_id,active_flag,released_flag,' // &
                'radiation_power_W,radiant_intensity_W_sr,detector_received_power_W,' // &
                'detector_irradiance_W_m2,screen_x_m,screen_y_m,in_screen_flag,range_to_detector_m'
        end if

        do s = 1, num_spheres
            ! 维护输入输出路径及文件数据，确保结果写入约定位置。
            active_i = merge(1, 0, is_input_enabled(s))
            released_i = merge(1, 0, is_released(s))
            emitted_power = 0.0d0
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            radiant_intensity = 0.0d0
            detector_power = 0.0d0
            detector_irradiance = 0.0d0
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            screen_x = 0.0d0
            screen_y = 0.0d0
            pixel_x = -1
            pixel_y = -1
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            in_screen_i = 0
            call compute_object_detector_geometry(s, distance_to_detector, los_azimuth, &
                                                  los_elevation, los_rate_magnitude)
            if (participates_in_ir_source(s)) then
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                emitted_power = sphere_power(s)
                ! Radiant intensity of the thermally emitting spherical target [W/sr].
                ! The target surface is diffuse/Lambertian and the complete sphere is
                ! directionally isotropic, therefore I_e = Phi_e / (4*pi).
                radiant_intensity = emitted_power / (4.0d0 * PI)
            end if
            ! 检查数值状态和业务条件，仅在满足约束时进入分支。
            if (participates_in_imaging(s)) then
                detector_power = sum(grid_direct(:,:,s)) + sum(grid_indirect(:,:,s))
                call project_point_to_detector(sphere_centers(:,s), projection_ok, hit_point, screen_x, screen_y)
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if (projection_ok) then
                    pixel_x = int((screen_x + spot_plane_size/2.0d0) / dxs) + 1
                    pixel_y = int((screen_y + spot_plane_size/2.0d0) / dys) + 1
                    ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                    if (pixel_x >= 1 .and. pixel_x <= nx .and. pixel_y >= 1 .and. pixel_y <= ny) then
                        in_screen_i = 1
                        if (detector_power > 0.0d0 .and. spot_radius_cells <= 0) &
                            detector_irradiance = detector_power / pixel_area
                    ! 当前条件不成立时执行替代计算路径。
                    else
                        screen_x = 0.0d0
                        screen_y = 0.0d0
                    end if
                ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
                end if
            end if
            write(uf,'(A)',advance='no') trim(case_id)
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,I0)',advance='no') ',',frame_idx
            write(uf,'(A,ES24.16)',advance='no') ',',time_value
            write(uf,'(A,I0)',advance='no') ',',s
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,I0)',advance='no') ',',active_i
            write(uf,'(A,I0)',advance='no') ',',released_i
            write(uf,'(A,ES24.16)',advance='no') ',',emitted_power
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,ES24.16)',advance='no') ',',radiant_intensity
            write(uf,'(A,ES24.16)',advance='no') ',',detector_power
            write(uf,'(A,ES24.16)',advance='no') ',',detector_irradiance
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,ES24.16)',advance='no') ',',screen_x
            write(uf,'(A,ES24.16)',advance='no') ',',screen_y
            write(uf,'(A,I0)',advance='no') ',',in_screen_i
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,ES24.16)') ',',distance_to_detector
        end do
        close(uf)
    end subroutine write_infrared_response_history

    ! 定义 write_runtime_summary 计算单元，封装该步骤的数据处理规则。
    subroutine write_runtime_summary()
        implicit none
        integer :: uf

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        uf = 87
        open(unit=uf, file=output_path('runtime_summary.txt'), status='replace')
        write(uf,'(A)') '===================================================='
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A)') 'Runtime Summary'
        write(uf,'(A)') '===================================================='
        write(uf,'(A,A)') 'Date: ', trim(date_str)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,A)') 'Time: ', trim(time_str)
        write(uf,*)
        write(uf,'(A,I0)') 'Current frame index: ', current_frame_index
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)') 'Current motion time [s]: ', current_motion_time
        write(uf,'(A,ES15.6)') 'Latest frame compute time [s]: ', last_frame_compute_time
        write(uf,'(A,ES15.6)') 'Cumulative compute time [s]: ', cumulative_compute_time
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)') 'Total compute time [s]: ', cumulative_compute_time
        write(uf,'(A,I0)') 'Active object count: ', active_object_count()
        write(uf,'(A,ES15.6)') 'Active mass [kg]: ', active_mass_total()
        write(uf,'(A,ES15.6)') 'Active internal energy [J]: ', active_internal_energy_total()
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES15.6)') 'Cumulative release-carried energy [J]: ', cumulative_release_carried_energy
        write(uf,'(A,ES15.6)') 'Cumulative system energy residual [J]: ', cumulative_system_energy_residual
        close(uf)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine write_runtime_summary

    !--------------------------------------------------------------------------
    ! 输出球体在光屏的投影位置和功率（历史记录，每次追加一行）
    ! 每个球体3列：Spot_U, Spot_V, Spot_Power_W
    ! 如果投影中心超出光屏范围，输出为零
    !--------------------------------------------------------------------------
        subroutine write_screen_history(frame_idx, time_value)
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in), optional :: frame_idx
        real(8), intent(in), optional :: time_value
        integer :: uf, s, ios
        ! 声明逻辑开关，用于控制对应计算或输出路径。
        logical :: file_exists, write_header
        character(len=1) :: first_char
        real(8) :: hit_point(3)
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: spot_u, spot_v, spot_power_W
        real(8) :: half_size
        logical :: in_range, projection_ok

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        uf = 42

        ! 光屏半尺寸
        half_size = spot_plane_size / 2.0d0

        ! 检查文件是否存在
        inquire(file=output_path('screen_history.dat'), exist=file_exists)

        write_header = .false.

        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. file_exists) then
            write_header = .true.
        else
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            open(unit=uf, file=output_path('screen_history.dat'), status='old', iostat=ios)
            if (ios == 0) then
                read(uf, '(A1)', iostat=ios) first_char
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if (ios /= 0) then
                    write_header = .true.
                end if
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                close(uf)
            else
                write_header = .true.
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
        end if

        ! 打开文件追加
        open(unit=uf, file=output_path('screen_history.dat'), status='unknown', position='append')

        ! 写入表头（仅首次）
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (write_header) then
            write(uf,'(A)') '#=============================================================================='
            write(uf,'(A)') '# Screen History File'
            write(uf,'(A)') '# Generated by: Sphere IR Radiation Simulation Program'
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A)') '#=============================================================================='
            write(uf,'(A)') '#'
            write(uf,'(A)') '# Description:'
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A)') '#   Each line records one frame: frame/time plus detector-plane position and power for all spheres.'
            write(uf,'(A)') '#   If projection center is out of screen range, values are set to zero.'
            write(uf,'(A)') '#   Projection is traced from the moving aperture center onto the moving detector plane.'
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A)') '#'
            write(uf,'(A)') '# Column Format (per sphere, 3 values):'
            write(uf,'(A)') '#   Spot_U [m]   - Projection U coordinate on detector plane'
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A)') '#   Spot_V [m]   - Projection V coordinate on detector plane'
            write(uf,'(A)') '#   Spot_Power [W] - Total received radiation power associated with the object'
            write(uf,'(A)') '#'
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,F10.4,A,F10.4,A)') '# Screen range: [', -half_size, ' , ', half_size, '] m'
            write(uf,'(A,I0)') '# Number of spheres: ', num_spheres
            write(uf,'(A,I0)') '# Columns per line:  ', 2 + 3 * num_spheres
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A)') '#'
            write(uf,'(A)') '#------------------------------------------------------------------------------'
            
            ! 列名行
            write(uf,'(A)', advance='no') '# frame time_s'
            do s = 1, num_spheres
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                write(uf,'(A,I0,A)', advance='no') '        S', s, '_U'
                write(uf,'(A,I0,A)', advance='no') '        S', s, '_V'
                write(uf,'(A,I0,A)', advance='no') '     S', s, '_Power_W'
            ! 结束本轮迭代范围，继续处理汇总后的计算结果。
            end do
            write(uf,*)
            write(uf,'(A)') '#------------------------------------------------------------------------------'
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if

        ! 写入数据行
        if (present(frame_idx)) then
            if (present(time_value)) then
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                write(uf,'(I0,1X,ES14.6)', advance='no') frame_idx, time_value
            else
                write(uf,'(I0,1X,ES14.6)', advance='no') frame_idx, 0.0d0
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
        else
            write(uf,'(I0,1X,ES14.6)', advance='no') 0, 0.0d0
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if

        do s = 1, num_spheres
            if (.not. participates_in_imaging(s)) then
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                spot_u = 0.0d0
                spot_v = 0.0d0
                spot_power_W = 0.0d0
            else
                ! 调用子过程完成当前数值计算或状态更新。
                call project_point_to_detector(sphere_centers(:,s), projection_ok, hit_point, spot_u, spot_v)
                if (.not. projection_ok) then
                    spot_u = huge(1.0d0)
                    ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                    spot_v = huge(1.0d0)
                end if

                ! 检查是否在光屏范围内
                in_range = (abs(spot_u) <= half_size) .and. (abs(spot_v) <= half_size)

                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if (in_range) then
                    ! 在范围内：输出实际值
                    spot_power_W = sum(grid_direct(:,:,s)) + sum(grid_indirect(:,:,s))
                else
                    ! 超出范围：输出零
                    ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                    spot_u = 0.0d0
                    spot_v = 0.0d0
                    spot_power_W = 0.0d0
                ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
                end if
            end if

            ! 输出
            write(uf,'(1X,ES14.6)', advance='no') spot_u
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(1X,ES14.6)', advance='no') spot_v
            write(uf,'(1X,ES14.6)', advance='no') spot_power_W
        end do

        write(uf,*)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)
    end subroutine write_screen_history
    subroutine write_input_parameter_summary()
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer :: uf, i

        uf = 47
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=output_path('input_parameters_summary.txt'), status='replace')
        write(uf,'(A)') '============================================================'
        write(uf,'(A)') ' Input Parameter Summary'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A)') '============================================================'
        write(uf,'(A)') '[Companion-object type / geometry]'
        write(uf,'(A,A)')      'Default companion type: ', trim(companion_type_default)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)')     'Number of objects:      ', num_spheres
        write(uf,'(A)')        'ID  TYPE             RADIUS[m]      X[m]           Y[m]           Z[m]'
        do i = 1, num_spheres
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(I4,1X,A16,4(1X,ES14.6))') i, trim(companion_type(i)), sphere_radius(i), &
                sphere_centers_initial(1,i), sphere_centers_initial(2,i), sphere_centers_initial(3,i)
        end do
        write(uf,*)

        write(uf,'(A)') '[Motion parameters]'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,F12.6,A)') 'Flight total time: ', total_time, ' s'
        write(uf,'(A,F12.6,A)') 'Current cumulative compute time: ', cumulative_compute_time, ' s'
        write(uf,'(A,F12.6,A)') 'Latest frame compute time:       ', last_frame_compute_time, ' s'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,A)')       'Attitude motion type:            ', trim(attitude_motion_type)
        write(uf,'(A,3ES14.6)') 'Group center:                    ', group_center_initial
        write(uf,'(A,3ES14.6)') 'Group velocity:                  ', group_velocity_initial
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,3ES14.6)') 'Group angular velocity:          ', group_angular_velocity
        write(uf,'(A,3ES14.6)') 'Micro-motion parameters:         ', micro_motion_params
        write(uf,'(A)') 'ID  release_time[s] active separation_vx separation_vy separation_vz'
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do i = 1, num_spheres
            write(uf,'(I4,1X,ES14.6,1X,L1,3(1X,ES14.6))') i, sphere_release_time(i), &
                sphere_is_active(i), sphere_velocity(1,i), sphere_velocity(2,i), sphere_velocity(3,i)
        end do
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,*)

        write(uf,'(A)') '[Thermophysical parameters]'
        write(uf,'(A)') 'ID  cp[J/(kg.K)] density[kg/m3] internal_heat[W] initial_T[K]'
        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do i = 1, num_spheres
            write(uf,'(I4,4(1X,ES14.6))') i, sphere_specific_heat(i), sphere_density(i), &
                sphere_internal_heat(i), sphere_initial_temp(i)
        end do
        write(uf,*)

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A)') '[Radiative parameters]'
        write(uf,'(A)') 'ID  emissivity_IR absorptivity_solar reflectivity_IR reflectivity_solar'
        do i = 1, num_spheres
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(I4,4(1X,ES14.6))') i, sphere_ir_emissivity(i), sphere_solar_absorptivity(i), &
                sphere_ir_reflectivity(i), sphere_solar_reflectivity(i)
        end do
        write(uf,*)

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A)') '[Radiation-scene evaluation]'
        write(uf,'(A,A)')       'Similarity level:        ', trim(similarity_level)
        write(uf,'(A,ES14.6)')  'Reference temperature:   ', reference_temperature
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,ES14.6)')  'Reference intensity:     ', reference_intensity
        write(uf,'(A,ES14.6)')  'Internal-energy datum [K]: ', INTERNAL_ENERGY_REFERENCE_K
        close(uf)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine write_input_parameter_summary

    subroutine write_trajectory_history_row(frame_idx, time_value)
        implicit none
        ! 声明计数器、索引或离散控制参数。
        integer, intent(in) :: frame_idx
        real(8), intent(in) :: time_value
        integer :: uf, ios, i, active_i, released_i
        logical :: file_exists, write_header
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=1) :: first_char
        character(len=256) :: case_id
        real(8) :: object_range, object_azimuth, object_elevation, object_los_rate
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8) :: object_speed

        case_id = base_name(directory_name(resolved_output_dir))
        if (trim(case_id) == '.' .or. len_trim(case_id) == 0) case_id = base_name(resolved_output_dir)

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        uf = 47
        inquire(file=output_path('trajectory_history.csv'), exist=file_exists)
        write_header = .false.
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. file_exists) then
            write_header = .true.
        else
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            open(unit=uf, file=output_path('trajectory_history.csv'), status='old', iostat=ios)
            if (ios == 0) then
                read(uf, '(A1)', iostat=ios) first_char
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if (ios /= 0) write_header = .true.
                close(uf)
            else
                write_header = .true.
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
        end if

        open(unit=uf, file=output_path('trajectory_history.csv'), status='unknown', position='append')
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (write_header) then
            write(uf,'(A)') 'case_id,frame_id,time_s,object_id,active_flag,released_flag,motion_stage,' // &
                'release_time_s,x_m,y_m,z_m,vx_m_s,vy_m_s,vz_m_s,speed_m_s,range_to_detector_m'
        end if

        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do i = 1, num_spheres
            active_i = merge(1, 0, is_input_enabled(i))
            released_i = merge(1, 0, is_released(i))
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            object_speed = sqrt(sum(sphere_world_velocity(:,i)**2))
            call compute_object_detector_geometry(i, object_range, object_azimuth, object_elevation, object_los_rate)

            write(uf,'(A)',advance='no') trim(case_id)
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,I0)',advance='no') ',',frame_idx
            write(uf,'(A,ES24.16)',advance='no') ',',time_value
            write(uf,'(A,I0)',advance='no') ',',i
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,I0)',advance='no') ',',active_i
            write(uf,'(A,I0)',advance='no') ',',released_i
            write(uf,'(A,I0)',advance='no') ',',sphere_motion_stage(i)
            write(uf,'(A,ES24.16)',advance='no') ',',sphere_release_time(i)
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,ES24.16)',advance='no') ',',sphere_centers(1,i)
            write(uf,'(A,ES24.16)',advance='no') ',',sphere_centers(2,i)
            write(uf,'(A,ES24.16)',advance='no') ',',sphere_centers(3,i)
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,ES24.16)',advance='no') ',',sphere_world_velocity(1,i)
            write(uf,'(A,ES24.16)',advance='no') ',',sphere_world_velocity(2,i)
            write(uf,'(A,ES24.16)',advance='no') ',',sphere_world_velocity(3,i)
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,ES24.16)',advance='no') ',',object_speed
            write(uf,'(A,ES24.16)') ',',object_range
        end do
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)
    end subroutine write_trajectory_history_row

    subroutine write_trajectory_output(frame_idx, time_value)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in), optional :: frame_idx
        real(8), intent(in), optional :: time_value
        ! 声明计数器、索引或离散控制参数。
        integer :: uf, i, ios, frame_value
        real(8) :: t_value, object_range, object_azimuth, object_elevation, object_los_rate
        logical :: file_exists, write_header
        character(len=1) :: first_char

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        frame_value = 0
        t_value = 0.0d0
        if (present(frame_idx)) frame_value = frame_idx
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (present(time_value)) t_value = time_value

        uf = 48
        inquire(file=output_path('trajectory_output.csv'), exist=file_exists)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        write_header = .false.
        if (.not. file_exists) then
            write_header = .true.
        ! 当前条件不成立时执行替代计算路径。
        else
            open(unit=uf, file=output_path('trajectory_output.csv'), status='old', iostat=ios)
            if (ios == 0) then
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(uf, '(A1)', iostat=ios) first_char
                if (ios /= 0) write_header = .true.
                close(uf)
            ! 当前条件不成立时执行替代计算路径。
            else
                write_header = .true.
            end if
        end if

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=output_path('trajectory_output.csv'), status='unknown', position='append')
        if (write_header) then
            write(uf,'(A)') 'frame,time_s,id,type,input_active,release_stage,released,' // &
                'solar_participant,ir_source_participant,ir_target_participant,' // &
                'thermal_participant,imaging_participant,occlusion_participant,' // &
                'statistics_participant,x_m,y_m,z_m,vx_m_s,vy_m_s,vz_m_s,' // &
                'range_to_detector_m,los_az_rad,los_el_rad,los_rate_rad_s'
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
        do i = 1, num_spheres
            call compute_object_detector_geometry(i, object_range, object_azimuth, object_elevation, object_los_rate)
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(I0)',advance='no') frame_value
            write(uf,'(A,ES15.6)',advance='no') ',',t_value
            write(uf,'(A,I0)',advance='no') ',',i
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,A)',advance='no') ',',trim(companion_type(i))
            write(uf,'(A,I0)',advance='no') ',',merge(1,0,is_input_enabled(i))
            write(uf,'(A,I0)',advance='no') ',',sphere_motion_stage(i)
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,I0)',advance='no') ',',merge(1,0,is_released(i))
            write(uf,'(A,I0)',advance='no') ',',merge(1,0,participates_in_solar(i))
            write(uf,'(A,I0)',advance='no') ',',merge(1,0,participates_in_ir_source(i))
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,I0)',advance='no') ',',merge(1,0,participates_in_ir_target(i))
            write(uf,'(A,I0)',advance='no') ',',merge(1,0,participates_in_thermal(i))
            write(uf,'(A,I0)',advance='no') ',',merge(1,0,participates_in_imaging(i))
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,I0)',advance='no') ',',merge(1,0,participates_in_occlusion(i))
            write(uf,'(A,I0)',advance='no') ',',merge(1,0,participates_in_statistics(i))
            write(uf,'(A,ES15.6)',advance='no') ',',sphere_centers(1,i)
            write(uf,'(A,ES15.6)',advance='no') ',',sphere_centers(2,i)
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,ES15.6)',advance='no') ',',sphere_centers(3,i)
            write(uf,'(A,ES15.6)',advance='no') ',',sphere_world_velocity(1,i)
            write(uf,'(A,ES15.6)',advance='no') ',',sphere_world_velocity(2,i)
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,ES15.6)',advance='no') ',',sphere_world_velocity(3,i)
            write(uf,'(A,ES15.6)',advance='no') ',',object_range
            write(uf,'(A,ES15.6)',advance='no') ',',object_azimuth
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(A,ES15.6)',advance='no') ',',object_elevation
            write(uf,'(A,ES15.6)') ',',object_los_rate
        end do
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)
    end subroutine write_trajectory_output

    subroutine write_infrared_characteristics(frame_idx, time_value)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in), optional :: frame_idx
        real(8), intent(in), optional :: time_value
        ! 声明计数器、索引或离散控制参数。
        integer :: uf, i, ios, frame_value
        real(8) :: t_value, object_received
        logical :: file_exists, write_header
        character(len=1) :: first_char

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        frame_value = 0
        t_value = 0.0d0
        if (present(frame_idx)) frame_value = frame_idx
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (present(time_value)) t_value = time_value

        uf = 49
        inquire(file=output_path('infrared_characteristics.csv'), exist=file_exists)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        write_header = .false.
        if (.not. file_exists) then
            write_header = .true.
        ! 当前条件不成立时执行替代计算路径。
        else
            open(unit=uf, file=output_path('infrared_characteristics.csv'), status='old', iostat=ios)
            if (ios == 0) then
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                read(uf, '(A1)', iostat=ios) first_char
                if (ios /= 0) write_header = .true.
                close(uf)
            ! 当前条件不成立时执行替代计算路径。
            else
                write_header = .true.
            end if
        end if

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=output_path('infrared_characteristics.csv'), status='unknown', position='append')
        if (write_header) then
            write(uf,'(A)') 'frame,time_s,id,type,temperature_K,radiation_power_W,solar_heat_W,internal_heat_W,space_loss_W,aperture_loss_W,environment_input_W,net_exchange_W,received_power_W,total_received_power_W,max_irradiance_W_m2'
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if
        do i = 1, num_spheres
            if (.not. participates_in_statistics(i)) cycle
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            object_received = sum(grid_direct(:,:,i)) + sum(grid_indirect(:,:,i))
            write(uf,'(I0,A,ES15.6,A,I0,A,A,11(A,ES15.6))') frame_value, ',', t_value, ',', &
                i, ',', trim(companion_type(i)), ',', sphere_temperature(i), ',', sphere_power(i), ',', &
                sphere_solar_heat(i), ',', sphere_internal_heat(i), ',', sphere_radiation_to_space(i), ',', &
                sphere_radiation_to_aperture(i), ',', sphere_radiation_from_environment(i), ',', &
                sphere_net_exchange(i), ',', object_received, ',', sum(grid_total), ',', maxval(grid_irradiance)
        end do
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)
    end subroutine write_infrared_characteristics

    subroutine write_similarity_evaluation(frame_idx, time_value)
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer, intent(in), optional :: frame_idx
        real(8), intent(in), optional :: time_value
        ! 声明计数器、索引或离散控制参数。
        integer :: uf, ios, frame_value, participant_count
        real(8) :: t_value, temp_avg, temp_min, temp_max
        real(8) :: intensity_total, temp_error, intensity_error, score
        logical :: file_exists, write_header
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=1) :: first_char

        frame_value = 0
        t_value = 0.0d0
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (present(frame_idx)) frame_value = frame_idx
        if (present(time_value)) t_value = time_value

        call get_participant_temperature_statistics(temp_avg, temp_min, temp_max, participant_count)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        intensity_total = sum(grid_total)
        if (reference_temperature > 0.0d0) then
            temp_error = abs(temp_avg - reference_temperature) / reference_temperature
        ! 当前条件不成立时执行替代计算路径。
        else
            temp_error = -1.0d0
        end if
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (reference_intensity > 0.0d0) then
            intensity_error = abs(intensity_total - reference_intensity) / reference_intensity
        else
            ! 检查并记录计算状态，使异常能够被上层流程识别。
            intensity_error = -1.0d0
        end if
        if (temp_error >= 0.0d0 .and. intensity_error >= 0.0d0) then
            score = 1.0d0 / (1.0d0 + 0.5d0 * (temp_error + intensity_error))
        ! 当前条件不成立时执行替代计算路径。
        else
            score = -1.0d0
        end if

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        uf = 50
        inquire(file=output_path('similarity_evaluation.csv'), exist=file_exists)
        write_header = .false.
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (.not. file_exists) then
            write_header = .true.
        else
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            open(unit=uf, file=output_path('similarity_evaluation.csv'), status='old', iostat=ios)
            if (ios == 0) then
                read(uf, '(A1)', iostat=ios) first_char
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if (ios /= 0) write_header = .true.
                close(uf)
            else
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                write_header = .true.
            end if
        end if

        open(unit=uf, file=output_path('similarity_evaluation.csv'), status='unknown', position='append')
        ! 检查数值状态和业务条件，仅在满足约束时进入分支。
        if (write_header) then
            write(uf,'(A)') 'frame,time_s,similarity_level,temp_avg_K,total_received_power_W,temp_error,intensity_error,similarity_score'
        end if
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(I0,A,ES15.6,A,A,5(A,ES15.6))') frame_value, ',', t_value, ',', &
            trim(similarity_level), ',', temp_avg, ',', intensity_total, ',', temp_error, ',', intensity_error, ',', score
        close(uf)
    end subroutine write_similarity_evaluation
    ! 定义 write_tecplot_temperature 计算单元，封装该步骤的数据处理规则。
    subroutine write_tecplot_temperature()
        implicit none
        integer :: uf, idx, participant_count

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        participant_count = count_statistics_participants()
        uf = 32
        open(unit=uf,file=output_path('sphere_temperature.dat'),status='replace')
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A)') 'TITLE = "Sphere Temperature"'
        write(uf,'(A)') 'VARIABLES = "X", "Y", "Z", "T [K]", "Power [W]", "Q_solar", "Q_int", "Q_space", "Q_exch"'
        write(uf,'(A,I0,A)') 'ZONE T="ReleasedSpheres", I=',participant_count,', F=POINT'

        ! 进入迭代计算，逐项更新仿真状态或累计量。
        do idx = 1, num_spheres
            if (.not. participates_in_thermal(idx)) cycle
            write(uf,'(9ES15.6)') sphere_centers(1,idx),sphere_centers(2,idx), &
                sphere_centers(3,idx),sphere_temperature(idx),sphere_power(idx), &
                sphere_solar_heat(idx),sphere_internal_heat(idx), &
                sphere_radiation_to_space(idx),sphere_net_exchange(idx)
        end do

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)
    end subroutine write_tecplot_temperature
    subroutine write_mc_statistics()
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer :: uf, i
        integer(8) :: total_accounted, total_unclosed
        ! 声明计数器、索引或离散控制参数。
        integer(8) :: src_accounted, src_unclosed

        uf = 36
        open(unit=uf, file=output_path('mc_statistics.txt'), status='replace')

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A)') '===================================================='
        write(uf,'(A)') ' Monte Carlo IR Radiation Statistics'
        write(uf,'(A)') '===================================================='
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,*)
        write(uf,'(A,I0)') 'Number of spheres: ', num_spheres
        write(uf,'(A,I0)') 'Rays per sphere: ', rays_per_sphere
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0)') 'Rays solar: ', rays_solar
        write(uf,'(A,I0)') 'Max bounces: ', max_bounces
        write(uf,'(A,F6.3)') 'MC_MIX_BETA: ', mc_mix_beta
        write(uf,'(A,I0)')   'Random seed used: ', random_seed_used
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,*)

        write(uf,'(A,I20)') 'Total rays emitted: ', sum(rays_emitted)
        write(uf,'(A,I20)') 'Total rays hit pupil: ', rays_hit_pupil
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I20)') 'Total rays on sensor grid: ', rays_on_sensor
        write(uf,'(A,I20)') 'Total rays escaped: ', rays_escaped
        write(uf,'(A,I20)') 'Total direct hits: ', sum(rays_direct_hit)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I20)') 'Total indirect hits: ', sum(rays_indirect_hit)
        write(uf,'(A,I20)') 'Total terminated on sphere: ', sum(rays_terminated_on_sphere)
        write(uf,'(A,I20)') 'Total self-absorbed: ', sum(rays_self_absorbed)

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        total_accounted = sum(rays_direct_hit) + sum(rays_indirect_hit) + &
                          sum(rays_escaped_source) + sum(rays_terminated_on_sphere)
        total_unclosed = sum(rays_emitted) - total_accounted

        write(uf,'(A,I20)') 'Total accounted rays: ', total_accounted
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I20)') 'Count closure residual: ', total_unclosed
        write(uf,'(A,ES14.6)') 'Relative closure error: ', &
            dble(abs(total_unclosed)) / max(1.0d0, dble(sum(rays_emitted)))
        write(uf,*)

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A)') 'Per-sphere statistics:'
        write(uf,'(A)') 'ID Emitted DirHits IndHits Escaped Terminated SelfAbs ClosureRes P_direct[W] P_indirect[W]'

        do i = 1, num_spheres
            src_accounted = rays_direct_hit(i) + rays_indirect_hit(i) + &
                            rays_escaped_source(i) + rays_terminated_on_sphere(i)
            ! 更新仿真变量或中间量，供下一数值步骤继续计算。
            src_unclosed = rays_emitted(i) - src_accounted

            write(uf,'(I3,1X,I10,1X,I10,1X,I10,1X,I10,1X,I10,1X,I10,1X,I10,2(1X,ES14.6))') &
                i, rays_emitted(i), rays_direct_hit(i), rays_indirect_hit(i), &
                rays_escaped_source(i), rays_terminated_on_sphere(i), &
                rays_self_absorbed(i), src_unclosed, &
                power_direct(i), power_indirect(i)
        end do

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        close(uf)
    end subroutine write_mc_statistics
    subroutine write_spot_image()
        ! 声明计算依赖并启用严格变量规则，减少隐式类型错误。
        implicit none
        integer :: uf, ix, iy
        real(8) :: dxs, dys, U, V

        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        dxs = spot_plane_size / dble(nx)
        dys = spot_plane_size / dble(ny)

        uf = 37
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=output_path('spot_image.dat'), status='replace')
        write(uf,'(A)') 'TITLE = "Detector Plane Spot Image (one spot per object)"'
        write(uf,'(A)') 'VARIABLES = "U [m]", "V [m]", "Total [W]", "Irradiance [W/m2]"'
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(A,I0,A,I0,A)') 'ZONE T="DetectorPlane", I=',nx,', J=',ny,', F=POINT'

        do iy = 1, ny
            V = -spot_plane_size/2.0d0 + (dble(iy)-0.5d0)*dys
            ! 进入迭代计算，逐项更新仿真状态或累计量。
            do ix = 1, nx
                U = -spot_plane_size/2.0d0 + (dble(ix)-0.5d0)*dxs
                write(uf,'(4ES15.6)') U, V, spot_total(ix,iy), spot_irradiance(ix,iy)
            end do
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do

        close(uf)
    end subroutine write_spot_image

    ! 定义 write_geometry_line 计算单元，封装该步骤的数据处理规则。
    subroutine write_geometry_line(frame_idx, time_value)
        implicit none
        integer, intent(in), optional :: frame_idx
        ! 声明双精度数值或物理量，为后续仿真计算保存状态。
        real(8), intent(in), optional :: time_value
        integer :: uf, i, ios
        logical :: file_exists, write_header
        ! 声明文本字段，用于保存路径、状态或接口数据。
        character(len=1) :: first_char

        uf = 38
        inquire(file=output_path('geometry_history.dat'), exist=file_exists)
        ! 更新仿真变量或中间量，供下一数值步骤继续计算。
        write_header = .false.
        if (.not. file_exists) then
            write_header = .true.
        ! 当前条件不成立时执行替代计算路径。
        else
            open(unit=uf, file=output_path('geometry_history.dat'), status='old', iostat=ios)
            if (ios == 0) then
                read(uf, '(A1)', iostat=ios) first_char
                ! 检查数值状态和业务条件，仅在满足约束时进入分支。
                if (ios /= 0) write_header = .true.
                close(uf)
            else
                ! 更新仿真变量或中间量，供下一数值步骤继续计算。
                write_header = .true.
            end if
        end if

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        open(unit=uf, file=output_path('geometry_history.dat'), &
             status='unknown', position='append')

        if (write_header) then
            write(uf,'(A)') &
                '# frame time_s group_center(3) group_velocity(3) group_normal(3) ' // &
                'aperture_center(3) aperture_velocity(3) aperture_normal(3) ' // &
                'detector_center(3) detector_velocity(3) detector_normal(3) ' // &
                'rel_pos(3) rel_vel(3) range az el los_rate sphere_xyz...'
        ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
        end if

        if (present(frame_idx)) then
            if (present(time_value)) then
                ! 按照接口约定读写数据文件，并维护文件单元状态。
                write(uf,'(I0,1X,ES15.6)', advance='no') frame_idx, time_value
            else
                write(uf,'(I0,1X,ES15.6)', advance='no') frame_idx, 0.0d0
            ! 执行当前数值处理步骤，并保持物理量和控制状态一致。
            end if
        else
            write(uf,'(I0,1X,ES15.6)', advance='no') 0, 0.0d0
        end if

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(1X,ES18.9)', advance='no') group_center(1)
        write(uf,'(1X,ES18.9)', advance='no') group_center(2)
        write(uf,'(1X,ES18.9)', advance='no') group_center(3)

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(1X,ES18.9)', advance='no') group_velocity(1)
        write(uf,'(1X,ES18.9)', advance='no') group_velocity(2)
        write(uf,'(1X,ES18.9)', advance='no') group_velocity(3)

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(1X,ES18.9)', advance='no') group_normal(1)
        write(uf,'(1X,ES18.9)', advance='no') group_normal(2)
        write(uf,'(1X,ES18.9)', advance='no') group_normal(3)

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(1X,ES18.9)', advance='no') aperture_center(1)
        write(uf,'(1X,ES18.9)', advance='no') aperture_center(2)
        write(uf,'(1X,ES18.9)', advance='no') aperture_center(3)

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(1X,ES18.9)', advance='no') aperture_velocity(1)
        write(uf,'(1X,ES18.9)', advance='no') aperture_velocity(2)
        write(uf,'(1X,ES18.9)', advance='no') aperture_velocity(3)

        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(1X,ES18.9)', advance='no') aperture_normal(1)
        write(uf,'(1X,ES18.9)', advance='no') aperture_normal(2)
        write(uf,'(1X,ES18.9)', advance='no') aperture_normal(3)

        write(uf,'(1X,ES18.9)', advance='no') detector_center(1)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(1X,ES18.9)', advance='no') detector_center(2)
        write(uf,'(1X,ES18.9)', advance='no') detector_center(3)

        write(uf,'(1X,ES18.9)', advance='no') detector_velocity(1)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(1X,ES18.9)', advance='no') detector_velocity(2)
        write(uf,'(1X,ES18.9)', advance='no') detector_velocity(3)

        write(uf,'(1X,ES18.9)', advance='no') detector_normal(1)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(1X,ES18.9)', advance='no') detector_normal(2)
        write(uf,'(1X,ES18.9)', advance='no') detector_normal(3)

        write(uf,'(1X,ES18.9)', advance='no') target_detector_relative_position(1)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(1X,ES18.9)', advance='no') target_detector_relative_position(2)
        write(uf,'(1X,ES18.9)', advance='no') target_detector_relative_position(3)

        write(uf,'(1X,ES18.9)', advance='no') target_detector_relative_velocity(1)
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(1X,ES18.9)', advance='no') target_detector_relative_velocity(2)
        write(uf,'(1X,ES18.9)', advance='no') target_detector_relative_velocity(3)

        write(uf,'(1X,ES18.9)', advance='no') target_detector_range
        ! 按照接口约定读写数据文件，并维护文件单元状态。
        write(uf,'(1X,ES18.9)', advance='no') target_detector_los_azimuth
        write(uf,'(1X,ES18.9)', advance='no') target_detector_los_elevation
        write(uf,'(1X,ES18.9)', advance='no') target_detector_los_rate_mag

        do i = 1, num_spheres
            ! 按照接口约定读写数据文件，并维护文件单元状态。
            write(uf,'(1X,ES18.9)', advance='no') sphere_centers(1,i)
            write(uf,'(1X,ES18.9)', advance='no') sphere_centers(2,i)
            write(uf,'(1X,ES18.9)', advance='no') sphere_centers(3,i)
        ! 结束本轮迭代范围，继续处理汇总后的计算结果。
        end do

        write(uf,*)
        close(uf)
    ! 结束当前计算单元，使过程边界保持清晰。
    end subroutine write_geometry_line

end program sphere_ir_radiation_production
