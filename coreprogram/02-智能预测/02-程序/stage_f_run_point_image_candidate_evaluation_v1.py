from __future__ import annotations

"""点图像代理候选评价。

正式固定场景合同：
- 固定场景运行特征模板位于 02-智能预测/03-模型文件/point_image_fixed_scene_runtime.csv。
- 该模板属于冻结模型资源，不属于用户业务输入。
- q_int / emissivity_ir / absorptivity_solar 由规范输入经 surrogate_standard_input.py 校验后在运行时注入。
- source_case_id 仅作为结果追踪元数据，不用于选择场景模板。
- 模型加载固定采用 Keras 3 + TensorFlow 后端，并注册训练时使用的自定义层与损失。
"""

import argparse
import json
import os
import time
from pathlib import Path
from typing import Any

import numpy as np
import pandas as pd

from stage_utils import *

MODEL_SHA="0d77a38f4457adf1a34275a9b7309ac1aa3e5e880542b19a59a0155f54c31135"
SCALER_SHA="98a4dd66d29dd64aa6ef4771cd0cea337b6ad590b847e14d2635bafc7180d92a"
GLOBAL=["q_int","emissivity_ir","absorptivity_solar","time_s","distance_to_detector"]
LOCAL=["sphere_id_norm","active_flag","sphere_init_x","sphere_init_y","sphere_init_z","sphere_vx","sphere_vy","sphere_vz","sphere_release_time","sphere_age_s","sphere_released_flag","sphere_pos_x","sphere_pos_y","sphere_pos_z"]
TARGET=["screen_x","screen_y","log_spot_power","log_spot_intensity"]
TEMPLATE_COLS=["frame_id","sphere_id","time_s","distance_to_detector",*LOCAL,"input_GRID_NX","input_GRID_NY","input_SPOT_PLANE_SIZE"]


def pixel(x,y,nx,ny,size):
    valid=np.isfinite(x)&np.isfinite(y)&(np.abs(x)<=size/2)&(np.abs(y)<=size/2)
    px=np.floor((x/size+.5)*nx).astype(int)+1; py=np.floor((y/size+.5)*ny).astype(int)+1
    valid&=(px>=1)&(px<=nx)&(py>=1)&(py<=ny); return px,py,valid


def load_scene_template(path: Path) -> pd.DataFrame:
    d=pd.read_csv(path)
    missing=set(TEMPLATE_COLS)-set(d.columns)
    if missing:
        raise ValueError(f"point_image_fixed_scene_runtime.csv 缺少列：{sorted(missing)}")
    d=d[TEMPLATE_COLS].copy()
    d["frame_id"]=pd.to_numeric(d["frame_id"],errors="raise").astype(int)
    d["sphere_id"]=pd.to_numeric(d["sphere_id"],errors="raise").astype(int)
    for c in [x for x in TEMPLATE_COLS if x not in {"frame_id","sphere_id"}]:
        d[c]=pd.to_numeric(d[c],errors="raise")
    d=d.sort_values(["frame_id","sphere_id"],kind="mergesort").reset_index(drop=True)
    if len(d)!=1616:
        raise ValueError(f"固定点图像模板必须为 1616 行，实际 {len(d)} 行。")
    if d.duplicated(["frame_id","sphere_id"]).any():
        raise ValueError("固定点图像模板存在 frame_id/sphere_id 重复键。")
    counts=d.groupby("frame_id")["sphere_id"].nunique()
    if len(counts)!=101 or not counts.eq(16).all():
        raise ValueError("固定点图像模板必须包含 101 个时刻且每时刻 16 个球。")
    times=d.groupby("frame_id",sort=True)["time_s"].first().to_numpy(float)
    if not np.array_equal(times,np.arange(0.0,1000.0+10.0,10.0)):
        raise ValueError("固定点图像模板时间轴必须为 0~1000 s、步长 10 s。")
    ids=np.sort(d.sphere_id.unique())
    if not np.array_equal(ids,np.arange(1,17)):
        raise ValueError("固定点图像模板 sphere_id 必须为 1~16。")
    if not np.isfinite(d[TEMPLATE_COLS].to_numpy(dtype=float)).all():
        raise ValueError("固定点图像模板包含非有限值。")
    return d


def _keras_runtime():
    """使用 Keras 3 + TensorFlow 后端加载正式点图像模型。"""
    os.environ["KERAS_BACKEND"] = "tensorflow"

    import tensorflow as tf
    import keras
    from keras import layers, ops

    return keras, layers, tf, ops


