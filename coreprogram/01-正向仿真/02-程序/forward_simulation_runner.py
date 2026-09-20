from __future__ import annotations
# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
"""01-正向仿真统一标准入口（完整 clean-input 参数合同）。

正式调用：
    python forward_simulation_runner.py --params-json forward_request.json

params-json 必须显式提供 CASE / ENVIRONMENT / GROUP_STATE / OBSERVATION /
# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
TARGET_PHYSICS / TARGET_SCENE 六个分区。不存在业务参数默认值或模板回退。
"""
import argparse, hashlib, json, math, os, platform, re, subprocess, sys, time, uuid
# 导入当前模块依赖的标准能力或领域组件。
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
import pandas as pd

# 读取或写入约定的数据文件，并维护统一的路径规则。
PROGRAM_DIR=Path(__file__).resolve().parent
MODULE_ROOT=PROGRAM_DIR.parent
PACKAGE_ROOT=MODULE_ROOT.parent
INPUT_DIR=MODULE_ROOT/'01-输入文件'
# 更新当前流程所需的中间数据，为下一计算步骤做好准备。
OUTPUT_ROOT=MODULE_ROOT/'03-输出文件'/'runs'
CONFIG_PATH=PACKAGE_ROOT/'config.json'
FORWARD_EXPECTED_SHA='5106b9d8b464a61d7b10aeb04c9f159f3d076fe1e0ddef766305b75c1572d745'
# 更新当前流程所需的中间数据，为下一计算步骤做好准备。
EXPECTED_GUI_TIMES=list(range(0,1001,10))
SECTIONS={'CASE','ENVIRONMENT','GROUP_STATE','OBSERVATION','TARGET_PHYSICS','TARGET_SCENE'}
SECTION_FIELDS={
 'CASE':{'TOTAL_TIME','NUM_SPHERES'},
 # 继续执行当前业务步骤，保持处理上下文与数据状态一致。
 'ENVIRONMENT':{'SOLAR_FLUX','SOLAR_DIRECTION','ENVIRONMENT_TEMP'},
 'GROUP_STATE':{'GROUP_CENTER','GROUP_NORMAL','GROUP_UP','GROUP_VELOCITY','GROUP_ANGULAR_VELOCITY','GROUP_ANGULAR_ACCELERATION','COMPANION_TYPE','ATTITUDE_MOTION_TYPE','MICRO_MOTION_PARAMS','SIMILARITY_LEVEL'},
 'OBSERVATION':{'APERTURE_SIZE','APERTURE_CENTER','APERTURE_NORMAL','APERTURE_UP','APERTURE_VELOCITY','APERTURE_ANGULAR_VELOCITY','APERTURE_ANGULAR_ACCELERATION','APERTURE_TRACK_TARGET','DETECTOR_NORMAL','DETECTOR_UP','DETECTOR_VELOCITY','DETECTOR_ANGULAR_VELOCITY','DETECTOR_ANGULAR_ACCELERATION','DETECTOR_TRACK_TARGET','SPOT_PLANE_SIZE','SPOT_FOCAL_LENGTH'},
# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
}
VECTOR_FIELDS={'SOLAR_DIRECTION','GROUP_CENTER','GROUP_NORMAL','GROUP_UP','GROUP_VELOCITY','GROUP_ANGULAR_VELOCITY','GROUP_ANGULAR_ACCELERATION','MICRO_MOTION_PARAMS','APERTURE_CENTER','APERTURE_NORMAL','APERTURE_UP','APERTURE_VELOCITY','APERTURE_ANGULAR_VELOCITY','APERTURE_ANGULAR_ACCELERATION','DETECTOR_NORMAL','DETECTOR_UP','DETECTOR_VELOCITY','DETECTOR_ANGULAR_VELOCITY','DETECTOR_ANGULAR_ACCELERATION'}
BOOL_FIELDS={'APERTURE_TRACK_TARGET','DETECTOR_TRACK_TARGET'}
STRING_FIELDS={'COMPANION_TYPE','ATTITUDE_MOTION_TYPE','SIMILARITY_LEVEL'}
# 更新当前流程所需的中间数据，为下一计算步骤做好准备。
PHYS_FIELDS=['id','r','rho','cp','eps_ir','alpha_s','rho_ir','q_int','t_init']
SCENE_FIELDS=['id','x','y','z','vx','vy','vz','ax','ay','az','release_time','active']

def sha256(path:Path)->str:
 # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
 h=hashlib.sha256()
 with path.open('rb') as f:
  for block in iter(lambda:f.read(4*1024*1024),b''): h.update(block)
 return h.hexdigest()

