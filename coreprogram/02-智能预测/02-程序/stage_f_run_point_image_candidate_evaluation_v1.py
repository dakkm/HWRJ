from __future__ import annotations

# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
"""点图像代理候选评价。

正式固定场景合同：
- 固定场景运行特征模板位于 02-智能预测/03-模型文件/point_image_fixed_scene_runtime.csv。
# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
- 该模板属于冻结模型资源，不属于用户业务输入。
- q_int / emissivity_ir / absorptivity_solar 由规范输入经 surrogate_standard_input.py 校验后在运行时注入。
- source_case_id 仅作为结果追踪元数据，不用于选择场景模板。
- 模型加载固定采用 Keras 3 + TensorFlow 后端，并注册训练时使用的自定义层与损失。
# 继续执行当前业务步骤，保持处理上下文与数据状态一致。
"""

import argparse
import json
# 导入当前模块依赖的标准能力或领域组件。
import os
import time
from pathlib import Path
# 导入当前模块依赖的标准能力或领域组件。
from typing import Any

import numpy as np
import pandas as pd

from stage_utils import *

# 更新当前流程所需的中间数据，为下一计算步骤做好准备。
MODEL_FILE="best_model_win7_savedmodel"
MODEL_SHA=""
SCALER_SHA="17102e09e6b4e43917cd8903d3c742521b907db26a5daac2bb7f1481aff8d2b7"
GLOBAL=["q_int","emissivity_ir","absorptivity_solar","time_s","distance_to_detector"]
# 更新当前流程所需的中间数据，为下一计算步骤做好准备。
LOCAL=["sphere_id_norm","active_flag","sphere_init_x","sphere_init_y","sphere_init_z","sphere_vx","sphere_vy","sphere_vz","sphere_release_time","sphere_age_s","sphere_released_flag","sphere_pos_x","sphere_pos_y","sphere_pos_z"]
TARGET=["screen_x","screen_y","log_spot_power","log_spot_intensity"]
TEMPLATE_COLS=["frame_id","sphere_id","time_s","distance_to_detector",*LOCAL,"input_GRID_NX","input_GRID_NY","input_SPOT_PLANE_SIZE"]


# 定义 pixel 处理过程，集中封装该步骤的输入、输出与异常边界。
def pixel(x,y,nx,ny,size):
    valid=np.isfinite(x)&np.isfinite(y)&(np.abs(x)<=size/2)&(np.abs(y)<=size/2)
    px=np.floor((x/size+.5)*nx).astype(int)+1; py=np.floor((y/size+.5)*ny).astype(int)+1
    valid&=(px>=1)&(px<=nx)&(py>=1)&(py<=ny); return px,py,valid


# 定义 load_scene_template 处理过程，集中封装该步骤的输入、输出与异常边界。
def load_scene_template(path: Path) -> pd.DataFrame:
    d=pd.read_csv(path)
    missing=set(TEMPLATE_COLS)-set(d.columns)
    # 检查当前条件，仅在满足约束时执行对应分支。
    if missing:
        raise ValueError(f"point_image_fixed_scene_runtime.csv 缺少列：{sorted(missing)}")
    d=d[TEMPLATE_COLS].copy()
    # 执行数组或表格数据运算，为后续数值处理准备结果。
    d["frame_id"]=pd.to_numeric(d["frame_id"],errors="raise").astype(int)
    d["sphere_id"]=pd.to_numeric(d["sphere_id"],errors="raise").astype(int)
    for c in [x for x in TEMPLATE_COLS if x not in {"frame_id","sphere_id"}]:
        # 执行数组或表格数据运算，为后续数值处理准备结果。
        d[c]=pd.to_numeric(d[c],errors="raise")
    d=d.sort_values(["frame_id","sphere_id"],kind="mergesort").reset_index(drop=True)
    if len(d)!=1616:
        raise ValueError(f"固定点图像模板必须为 1616 行，实际 {len(d)} 行。")
    # 检查当前条件，仅在满足约束时执行对应分支。
    if d.duplicated(["frame_id","sphere_id"]).any():
        raise ValueError("固定点图像模板存在 frame_id/sphere_id 重复键。")
    counts=d.groupby("frame_id")["sphere_id"].nunique()
    # 检查当前条件，仅在满足约束时执行对应分支。
    if len(counts)!=101 or not counts.eq(16).all():
        raise ValueError("固定点图像模板必须包含 101 个时刻且每时刻 16 个球。")
    times=d.groupby("frame_id",sort=True)["time_s"].first().to_numpy(float)
    # 检查当前条件，仅在满足约束时执行对应分支。
    if not np.array_equal(times,np.arange(0.0,1000.0+10.0,10.0)):
        raise ValueError("固定点图像模板时间轴必须为 0~1000 s、步长 10 s。")
    ids=np.sort(d.sphere_id.unique())
    if not np.array_equal(ids,np.arange(1,17)):
        # 检测到无效状态后立即报错，防止异常数据继续传播。
        raise ValueError("固定点图像模板 sphere_id 必须为 1~16。")
    if not np.isfinite(d[TEMPLATE_COLS].to_numpy(dtype=float)).all():
        raise ValueError("固定点图像模板包含非有限值。")
    # 向调用方返回当前步骤生成的数据或迭代结果。
    return d


