
// Pre-processView.cpp: CPreprocessView 类的实现
//

#include "pch.h"
#include "framework.h"
// SHARED_HANDLERS 可以在实现预览、缩略图和搜索筛选器句柄的
// ATL 项目中进行定义，并允许与该项目共享文档代码。
#ifndef SHARED_HANDLERS
#include "Pre-process.h"
#endif

#include "Pre-processDoc.h"
#include "Pre-processView.h"
#include<string>

#ifdef _DEBUG
#define new DEBUG_NEW
#endif


// CPreprocessView

IMPLEMENT_DYNCREATE(CPreprocessView, CView)

BEGIN_MESSAGE_MAP(CPreprocessView, CView)
	ON_WM_SIZE()
	ON_WM_CONTEXTMENU()
	ON_WM_RBUTTONUP()
	ON_COMMAND(ID_start_infrared_situation_constructing, &CPreprocessView::Onstartinfraredsituationconstructing)
	ON_COMMAND(ID_stop_Infrared_construction, &CPreprocessView::OnstopInfraredconstruction)
	ON_MESSAGE(WM_PYTHON_FINISH1, &CPreprocessView::OnPythonFinish1)
	ON_MESSAGE(WM_PYTHON_FINISH2, &CPreprocessView::OnPythonFinish2)
	ON_MESSAGE(WM_PYTHON_FINISH3, &CPreprocessView::OnPythonFinish3)
	ON_MESSAGE(WM_PYTHON_FINISH4, &CPreprocessView::OnPythonFinish4)

	ON_COMMAND(ID_start_forward_simulation, &CPreprocessView::Onstartforwardsimulation)
	ON_COMMAND(ID_start_surrogate_prediction, &CPreprocessView::Onstartsurrogateprediction)
	ON_COMMAND(ID_start_similarity_predict, &CPreprocessView::Onstartsimilaritypredict)
	ON_COMMAND(ID_stop_forward_simulation, &CPreprocessView::Onstopforwardsimulation)
	ON_COMMAND(ID_stop_surrogate_prediction, &CPreprocessView::Onstopsurrogateprediction)
	ON_COMMAND(ID_stop_similarity_predict, &CPreprocessView::Onstopsimilaritypredict)
	ON_COMMAND(ID_start_similarity_predict_Sphere, &CPreprocessView::OnstartsimilaritypredictSphere)
END_MESSAGE_MAP()

// CPreprocessView 构造/析构

CPreprocessView::CPreprocessView() noexcept
{
	// TODO: 在此处添加构造代码

}

CPreprocessView::~CPreprocessView()
{
}

BOOL CPreprocessView::PreCreateWindow(CREATESTRUCT& cs)
{
    cs.style |= WS_CLIPCHILDREN;
	// TODO: 在此处通过修改
	//  CREATESTRUCT cs 来修改窗口类或样式

	return CView::PreCreateWindow(cs);
}

// CPreprocessView 绘图

void CPreprocessView::OnDraw(CDC* /*pDC*/)
{
	CPreprocessDoc* pDoc = GetDocument();
	ASSERT_VALID(pDoc);
	if (!pDoc)
		return;

	// TODO: 在此处为本机数据添加绘制代码
}

void CPreprocessView::OnRButtonUp(UINT /* nFlags */, CPoint point)
{
	ClientToScreen(&point);
	OnContextMenu(this, point);
}

void CPreprocessView::OnContextMenu(CWnd* /* pWnd */, CPoint point)
{
#ifndef SHARED_HANDLERS
	theApp.GetContextMenuManager()->ShowPopupMenu(IDR_POPUP_EDIT, point.x, point.y, this, TRUE);
#endif
}


// CPreprocessView 诊断

#ifdef _DEBUG
void CPreprocessView::AssertValid() const
{
	CView::AssertValid();
}

void CPreprocessView::Dump(CDumpContext& dc) const
{
	CView::Dump(dc);
}