# 定义 load_config 处理过程，集中封装该步骤的输入、输出与异常边界。
def load_config()->dict[str,Any]:
 return json.loads(CONFIG_PATH.read_text(encoding='utf-8')) if CONFIG_PATH.exists() else {}

def package_path(raw:str|Path)->Path:
 # 读取或写入约定的数据文件，并维护统一的路径规则。
 p=Path(raw); return p.resolve() if p.is_absolute() else (PACKAGE_ROOT/p).resolve()

def emit_progress(**payload:Any)->None:
 print('GUI_PROGRESS '+json.dumps({'module':'01','timestamp':time.time(),**payload},ensure_ascii=False),flush=True)

def _read_csv_compatible(path:Path)->pd.DataFrame:
 # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
 last=None
 for encoding in ('utf-8-sig','utf-8','gb18030','cp936'):
  try:
   return pd.read_csv(path,encoding=encoding)
  # 捕获本阶段异常并转换为明确的错误信息或运行状态。
  except UnicodeDecodeError as exc:
   last=exc
 raise RuntimeError(f'无法读取CSV编码：{path}; last={last}')

# 定义 finite 处理过程，集中封装该步骤的输入、输出与异常边界。
def finite(name:str,v:Any)->float:
 if isinstance(v,bool): raise ValueError(f'{name} 必须为数值，不能是布尔值')
 try: x=float(v)
 except Exception as e: raise ValueError(f'{name} 必须为数值，当前={v!r}') from e
 # 检查当前条件，仅在满足约束时执行对应分支。
 if not math.isfinite(x): raise ValueError(f'{name} 必须为有限数值')
 return x

def integer(name:str,v:Any)->int:
 # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
 x=finite(name,v)
 if not x.is_integer(): raise ValueError(f'{name} 必须为整数')
 return int(x)

def vector3(name:str,v:Any)->list[float]:
 # 检查当前条件，仅在满足约束时执行对应分支。
 if not isinstance(v,(list,tuple)) or len(v)!=3: raise ValueError(f'{name} 必须为3分量数组')
 return [finite(f'{name}[{i}]',x) for i,x in enumerate(v)]

def boolean(name:str,v:Any)->bool:
 # 检查当前条件，仅在满足约束时执行对应分支。
 if type(v) is not bool: raise ValueError(f'{name} 必须显式使用 JSON true/false')
 return v

def token(name:str,v:Any)->str:
 if not isinstance(v,str) or not v.strip(): raise ValueError(f'{name} 必须为非空字符串')
 # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
 text=v.strip().upper()
 if re.search(r'[\s#=\[\]]',text): raise ValueError(f'{name} 不能包含空白或输入格式保留字符')
 return text

def exact_keys(name:str,obj:dict[str,Any],expected:set[str])->None:
 # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
 actual=set(obj)
 missing=expected-actual; extra=actual-expected
 if missing or extra: raise ValueError(f'{name} 字段不符合正式合同；缺少={sorted(missing)}，未知={sorted(extra)}')