def _keras_runtime():
    """使用 Keras 3 + TensorFlow 后端加载正式点图像模型。"""
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    os.environ["KERAS_BACKEND"] = "tensorflow"

    import tensorflow as tf
    import keras
    # 导入当前模块依赖的标准能力或领域组件。
    from keras import layers
    try:
        from keras import ops
    except ImportError:
        # Keras 2.10 (the Win7/Python 3.8 runtime) predates keras.ops.
        class _TensorOps:
            arange = staticmethod(tf.range)
            expand_dims = staticmethod(tf.expand_dims)
            convert_to_tensor = staticmethod(tf.convert_to_tensor)
            cast = staticmethod(tf.cast)
            reshape = staticmethod(tf.reshape)
            abs = staticmethod(tf.abs)
            minimum = staticmethod(tf.minimum)
            mean = staticmethod(tf.reduce_mean)
            square = staticmethod(tf.square)
        ops = _TensorOps()

    return keras, layers, tf, ops


def load_forward_model(model_path: Path):
    keras,layers,tf,ops=_keras_runtime()
    if model_path.is_dir():
        saved = tf.saved_model.load(str(model_path))
        signature = saved.signatures["serving_default"]
        class SavedModelAdapter:
            def predict(self, inputs, batch_size=None, verbose=0):
                global_input, local_input = inputs
                result = signature(global_in=tf.convert_to_tensor(global_input), local_in=tf.convert_to_tensor(local_input))
                return next(iter(result.values())).numpy()
        return SavedModelAdapter()
    register_serializable = getattr(keras.utils, "register_keras_serializable", lambda **kwargs: (lambda cls: cls))

    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    @register_serializable(package="thermal_optuna")
    class LearnedPositionEmbedding(layers.Layer):
        def __init__(self,n_obj:int,d_model:int,**kwargs):
            # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
            super().__init__(**kwargs); self.n_obj=int(n_obj); self.d_model=int(d_model)
            self.pos_embedding=layers.Embedding(input_dim=self.n_obj,output_dim=self.d_model)
        def call(self,x):
            # 检查当前条件，仅在满足约束时执行对应分支。
            if tf is not None:
                positions=tf.range(start=0,limit=self.n_obj,delta=1)
                return x+self.pos_embedding(positions)[tf.newaxis,:,:]
            positions=ops.arange(0,self.n_obj,1)
            # 向调用方返回当前步骤生成的数据或迭代结果。
            return x+ops.expand_dims(self.pos_embedding(positions),0)
        def get_config(self):
            return {**super().get_config(),"n_obj":self.n_obj,"d_model":self.d_model}

    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    @register_serializable(package="thermal_optuna")
    class TransformerBlock(layers.Layer):
        def __init__(self,d_model:int,num_heads:int,ff_mult:int,dropout:float,**kwargs):
            # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
            super().__init__(**kwargs); self.d_model=int(d_model); self.num_heads=int(num_heads); self.ff_mult=int(ff_mult); self.dropout=float(dropout)
            self.attn=layers.MultiHeadAttention(num_heads=self.num_heads,key_dim=max(1,self.d_model//max(1,self.num_heads)),dropout=self.dropout,output_shape=self.d_model)
            self.norm1=layers.LayerNormalization(epsilon=1.0e-6); self.norm2=layers.LayerNormalization(epsilon=1.0e-6); self.drop=layers.Dropout(self.dropout)
            self.ffn=keras.Sequential([layers.Dense(self.d_model*self.ff_mult,activation="gelu"),layers.Dropout(self.dropout),layers.Dense(self.d_model)])
        # 定义 call 处理过程，集中封装该步骤的输入、输出与异常边界。
        def call(self,x,training:bool=False):
            attn_out=self.attn(x,x,training=training); x=self.norm1(x+self.drop(attn_out,training=training)); ffn_out=self.ffn(x,training=training); return self.norm2(x+self.drop(ffn_out,training=training))
        def get_config(self):
            # 向调用方返回当前步骤生成的数据或迭代结果。
            return {**super().get_config(),"d_model":self.d_model,"num_heads":self.num_heads,"ff_mult":self.ff_mult,"dropout":self.dropout}

    @register_serializable(package="thermal_optuna")
    class WeightedHuberLoss(keras.losses.Loss):
        # 定义 __init__ 处理过程，集中封装该步骤的输入、输出与异常边界。
        def __init__(self,delta:float=1.0,weights:list[float]|None=None,target_dim:int=4,name:str="weighted_huber_loss",**kwargs):
            super().__init__(name=name,**kwargs); self.delta=float(delta); self.weights=[float(w) for w in (weights or [1.0]*target_dim)]; self.target_dim=int(target_dim)
        def call(self,y_true,y_pred):
            # 检查当前条件，仅在满足约束时执行对应分支。
            if tf is not None:
                w=tf.constant(self.weights,dtype=tf.float32); delta=tf.constant(self.delta,dtype=tf.float32); yt=tf.reshape(y_true,(-1,self.target_dim)); yp=tf.reshape(y_pred,(-1,self.target_dim)); err=yt-yp; ae=tf.abs(err); q=tf.minimum(ae,delta); lin=ae-q; return tf.reduce_mean((0.5*tf.square(q)+delta*lin)*w)
            w=ops.convert_to_tensor(self.weights,dtype="float32"); delta=ops.cast(self.delta,"float32"); yt=ops.reshape(y_true,(-1,self.target_dim)); yp=ops.reshape(y_pred,(-1,self.target_dim)); err=yt-yp; ae=ops.abs(err); q=ops.minimum(ae,delta); lin=ae-q; return ops.mean((0.5*ops.square(q)+delta*lin)*w)
        def get_config(self):
            # 向调用方返回当前步骤生成的数据或迭代结果。
            return {**super().get_config(),"delta":self.delta,"weights":self.weights,"target_dim":self.target_dim}

    custom_objects = {
        "LearnedPositionEmbedding": LearnedPositionEmbedding,
        "TransformerBlock": TransformerBlock,
        "WeightedHuberLoss": WeightedHuberLoss,
        "thermal_optuna>LearnedPositionEmbedding": LearnedPositionEmbedding,
        "thermal_optuna>TransformerBlock": TransformerBlock,
        "thermal_optuna>WeightedHuberLoss": WeightedHuberLoss,
    }
    return keras.models.load_model(model_path, custom_objects=custom_objects, compile=False)


def validate_contract(template_path: Path|None=None, model_dir: Path|None=None) -> dict[str,Any]:
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    template_path=template_path or config_path(
        "point_image_scene_template",
        MODEL_DIR/"point_image_fixed_scene_runtime.csv",
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        must_exist=True,
    )
    model_dir=model_dir or config_path("point_image_model_dir",MODEL_DIR,must_exist=True)
    modelp=model_dir/MODEL_FILE; scalep=model_dir/"scalers.json"
    # 检查当前条件，仅在满足约束时执行对应分支。
    if not modelp.is_dir(): raise RuntimeError("Win7 点图像 SavedModel 目录不存在。")
    if MODEL_SHA and sha256(modelp)!=MODEL_SHA: raise RuntimeError("点图像模型 SHA256 不匹配。")
    if sha256(scalep)!=SCALER_SHA: raise RuntimeError("scalers.json SHA256 不匹配。")
    d=load_scene_template(template_path)
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    scalers=json.loads(scalep.read_text(encoding="utf-8"))
    if list(scalers.get("global_cols",[]))!=GLOBAL or list(scalers.get("local_cols",[]))!=LOCAL or list(scalers.get("target_cols",[]))!=TARGET or int(scalers.get("n_obj",0))!=16:
        raise RuntimeError("scalers.json 输入/输出合同与 Stage F 不一致。")
    # 向调用方返回当前步骤生成的数据或迭代结果。
    return {"template":str(template_path),"rows":len(d),"frames":d.frame_id.nunique(),"spheres_per_frame":16,"model_sha256":MODEL_SHA,"scaler_sha256":SCALER_SHA}


def main(validate_only: bool=False):
    ensure(); start=time.time()
    if MODULE_ROOT.name.startswith("02-"):
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        selp=OUT/"point_image_selection.csv"  # 仅供内部调试；正式GUI通过 surrogate_prediction_runner.py 调用
    else:
        selp=OUT/"13_point_image_evaluation_selection.csv"
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    templatep=config_path(
        "point_image_scene_template",
        PACKAGE_ROOT/"02-智能预测"/"03-模型文件"/"point_image_fixed_scene_runtime.csv",
        # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
        must_exist=True,
    )
    md=config_path("point_image_model_dir",MODEL_DIR,must_exist=True); modelp=md/MODEL_FILE; scalep=md/"scalers.json"
    # 校验输入数据及运行前提，提前拒绝不符合契约的参数。
    contract=validate_contract(templatep,md)
    if validate_only:
        print(json.dumps({"status":"TemplateContractValid",**contract},ensure_ascii=False,indent=2)); return 0
    if not selp.exists(): raise FileNotFoundError(f"点图像候选输入不存在：{selp}")
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    sel=pd.read_csv(selp)
    need={"target_id","candidate_id","q_int","emissivity_ir","absorptivity_solar"}
    missing=need-set(sel.columns)
    # 检查当前条件，仅在满足约束时执行对应分支。
    if missing: raise ValueError(f"点图像候选输入缺少列：{sorted(missing)}")
    if "source_case_id" not in sel.columns: sel["source_case_id"]="fixed_scene"
    state=load_scene_template(templatep)
    # 读取或写入约定的数据文件，并维护统一的路径规则。
    scalers=json.loads(scalep.read_text(encoding="utf-8")); model=load_forward_model(modelp); nobj=16
    tokens=[]; metrics=[]; recon=[]; infer_s=0.0
    for r in sel.itertuples(index=False):
        rows=state.copy()
        # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
        rows["q_int"]=float(r.q_int); rows["emissivity_ir"]=float(r.emissivity_ir); rows["absorptivity_solar"]=float(r.absorptivity_solar)
        sample=rows.groupby("frame_id",sort=False).head(1)
        gr=sample[GLOBAL].to_numpy(np.float32); lr=rows[LOCAL].to_numpy(np.float32).reshape(-1,nobj,len(LOCAL))
        # 执行数组或表格数据运算，为后续数值处理准备结果。
        g=(gr-np.asarray(scalers["global_mean"],np.float32))/np.asarray(scalers["global_scale"],np.float32)
        l=(lr-np.asarray(scalers["local_mean"],np.float32))/np.asarray(scalers["local_scale"],np.float32)
        ts=time.perf_counter(); ps=model.predict([g,l],batch_size=64,verbose=0); infer_s+=time.perf_counter()-ts
        # 执行数组或表格数据运算，为后续数值处理准备结果。
        y=ps.reshape(-1,nobj,4)*np.asarray(scalers["target_scale"])[None,None,:]+np.asarray(scalers["target_offset"])[None,None,:]
        base=rows[["frame_id","sphere_id","time_s","sphere_released_flag","input_GRID_NX","input_GRID_NY","input_SPOT_PLANE_SIZE"]].copy().reset_index(drop=True)
        flat=y.reshape(-1,4)
        # 遍历当前数据或迭代计算，逐项更新处理结果。
        for i,c in enumerate(TARGET): base[f"pred_{c}"]=flat[:,i]
        base.insert(0,"candidate_id",r.candidate_id); base.insert(0,"source_case_id",r.source_case_id); base.insert(0,"target_id",r.target_id)
        base["pred_spot_power"]=10**base.pred_log_spot_power; base["pred_spot_intensity"]=10**base.pred_log_spot_intensity
        base["physical_pixel_area_m2"]=(base.input_SPOT_PLANE_SIZE/base.input_GRID_NX)*(base.input_SPOT_PLANE_SIZE/base.input_GRID_NY)
        base["pred_spot_power_W"]=np.maximum(base.pred_spot_intensity-1e-18,0.0)
        base["pred_detector_irradiance_W_m2"]=base.pred_spot_power_W/base.physical_pixel_area_m2
        px,py,v=pixel(base.pred_screen_x.to_numpy(),base.pred_screen_y.to_numpy(),base.input_GRID_NX.to_numpy(),base.input_GRID_NY.to_numpy(),base.input_SPOT_PLANE_SIZE.to_numpy())
        # 把本次处理结果加入集合，供后续汇总或输出使用。
        base["pixel_x"]=px; base["pixel_y"]=py; base["in_bounds"]=v; tokens.append(base)
        fr=[]
        for frame,fg in base.groupby("frame_id",sort=True):
            # 执行数组或表格数据运算，为后续数值处理准备结果。
            valid=fg.in_bounds.to_numpy(bool); power=fg.pred_spot_power_W.to_numpy(); intensity=fg.pred_spot_intensity.to_numpy(); w=power[valid]; xx=fg.pixel_x.to_numpy()[valid]; yy=fg.pixel_y.to_numpy()[valid]
            fr.append({"frame_id":frame,"time_s":fg.time_s.iloc[0],"total_power":w.sum(),"peak_power":w.max() if len(w) else 0,"centroid_x":np.average(xx,weights=w) if w.sum()>0 else np.nan,"centroid_y":np.average(yy,weights=w) if w.sum()>0 else np.nan,"total_intensity":intensity[valid].sum(),"released_count":int((fg.sphere_released_flag>=.5).sum())})
        fr=pd.DataFrame(fr)
        # 把本次处理结果加入集合，供后续汇总或输出使用。
        metrics.append({"target_id":r.target_id,"source_case_id":r.source_case_id,"candidate_id":r.candidate_id,"frame_count":len(fr),"median_total_power":fr.total_power.median(),"max_total_power":fr.total_power.max(),"final_total_power":fr.total_power.iloc[-1],"median_peak_power":fr.peak_power.median(),"max_peak_power":fr.peak_power.max(),"centroid_x_range":fr.centroid_x.max()-fr.centroid_x.min(),"centroid_y_range":fr.centroid_y.max()-fr.centroid_y.min(),"interpretation":"fixed-scene candidate-relative predicted-token reconstruction; no true-token error"})
        recon.append({"target_id":r.target_id,"candidate_id":r.candidate_id,"frames":101,"dimensions":"256x256","pixel_index":"1-based","accumulation":"same-pixel sum","inverse":"10**log_value","PSF":"none","interpolation":"none","smoothing":"none"})
    tok=pd.concat(tokens,ignore_index=True); met=pd.DataFrame(metrics)
    tp=OUT/"candidate_point_token_predictions.csv.gz"; tok.to_csv(tp,index=False,compression="gzip")
    # 更新当前流程所需的中间数据，为下一计算步骤做好准备。
    mp=OUT/"candidate_point_image_metrics.csv"; met.to_csv(mp,index=False)
    rp=OUT/"candidate_reconstruction_manifest.csv"; pd.DataFrame(recon).to_csv(rp,index=False)
    lineage=[]
    # 遍历当前数据或迭代计算，逐项更新处理结果。
    for i,f in enumerate(GLOBAL): lineage.append({"field_name":f,"tensor_group":"global","tensor_index":i,"source_type":"candidate" if i<3 else "fixed_scene_template","source_file":str(selp if i<3 else templatep),"source_field":f,"candidate_dependent":i<3,"fixed_scene_inherited":i>=3,"time_dependent":i>=3,"transformation":"standardize scaler","missing_policy":"block"})
    for i,f in enumerate(LOCAL): lineage.append({"field_name":f,"tensor_group":"local","tensor_index":i,"source_type":"fixed_scene_template","source_file":str(templatep),"source_field":f,"candidate_dependent":False,"fixed_scene_inherited":True,"time_dependent":f in ["sphere_age_s","sphere_released_flag","sphere_pos_x","sphere_pos_y","sphere_pos_z"],"transformation":"standardize scaler","missing_policy":"block"})
    lp=OUT/"point_image_feature_lineage.csv"; pd.DataFrame(lineage).to_csv(lp,index=False)
    # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
    cfg=OUT/"configs/stage_f_point_image_config.json"; jwrite(cfg,{"model":str(modelp),"model_format":"TensorFlow SavedModel","model_sha256":MODEL_SHA,"model_manifest":str(modelp/"MODEL_MANIFEST.json"),"scaler":str(scalep),"scaler_sha256":SCALER_SHA,"scene_template":str(templatep),"scene_state_policy":"one fixed 1616-row scene template; candidate changes only q_int/emissivity_ir/absorptivity_solar","source_case_id_policy":"metadata only; not used to select scene template","n_obj":16,"global_cols":GLOBAL,"local_cols":LOCAL,"targets":TARGET,"inference_seconds":infer_s,"backend":"TensorFlow SavedModel compatible with TensorFlow 2.10"})
    finish("F",start,[selp,templatep,modelp,scalep],[tp,mp,rp,lp],cfg,"Completed with controlled boundary",{"candidate_count":len(sel),"token_rows":len(tok),"expected_rows":len(sel)*101*16,"template_rows":len(state),"point_image_not_screening_gate":True})
    return 0


def _cli():
    # 将外部值转换为内部格式，保证后续计算使用一致的数据类型。
    ap=argparse.ArgumentParser(description="点图像代理候选评价（规范模型资源 + Keras 3/TensorFlow 固定兼容版）")
    ap.add_argument("--validate-template-only",action="store_true")
    args=ap.parse_args(); return main(validate_only=args.validate_template_only)

# 检查当前条件，仅在满足约束时执行对应分支。
if __name__=="__main__":
    raise SystemExit(_cli())
