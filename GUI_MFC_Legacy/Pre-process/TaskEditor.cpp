#include "pch.h"
#include "TaskEditor.h"
#include "resource.h"
#include <cmath>
namespace { const wchar_t* titles[]={L"任务设置",L"目标参数",L"场景与运动",L"环境与观测",L"计算与输出"}; }
BEGIN_MESSAGE_MAP(CTaskEditor,CWnd)
 ON_WM_SIZE()
 ON_NOTIFY(TVN_SELCHANGED,IDC_TASK_TREE,OnSelection)
 ON_REGISTERED_MESSAGE(AFX_WM_PROPERTY_CHANGED,OnChanged)
 ON_MESSAGE(WM_APP+210,OnRefresh)
 ON_COMMAND_RANGE(IDC_TASK_NEW,IDC_TASK_SAVE_AS,OnFileButton)
END_MESSAGE_MAP()
BOOL CTaskEditor::Create(CWnd* parent,BusinessTask* task)
{
 m_task=task;
 if(!CWnd::CreateEx(WS_EX_CONTROLPARENT,AfxRegisterWndClass(0,::LoadCursor(nullptr,IDC_ARROW),(HBRUSH)(COLOR_WINDOW+1)),L"场景任务编辑器",WS_CHILD|WS_VISIBLE|WS_CLIPCHILDREN,CRect(),parent,IDC_TASK_EDITOR)) return FALSE;
 if(!m_tree.Create(WS_CHILD|WS_VISIBLE|WS_TABSTOP|WS_BORDER|TVS_HASLINES|TVS_SHOWSELALWAYS,CRect(),this,IDC_TASK_TREE)) return FALSE;
 m_title.Create(L"",WS_CHILD|WS_VISIBLE,CRect(),this);
 m_note.Create(L"本阶段仅编辑任务；计算入口尚未连接这些参数。",WS_CHILD|WS_VISIBLE,CRect(),this);
 auto font=CFont::FromHandle((HFONT)GetStockObject(DEFAULT_GUI_FONT));
 m_tree.SetFont(font); m_title.SetFont(font); m_note.SetFont(font);
 const wchar_t* buttons[]={L"新建任务",L"打开任务",L"保存任务",L"另存为"};
 for(int i=0;i<4;++i) { m_files[i].Create(buttons[i],WS_CHILD|WS_TABSTOP|BS_PUSHBUTTON,CRect(),this,IDC_TASK_NEW+i); m_files[i].SetFont(CFont::FromHandle((HFONT)GetStockObject(DEFAULT_GUI_FONT))); }
 for(int i=0;i<5;++i) {
  if(!m_pages[i].Create(WS_CHILD|WS_TABSTOP|WS_BORDER,CRect(),this,IDC_TASK_PAGE_FIRST+i)) return FALSE;
  m_pages[i].EnableHeaderCtrl(FALSE); m_pages[i].EnableDescriptionArea(TRUE); m_pages[i].SetVSDotNetLook(TRUE);
  m_pages[i].SetBoolLabels(L"是",L"否");
  auto item=m_tree.InsertItem(titles[i]); m_tree.SetItemData(item,i+1);
 }
 auto root=m_tree.InsertItem(L"功能模块");
 const wchar_t* modules[]={L"01 正向仿真",L"02 智能预测",L"03 相似度评估",L"04 红外场景构建"};
 for(int i=0;i<4;++i) { auto item=m_tree.InsertItem(modules[i],root); m_tree.SetItemData(item,10+i); }
 m_tree.Expand(root,TVE_EXPAND); BuildPages(); m_tree.SelectItem(m_tree.GetRootItem()); return TRUE;
}
CMFCPropertyGridProperty* CTaskEditor::Group(int page,const wchar_t* name,bool expanded)
{ auto g=new CMFCPropertyGridProperty(name); m_pages[page].AddProperty(g); g->Expand(expanded); return g; }
void CTaskEditor::Number(CMFCPropertyGridProperty* g,const wchar_t* n,double& v)
{ auto p=new CMFCPropertyGridProperty(n,COleVariant(v),L"双击数值编辑；使用有限数值。向量按 X、Y、Z 分量填写。"); g->AddSubItem(p); m_bindings.push_back({p,[&v](const COleVariant& x){v=x.dblVal;}}); }
void CTaskEditor::Integer(CMFCPropertyGridProperty* g,const wchar_t* n,int& v)
{ auto p=new CMFCPropertyGridProperty(n,COleVariant((long)v)); g->AddSubItem(p); m_bindings.push_back({p,[&v](const COleVariant& x){v=x.lVal;}}); }
void CTaskEditor::Text(CMFCPropertyGridProperty* g,const wchar_t* n,CString& v,const wchar_t* choices)
{ auto p=new CMFCPropertyGridProperty(n,COleVariant(v)); g->AddSubItem(p); if(choices) { CString list(choices); int pos=0; CString s; while(!(s=list.Tokenize(L"|",pos)).IsEmpty()) p->AddOption(s); p->AllowEdit(FALSE); } m_bindings.push_back({p,[&v](const COleVariant& x){v=x.bstrVal;}}); }
void CTaskEditor::Boolean(CMFCPropertyGridProperty* g,const wchar_t* n,bool& v)
{ auto p=new CMFCPropertyGridProperty(n,COleVariant((short)(v?VARIANT_TRUE:VARIANT_FALSE),VT_BOOL)); g->AddSubItem(p); m_bindings.push_back({p,[&v](const COleVariant& x){v=x.boolVal!=VARIANT_FALSE;}}); }
bool CTaskEditor::Commit()
{
 for(auto& page:m_pages) if(page.GetSafeHwnd() && !page.EndEditItem()) return false;
 if(m_task && m_task->targetMotion.size()!=static_cast<size_t>(m_task->task.targetCount)) {
  m_bindings.clear();
  size_t previous=m_task->targetMotion.size();
  m_task->targetMotion.resize(m_task->task.targetCount);
  for(size_t i=previous;i<m_task->targetMotion.size();++i) m_task->targetMotion[i].x=static_cast<double>(i);
  m_selectedTarget=min(m_selectedTarget,m_task->task.targetCount);
  Reload();
 }
 return true;
}
LRESULT CTaskEditor::OnChanged(WPARAM,LPARAM lp)
{
 auto p=reinterpret_cast<CMFCPropertyGridProperty*>(lp);
 auto v=p->GetValue();
 CString name=p->GetName();
 if((v.vt==VT_R8 && !std::isfinite(v.dblVal)) || (name==L"目标数量" && (v.lVal<1 || v.lVal>100000)) || (name==L"目标编号" && (v.lVal<1 || v.lVal>m_task->task.targetCount))) { AfxMessageBox(L"请输入有效数值；目标编号不能超出目标数量，编辑器最多支持 100000 个目标。"); p->ResetOriginalValue(); return 0; }
 for(auto& b:m_bindings) if(b.property==p) { b.set(v); break; }
 p->SetOriginalValue(v);
 if(name==L"目标编号") { PostMessage(WM_APP+210); return 0; }
 auto doc=static_cast<CFrameWnd*>(AfxGetMainWnd())->GetActiveDocument(); if(doc) doc->SetModifiedFlag();
 return 0;
}
LRESULT CTaskEditor::OnRefresh(WPARAM,LPARAM)
{
 if(!Commit()) return 0;
 Reload();
 // Keep the target selector visible after changing the selected target.
 for(int i=0;i<m_pages[2].GetPropertyCount();++i) {
  auto group=m_pages[2].GetProperty(i);
  if(CString(group->GetName())==L"高级设置：单目标状态") group->Expand(TRUE);
 }
 m_pages[2].AdjustLayout();
 return 0;
}
void CTaskEditor::Reload()
{ if(!GetSafeHwnd()) return; for(auto& page:m_pages) page.EndEditItem(FALSE); m_selectedTarget=max(1,min(m_selectedTarget,m_task->task.targetCount)); m_bindings.clear(); for(auto& page:m_pages) page.RemoveAll(); BuildPages(); ShowPage(m_page); }
void CTaskEditor::SelectModule(int module)
{ if(!Commit()) return; const wchar_t* names[]={L"正向仿真",L"智能预测",L"相似度评估",L"红外场景构建"}; m_task->calculation.module=names[module]; auto doc=static_cast<CFrameWnd*>(AfxGetMainWnd())->GetActiveDocument(); if(doc) doc->SetModifiedFlag(); Reload(); ShowPage(4); }
void CTaskEditor::ShowPage(int page)
{ if(!Commit()) return; m_page=page; for(int i=0;i<5;++i) m_pages[i].ShowWindow(i==page?SW_SHOW:SW_HIDE); for(auto& b:m_files) b.ShowWindow(page==0?SW_SHOW:SW_HIDE); m_title.SetWindowText(titles[page]); Layout(); }
void CTaskEditor::OnSelection(NMHDR* h,LRESULT* result)
{ *result=0; auto n=reinterpret_cast<NMTREEVIEW*>(h); auto data=m_tree.GetItemData(n->itemNew.hItem); if(data>=1 && data<=5) ShowPage((int)data-1); else if(data>=10 && data<=13) SelectModule((int)data-10); }
void CTaskEditor::OnFileButton(UINT id)
{ if(!Commit()) return; const UINT commands[]={ID_FILE_NEW,ID_FILE_OPEN,ID_FILE_SAVE,ID_FILE_SAVE_AS}; AfxGetMainWnd()->SendMessage(WM_COMMAND,commands[id-IDC_TASK_NEW]); }
void CTaskEditor::OnSize(UINT type,int cx,int cy) { CWnd::OnSize(type,cx,cy); Layout(); }
void CTaskEditor::Layout()
{
 if(!m_tree.GetSafeHwnd()) return;
 CRect r; GetClientRect(r); int w=r.Width(),h=r.Height();
 CClientDC dc(this); int dpi=dc.GetDeviceCaps(LOGPIXELSX); auto scale=[dpi](int value){return MulDiv(value,dpi,96);};
 int gap=scale(4), left=max(scale(100),min(scale(210),w/4)), x=left+gap*2, width=max(0,w-x-gap*2);
 m_tree.MoveWindow(gap,gap,max(0,left-gap),max(0,h-gap*2)); m_title.MoveWindow(x,scale(6),width,scale(24));
 int top=scale(36); if(m_page==0) { int bw=max(0,(width-gap*3)/4); for(int i=0;i<4;++i) m_files[i].MoveWindow(x+i*(bw+gap),top,bw,scale(28)); top+=scale(36); }
 m_note.MoveWindow(x,max(top,h-scale(40)),width,scale(36));
 for(auto& page:m_pages) if(page.GetSafeHwnd()) page.MoveWindow(x,top,width,max(0,h-top-scale(44)));
}
void CTaskEditor::BuildPages()
{
 auto& t=*m_task;
 auto g=Group(0,L"任务信息"); Text(g,L"任务名称",t.task.metadata.name); Text(g,L"任务说明",t.task.metadata.description);
 g=Group(0,L"仿真规模"); Number(g,L"仿真时长 (s)",t.task.duration); Integer(g,L"目标数量",t.task.targetCount);
 g=Group(1,L"全部目标统一设置（修改即批量生效）");
 Number(g,L"球体半径 (m)",t.targets.radius);
 Number(g,L"密度 (kg/m³)",t.targets.density);
 Number(g,L"比热 (J/(kg·K))",t.targets.heatCapacity);
 Number(g,L"初始温度 (K)",t.targets.initialTemperature);
 Number(g,L"内部热源功率 (W)",t.targets.heatPower);
 Number(g,L"红外发射率",t.targets.emissivity);
 Number(g,L"太阳吸收率",t.targets.solarAbsorption);
 g=Group(1,L"高级设置",false); Number(g,L"红外反射率",t.targets.irReflection);
 auto reserved=new CMFCPropertyGridProperty(L"单目标独立物性",COleVariant(L"后续开放；当前全部目标使用统一物性")); reserved->Enable(FALSE); g->AddSubItem(reserved);
 g=Group(2,L"目标群初始状态");
 Number(g,L"群中心位置 (m) X",t.scene.centerX);
 Number(g,L"群中心位置 (m) Y",t.scene.centerY);
 Number(g,L"群中心位置 (m) Z",t.scene.centerZ);
 Number(g,L"群方向 X",t.scene.directionX);
 Number(g,L"群方向 Y",t.scene.directionY);
 Number(g,L"群方向 Z",t.scene.directionZ);
 Number(g,L"群参考上方向 X",t.scene.upX);
 Number(g,L"群参考上方向 Y",t.scene.upY);
 Number(g,L"群参考上方向 Z",t.scene.upZ);
 Number(g,L"群速度 (m/s) X",t.scene.velocityX);
 Number(g,L"群速度 (m/s) Y",t.scene.velocityY);
 Number(g,L"群速度 (m/s) Z",t.scene.velocityZ);
 g=Group(2,L"高级设置：群角运动",false);
 Number(g,L"角速度 (rad/s) X",t.scene.angularVelocityX);
 Number(g,L"角速度 (rad/s) Y",t.scene.angularVelocityY);
 Number(g,L"角速度 (rad/s) Z",t.scene.angularVelocityZ);
 Number(g,L"角加速度 (rad/s²) X",t.scene.angularAccelerationX);
 Number(g,L"角加速度 (rad/s²) Y",t.scene.angularAccelerationY);
 Number(g,L"角加速度 (rad/s²) Z",t.scene.angularAccelerationZ);
 g=Group(2,L"高级设置：单目标状态",false);
 Integer(g,L"目标编号",m_selectedTarget);
 // Edit one selected target; other targets remain in the business model.
 {
  size_t i=static_cast<size_t>(m_selectedTarget-1);
  CString label; label.Format(L"目标 %u",static_cast<unsigned>(i+1));
  auto row=new CMFCPropertyGridProperty(label); g->AddSubItem(row); auto& m=t.targetMotion[i];
 Number(row,L"相对位置 X (m)",m.x);
 Number(row,L"相对位置 Y (m)",m.y);
 Number(row,L"相对位置 Z (m)",m.z);
 Number(row,L"相对速度 X (m/s)",m.vx);
 Number(row,L"相对速度 Y (m/s)",m.vy);
 Number(row,L"相对速度 Z (m/s)",m.vz);
 Number(row,L"相对加速度 X (m/s²)",m.ax);
 Number(row,L"相对加速度 Y (m/s²)",m.ay);
 Number(row,L"相对加速度 Z (m/s²)",m.az);
 Number(row,L"释放时间 (s)",m.releaseTime);
 Boolean(row,L"启用目标",m.active); if(i==0) { row->GetSubItem(9)->Enable(FALSE); row->GetSubItem(10)->Enable(FALSE); }
 }
 g=Group(3,L"辐射环境"); Number(g,L"太阳辐照度 (W/m²)",t.environment.solarFlux); Number(g,L"环境辐射温度 (K)",t.environment.radiationTemperature);
 Number(g,L"太阳方向 X",t.environment.sunX);
 Number(g,L"太阳方向 Y",t.environment.sunY);
 Number(g,L"太阳方向 Z",t.environment.sunZ);
 g=Group(3,L"观测位置与光学");
 Number(g,L"观测位置 (m) X",t.environment.observerPositionX);
 Number(g,L"观测位置 (m) Y",t.environment.observerPositionY);
 Number(g,L"观测位置 (m) Z",t.environment.observerPositionZ);
 Number(g,L"观测方向 X",t.environment.observerDirectionX);
 Number(g,L"观测方向 Y",t.environment.observerDirectionY);
 Number(g,L"观测方向 Z",t.environment.observerDirectionZ);
 Number(g,L"观测参考上方向 X",t.environment.observerUpX);
 Number(g,L"观测参考上方向 Y",t.environment.observerUpY);
 Number(g,L"观测参考上方向 Z",t.environment.observerUpZ);
 Number(g,L"孔径尺寸 (m)",t.environment.apertureSize);
 Number(g,L"焦距 (m)",t.environment.focalLength);
 Number(g,L"像面尺寸 (m)",t.environment.planeSize);
 Boolean(g,L"孔径跟踪目标群中心",t.environment.apertureTracking); Boolean(g,L"探测器跟踪目标群中心",t.environment.detectorTracking);
 g=Group(3,L"高级设置：观测系统运动与探测器姿态",false);
 Number(g,L"孔径速度 (m/s) X",t.environment.observerVelocityX);
 Number(g,L"孔径速度 (m/s) Y",t.environment.observerVelocityY);
 Number(g,L"孔径速度 (m/s) Z",t.environment.observerVelocityZ);
 Number(g,L"孔径角速度 (rad/s) X",t.environment.observerAngularVelocityX);
 Number(g,L"孔径角速度 (rad/s) Y",t.environment.observerAngularVelocityY);
 Number(g,L"孔径角速度 (rad/s) Z",t.environment.observerAngularVelocityZ);
 Number(g,L"孔径角加速度 (rad/s²) X",t.environment.observerAngularAccelerationX);
 Number(g,L"孔径角加速度 (rad/s²) Y",t.environment.observerAngularAccelerationY);
 Number(g,L"孔径角加速度 (rad/s²) Z",t.environment.observerAngularAccelerationZ);
 Number(g,L"探测器方向 X",t.environment.detectorDirectionX);
 Number(g,L"探测器方向 Y",t.environment.detectorDirectionY);
 Number(g,L"探测器方向 Z",t.environment.detectorDirectionZ);
 Number(g,L"探测器参考上方向 X",t.environment.detectorUpX);
 Number(g,L"探测器参考上方向 Y",t.environment.detectorUpY);
 Number(g,L"探测器参考上方向 Z",t.environment.detectorUpZ);
 Number(g,L"探测器速度 (m/s) X",t.environment.detectorVelocityX);
 Number(g,L"探测器速度 (m/s) Y",t.environment.detectorVelocityY);
 Number(g,L"探测器速度 (m/s) Z",t.environment.detectorVelocityZ);
 Number(g,L"探测器角速度 (rad/s) X",t.environment.detectorAngularVelocityX);
 Number(g,L"探测器角速度 (rad/s) Y",t.environment.detectorAngularVelocityY);
 Number(g,L"探测器角速度 (rad/s) Z",t.environment.detectorAngularVelocityZ);
 Number(g,L"探测器角加速度 (rad/s²) X",t.environment.detectorAngularAccelerationX);
 Number(g,L"探测器角加速度 (rad/s²) Y",t.environment.detectorAngularAccelerationY);
 Number(g,L"探测器角加速度 (rad/s²) Z",t.environment.detectorAngularAccelerationZ);
 g=Group(4,L"当前功能模块（通过左侧功能模块切换）"); auto module=new CMFCPropertyGridProperty(L"功能模块",COleVariant(t.calculation.module)); module->Enable(FALSE); g->AddSubItem(module);
 if(t.calculation.module==L"正向仿真") { g=Group(4,L"正向仿真"); Text(g,L"运行模式",t.calculation.forwardMode,L"正式计算|仅准备输入"); Integer(g,L"超时时间 (s)",t.calculation.timeout);
 auto output=new CMFCPropertyGridProperty(L"输出选项",COleVariant(L"采用正式输出策略（本阶段不可配置）")); output->Enable(FALSE); g->AddSubItem(output); }
 else if(t.calculation.module==L"智能预测") { g=Group(4,L"智能预测"); Text(g,L"预测内容",t.calculation.prediction,L"温度|点图像|温度与点图像"); }
 else if(t.calculation.module==L"相似度评估") { g=Group(4,L"相似度评估"); Text(g,L"评估方式",t.calculation.evaluation,L"特征提取|相似度评估"); Text(g,L"参考运行目录",t.calculation.referenceRun); Text(g,L"候选运行目录",t.calculation.candidateRun); }
 else { g=Group(4,L"红外场景构建（预留）"); for(auto name:{L"相似度要求",L"候选数量",L"搜索设置"}) { auto prop=new CMFCPropertyGridProperty(name,COleVariant(L"后续接入")); prop->Enable(FALSE); g->AddSubItem(prop); } }
 for(auto& page:m_pages) page.AdjustLayout();
}