# 定义 validate_request 处理过程，集中封装该步骤的输入、输出与异常边界。
def validate_request(req:dict[str,Any])->dict[str,Any]:
 if not isinstance(req,dict): raise ValueError('顶层请求必须为 JSON 对象')
 exact_keys('顶层',req,SECTIONS)
 clean:dict[str,Any]={}
 # 遍历当前数据或迭代计算，逐项更新处理结果。
 for sec in ['CASE','ENVIRONMENT','GROUP_STATE','OBSERVATION']:
  obj=req[sec]
  if not isinstance(obj,dict): raise ValueError(f'{sec} 必须为对象')
  # 调用对应组件完成当前处理步骤，并保留返回结果。
  exact_keys(sec,obj,SECTION_FIELDS[sec])
  clean[sec]={}
  for k,v in obj.items():
   if k in VECTOR_FIELDS: clean[sec][k]=vector3(k,v)
   # 检查当前条件，仅在满足约束时执行对应分支。
   elif k in BOOL_FIELDS: clean[sec][k]=boolean(k,v)
   elif k in STRING_FIELDS: clean[sec][k]=token(k,v)
   elif k=='NUM_SPHERES': clean[sec][k]=integer(k,v)
   # 前置条件不成立时进入备用处理路径。
   else: clean[sec][k]=finite(k,v)
 if clean['CASE']['TOTAL_TIME']<=0: raise ValueError('TOTAL_TIME 必须 > 0')
 n=clean['CASE']['NUM_SPHERES']
 if n<1: raise ValueError('NUM_SPHERES 必须为正整数')
 # 遍历当前数据或迭代计算，逐项更新处理结果。
 for sec,fields in [('TARGET_PHYSICS',PHYS_FIELDS),('TARGET_SCENE',SCENE_FIELDS)]:
  rows=req[sec]
  if not isinstance(rows,list) or len(rows)!=n: raise ValueError(f'{sec} 必须恰好包含 NUM_SPHERES={n} 行')
  out=[]; ids=[]
  # 遍历当前数据或迭代计算，逐项更新处理结果。
  for j,row in enumerate(rows,1):
   if not isinstance(row,dict): raise ValueError(f'{sec}[{j}] 必须为对象')
   exact_keys(f'{sec}[{j}]',row,set(fields))
   # 把本次处理结果加入集合，供后续汇总或输出使用。
   d={}; d['id']=integer(f'{sec}[{j}].id',row['id']); ids.append(d['id'])
   for k in fields[1:]:
    if k=='active': d[k]=boolean(f'{sec}[{j}].active',row[k])
    else: d[k]=finite(f'{sec}[{j}].{k}',row[k])
   # 把本次处理结果加入集合，供后续汇总或输出使用。
   out.append(d)
  if sorted(ids)!=list(range(1,n+1)) or len(set(ids))!=n: raise ValueError(f'{sec} 的 id 必须唯一并完整覆盖 1..{n}')
  clean[sec]=out
 # 检查当前条件，仅在满足约束时执行对应分支。
 if {r['id'] for r in clean['TARGET_PHYSICS']}!={r['id'] for r in clean['TARGET_SCENE']}: raise ValueError('两个目标表的 ID 集必须完全一致')
 for r in clean['TARGET_PHYSICS']:
  if r['r']<=0.005: raise ValueError(f"目标{r['id']}：r 必须 > 0.005 m")
  for k in ['rho','cp','t_init']:
   # 检查当前条件，仅在满足约束时执行对应分支。
   if r[k]<=0: raise ValueError(f"目标{r['id']}：{k} 必须 > 0")
 for r in clean['TARGET_SCENE']:
  if r['release_time']<0: raise ValueError(f"目标{r['id']}：release_time 必须 >= 0")
 # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
 p1=next(r for r in clean['TARGET_SCENE'] if r['id']==1)
 if abs(p1['release_time'])>1e-12: raise ValueError('1号目标 release_time 必须为 0')
 if not p1['active']: raise ValueError('1号目标 active 必须为 true')
 return clean

# 定义 fmt 处理过程，集中封装该步骤的输入、输出与异常边界。
def fmt(x:Any)->str:
 if type(x) is bool: return 'T' if x else 'F'
 if isinstance(x,(list,tuple)): return ' '.join(fmt(v) for v in x)
 # 检查当前条件，仅在满足约束时执行对应分支。
 if isinstance(x,int): return str(x)
 if isinstance(x,float): return format(x,'.15g')
 return str(x)

def to_generator_config(clean:dict[str,Any])->dict[str,Any]:
 # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
 flat={}
 for sec in ['CASE','ENVIRONMENT','GROUP_STATE','OBSERVATION']:
  for k,v in clean[sec].items(): flat[k]=fmt(v)
 flat['TARGET_PHYSICS']=[[fmt(row[k]) for k in PHYS_FIELDS] for row in clean['TARGET_PHYSICS']]
 # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
 flat['TARGET_SCENE']=[[fmt(row[k]) for k in SCENE_FIELDS] for row in clean['TARGET_SCENE']]
 return flat

def generate_clean_input(generator:Path,case_config:Path,template:Path,input_path:Path)->None:
 # 读取或写入约定的数据文件，并维护统一的路径规则。
 cp=subprocess.run([sys.executable,str(generator),str(case_config),str(template),str(input_path)],cwd=input_path.parent,capture_output=True,text=True)
 if cp.returncode!=0 or not input_path.exists(): raise RuntimeError(f'clean-input 生成失败 return_code={cp.returncode}; stderr={cp.stderr[-1000:]!r}')

def parse_solver_status(path:Path)->dict[str,Any]:
 out={'solver_status':'MISSING','result_valid':False,'message':'solver_status.txt missing'}
 # 检查当前条件，仅在满足约束时执行对应分支。
 if not path.exists(): return out
 for line in path.read_text(encoding='utf-8',errors='replace').splitlines():
  if '=' not in line: continue
  # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
  k,v=[x.strip() for x in line.split('=',1)]
  if k=='solver_status': out[k]=v
  elif k=='result_valid': out[k]=v.upper() in {'T','TRUE','1','YES'}
  elif k=='message': out[k]=v
 # 向调用方返回当前步骤生成的数据或迭代结果。
 return out

