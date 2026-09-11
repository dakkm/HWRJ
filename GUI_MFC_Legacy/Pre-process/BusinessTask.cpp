#include "pch.h"
#include "BusinessTask.h"
namespace {
template<class T> void Field(CArchive& ar, T& value) { if(ar.IsStoring()) ar << value; else ar >> value; }
void Field(CArchive& ar, bool& value) { BYTE b=value?1:0; Field(ar,b); if(b>1) AfxThrowArchiveException(CArchiveException::badSchema); value=b!=0; }
}
void BusinessTask::Serialize(CArchive& ar)
{
    CString signature=L"Preprocess GUI task v1";
    if(ar.IsStoring()) ar << signature;
    else { CString actual; ar >> actual; if(actual!=signature) AfxThrowArchiveException(CArchiveException::badSchema); }
    Field(ar,task.metadata.name);
    Field(ar,task.metadata.description);
    Field(ar,task.duration);
    Field(ar,task.targetCount);
    Field(ar,targets.radius);
    Field(ar,targets.density);
    Field(ar,targets.heatCapacity);
    Field(ar,targets.initialTemperature);
    Field(ar,targets.heatPower);
    Field(ar,targets.emissivity);
    Field(ar,targets.solarAbsorption);
    Field(ar,targets.irReflection);
    Field(ar,scene.centerX);
    Field(ar,scene.centerY);
    Field(ar,scene.centerZ);
    Field(ar,scene.directionX);
    Field(ar,scene.directionY);
    Field(ar,scene.directionZ);
    Field(ar,scene.upX);
    Field(ar,scene.upY);
    Field(ar,scene.upZ);
    Field(ar,scene.velocityX);
    Field(ar,scene.velocityY);
    Field(ar,scene.velocityZ);
    Field(ar,scene.angularVelocityX);
    Field(ar,scene.angularVelocityY);
    Field(ar,scene.angularVelocityZ);
    Field(ar,scene.angularAccelerationX);
    Field(ar,scene.angularAccelerationY);
    Field(ar,scene.angularAccelerationZ);
    Field(ar,environment.solarFlux);
    Field(ar,environment.radiationTemperature);
    Field(ar,environment.apertureSize);
    Field(ar,environment.planeSize);
    Field(ar,environment.focalLength);
    Field(ar,environment.apertureTracking);
    Field(ar,environment.detectorTracking);
    Field(ar,environment.sunX);
    Field(ar,environment.sunY);
    Field(ar,environment.sunZ);
    Field(ar,environment.observerPositionX);
    Field(ar,environment.observerPositionY);
    Field(ar,environment.observerPositionZ);
    Field(ar,environment.observerDirectionX);
    Field(ar,environment.observerDirectionY);
    Field(ar,environment.observerDirectionZ);
    Field(ar,environment.observerUpX);
    Field(ar,environment.observerUpY);
    Field(ar,environment.observerUpZ);
    Field(ar,environment.observerVelocityX);
    Field(ar,environment.observerVelocityY);
    Field(ar,environment.observerVelocityZ);
    Field(ar,environment.observerAngularVelocityX);
    Field(ar,environment.observerAngularVelocityY);
    Field(ar,environment.observerAngularVelocityZ);
    Field(ar,environment.observerAngularAccelerationX);
    Field(ar,environment.observerAngularAccelerationY);
    Field(ar,environment.observerAngularAccelerationZ);
    Field(ar,environment.detectorDirectionX);
    Field(ar,environment.detectorDirectionY);
    Field(ar,environment.detectorDirectionZ);
    Field(ar,environment.detectorUpX);
    Field(ar,environment.detectorUpY);
    Field(ar,environment.detectorUpZ);
    Field(ar,environment.detectorVelocityX);
    Field(ar,environment.detectorVelocityY);
    Field(ar,environment.detectorVelocityZ);
    Field(ar,environment.detectorAngularVelocityX);
    Field(ar,environment.detectorAngularVelocityY);
    Field(ar,environment.detectorAngularVelocityZ);
    Field(ar,environment.detectorAngularAccelerationX);
    Field(ar,environment.detectorAngularAccelerationY);
    Field(ar,environment.detectorAngularAccelerationZ);
    Field(ar,calculation.module);
    Field(ar,calculation.forwardMode);
    Field(ar,calculation.timeout);
    Field(ar,calculation.prediction);
    Field(ar,calculation.evaluation);
    Field(ar,calculation.referenceRun);
    Field(ar,calculation.candidateRun);
    UINT count=static_cast<UINT>(targetMotion.size()); Field(ar,count);
    if(count<1 || count>100000 || task.targetCount!=static_cast<int>(count)) AfxThrowArchiveException(CArchiveException::badSchema);
    if(ar.IsLoading()) targetMotion.resize(count);
    for(auto& t: targetMotion) {
        Field(ar,t.x);
        Field(ar,t.y);
        Field(ar,t.z);
        Field(ar,t.vx);
        Field(ar,t.vy);
        Field(ar,t.vz);
        Field(ar,t.ax);
        Field(ar,t.ay);
        Field(ar,t.az);
        Field(ar,t.releaseTime);
        Field(ar,t.active);
    }
}
