#pragma once
#include <afx.h>
#include <vector>

// GUI business state, deliberately independent of the backend request format.
struct GuiTaskMetadata
{
    CString name = L"未命名任务";
    CString description = L"";
};
struct TaskSettings
{
    GuiTaskMetadata metadata; // Never mapped to backend contract fields.
    double duration = 1000;
    int targetCount = 16;
};
struct TargetSettings
{
    double radius = 0.2;
    double density = 2700;
    double heatCapacity = 900;
    double initialTemperature = 300;
    double heatPower = 300;
    double emissivity = 0.95;
    double solarAbsorption = 0.95;
    double irReflection = 0.05;
};
struct SceneMotionSettings
{
    double centerX = 0;
    double centerY = 100000;
    double centerZ = 6500000;
    double directionX = 0;
    double directionY = -0.996193717;
    double directionZ = 0.0871669503;
    double upX = 0;
    double upY = 0.0871669503;
    double upZ = 0.996193717;
    double velocityX = 0;
    double velocityY = -4000;
    double velocityZ = 350;
    double angularVelocityX = 0;
    double angularVelocityY = 0;
    double angularVelocityZ = 0;
    double angularAccelerationX = 0;
    double angularAccelerationY = 0;
    double angularAccelerationZ = 0;
};
struct EnvironmentObservationSettings
{
    double solarFlux = 1361;
    double radiationTemperature = 3;
    double apertureSize = 0.27;
    double planeSize = 0.0128;
    double focalLength = 1.34;
    bool apertureTracking = true;
    bool detectorTracking = true;
    double sunX = 1;
    double sunY = 0;
    double sunZ = 0;
    double observerPositionX = 100000;
    double observerPositionY = -100000;
    double observerPositionZ = 6500000;
    double observerDirectionX = -0.447213595;
    double observerDirectionY = 0.894427191;
    double observerDirectionZ = 0;
    double observerUpX = 0;
    double observerUpY = 0;
    double observerUpZ = 1;
    double observerVelocityX = -2100;
    double observerVelocityY = 2100;
    double observerVelocityZ = 69;
    double observerAngularVelocityX = 0;
    double observerAngularVelocityY = 0;
    double observerAngularVelocityZ = 0;
    double observerAngularAccelerationX = 0;
    double observerAngularAccelerationY = 0;
    double observerAngularAccelerationZ = 0;
    double detectorDirectionX = 0;
    double detectorDirectionY = 0;
    double detectorDirectionZ = 1;
    double detectorUpX = -0.447213595;
    double detectorUpY = 0.894427191;
    double detectorUpZ = 0;
    double detectorVelocityX = 0;
    double detectorVelocityY = 0;
    double detectorVelocityZ = 1;
    double detectorAngularVelocityX = 0;
    double detectorAngularVelocityY = 0;
    double detectorAngularVelocityZ = 0;
    double detectorAngularAccelerationX = 0;
    double detectorAngularAccelerationY = 0;
    double detectorAngularAccelerationZ = 0;
};
struct CalculationOutputSettings
{
    CString module = L"正向仿真";
    CString forwardMode = L"正式计算";
    int timeout = 1800;
    CString prediction = L"温度与点图像";
    CString evaluation = L"相似度评估";
    CString referenceRun = L"";
    CString candidateRun = L"";
};
struct TargetMotionSettings
{
    double x=0, y=0, z=0, vx=0, vy=0, vz=0, ax=0, ay=0, az=0, releaseTime=0;
    bool active=true;
};
struct BusinessTask
{
    TaskSettings task;
    TargetSettings targets;
    SceneMotionSettings scene;
    EnvironmentObservationSettings environment;
    CalculationOutputSettings calculation;
    std::vector<TargetMotionSettings> targetMotion = std::vector<TargetMotionSettings>(16);
    BusinessTask() { for(size_t i=0;i<targetMotion.size();++i) targetMotion[i].x=static_cast<double>(i); }
    // Uniform physical values apply to all targets; independent overrides are reserved for P3+.
    void Serialize(CArchive& ar);
};