def parse_forward_outputs(output_dir:Path,run_dir:Path,total_time:float)->dict[str,Any]:
 temperature=output_dir/'temperature_history.csv'; infrared=output_dir/'infrared_response_history.csv'; status_path=output_dir/'solver_status.txt'; frame_summary=output_dir/'frame_summary.csv'; metadata=output_dir/'run_metadata.json'
 # 读取或写入约定的数据文件，并维护统一的路径规则。
 summary={'output_dir':str(output_dir),'temperature_history_csv':str(temperature),'infrared_response_history_csv':str(infrared),'solver_status_txt':str(status_path),'frame_summary_csv':str(frame_summary),'run_metadata_json':str(metadata)}; summary.update(parse_solver_status(status_path))
 missing=[p.name for p in (temperature,infrared,status_path) if not p.exists()]
 if missing: return {**summary,'output_valid':False,'output_message':f'缺少新版正式输出：{missing}'}
 th=_read_csv_compatible(temperature); required={'frame','time_s','T_1'}
 # 检查当前条件，仅在满足约束时执行对应分支。
 if not required.issubset(th.columns): return {**summary,'output_valid':False,'output_message':f'temperature_history.csv 缺少列：{sorted(required-set(th.columns))}'}
 th['time_s']=pd.to_numeric(th['time_s'],errors='coerce'); th['T_1']=pd.to_numeric(th['T_1'],errors='coerce'); th=th.dropna(subset=['time_s','T_1']).sort_values('time_s')
 # 只有标准1000 s任务才生成旧101点兼容曲线；正式真值始终是 temperature_history.csv。
 curve_path=None
 if abs(total_time-1000.0)<1e-9:
  # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
  lookup=th.set_index('time_s')['T_1']
  try:
   sphere1=pd.DataFrame({'time_s':EXPECTED_GUI_TIMES,'temperature_K':[float(lookup.loc[float(t)]) for t in EXPECTED_GUI_TIMES]})
   # 读取或写入约定的数据文件，并维护统一的路径规则。
   curve_path=run_dir/'sphere1_temperature.csv'; sphere1.to_csv(curve_path,index=False,encoding='utf-8-sig')
  except Exception: curve_path=None
 ir=_read_csv_compatible(infrared); ir_required={'frame_id','time_s','object_id','radiation_power_W','detector_received_power_W'}; ir_ok=ir_required.issubset(ir.columns) and len(ir)>0
 status=summary; valid=bool(status.get('result_valid')) and str(status.get('solver_status','')).upper()=='SUCCESS' and ir_ok
 # 读取或写入约定的数据文件，并维护统一的路径规则。
 summary.update({'sphere1_temperature_csv':str(curve_path) if curve_path else None,'temperature_history_point_count':len(th),'infrared_response_row_count':len(ir),'output_valid':valid,'output_message':'新版业务输出有效' if valid else '新版业务输出存在状态或字段异常'})
 return summary