CPreprocessDoc* CPreprocessView::GetDocument() const // 非调试版本是内联的
{
	ASSERT(m_pDocument->IsKindOf(RUNTIME_CLASS(CPreprocessDoc)));
	return (CPreprocessDoc*)m_pDocument;
}
#endif //_DEBUG
void CPreprocessView::Onstartinfraredsituationconstructing()
{
 m_taskEditor.SelectModule(3);
}
void CPreprocessView::OnstopInfraredconstruction()
{
	CPreprocessDoc* pdoc = GetDocument();
	ASSERT_VALID(pdoc);
	if (pdoc->m_bIsIsPyRunning4)
	{
		pdoc->m_bIsIsPyRunning4 = FALSE;
	}
	else
	{
		AfxMessageBox(_T("当前没有进行的红外场景构建计算"));
	}
}
LRESULT CPreprocessView::OnPythonFinish1(WPARAM wParam, LPARAM lParam)
{
	DWORD dwExitcode = (DWORD)wParam;
	if (dwExitcode == 0)
	{
		AfxMessageBox(_T("正向仿真计算完成"));
	}
	else
	{
		AfxMessageBox(_T("正向仿真计算异常，退出码：") + CString(std::to_string(dwExitcode).c_str()));
	}
	return 0;
}
LRESULT CPreprocessView::OnPythonFinish2(WPARAM wParam, LPARAM lParam)
{
	DWORD dwExitcode = (DWORD)wParam;
	if (dwExitcode == 0)
	{
		AfxMessageBox(_T("智能预测计算完成"));
	}
	else
	{
		AfxMessageBox(_T("智能预测计算异常，退出码：") + CString(std::to_string(dwExitcode).c_str()));
	}
	return 0;
}
LRESULT CPreprocessView::OnPythonFinish3(WPARAM wParam, LPARAM lParam)
{
	DWORD dwExitcode = (DWORD)wParam;
	if (dwExitcode == 0)
	{
		AfxMessageBox(_T("相似度评估计算完成"));
	}
	else
	{
		AfxMessageBox(_T("相似度评估计算异常，退出码：") + CString(std::to_string(dwExitcode).c_str()));
	}
	return 0;
}
LRESULT CPreprocessView::OnPythonFinish4(WPARAM wParam, LPARAM lParam)
{
	DWORD dwExitcode = (DWORD)wParam;
	if (dwExitcode == 0)
	{
		AfxMessageBox(_T("红外场景构建计算完成"));
	}
	else
	{
		AfxMessageBox(_T("红外场景构建计算异常，退出码：") + CString(std::to_string(dwExitcode).c_str()));
	}
	return 0;
}
void CPreprocessView::Onstartforwardsimulation()
{
 m_taskEditor.SelectModule(0);
}
void CPreprocessView::Onstopforwardsimulation()
{
	CPreprocessDoc* pdoc = GetDocument();
	ASSERT_VALID(pdoc);
	if (pdoc->m_bIsIsPyRunning1)
	{
		pdoc->m_bIsIsPyRunning1 = FALSE;
	}
	else
	{
		AfxMessageBox(_T("当前没有进行的正向仿真计算"));
	}
}
void CPreprocessView::Onstartsurrogateprediction()
{
 m_taskEditor.SelectModule(1);
}
void CPreprocessView::Onstopsurrogateprediction()
{
	CPreprocessDoc* pdoc = GetDocument();
	ASSERT_VALID(pdoc);
	if (pdoc->m_bIsIsPyRunning2)
	{
		pdoc->m_bIsIsPyRunning2 = FALSE;
	}
	else
	{
		AfxMessageBox(_T("当前没有进行的智能预测计算"));
	}
}
void CPreprocessView::Onstartsimilaritypredict()
{
 m_taskEditor.SelectModule(2);
}
void CPreprocessView::OnstartsimilaritypredictSphere()
{
 m_taskEditor.SelectModule(2);
}
void CPreprocessView::Onstopsimilaritypredict()
{
	CPreprocessDoc* pdoc = GetDocument();
	ASSERT_VALID(pdoc);
	if (pdoc->m_bIsIsPyRunning3)
	{
		pdoc->m_bIsIsPyRunning3 = FALSE;
	}
	else
	{
		AfxMessageBox(_T("当前没有进行的相似度评估计算"));
	}
}
void CPreprocessView::OnInitialUpdate()
{
 CView::OnInitialUpdate();
 if(!m_taskEditor.GetSafeHwnd() && !m_taskEditor.Create(this,&GetDocument()->m_task)) { AfxMessageBox(L"任务编辑器创建失败。"); return; }
 CRect r; GetClientRect(r); m_taskEditor.MoveWindow(r);
}
void CPreprocessView::OnSize(UINT type,int cx,int cy)
{ CView::OnSize(type,cx,cy); if(m_taskEditor.GetSafeHwnd()) m_taskEditor.MoveWindow(0,0,cx,cy); }
void CPreprocessView::OnUpdate(CView*,LPARAM,CObject*) { m_taskEditor.Reload(); }