def load_forward_model(model_path: Path):
    keras,layers,tf,ops=_keras_runtime()

    @keras.utils.register_keras_serializable(package="thermal_optuna")
    class LearnedPositionEmbedding(layers.Layer):
        def __init__(self,n_obj:int,d_model:int,**kwargs):
            super().__init__(**kwargs); self.n_obj=int(n_obj); self.d_model=int(d_model)
            self.pos_embedding=layers.Embedding(input_dim=self.n_obj,output_dim=self.d_model)
        def call(self,x):
            if tf is not None:
                positions=tf.range(start=0,limit=self.n_obj,delta=1)
                return x+self.pos_embedding(positions)[tf.newaxis,:,:]
            positions=ops.arange(0,self.n_obj,1)
            return x+ops.expand_dims(self.pos_embedding(positions),0)
        def get_config(self):
            return {**super().get_config(),"n_obj":self.n_obj,"d_model":self.d_model}

    @keras.utils.register_keras_serializable(package="thermal_optuna")
    class TransformerBlock(layers.Layer):
        def __init__(self,d_model:int,num_heads:int,ff_mult:int,dropout:float,**kwargs):
            super().__init__(**kwargs); self.d_model=int(d_model); self.num_heads=int(num_heads); self.ff_mult=int(ff_mult); self.dropout=float(dropout)
            self.attn=layers.MultiHeadAttention(num_heads=self.num_heads,key_dim=max(1,self.d_model//max(1,self.num_heads)),dropout=self.dropout,output_shape=self.d_model)
            self.norm1=layers.LayerNormalization(epsilon=1.0e-6); self.norm2=layers.LayerNormalization(epsilon=1.0e-6); self.drop=layers.Dropout(self.dropout)
            self.ffn=keras.Sequential([layers.Dense(self.d_model*self.ff_mult,activation="gelu"),layers.Dropout(self.dropout),layers.Dense(self.d_model)])
        def call(self,x,training:bool=False):
            attn_out=self.attn(x,x,training=training); x=self.norm1(x+self.drop(attn_out,training=training)); ffn_out=self.ffn(x,training=training); return self.norm2(x+self.drop(ffn_out,training=training))
        def get_config(self):
            return {**super().get_config(),"d_model":self.d_model,"num_heads":self.num_heads,"ff_mult":self.ff_mult,"dropout":self.dropout}

    @keras.utils.register_keras_serializable(package="thermal_optuna")
    class WeightedHuberLoss(keras.losses.Loss):
        def __init__(self,delta:float=1.0,weights:list[float]|None=None,target_dim:int=4,name:str="weighted_huber_loss",**kwargs):
            super().__init__(name=name,**kwargs); self.delta=float(delta); self.weights=[float(w) for w in (weights or [1.0]*target_dim)]; self.target_dim=int(target_dim)
        def call(self,y_true,y_pred):
            if tf is not None:
                w=tf.constant(self.weights,dtype=tf.float32); delta=tf.constant(self.delta,dtype=tf.float32); yt=tf.reshape(y_true,(-1,self.target_dim)); yp=tf.reshape(y_pred,(-1,self.target_dim)); err=yt-yp; ae=tf.abs(err); q=tf.minimum(ae,delta); lin=ae-q; return tf.reduce_mean((0.5*tf.square(q)+delta*lin)*w)
            w=ops.convert_to_tensor(self.weights,dtype="float32"); delta=ops.cast(self.delta,"float32"); yt=ops.reshape(y_true,(-1,self.target_dim)); yp=ops.reshape(y_pred,(-1,self.target_dim)); err=yt-yp; ae=ops.abs(err); q=ops.minimum(ae,delta); lin=ae-q; return ops.mean((0.5*ops.square(q)+delta*lin)*w)
        def get_config(self):
            return {**super().get_config(),"delta":self.delta,"weights":self.weights,"target_dim":self.target_dim}

    return keras.models.load_model(model_path,custom_objects={"LearnedPositionEmbedding":LearnedPositionEmbedding,"TransformerBlock":TransformerBlock,"WeightedHuberLoss":WeightedHuberLoss},compile=False)


def validate_contract(template_path: Path|None=None, model_dir: Path|None=None) -> dict[str,Any]:
    template_path=template_path or config_path(
        "point_image_scene_template",
        MODEL_DIR/"point_image_fixed_scene_runtime.csv",
        must_exist=True,
    )
    model_dir=model_dir or config_path("point_image_model_dir",MODEL_DIR,must_exist=True)
    modelp=model_dir/"best_model.keras"; scalep=model_dir/"scalers.json"
    if sha256(modelp)!=MODEL_SHA: raise RuntimeError("best_model.keras SHA256 不匹配。")
    if sha256(scalep)!=SCALER_SHA: raise RuntimeError("scalers.json SHA256 不匹配。")
    d=load_scene_template(template_path)
    scalers=json.loads(scalep.read_text(encoding="utf-8"))
    if list(scalers.get("global_cols",[]))!=GLOBAL or list(scalers.get("local_cols",[]))!=LOCAL or list(scalers.get("target_cols",[]))!=TARGET or int(scalers.get("n_obj",0))!=16:
        raise RuntimeError("scalers.json 输入/输出合同与 Stage F 不一致。")
    return {"template":str(template_path),"rows":len(d),"frames":d.frame_id.nunique(),"spheres_per_frame":16,"model_sha256":MODEL_SHA,"scaler_sha256":SCALER_SHA}


def main(validate_only: bool=False):
    ensure(); start=time.time()
    if MODULE_ROOT.name.startswith("02-"):
        selp=OUT/"point_image_selection.csv"  # 仅供内部调试；正式GUI通过 surrogate_prediction_runner.py 调用
    else:
        selp=OUT/"13_point_image_evaluation_selection.csv"
    templatep=config_path(
        "point_image_scene_template",
        PACKAGE_ROOT/"02-智能预测"/"03-模型文件"/"point_image_fixed_scene_runtime.csv",
        must_exist=True,
    )
    md=config_path("point_image_model_dir",MODEL_DIR,must_exist=True); modelp=md/"best_model.keras"; scalep=md/"scalers.json"
    contract=validate_contract(templatep,md)
    if validate_only:
        print(json.dumps({"status":"TemplateContractValid",**contract},ensure_ascii=False,indent=2)); return 0
    if not selp.exists(): raise FileNotFoundError(f"点图像候选输入不存在：{selp}")
    sel=pd.read_csv(selp)
    need={"target_id","candidate_id","q_int","emissivity_ir","absorptivity_solar"}
    missing=need-set(sel.columns)
    if missing: raise ValueError(f"点图像候选输入缺少列：{sorted(missing)}")
    if "source_case_id" not in sel.columns: sel["source_case_id"]="fixed_scene"
    state=load_scene_template(templatep)
    scalers=json.loads(scalep.read_text(encoding="utf-8")); model=load_forward_model(modelp); nobj=16
    tokens=[]; metrics=[]; recon=[]; infer_s=0.0
    for r in sel.itertuples(index=False):
        rows=state.copy()
        rows["q_int"]=float(r.q_int); rows["emissivity_ir"]=float(r.emissivity_ir); rows["absorptivity_solar"]=float(r.absorptivity_solar)
        sample=rows.groupby("frame_id",sort=False).head(1)
        gr=sample[GLOBAL].to_numpy(np.float32); lr=rows[LOCAL].to_numpy(np.float32).reshape(-1,nobj,len(LOCAL))
        g=(gr-np.asarray(scalers["global_mean"],np.float32))/np.asarray(scalers["global_scale"],np.float32)
        l=(lr-np.asarray(scalers["local_mean"],np.float32))/np.asarray(scalers["local_scale"],np.float32)
        ts=time.perf_counter(); ps=model.predict([g,l],batch_size=64,verbose=0); infer_s+=time.perf_counter()-ts
        y=ps.reshape(-1,nobj,4)*np.asarray(scalers["target_scale"])[None,None,:]+np.asarray(scalers["target_offset"])[None,None,:]
        base=rows[["frame_id","sphere_id","time_s","sphere_released_flag","input_GRID_NX","input_GRID_NY","input_SPOT_PLANE_SIZE"]].copy().reset_index(drop=True)
        flat=y.reshape(-1,4)
        for i,c in enumerate(TARGET): base[f"pred_{c}"]=flat[:,i]
        base.insert(0,"candidate_id",r.candidate_id); base.insert(0,"source_case_id",r.source_case_id); base.insert(0,"target_id",r.target_id)
        base["pred_spot_power"]=10**base.pred_log_spot_power; base["pred_spot_intensity"]=10**base.pred_log_spot_intensity
        px,py,v=pixel(base.pred_screen_x.to_numpy(),base.pred_screen_y.to_numpy(),base.input_GRID_NX.to_numpy(),base.input_GRID_NY.to_numpy(),base.input_SPOT_PLANE_SIZE.to_numpy())
        base["pixel_x"]=px; base["pixel_y"]=py; base["in_bounds"]=v; tokens.append(base)
        fr=[]
        for frame,fg in base.groupby("frame_id",sort=True):
            valid=fg.in_bounds.to_numpy(bool); power=fg.pred_spot_power.to_numpy(); intensity=fg.pred_spot_intensity.to_numpy(); w=power[valid]; xx=fg.pixel_x.to_numpy()[valid]; yy=fg.pixel_y.to_numpy()[valid]
            fr.append({"frame_id":frame,"time_s":fg.time_s.iloc[0],"total_power":w.sum(),"peak_power":w.max() if len(w) else 0,"centroid_x":np.average(xx,weights=w) if w.sum()>0 else np.nan,"centroid_y":np.average(yy,weights=w) if w.sum()>0 else np.nan,"total_intensity":intensity[valid].sum(),"released_count":int((fg.sphere_released_flag>=.5).sum())})
        fr=pd.DataFrame(fr)
        metrics.append({"target_id":r.target_id,"source_case_id":r.source_case_id,"candidate_id":r.candidate_id,"frame_count":len(fr),"median_total_power":fr.total_power.median(),"max_total_power":fr.total_power.max(),"final_total_power":fr.total_power.iloc[-1],"median_peak_power":fr.peak_power.median(),"max_peak_power":fr.peak_power.max(),"centroid_x_range":fr.centroid_x.max()-fr.centroid_x.min(),"centroid_y_range":fr.centroid_y.max()-fr.centroid_y.min(),"interpretation":"fixed-scene candidate-relative predicted-token reconstruction; no true-token error"})
        recon.append({"target_id":r.target_id,"candidate_id":r.candidate_id,"frames":101,"dimensions":"256x256","pixel_index":"1-based","accumulation":"same-pixel sum","inverse":"10**log_value","PSF":"none","interpolation":"none","smoothing":"none"})
    tok=pd.concat(tokens,ignore_index=True); met=pd.DataFrame(metrics)
    tp=OUT/"candidate_point_token_predictions.csv.gz"; tok.to_csv(tp,index=False,compression="gzip")
    mp=OUT/"candidate_point_image_metrics.csv"; met.to_csv(mp,index=False)
    rp=OUT/"candidate_reconstruction_manifest.csv"; pd.DataFrame(recon).to_csv(rp,index=False)
    lineage=[]
    for i,f in enumerate(GLOBAL): lineage.append({"field_name":f,"tensor_group":"global","tensor_index":i,"source_type":"candidate" if i<3 else "fixed_scene_template","source_file":str(selp if i<3 else templatep),"source_field":f,"candidate_dependent":i<3,"fixed_scene_inherited":i>=3,"time_dependent":i>=3,"transformation":"standardize scaler","missing_policy":"block"})
    for i,f in enumerate(LOCAL): lineage.append({"field_name":f,"tensor_group":"local","tensor_index":i,"source_type":"fixed_scene_template","source_file":str(templatep),"source_field":f,"candidate_dependent":False,"fixed_scene_inherited":True,"time_dependent":f in ["sphere_age_s","sphere_released_flag","sphere_pos_x","sphere_pos_y","sphere_pos_z"],"transformation":"standardize scaler","missing_policy":"block"})
    lp=OUT/"point_image_feature_lineage.csv"; pd.DataFrame(lineage).to_csv(lp,index=False)
    cfg=OUT/"configs/stage_f_point_image_config.json"; jwrite(cfg,{"model":str(modelp),"model_sha256":MODEL_SHA,"scaler":str(scalep),"scaler_sha256":SCALER_SHA,"scene_template":str(templatep),"scene_state_policy":"one fixed 1616-row scene template; candidate changes only q_int/emissivity_ir/absorptivity_solar","source_case_id_policy":"metadata only; not used to select model state","n_obj":16,"global_cols":GLOBAL,"local_cols":LOCAL,"targets":TARGET,"inference_seconds":infer_s,"backend":"Keras 3 + TensorFlow (fixed)"})
    finish("F",start,[selp,templatep,modelp,scalep],[tp,mp,rp,lp],cfg,"Completed with controlled boundary",{"candidate_count":len(sel),"token_rows":len(tok),"expected_rows":len(sel)*101*16,"template_rows":len(state),"point_image_not_screening_gate":True})
    return 0


def _cli():
    ap=argparse.ArgumentParser(description="点图像代理候选评价（规范模型资源 + Keras 3/TensorFlow 固定兼容版）")
    ap.add_argument("--validate-template-only",action="store_true")
    args=ap.parse_args(); return main(validate_only=args.validate_template_only)

if __name__=="__main__":
    raise SystemExit(_cli())