def run_forward_simulation(params:dict[str,Any],*,run_root:Path|None=None,timeout_seconds:int=1800,prepare_only:bool=False)->tuple[int,dict[str,Any]]:
 # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
 start=time.time(); clean=validate_request(params); cfg=load_config()
 template=package_path(cfg.get('forward_clean_template','01-正向仿真/01-输入文件/clean_case_template.dat')); generator=package_path(cfg.get('forward_case_generator','01-正向仿真/02-程序/generate_clean_formal_case.py')); exe=package_path(cfg.get('forward_exe','01-正向仿真/02-程序/production_main_output_interface.exe'))
 for p,label in ((template,'clean输入模板'),(generator,'clean输入生成器')):
  if not p.exists(): raise FileNotFoundError(f'{label}不存在：{p}')
 # 读取或写入约定的数据文件，并维护统一的路径规则。
 root=Path(run_root).resolve() if run_root else OUTPUT_ROOT.resolve(); run_id=datetime.now().strftime('run_%Y%m%d_%H%M%S_')+uuid.uuid4().hex[:8]; run_dir=root/run_id; output_dir=run_dir/'output'; output_dir.mkdir(parents=True,exist_ok=False)
 request_path=run_dir/'request.json'; request_path.write_text(json.dumps(clean,ensure_ascii=False,indent=2),encoding='utf-8')
 case_config=run_dir/'case_config.json'; case_config.write_text(json.dumps(to_generator_config(clean),ensure_ascii=False,indent=2),encoding='utf-8')
 # 读取或写入约定的数据文件，并维护统一的路径规则。
 input_path=run_dir/'input.dat'; generate_clean_input(generator,case_config,template,input_path); emit_progress(state='input_ready',run_id=run_id,run_dir=str(run_dir))
 base={'module':'01','run_id':run_id,'input_contract':'forward-request-v1','parameters':clean,'case_config_json':str(case_config),'input_dat':str(input_path),'output_dir':str(output_dir),'request_json':str(request_path),'platform':platform.platform()}
 if prepare_only:
  result={**base,'status':'prepared','return_code':0,'elapsed_seconds':time.time()-start}; rp=run_dir/'result.json'; result['result_json']=str(rp); rp.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8'); emit_progress(state='prepared',run_id=run_id); return 0,result
 # 检查当前条件，仅在满足约束时执行对应分支。
 if not exe.exists(): raise FileNotFoundError(f'正向计算 EXE 不存在：{exe}')
 actual_sha=sha256(exe)
 # 本地使用 Intel oneAPI 重新编译后 EXE 哈希会变化。默认只记录实际 SHA，不阻断运行；
 # 如确需冻结校验，可在根 config.json 中设置 forward_exe_sha_policy='enforce'
 # 以及 forward_exe_expected_sha256。
 sha_policy=str(cfg.get('forward_exe_sha_policy','record_only')).strip().lower()
 # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
 expected_sha=str(cfg.get('forward_exe_expected_sha256',FORWARD_EXPECTED_SHA)).strip()
 if sha_policy=='enforce' and expected_sha and actual_sha!=expected_sha:
  raise RuntimeError(f'正向计算 EXE SHA256 不匹配：actual={actual_sha}, expected={expected_sha}')
 if os.name!='nt': raise RuntimeError('随包正向程序为 Windows EXE；非 Windows 环境可使用 --prepare-only 做完整输入合同自检。')
 # 读取或写入约定的数据文件，并维护统一的路径规则。
 emit_progress(state='running_forward',run_id=run_id); t0=time.perf_counter(); cp=subprocess.run([str(exe),str(input_path)],cwd=run_dir,capture_output=True,text=True,timeout=int(timeout_seconds)); elapsed=time.perf_counter()-t0
 (run_dir/'stdout.txt').write_text(cp.stdout or '',encoding='utf-8',errors='replace'); (run_dir/'stderr.txt').write_text(cp.stderr or '',encoding='utf-8',errors='replace')
 summary=parse_forward_outputs(output_dir,run_dir,clean['CASE']['TOTAL_TIME']); success=cp.returncode==0 and bool(summary.get('output_valid'))
 result={**base,'status':'success' if success else 'forward_failed','return_code':cp.returncode,'forward_exe':str(exe),'forward_exe_sha256':actual_sha,'forward_exe_sha_policy':sha_policy,'forward_exe_expected_sha256':expected_sha,'forward_exe_sha_match':(actual_sha==expected_sha),'forward_elapsed_seconds':elapsed,**summary,'elapsed_seconds':time.time()-start}; rp=run_dir/'result.json'; result['result_json']=str(rp); rp.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8'); emit_progress(state=result['status'],run_id=run_id); return (0 if success else 3),result

# 定义 parse_args 处理过程，集中封装该步骤的输入、输出与异常边界。
def parse_args()->argparse.Namespace:
 ap=argparse.ArgumentParser(description='01-正向仿真统一入口：完整业务参数 -> 严格校验 -> clean input -> 正式求解器。'); ap.add_argument('--params-json',type=Path,required=True,help='完整 forward-request-v1 JSON；六个分区均必须显式提供。'); ap.add_argument('--run-root',type=Path,default=None); ap.add_argument('--timeout-seconds',type=int,default=1800); ap.add_argument('--prepare-only',action='store_true'); return ap.parse_args()
def main()->int:
 # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
 args=parse_args()
 try:
  params=json.loads(args.params_json.read_text(encoding='utf-8')); code,result=run_forward_simulation(params,run_root=args.run_root,timeout_seconds=args.timeout_seconds,prepare_only=args.prepare_only); print(json.dumps(result,ensure_ascii=False)); return code
 except Exception as exc:
  # 读取或写入约定的数据文件，并维护统一的路径规则。
  print(json.dumps({'module':'01','status':'error','error_type':type(exc).__name__,'message':str(exc)},ensure_ascii=False),file=sys.stderr); return 2
if __name__=='__main__': raise SystemExit(main())
